using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class StingrayLaneSwimmer : MonoBehaviour
    {
        private enum StingrayState { Glide, Hunt, Telegraph, Charge, Recover, Retreat }

        [Header("Swimming")]
        [SerializeField, Min(0.05f)] private float glideSpeed = 0.38f;
        [SerializeField, Range(1f, 5f)] private float chargeSpeedMultiplier = 3.15f;
        [SerializeField, Range(1f, 3f)] private float huntSpeedMultiplier = 1.28f;
        [SerializeField, Range(0f, 0.45f)] private float laneDepthBias = 0.16f;
        [SerializeField, Range(0f, 1f)] private float waveFollow = 0.9f;
        [SerializeField, Range(1f, 20f)] private float verticalResponsiveness = 7f;
        [SerializeField, Range(0f, 0.2f)] private float currentInfluence = 0.045f;

        [Header("Attack Without Attack Sheet")]
        [SerializeField, Min(0.5f)] private float detectionRange = 3.25f;
        [SerializeField, Min(0.2f)] private float attackRange = 2.15f;
        [SerializeField, Min(0.1f)] private float contactRange = 0.78f;
        [SerializeField, Min(0.1f)] private float telegraphDuration = 0.62f;
        [SerializeField, Min(0.1f)] private float chargeDuration = 1.05f;
        [SerializeField, Min(0.1f)] private float recoveryDuration = 1.35f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 4.25f;
        [SerializeField, Range(0f, 20f)] private float chargeWobbleAngle = 8f;

        [Header("Lane Changes")]
        [SerializeField] private Vector2 laneChangeDelayRange = new(4.5f, 8f);
        [SerializeField, Min(0.2f)] private float laneChangeDuration = 1.3f;

        [Header("Thrown Item Reaction")]
        [SerializeField, Min(0.1f)] private float hitRetreatDuration = 2.2f;
        [SerializeField, Range(1f, 5f)] private float hitRetreatSpeedMultiplier = 2.35f;

        [Header("Animation")]
        [SerializeField, Min(1f)] private float moveFramesPerSecond = 11f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;
        private Sprite[] moveFrames;
        private TinyWaveSurfer target;
        private StingrayState state;
        private int currentLane;
        private int targetLane;
        private float direction;
        private float depthOffset;
        private bool changingLane;
        private float laneChangeElapsed;
        private float nextLaneChangeTime;
        private float stateUntil;
        private float nextAttackTime;
        private float animationTime;
        private bool attackHitApplied;
        private bool initialised;
        private Color baseColour = Color.white;
        private Vector3 baseScale;
        private Coroutine hitRoutine;

        public void Initialise(int requestedLane, Sprite[] frames, bool spawnAtSectionEdge = false)
        {
            ResolveReferences();
            moveFrames = frames ?? System.Array.Empty<Sprite>();
            if (waterLayers.Count < 2 || moveFrames.Length == 0)
            {
                Debug.LogError("StingrayLaneSwimmer could not initialise.", this);
                enabled = false;
                return;
            }

            currentLane = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            targetLane = currentLane;
            direction = Random.value < 0.5f ? -1f : 1f;
            depthOffset = -Mathf.Abs(laneDepthBias);
            renderItem.SetLane(currentLane);

            Vector2 position = transform.position;
            float minX = waterLayers[0].TankMinimum.x;
            float maxX = waterLayers[0].TankMaximum.x;
            if (spawnAtSectionEdge)
            {
                position.x = CameraSafeSpawnUtility.ChooseOffscreenEntryX(
                    waterLayers, spriteRenderer, out bool fromLeft);
                direction = fromLeft ? 1f : -1f;
            }
            else position.x = (minX + maxX) * 0.5f;

            position.y = GetLaneCentreY(currentLane, position.x) + depthOffset;
            SetPositionImmediate(position);
            spriteRenderer.flipX = direction < 0f;
            baseColour = spriteRenderer.color;
            baseScale = transform.localScale;
            state = StingrayState.Glide;
            ScheduleNextLaneChange();
            initialised = true;
        }

        private void Awake() => ResolveReferences();
        private void Start()
        {
            if (!initialised)
            {
                Texture2D sheet = Resources.Load<Texture2D>("Stingray/stingray_move");
                Initialise(0, SliceFallback(sheet));
            }
        }

        private void ResolveReferences()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            Collider2D collider = GetComponent<Collider2D>();
            if (collider == null)
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.15f, 0.55f);
                box.offset = new Vector2(0f, -0.04f);
            }

            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
        }

        private void Update()
        {
            if (moveFrames == null || moveFrames.Length == 0 || spriteRenderer == null) return;
            float fps = state == StingrayState.Telegraph ? moveFramesPerSecond * 0.35f :
                state == StingrayState.Charge ? moveFramesPerSecond * 1.8f : moveFramesPerSecond;
            animationTime += Time.deltaTime * fps;
            spriteRenderer.sprite = moveFrames[Mathf.FloorToInt(animationTime) % moveFrames.Length];
            UpdateAttackPresentation();
        }

        private void FixedUpdate()
        {
            if (!initialised || waterLayers.Count < 2) return;
            Vector2 position = body.position;
            UpdateState(position);

            float multiplier = state == StingrayState.Charge ? chargeSpeedMultiplier :
                state == StingrayState.Hunt ? huntSpeedMultiplier :
                state == StingrayState.Retreat ? hitRetreatSpeedMultiplier : 1f;
            Vector2 waterVelocity = GetLaneVelocity(currentLane, position.x);
            position.x += direction * Mathf.Max(0.08f, glideSpeed * multiplier + waterVelocity.x * currentInfluence) * Time.fixedDeltaTime;
            KeepInsideGameArea(ref position);

            if (!changingLane && state != StingrayState.Telegraph && state != StingrayState.Charge)
            {
                if (target != null && !target.IsDead && (state == StingrayState.Hunt || state == StingrayState.Recover))
                    BeginLaneChangeToward(GetTargetLane(target));
                else if (Time.time >= nextLaneChangeTime)
                    BeginRandomLaneChange();
            }

            float desiredY = UpdateLaneTransition(position.x);
            float follow = 1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime);
            position.y = Mathf.Lerp(position.y, desiredY, follow * waveFollow);
            body.MovePosition(position);
        }

        private void UpdateState(Vector2 position)
        {
            if (state == StingrayState.Retreat)
            {
                if (Time.time >= stateUntil)
                {
                    state = StingrayState.Glide;
                    nextAttackTime = Time.time + recoveryDuration;
                }
                return;
            }

            if (state == StingrayState.Telegraph)
            {
                if (target != null && !target.IsDead)
                {
                    float dx = target.transform.position.x - position.x;
                    if (Mathf.Abs(dx) > 0.05f) SetDirection(Mathf.Sign(dx));
                }
                if (Time.time >= stateUntil)
                {
                    state = StingrayState.Charge;
                    stateUntil = Time.time + chargeDuration;
                    attackHitApplied = false;
                }
                return;
            }

            if (state == StingrayState.Charge)
            {
                if (Time.time >= stateUntil)
                {
                    state = StingrayState.Recover;
                    stateUntil = Time.time + recoveryDuration;
                    nextAttackTime = Time.time + attackCooldown;
                }
                return;
            }

            if (state == StingrayState.Recover)
            {
                if (Time.time >= stateUntil) state = StingrayState.Glide;
                return;
            }

            if (target == null || target.IsDead || Vector2.Distance(position, target.transform.position) > detectionRange * 1.35f)
                target = FindTarget(position);

            if (target == null)
            {
                state = StingrayState.Glide;
                return;
            }

            state = StingrayState.Hunt;
            float deltaX = target.transform.position.x - position.x;
            if (Mathf.Abs(deltaX) > 0.18f) SetDirection(Mathf.Sign(deltaX));
            float distance = Vector2.Distance(position, target.transform.position);
            bool sameLane = GetTargetLane(target) == currentLane;
            if (sameLane && distance <= attackRange && Time.time >= nextAttackTime)
            {
                state = StingrayState.Telegraph;
                stateUntil = Time.time + telegraphDuration;
                attackHitApplied = false;
            }
        }

        private void UpdateAttackPresentation()
        {
            if (hitRoutine != null) return;
            if (state == StingrayState.Telegraph)
            {
                float pulse = 0.88f + Mathf.PingPong(Time.time * 5f, 0.18f);
                transform.localScale = new Vector3(baseScale.x * 1.08f, baseScale.y * pulse, baseScale.z);
                spriteRenderer.color = Color.Lerp(baseColour, new Color(1f, 0.42f, 0.22f, baseColour.a), 0.65f);
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 18f) * chargeWobbleAngle);
            }
            else if (state == StingrayState.Charge)
            {
                transform.localScale = new Vector3(baseScale.x * 1.18f, baseScale.y * 0.82f, baseScale.z);
                spriteRenderer.color = Color.white;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 24f) * chargeWobbleAngle * 0.45f);
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 8f);
                spriteRenderer.color = Color.Lerp(spriteRenderer.color, baseColour, Time.deltaTime * 8f);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 8f);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (state != StingrayState.Charge || attackHitApplied || other == null) return;
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || surfer.IsDead || surfer.HasObstacleClearance) return;
            if (Vector2.Distance(transform.position, surfer.transform.position) > contactRange + 0.3f) return;
            if (surfer.TakeSharkHit(transform.position)) attackHitApplied = true;
        }

        public void TakeSodaCanHit(Vector2 hitPosition)
        {
            Vector2 away = (Vector2)transform.position - hitPosition;
            SetDirection(Mathf.Abs(away.x) < 0.01f ? -direction : Mathf.Sign(away.x));
            target = null;
            changingLane = false;
            state = StingrayState.Retreat;
            stateUntil = Time.time + hitRetreatDuration;
            nextAttackTime = stateUntil + attackCooldown;
            attackHitApplied = false;
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            hitRoutine = StartCoroutine(HitFlash());
        }

        private IEnumerator HitFlash()
        {
            for (int i = 0; i < 6; i++)
            {
                spriteRenderer.color = i % 2 == 0 ? Color.red : baseColour;
                yield return new WaitForSeconds(0.055f);
            }
            spriteRenderer.color = baseColour;
            transform.localScale = baseScale;
            transform.rotation = Quaternion.identity;
            hitRoutine = null;
        }

        private TinyWaveSurfer FindTarget(Vector2 position)
        {
            TinyWaveSurfer best = null;
            float bestDistance = detectionRange;
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
            {
                if (surfer == null || surfer.IsDead) continue;
                float distance = Vector2.Distance(position, surfer.transform.position);
                if (distance <= bestDistance && (best == null || surfer.IsPlayerControlled))
                {
                    best = surfer;
                    bestDistance = distance;
                    if (surfer.IsPlayerControlled) break;
                }
            }
            return best;
        }

        private int GetTargetLane(TinyWaveSurfer surfer) => Mathf.Clamp(surfer.CurrentWaveIndex, 0, waterLayers.Count - 2);
        private void SetDirection(float value)
        {
            if (Mathf.Abs(value) < 0.01f) return;
            direction = Mathf.Sign(value);
            spriteRenderer.flipX = direction < 0f;
        }

        private void BeginLaneChangeToward(int desiredLane)
        {
            desiredLane = Mathf.Clamp(desiredLane, 0, waterLayers.Count - 2);
            if (desiredLane == currentLane) return;
            targetLane = currentLane + (desiredLane > currentLane ? 1 : -1);
            changingLane = true;
            laneChangeElapsed = 0f;
        }

        private void BeginRandomLaneChange()
        {
            int laneCount = waterLayers.Count - 1;
            if (laneCount <= 1) return;
            if (currentLane <= 0) targetLane = 1;
            else if (currentLane >= laneCount - 1) targetLane = laneCount - 2;
            else targetLane = currentLane + (Random.value < 0.5f ? -1 : 1);
            changingLane = targetLane != currentLane;
            laneChangeElapsed = 0f;
        }

        private float UpdateLaneTransition(float worldX)
        {
            if (!changingLane) return GetLaneCentreY(currentLane, worldX) + depthOffset;
            laneChangeElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(laneChangeElapsed / laneChangeDuration);
            float eased = t * t * (3f - 2f * t);
            float y = Mathf.Lerp(GetLaneCentreY(currentLane, worldX), GetLaneCentreY(targetLane, worldX), eased) + depthOffset;
            if (t >= 0.5f) renderItem.SetLane(targetLane);
            if (t >= 1f)
            {
                currentLane = targetLane;
                changingLane = false;
                renderItem.SetLane(currentLane);
                ScheduleNextLaneChange();
            }
            return y;
        }

        private float GetLaneCentreY(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(waterLayers[clamped].GetGameplaySurfaceHeight(worldX), waterLayers[clamped + 1].GetGameplaySurfaceHeight(worldX), 0.5f);
        }

        private Vector2 GetLaneVelocity(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Vector2.Lerp(waterLayers[clamped].GetGameplayWaveVelocity(worldX), waterLayers[clamped + 1].GetGameplayWaveVelocity(worldX), 0.5f);
        }

        private void KeepInsideGameArea(ref Vector2 position)
        {
            float halfWidth = spriteRenderer != null ? spriteRenderer.bounds.extents.x : 0.75f;
            float minX = waterLayers[0].TankMinimum.x + halfWidth;
            float maxX = waterLayers[0].TankMaximum.x - halfWidth;
            if (position.x > maxX) { position.x = maxX; SetDirection(-1f); }
            else if (position.x < minX) { position.x = minX; SetDirection(1f); }
        }

        private void ScheduleNextLaneChange()
        {
            float low = Mathf.Min(laneChangeDelayRange.x, laneChangeDelayRange.y);
            float high = Mathf.Max(laneChangeDelayRange.x, laneChangeDelayRange.y);
            nextLaneChangeTime = Time.time + Random.Range(low, high);
        }

        private void SetPositionImmediate(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            body.position = position;
        }

        private static Sprite[] SliceFallback(Texture2D sheet)
        {
            if (sheet == null) return System.Array.Empty<Sprite>();
            const int size = 64;
            int count = Mathf.Max(1, sheet.width / size);
            Sprite[] frames = new Sprite[count];
            for (int i = 0; i < count; i++)
                frames[i] = Sprite.Create(sheet, new Rect(i * size, 0, size, size), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            return frames;
        }
    }
}
