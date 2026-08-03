using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class SharkLaneSwimmer : MonoBehaviour
    {
        private enum PredatorState { Patrol, Stalk, Attack, Search, Retreat }

        [Header("Swimming")]
        [SerializeField, Min(0.05f)] private float horizontalSpeed = 0.75f;
        [SerializeField] private bool startMovingRight = true;
        [SerializeField, Range(0f, 0.35f)] private float viewportPadding = 0.045f;

        [Header("Predator Awareness")]
        [SerializeField, Min(0.5f)] private float detectionRange = 1f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 1.5f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.72f;
        [SerializeField, Min(0.05f)] private float hitRange = 0.72f;
        [SerializeField, Range(1f, 3f)] private float stalkSpeedMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float attackRecovery = 2.2f;
        [SerializeField, Min(0f)] private float searchDuration = 3f;

        [Header("Facing Stability")]
        [SerializeField, Min(0.05f)] private float facingDeadZone = 0.32f;
        [SerializeField, Min(0f)] private float minimumFacingHoldTime = 0.18f;

        [Header("Attack Audio")]
        [SerializeField] private AudioClip sharkAttackClip;
        [SerializeField, Range(0f, 1f)] private float sharkAttackVolume = 1f;

        [Header("Hit Retreat")]
        [SerializeField, Min(0.1f)] private float hitRetreatDuration = 2.25f;
        [SerializeField, Range(1f, 5f)] private float hitRetreatSpeedMultiplier = 2.4f;
        [SerializeField, Min(0f)] private float hitRetreatRecovery = 1.1f;

        [Header("Lane Changes")]
        [SerializeField] private Vector2 laneChangeDelayRange = new(2.8f, 5.5f);
        [SerializeField, Min(0.2f)] private float laneChangeDuration = 1.25f;
        [SerializeField, Range(0f, 0.45f)] private float laneDepthBias = 0.16f;

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
        private SharkSpriteAnimation sharkAnimation;
        private TinyWaveSurfer target;
        private PredatorState predatorState;

        private int currentLane;
        private int targetLane;
        private bool changingLane;
        private float laneChangeElapsed;
        private float nextLaneChangeTime;
        private float direction;
        private float lastFacingChangeTime = -999f;
        private float depthOffset;
        private float nextAttackTime;
        private float searchUntil;
        private bool attackHitApplied;
        private bool initialised;
        private AudioSource attackAudioSource;
        private Coroutine sodaHitRoutine;
        private float retreatUntil;
        private Color baseSpriteTint = Color.white;

        public void Initialise(int requestedLane, bool spawnAtSectionEdge = false)
        {
            ResolveReferences();
            EnsureAttackAudio();
            if (waterLayers.Count < 2)
            {
                Debug.LogError("SharkLaneSwimmer requires at least two water layers.", this);
                enabled = false;
                return;
            }

            currentLane = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            targetLane = currentLane;
            direction = startMovingRight ? 1f : -1f;
            renderItem.SetLane(currentLane);
            if (sharkAnimation != null) sharkAnimation.SetAutonomousRandomAttacks(false);

            Vector2 position = transform.position;
            if (spawnAtSectionEdge)
            {
                position.x = CameraSafeSpawnUtility.ChooseOffscreenEntryX(
                    waterLayers, spriteRenderer, out bool enterFromLeft);
                direction = enterFromLeft ? 1f : -1f;
                if (spriteRenderer != null)
                    spriteRenderer.flipX = !enterFromLeft;
            }
            else
            {
                position.x = GetVisibleHorizontalCentre();
            }
            depthOffset = -Mathf.Abs(laneDepthBias);
            position.y = GetLaneCentreY(currentLane, position.x) + depthOffset;
            SetPosition(position);
            ScheduleNextLaneChange();
            predatorState = PredatorState.Patrol;
            initialised = true;
        }

        private void Awake()
        {
            ResolveReferences();
            CacheAndResetSpriteTint();
        }

        private void OnEnable()
        {
            CacheAndResetSpriteTint();
        }

        private void OnDisable()
        {
            if (sodaHitRoutine != null)
            {
                StopCoroutine(sodaHitRoutine);
                sodaHitRoutine = null;
            }

            ResetHitVisuals();
        }

        private void Start() { if (!initialised) Initialise(0); }

        private void ResolveReferences()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            gameplayCamera = Camera.main;
            sharkAnimation = GetComponent<SharkSpriteAnimation>();

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
                    ? spriteRenderer.sprite.bounds.size * 0.52f
                    : new Vector2(1.2f, 0.45f);
            }

            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
        }

        private void FixedUpdate()
        {
            if (!initialised || waterLayers.Count < 2) return;
            if (gameplayCamera == null) gameplayCamera = Camera.main;

            Vector2 position = body != null ? body.position : (Vector2)transform.position;

            bool retreating = Time.time < retreatUntil;
            if (retreating)
            {
                predatorState = PredatorState.Retreat;
                target = null;
            }
            else
            {
                if (predatorState == PredatorState.Retreat)
                {
                    predatorState = PredatorState.Search;
                    searchUntil = Time.time + hitRetreatRecovery;
                }

                UpdatePredatorBrain(position);
            }

            float speedMultiplier = retreating
                ? hitRetreatSpeedMultiplier
                : predatorState == PredatorState.Stalk ? stalkSpeedMultiplier : 1f;
            if (!retreating && sharkAnimation != null) speedMultiplier *= sharkAnimation.MovementSpeedMultiplier;
            Vector2 waterVelocity = GetLaneVelocity(currentLane, position.x);
            float swimSpeed = horizontalSpeed * speedMultiplier + waterVelocity.x * currentInfluence;
            position.x += direction * Mathf.Max(0.08f, swimSpeed) * Time.fixedDeltaTime;
            KeepInsideGameArea(ref position);

            if (!retreating && !changingLane && (sharkAnimation == null || !sharkAnimation.IsAttacking))
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
            TryFaceHorizontalDelta(deltaX);

            bool sameLane = Mathf.Abs(GetTargetLane(target) - currentLane) <= 0;
            if (sameLane && distance <= attackRange && Time.time >= nextAttackTime && sharkAnimation != null)
            {
                if (sharkAnimation.Attack())
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

            if (sharkAttackClip == null)
                sharkAttackClip = Resources.Load<AudioClip>("Audio/SFX/shark_attack");
        }

        private void ApplyAttackHit(Vector2 sharkPosition)
        {
            if (sharkAnimation == null || !sharkAnimation.IsAttacking)
            {
                attackHitApplied = false;
                return;
            }
            if (attackHitApplied || target == null || target.IsDead) return;
            if (!sharkAnimation.IsInHitWindow) return;
            if (Vector2.Distance(sharkPosition, target.transform.position) > hitRange) return;

            attackHitApplied = target.TakeSharkHit(sharkPosition);
            if (attackHitApplied)
            {
                EnsureAttackAudio();
                if (sharkAttackClip != null && attackAudioSource != null)
                    attackAudioSource.PlayOneShot(sharkAttackClip, sharkAttackVolume);
                predatorState = PredatorState.Search;
                searchUntil = Time.time + searchDuration;
                target = null;
            }
        }


        private void TryFaceHorizontalDelta(float deltaX)
        {
            // Keep the previous facing while nearly aligned with the target. This
            // prevents tiny position changes from flipping the sprite every frame.
            if (Mathf.Abs(deltaX) <= facingDeadZone)
                return;

            float desiredDirection = Mathf.Sign(deltaX);
            if (Mathf.Approximately(desiredDirection, direction))
                return;

            if (Time.time - lastFacingChangeTime < minimumFacingHoldTime)
                return;

            direction = desiredDirection;
            lastFacingChangeTime = Time.time;
            if (spriteRenderer != null)
                spriteRenderer.flipX = direction < 0f;
        }

        public void TakeSodaCanHit(Vector2 hitPosition)
        {
            Vector2 away = (Vector2)transform.position - hitPosition;
            if (Mathf.Abs(away.x) < 0.01f)
                away.x = -direction;

            direction = Mathf.Sign(away.x);
            if (spriteRenderer != null)
                spriteRenderer.flipX = direction < 0f;

            retreatUntil = Time.time + hitRetreatDuration;
            predatorState = PredatorState.Retreat;
            target = null;
            changingLane = false;
            attackHitApplied = false;
            nextAttackTime = retreatUntil + hitRetreatRecovery;

            if (sodaHitRoutine != null)
                StopCoroutine(sodaHitRoutine);

            ResetHitVisuals();
            sodaHitRoutine = StartCoroutine(SodaHitReaction());
        }

        private System.Collections.IEnumerator SodaHitReaction()
        {
            for (int i = 0; i < 6; i++)
            {
                if (spriteRenderer != null)
                    spriteRenderer.color = i % 2 == 0
                        ? new Color(1f, 0.05f, 0.05f, baseSpriteTint.a)
                        : baseSpriteTint;

                transform.rotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 9f);
                yield return new WaitForSeconds(0.055f);
            }

            ResetHitVisuals();
            sodaHitRoutine = null;
        }

        private void CacheAndResetSpriteTint()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
                return;

            // Do not cache the temporary blood-red flash as the permanent tint.
            Color current = spriteRenderer.color;
            bool looksLikeHitFlash = current.r > 0.85f && current.g < 0.25f && current.b < 0.25f;
            if (!looksLikeHitFlash)
                baseSpriteTint = current;

            spriteRenderer.color = baseSpriteTint;
            transform.rotation = Quaternion.identity;
        }

        private void ResetHitVisuals()
        {
            if (spriteRenderer != null)
                spriteRenderer.color = baseSpriteTint;

            transform.rotation = Quaternion.identity;
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
