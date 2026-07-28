using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class GiantSquidLaneSwimmer : MonoBehaviour
    {
        private enum PredatorState { Patrol, Stalk, Attack, Search }

        [Header("Swimming")]
        [SerializeField, Min(0.05f)] private float horizontalSpeed = 0.48f;
        [SerializeField] private bool startMovingRight = true;
        [SerializeField, Range(0f, 0.35f)] private float viewportPadding = 0.045f;

        [Header("Predator Awareness")]
        [SerializeField, Min(0.5f)] private float detectionRange = 2.25f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 3.1f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.45f;
        [SerializeField, Min(0.05f)] private float hitRange = 0.88f;
        [SerializeField, Range(1f, 3f)] private float stalkSpeedMultiplier = 1.22f;
        [SerializeField, Min(0f)] private float attackRecovery = 3.4f;
        [SerializeField, Min(0f)] private float searchDuration = 3f;

        [Header("Attack Audio")]
        [SerializeField] private AudioClip squidAttackClip;
        [SerializeField, Range(0f, 1f)] private float squidAttackVolume = 1f;

        [Header("Lane Changes")]
        [SerializeField] private Vector2 laneChangeDelayRange = new(2.8f, 5.5f);
        [SerializeField, Min(0.2f)] private float laneChangeDuration = 1.25f;
        [SerializeField, Range(0f, 0.45f)] private float laneDepthBias = 0.08f;

        [Header("Water Response")]
        [SerializeField, Range(0f, 1f)] private float waveFollow = 0.9f;
        [SerializeField, Range(0f, 0.35f)] private float currentInfluence = 0.055f;
        [SerializeField, Range(1f, 20f)] private float verticalResponsiveness = 8f;
        [SerializeField, Range(0f, 1f)] private float surfaceTilt = 0.32f;
        [SerializeField, Range(0f, 25f)] private float maximumTilt = 10f;
        [SerializeField, Range(0.05f, 0.8f)] private float slopeSampleDistance = 0.24f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;
        private Camera gameplayCamera;
        private GiantSquidSpriteAnimation squidAnimation;
        private TinyWaveSurfer target;
        private PredatorState predatorState;

        private int currentLane;
        private int targetLane;
        private bool changingLane;
        private float laneChangeElapsed;
        private float nextLaneChangeTime;
        private float direction;
        private float depthOffset;
        private float nextAttackTime;
        private float searchUntil;
        private bool attackHitApplied;
        private bool previousStrikeWindow;
        private bool initialised;
        private AudioSource attackAudioSource;
        private Coroutine sodaHitRoutine;

        public void Initialise(int requestedLane)
        {
            ResolveReferences();
            EnsureAttackAudio();
            if (waterLayers.Count < 2)
            {
                Debug.LogError("GiantSquidLaneSwimmer requires at least two water layers.", this);
                enabled = false;
                return;
            }

            currentLane = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            targetLane = currentLane;
            direction = startMovingRight ? 1f : -1f;
            renderItem.SetLane(currentLane);

            Vector2 position = transform.position;
            position.x = GetVisibleHorizontalCentre();
            depthOffset = -Mathf.Abs(laneDepthBias);
            position.y = GetLaneCentreY(currentLane, position.x) + depthOffset;
            SetPosition(position);
            ScheduleNextLaneChange();
            predatorState = PredatorState.Patrol;
            initialised = true;
        }

        private void Awake() => ResolveReferences();
        private void Start() { if (!initialised) Initialise(0); }

        private void ResolveReferences()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            gameplayCamera = Camera.main;
            squidAnimation = GetComponent<GiantSquidSpriteAnimation>();

            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            Collider2D existingCollider = GetComponent<Collider2D>();
            if (existingCollider == null)
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = spriteRenderer != null && spriteRenderer.sprite != null
                    ? new Vector2(spriteRenderer.sprite.bounds.size.x * 0.46f, spriteRenderer.sprite.bounds.size.y * 0.58f)
                    : new Vector2(0.8f, 0.9f);
                box.offset = new Vector2(0f, 0.08f);
            }

            waterLayers.Clear();
            waterLayers.AddRange(FindObjectsByType<PixelWaterGPU>(FindObjectsSortMode.None)
                .Where(layer => layer != null)
                .OrderBy(layer => layer.IndependentLayerIndex));
        }

        private void FixedUpdate()
        {
            if (!initialised || waterLayers.Count < 2) return;
            if (gameplayCamera == null) gameplayCamera = Camera.main;

            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            UpdatePredatorBrain(position);

            float speedMultiplier = predatorState == PredatorState.Stalk ? stalkSpeedMultiplier : 1f;
            if (squidAnimation != null) speedMultiplier *= squidAnimation.MovementSpeedMultiplier;
            Vector2 waterVelocity = GetLaneVelocity(currentLane, position.x);
            float swimSpeed = horizontalSpeed * speedMultiplier + waterVelocity.x * currentInfluence;
            position.x += direction * Mathf.Max(0.08f, swimSpeed) * Time.fixedDeltaTime;
            KeepInsideGameArea(ref position);

            if (!changingLane && (squidAnimation == null || !squidAnimation.IsAttacking))
            {
                if (target != null && !target.IsDead && predatorState != PredatorState.Patrol)
                    BeginLaneChangeToward(GetTargetLane(target));
                else if (Time.time >= nextLaneChangeTime)
                    BeginRandomLaneChange();
            }

            float desiredY = UpdateLaneTransition(position.x);
            float follow = 1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime);
            position.y = Mathf.Lerp(position.y, desiredY, follow * waveFollow);
            SetPosition(position);
            ApplyWaterTilt(position.x, follow);
            ApplyAttackHit(position);
        }

        private void UpdatePredatorBrain(Vector2 position)
        {
            if (target == null || target.IsDead)
                target = FindBestTarget(position);

            if (target == null)
            {
                predatorState = Time.time < searchUntil ? PredatorState.Search : PredatorState.Patrol;
                return;
            }

            float distance = Vector2.Distance(position, target.transform.position);
            if (distance > loseTargetRange)
            {
                target = null;
                predatorState = PredatorState.Search;
                searchUntil = Time.time + searchDuration;
                return;
            }

            predatorState = PredatorState.Stalk;
            float deltaX = target.transform.position.x - position.x;
            if (Mathf.Abs(deltaX) > 0.08f)
            {
                direction = Mathf.Sign(deltaX);
                if (spriteRenderer != null) spriteRenderer.flipX = direction < 0f;
            }

            bool sameLane = Mathf.Abs(GetTargetLane(target) - currentLane) <= 0;
            if (sameLane && distance <= attackRange && Time.time >= nextAttackTime && squidAnimation != null)
            {
                if (squidAnimation.Attack())
                {
                    predatorState = PredatorState.Attack;
                    attackHitApplied = false;
                    nextAttackTime = Time.time + attackRecovery;
                }
            }
        }

        private TinyWaveSurfer FindBestTarget(Vector2 position)
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

        private void EnsureAttackAudio()
        {
            if (attackAudioSource == null)
            {
                attackAudioSource = GetComponent<AudioSource>();
                if (attackAudioSource == null)
                    attackAudioSource = gameObject.AddComponent<AudioSource>();
                attackAudioSource.playOnAwake = false;
                attackAudioSource.loop = false;
                attackAudioSource.spatialBlend = 0f;
            }

            if (squidAttackClip == null)
                squidAttackClip = Resources.Load<AudioClip>("Audio/SFX/shark_attack");
        }

        private void ApplyAttackHit(Vector2 squidPosition)
        {
            if (squidAnimation == null || !squidAnimation.IsAttacking)
            {
                attackHitApplied = false;
                previousStrikeWindow = false;
                return;
            }

            bool strikeWindow = squidAnimation.IsInStrikeWindow;
            bool newStrikeBeat = strikeWindow && !previousStrikeWindow;
            previousStrikeWindow = strikeWindow;

            if (!newStrikeBeat || target == null || target.IsDead)
                return;
            if (Vector2.Distance(squidPosition, target.transform.position) > hitRange)
                return;

            // The squid owns a special combo pathway: every strike beat restarts
            // the surfer's hurt reaction, while only the first valid beat removes
            // one health point. Sharks and all other hazards keep their normal
            // invulnerability behaviour.
            bool applyDamage = !attackHitApplied;
            bool beatAccepted = target.TakeSquidComboBeat(squidPosition, applyDamage);
            if (!beatAccepted)
                return;

            if (applyDamage)
            {
                attackHitApplied = true;
                EnsureAttackAudio();
                if (squidAttackClip != null && attackAudioSource != null)
                    attackAudioSource.PlayOneShot(squidAttackClip, squidAttackVolume);
            }

            // Do not clear the target until the full animation combo ends.
            predatorState = PredatorState.Attack;
        }


        public void TakeSodaCanHit(Vector2 hitPosition)
        {
            if (sodaHitRoutine != null) StopCoroutine(sodaHitRoutine);
            sodaHitRoutine = StartCoroutine(SodaHitReaction(hitPosition));
            predatorState = PredatorState.Search; searchUntil = Time.time + 1.4f; target = null;
        }

        private System.Collections.IEnumerator SodaHitReaction(Vector2 hitPosition)
        {
            Color original = spriteRenderer != null ? spriteRenderer.color : Color.white;
            Vector2 away = ((Vector2)transform.position - hitPosition).normalized;
            if (away.sqrMagnitude < .01f) away = Vector2.right * -direction;
            for (int i = 0; i < 6; i++)
            {
                if (spriteRenderer != null) spriteRenderer.color = (i % 2 == 0) ? new Color(1f,.05f,.05f,1f) : original;
                Vector2 p = body != null ? body.position : (Vector2)transform.position; p += away * .045f + Vector2.up * .012f; SetPosition(p);
                transform.rotation = Quaternion.Euler(0,0,(i%2==0?1f:-1f)*9f);
                yield return new WaitForSeconds(.055f);
            }
            if (spriteRenderer != null) spriteRenderer.color = original; transform.rotation = Quaternion.identity; sodaHitRoutine = null;
        }

        private int GetTargetLane(TinyWaveSurfer surfer)
        {
            return Mathf.Clamp(surfer.CurrentWaveIndex, 0, waterLayers.Count - 2);
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
            float desiredY = Mathf.Lerp(GetLaneCentreY(currentLane, worldX), GetLaneCentreY(targetLane, worldX), eased) + depthOffset;
            if (t >= 0.5f) renderItem.SetLane(targetLane);
            if (t >= 1f)
            {
                currentLane = targetLane;
                changingLane = false;
                laneChangeElapsed = 0f;
                renderItem.SetLane(currentLane);
                ScheduleNextLaneChange();
            }
            return desiredY;
        }

        private float GetLaneCentreY(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(waterLayers[clamped].GetGameplaySurfaceHeight(worldX),
                waterLayers[clamped + 1].GetGameplaySurfaceHeight(worldX), 0.5f);
        }

        private Vector2 GetLaneVelocity(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Vector2.Lerp(waterLayers[clamped].GetGameplayWaveVelocity(worldX),
                waterLayers[clamped + 1].GetGameplayWaveVelocity(worldX), 0.5f);
        }

        private void KeepInsideGameArea(ref Vector2 position)
        {
            float minX = waterLayers[0].TankMinimum.x;
            float maxX = waterLayers[0].TankMaximum.x;

            float halfWidth =
                spriteRenderer != null
                    ? spriteRenderer.bounds.extents.x
                    : 0.45f;

            minX += halfWidth;
            maxX -= halfWidth;

            if (position.x > maxX)
            {
                position.x = maxX;
                direction = -1f;
                spriteRenderer.flipX = true;
            }
            else if (position.x < minX)
            {
                position.x = minX;
                direction = 1f;
                spriteRenderer.flipX = false;
            }
        }

        private float GetVisibleHorizontalCentre() => gameplayCamera != null && gameplayCamera.orthographic
            ? gameplayCamera.transform.position.x
            : (waterLayers[0].TankMinimum.x + waterLayers[0].TankMaximum.x) * 0.5f;

        private void ApplyWaterTilt(float worldX, float follow)
        {
            float left = GetLaneCentreY(currentLane, worldX - slopeSampleDistance);
            float right = GetLaneCentreY(currentLane, worldX + slopeSampleDistance);
            float slope = Mathf.Atan2(right - left, slopeSampleDistance * 2f) * Mathf.Rad2Deg;
            float angle = Mathf.Clamp(slope * surfaceTilt, -maximumTilt, maximumTilt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, 0f, angle), follow);
        }

        private void SetPosition(Vector2 position)
        {
            if (body != null) body.position = position;
            else transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void ScheduleNextLaneChange()
        {
            float minimum = Mathf.Min(laneChangeDelayRange.x, laneChangeDelayRange.y);
            float maximum = Mathf.Max(laneChangeDelayRange.x, laneChangeDelayRange.y);
            nextLaneChangeTime = Time.time + Random.Range(minimum, maximum);
        }
    }
}
