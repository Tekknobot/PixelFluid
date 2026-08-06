using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class GodzillaLaneSwimmer : MonoBehaviour
    {
        public event Action<GodzillaLaneSwimmer> ArenaHitAccepted;
        private enum CreatureState
        {
            Roam,
            Search,
            Pursue,
            WindUp,
            Lunge,
            Recover,
            InvestigateDeath,
            MournDeath
        }

        [Header("Movement")]
        [SerializeField, Min(0.05f)] private float cruiseSpeed = 0.65f;
        [SerializeField, Min(0.05f)] private float pursuitSpeed = 1.35f;
        [SerializeField, Min(0.1f)] private float lungeSpeed = 2.35f;
        [SerializeField, Range(0f, 0.35f)] private float currentInfluence = 0.025f;

        [Header("Unique Behaviour")]
        [SerializeField, Min(0.5f)] private float detectionRange = 15f;
        [SerializeField, Min(0.5f)] private float abandonRange = 21f;
        [SerializeField, Min(0.1f)] private float attackRange = 2.55f;
        [SerializeField, Min(0.2f)] private float searchDuration = 1.6f;
        [SerializeField, Min(0.05f)] private float searchSpeed = 0.82f;
        [SerializeField, Min(0.1f)] private float targetRefreshInterval = 0.25f;
        [SerializeField, Min(0.25f)] private float pursuitTurnThreshold = 1.4f;
        [SerializeField, Min(0.05f)] private float hitRange = 0.68f;
        [SerializeField, Min(0f)] private float windUpDuration = 0.42f;
        [SerializeField, Min(0f)] private float attackRecovery = 1.15f;
        [SerializeField] private Vector2 laneShiftDelayRange = new(1.15f, 2.25f);
        [SerializeField, Min(0.2f)] private float laneChangeDuration = 0.72f;
        [SerializeField, Range(0f, 0.45f)] private float laneDepthBias = 0.08f;

        [Header("Health and Thrown Item Damage")]
        [SerializeField, Min(1)] private int maximumHealth = 8;
        [SerializeField, Min(1)] private int thrownItemDamage = 1;
        [SerializeField, Min(0.01f)] private float hurtFlashDuration = 0.12f;
        [SerializeField, Range(0f, 1f)] private float hurtFlashRed = 1f;
        [SerializeField, Range(0f, 1f)] private float hurtFlashGreen = 0.12f;
        [SerializeField, Range(0f, 1f)] private float hurtFlashBlue = 0.12f;
        [SerializeField, Min(0f)] private float hitAggressionDuration = 4f;
        [Tooltip("Minimum time between accepted projectile hits. Hits during armour recovery ricochet without damage.")]
        [SerializeField, Min(0.05f)] private float vulnerabilityCooldown = 1.15f;
        [SerializeField, Min(0f)] private float openingInvulnerability = 1.6f;
        [SerializeField, Min(0f)] private float deathDelay = 0.3f;
        [SerializeField] private AudioClip hurtClip;
        [SerializeField, Range(0f, 1f)] private float hurtVolume = 1f;
        [SerializeField, Min(0.08f)] private float hitReactionDuration = 3.28f;
        [SerializeField, Range(0.01f, 0.35f)] private float hitReactionScalePunch = 0.14f;
        [SerializeField, Range(0.05f, 0.6f)] private float hitCameraFocusDuration = 3.32f;

        [Header("Boss Death Sequence")]
        [SerializeField, Min(0.5f)] private float bossDeathDuration = 5.6f;
        [SerializeField, Min(0f)] private float bossDeathSinkDistance = 2.8f;
        [SerializeField, Min(0f)] private float bossDeathShakeAmount = 0.11f;
        [SerializeField, Min(1f)] private float bossDeathShakeFrequency = 28f;
        [SerializeField, Min(0.02f)] private float bossDeathFlashInterval = 0.09f;
        [SerializeField, Range(0f, 1f)] private float bossDeathRedGreen = 0.02f;
        [SerializeField, Range(0f, 1f)] private float bossDeathRedBlue = 0.02f;
        [SerializeField, Min(0f)] private float bossDeathTiltDegrees = 12f;

        [Header("Player Death Response")]
        [SerializeField, Min(0.05f)] private float deathApproachSpeed = 6.82f;
        [SerializeField, Min(0.05f)] private float deathArrivalDistance = 0.7f;
        [SerializeField, Min(0f)] private float deathPauseDuration = 2.5f;

        [Header("Summoned Skulls")]
        [Tooltip("Time range between unique skull swarms.")]
        [SerializeField] private Vector2 skullSpawnInterval = new(5.5f, 8.5f);
        [Tooltip("Number of skulls released in one staggered spiral.")]
        [SerializeField, Range(1, 5)] private int skullsPerWave = 3;
        [Tooltip("Extra delay before the first skull swarm.")]
        [SerializeField, Min(0f)] private float skullOpeningDelay = 2.25f;

        [Header("Water Response")]
        [SerializeField, Range(0f, 1f)] private float waveFollow = 0.88f;
        [SerializeField, Range(1f, 20f)] private float verticalResponsiveness = 7f;
        [SerializeField, Range(0f, 1f)] private float surfaceTilt = 0.2f;
        [SerializeField, Range(0f, 25f)] private float maximumTilt = 7f;
        [SerializeField, Range(0.05f, 0.8f)] private float slopeSampleDistance = 0.3f;

        [Header("Facing Stability")]
        [SerializeField, Min(0.05f)] private float facingDeadZone = 0.55f;
        [SerializeField, Min(0f)] private float minimumFacingHoldTime = 0.8f;

        [Header("Spatial Reaper Horn")]
        [SerializeField] private AudioClip reaperHornClip;
        [SerializeField, Range(0f, 1f)] private float reaperHornVolume = 0.95f;
        [SerializeField] private Vector2 reaperHornInterval = new(8f, 14f);
        [SerializeField, Min(0.1f)] private float audioMinDistance = 5f;
        [SerializeField, Min(1f)] private float audioMaxDistance = 32f;

        [Header("Attack Audio")]
        [SerializeField] private AudioClip attackClip;
        [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;
        private Camera gameplayCamera;
        private GodzillaSpriteAnimation animation;
        private TinyWaveSurfer target;
        private AudioSource audioSource;
        private float nextReaperHornTime;

        [SerializeField] private AudioClip reaperDeathClip;
        [SerializeField, Range(0f, 1f)] private float reaperDeathVolume = 1f;

        private CreatureState state;
        private int currentLane;
        private int targetLane;
        private bool changingLane;
        private float laneChangeElapsed;
        private float nextLaneShiftTime;
        private float nextAttackTime;
        private float stateUntil;
        private float direction = 1f;
        private float lastFacingChangeTime = -999f;
        private float depthOffset;
        private bool attackHitApplied;
        private bool initialised;
        private bool respondingToDeath;
        private Vector2 deathLocation;
        private int deathLane;
        private float deathPauseUntil;
        private float trackedSectionCentreX;
        private bool hasTrackedSectionCentre;
        private float nextWaterRefreshTime;
        private float nextSkullSpawnTime;
        private float nextTargetRefreshTime;
        private float lastKnownTargetX;
        private bool hasLastKnownTargetX;

        private readonly HashSet<GameObject> consumedProjectiles = new();
        private int currentHealth;
        private bool defeated;
        private float enragedUntil;
        private Coroutine hurtFlashRoutine;
        private Coroutine hitReactionRoutine;
        private Vector3 normalLocalScale;
        private Color normalSpriteColour = Color.white;
        private float nextVulnerableTime;
        private bool arenaEntranceActive;
        private float arenaEntranceTargetX;
        private float arenaEntranceSpeed;

        public bool IsArenaEntranceActive => arenaEntranceActive;

        public void BeginArenaEntrance(float targetX, float speed)
        {
            ResolveReferences();
            arenaEntranceTargetX = targetX;
            arenaEntranceSpeed = Mathf.Max(0.5f, speed);
            arenaEntranceActive = true;
            respondingToDeath = false;
            target = null;
            changingLane = false;
            attackHitApplied = false;
            state = CreatureState.Roam;
            nextVulnerableTime = float.PositiveInfinity;
        }

        private void UpdateArenaEntrance(Vector2 position)
        {
            float delta = arenaEntranceTargetX - position.x;
            if (Mathf.Abs(delta) > 0.01f)
            {
                direction = Mathf.Sign(delta);
                if (spriteRenderer != null)
                    spriteRenderer.flipX = direction < 0f;
            }

            position.x = Mathf.MoveTowards(
                position.x,
                arenaEntranceTargetX,
                arenaEntranceSpeed * Time.fixedDeltaTime);

            float desiredY = UpdateLaneTransition(position.x);
            float follow = 1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime);
            position.y = Mathf.Lerp(position.y, desiredY, follow * waveFollow);
            SetPosition(position);
            ApplyWaterTilt(position.x, follow);

            if (Mathf.Abs(position.x - arenaEntranceTargetX) <= 0.025f)
            {
                arenaEntranceActive = false;
                nextVulnerableTime = Time.time + Mathf.Max(0f, openingInvulnerability);
                state = CreatureState.Roam;
                ScheduleLaneShift();
            }
        }



        /// <summary>
        /// Sends every active Godzilla-based Death swimmer to the player's death location.
        /// The swimmer temporarily interrupts combat and roaming, visits the location,
        /// pauses there, then resumes its normal behaviour.
        /// </summary>
        public static void NotifyPlayerDeath(Vector2 worldPosition)
        {
            GodzillaLaneSwimmer[] swimmers = FindObjectsByType<GodzillaLaneSwimmer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (GodzillaLaneSwimmer swimmer in swimmers)
            {
                if (swimmer != null && swimmer.isActiveAndEnabled)
                    swimmer.GoToDeathLocation(worldPosition);
            }
        }

        public void GoToDeathLocation(Vector2 worldPosition)
        {
            ResolveReferences();
            if (waterLayers.Count < 2)
                return;

            respondingToDeath = true;
            deathLocation = worldPosition;
            deathLane = FindClosestLane(worldPosition);
            target = null;
            attackHitApplied = false;
            changingLane = false;
            currentLane = Mathf.Clamp(currentLane, 0, waterLayers.Count - 2);
            targetLane = currentLane;
            state = CreatureState.InvestigateDeath;

            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            FaceWorldX(position, deathLocation.x);
            BeginLaneChangeToward(deathLane);
        }

        public void Initialise(int requestedLane)
        {
            ResolveReferences();
            if (waterLayers.Count < 2)
            {
                Debug.LogError("GodzillaLaneSwimmer requires at least two water layers.", this);
                enabled = false;
                return;
            }

            currentLane = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            targetLane = currentLane;
            renderItem.SetLane(currentLane);
            depthOffset = -Mathf.Abs(laneDepthBias);
            direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;

            Vector2 position = transform.position;
            position.x = CameraSafeSpawnUtility.ChooseOffscreenEntryX(
                waterLayers, spriteRenderer, out bool enterFromLeft, 0.9f);
            direction = enterFromLeft ? 1f : -1f;
            position.y = GetLaneCentreY(currentLane, position.x) + depthOffset;
            SetPosition(position);

            state = CreatureState.Roam;
            ScheduleLaneShift();
            ScheduleSkullWave(skullOpeningDelay);
            initialised = true;
        }

        private void Awake()
        {
            
            normalLocalScale = transform.localScale;
            ResolveReferences();
            currentHealth = Mathf.Max(1, maximumHealth);
            nextVulnerableTime = Time.time + openingInvulnerability;
            if (spriteRenderer != null)
                normalSpriteColour = spriteRenderer.color;
        }

        private void Start()
        {
            if (!initialised)
                Initialise(2);
        }

        private void ResolveReferences()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            animation = GetComponent<GodzillaSpriteAnimation>();
            gameplayCamera = Camera.main;

            body = GetComponent<Rigidbody2D>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            Collider2D collider = GetComponent<Collider2D>();
            if (collider == null)
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = spriteRenderer != null && spriteRenderer.sprite != null
                    ? spriteRenderer.sprite.bounds.size * 0.42f
                    : new Vector2(1.4f, 1f);
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = audioMinDistance;
            audioSource.maxDistance = Mathf.Max(audioMinDistance + 0.1f, audioMaxDistance);
            audioSource.dopplerLevel = 0.1f;
            reaperHornClip ??= Resources.Load<AudioClip>("Audio/SFX/reaper_horn");

            hurtClip ??= Resources.Load<AudioClip>("Audio/SFX/reaper_hurt");

            AudioClip reaperDeathClip =
                Resources.Load<AudioClip>("Audio/SFX/reaper_death");

            ScheduleReaperHorn();
            if (attackClip == null)
                attackClip = Resources.Load<AudioClip>("Audio/SFX/shark_attack");

            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
            CaptureTrackedSectionCentre();
        }

        private void ScheduleReaperHorn()
        {
            float minimum = Mathf.Max(1f, reaperHornInterval.x);
            float maximum = Mathf.Max(minimum, reaperHornInterval.y);
            nextReaperHornTime = Time.time + UnityEngine.Random.Range(minimum, maximum);
        }

        private void FixedUpdate()
        {
            if (!defeated && Time.time >= nextReaperHornTime)
            {
                if (reaperHornClip != null && audioSource != null)
                    audioSource.PlayOneShot(reaperHornClip, reaperHornVolume);
                ScheduleReaperHorn();
            }

            if (defeated || !initialised || waterLayers.Count < 2)
                return;

            RefreshWaterLayersIfNeeded();
            FollowRecycledSection();
            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            if (arenaEntranceActive)
            {
                UpdateArenaEntrance(position);
                return;
            }

            UpdateSkullSpawner();

            if (respondingToDeath)
                UpdateDeathInvestigation(position);
            else
                UpdateBrain(position);

            Vector2 waterVelocity = GetLaneVelocity(currentLane, position.x);
            float aggressionMultiplier = Time.time < enragedUntil ? 1.28f : 1f;
            float speed = state switch
            {
                CreatureState.Search => searchSpeed * aggressionMultiplier,
                CreatureState.Pursue => pursuitSpeed * aggressionMultiplier,
                CreatureState.Lunge => lungeSpeed * aggressionMultiplier,
                CreatureState.WindUp => cruiseSpeed * 0.15f,
                CreatureState.Recover => cruiseSpeed * 0.55f,
                CreatureState.InvestigateDeath => deathApproachSpeed,
                CreatureState.MournDeath => 0f,
                _ => cruiseSpeed
            };

            if (state == CreatureState.InvestigateDeath)
                FaceWorldX(position, deathLocation.x);

            if (state != CreatureState.MournDeath)
                position.x += direction * Mathf.Max(0.05f, speed + waterVelocity.x * currentInfluence) * Time.fixedDeltaTime;
            KeepInsideGameArea(ref position);

            if (!respondingToDeath && !changingLane && state != CreatureState.WindUp && state != CreatureState.Lunge)
            {
                if (target != null && !target.IsDead && state == CreatureState.Pursue)
                {
                    int desiredLane = GetTargetLane(target);
                    if (desiredLane != currentLane)
                        BeginLaneChangeToward(desiredLane);
                }
                else if (Time.time >= nextLaneShiftTime)
                    BeginDistinctLaneShift();
            }

            float desiredY = UpdateLaneTransition(position.x);
            float follow = 1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime);
            position.y = Mathf.Lerp(position.y, desiredY, follow * waveFollow);
            SetPosition(position);
            ApplyWaterTilt(position.x, follow);
            ApplyAttackHit(position);
        }


        private void RefreshWaterLayersIfNeeded()
        {
            if (Time.time < nextWaterRefreshTime)
                return;

            nextWaterRefreshTime = Time.time + 1f;
            if (waterLayers.Count >= 2 && waterLayers.All(layer => layer != null && layer.isActiveAndEnabled))
                return;

            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(position.x));
            waterLayers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            waterLayers.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));

            if (waterLayers.Count >= 2)
            {
                currentLane = Mathf.Clamp(currentLane, 0, waterLayers.Count - 2);
                targetLane = Mathf.Clamp(targetLane, 0, waterLayers.Count - 2);
                renderItem?.SetLane(currentLane);
                CaptureTrackedSectionCentre();
            }
        }

        /// <summary>
        /// EndlessWaveSections recycles a complete water section by shifting its
        /// existing PixelWaterGPU objects several section widths. Godzilla is not
        /// parented to those water objects, so it must receive the same translation
        /// or its old clamp bounds will pin it to an edge and repeatedly reverse it.
        /// </summary>
        private void FollowRecycledSection()
        {
            if (waterLayers.Count == 0 || waterLayers[0] == null)
                return;

            float sectionCentre = GetCurrentSectionCentreX();
            if (!hasTrackedSectionCentre)
            {
                trackedSectionCentreX = sectionCentre;
                hasTrackedSectionCentre = true;
                return;
            }

            float shift = sectionCentre - trackedSectionCentreX;
            trackedSectionCentreX = sectionCentre;

            // Ignore ordinary floating-point movement. A recycled section moves by
            // roughly three whole section widths in one frame.
            if (Mathf.Abs(shift) < 0.25f)
                return;

            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            position.x += shift;
            SetPosition(position);

            // A death-investigation destination belongs to the same recycled world
            // space while Godzilla is travelling toward or pausing at it.
            if (respondingToDeath)
                deathLocation.x += shift;
        }

        private void CaptureTrackedSectionCentre()
        {
            if (waterLayers.Count == 0 || waterLayers[0] == null)
                return;

            trackedSectionCentreX = GetCurrentSectionCentreX();
            hasTrackedSectionCentre = true;
        }

        private float GetCurrentSectionCentreX()
        {
            PixelWaterGPU layer = waterLayers[0];
            return (layer.TankMinimum.x + layer.TankMaximum.x) * 0.5f;
        }

        private void UpdateDeathInvestigation(Vector2 position)
        {
            if (state == CreatureState.MournDeath)
            {
                if (Time.time >= deathPauseUntil)
                {
                    respondingToDeath = false;
                    state = CreatureState.Roam;
                    ScheduleLaneShift();
                }
                return;
            }

            state = CreatureState.InvestigateDeath;
            FaceWorldX(position, deathLocation.x);

            if (!changingLane && currentLane != deathLane)
                BeginLaneChangeToward(deathLane);

            float horizontalDistance = Mathf.Abs(position.x - deathLocation.x);
            if (horizontalDistance <= deathArrivalDistance &&
                !changingLane && currentLane == deathLane)
            {
                state = CreatureState.MournDeath;
                deathPauseUntil = Time.time + deathPauseDuration;
            }
        }

        private int FindClosestLane(Vector2 worldPosition)
        {
            int bestLane = 0;
            float bestDistance = float.PositiveInfinity;
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);

            for (int lane = 0; lane < laneCount; lane++)
            {
                float laneY = GetLaneCentreY(lane, worldPosition.x) + depthOffset;
                float distance = Mathf.Abs(worldPosition.y - laneY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLane = lane;
                }
            }

            return bestLane;
        }

        private void FaceWorldX(Vector2 position, float worldX)
        {
            TryFaceHorizontalDelta(worldX - position.x);
        }

        private void UpdateBrain(Vector2 position)
        {
            if (state == CreatureState.WindUp)
            {
                if (target == null || target.IsDead)
                {
                    BeginSearch(position);
                    return;
                }

                RememberTarget();
                // Facing is committed during wind-up. Do not mirror every time the
                // player crosses the Reaper's centre.
                if (Time.time >= stateUntil)
                {
                    animation?.Attack();
                    attackHitApplied = false;
                    state = CreatureState.Lunge;
                }
                return;
            }

            if (state == CreatureState.Lunge)
            {
                if (animation == null || !animation.IsAttacking)
                {
                    state = CreatureState.Recover;
                    stateUntil = Time.time +
                        (Time.time < enragedUntil ? 0.35f : 0.55f);
                    nextAttackTime = Time.time + attackRecovery;
                }
                return;
            }

            if (state == CreatureState.Recover)
            {
                if (Time.time >= stateUntil)
                    BeginSearch(position);
                return;
            }

            if (state == CreatureState.Search)
            {
                // Search in the last known direction for a readable amount of time.
                // Reacquire on a small interval rather than changing decisions every
                // physics frame.
                if (Time.time >= nextTargetRefreshTime)
                {
                    nextTargetRefreshTime =
                        Time.time + Mathf.Max(0.05f, targetRefreshInterval);
                    target = FindBestTarget(position);
                }

                if (target != null && !target.IsDead)
                {
                    state = CreatureState.Pursue;
                    CommitFacingTowardTarget(position, true);
                    return;
                }

                if (Time.time >= stateUntil)
                {
                    state = CreatureState.Roam;
                    hasLastKnownTargetX = false;
                }
                return;
            }

            if (target == null ||
                target.IsDead ||
                Time.time >= nextTargetRefreshTime)
            {
                nextTargetRefreshTime =
                    Time.time + Mathf.Max(0.05f, targetRefreshInterval);
                target = FindBestTarget(position);
            }

            if (target == null)
            {
                if (hasLastKnownTargetX)
                    BeginSearch(position);
                else
                    state = CreatureState.Roam;
                return;
            }

            float distance =
                Vector2.Distance(position, target.transform.position);

            if (distance > abandonRange)
            {
                RememberTarget();
                target = null;
                BeginSearch(position);
                return;
            }

            state = CreatureState.Pursue;
            RememberTarget();
            CommitFacingTowardTarget(position, false);

            bool sameLane = GetTargetLane(target) == currentLane;
            if (sameLane &&
                distance <= attackRange &&
                Time.time >= nextAttackTime)
            {
                // Lock the facing direction for the whole attack sequence.
                CommitFacingTowardTarget(position, true);
                state = CreatureState.WindUp;
                stateUntil = Time.time + windUpDuration;
            }
        }

        private void BeginSearch(Vector2 position)
        {
            RememberTarget();
            target = null;
            state = CreatureState.Search;
            stateUntil = Time.time + Mathf.Max(0.2f, searchDuration);
            nextTargetRefreshTime = Time.time;

            if (hasLastKnownTargetX)
                TryFaceHorizontalDelta(
                    lastKnownTargetX - position.x,
                    true);
        }

        private void RememberTarget()
        {
            if (target == null || target.IsDead)
                return;

            lastKnownTargetX = target.transform.position.x;
            hasLastKnownTargetX = true;
        }

        private void CommitFacingTowardTarget(
            Vector2 position,
            bool force)
        {
            if (target == null)
                return;

            float deltaX =
                target.transform.position.x - position.x;

            if (!force &&
                Mathf.Sign(deltaX) != direction &&
                Mathf.Abs(deltaX) < pursuitTurnThreshold)
            {
                return;
            }

            TryFaceHorizontalDelta(deltaX, force);
        }

        private TinyWaveSurfer FindBestTarget(Vector2 position)
        {
            TinyWaveSurfer player = FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None)
                .Where(surfer => surfer != null && !surfer.IsDead)
                .OrderByDescending(surfer => surfer.IsPlayerControlled)
                .ThenBy(surfer => Vector2.Distance(position, surfer.transform.position))
                .FirstOrDefault();

            if (player == null) return null;
            float distance = Vector2.Distance(position, player.transform.position);
            return distance <= detectionRange ? player : null;
        }

        private void FaceTarget(Vector2 position)
        {
            CommitFacingTowardTarget(position, false);
        }

        private void TryFaceHorizontalDelta(
            float deltaX,
            bool force = false)
        {
            if (!force && Mathf.Abs(deltaX) <= facingDeadZone)
                return;

            float desiredDirection = Mathf.Sign(deltaX);
            if (Mathf.Approximately(desiredDirection, direction))
                return;

            if (!force &&
                Time.time - lastFacingChangeTime <
                minimumFacingHoldTime)
            {
                return;
            }

            direction = desiredDirection;
            lastFacingChangeTime = Time.time;

            if (spriteRenderer != null)
                spriteRenderer.flipX = direction < 0f;
        }

        private void ApplyAttackHit(Vector2 position)
        {
            if (state != CreatureState.Lunge || animation == null || !animation.IsAttacking)
            {
                attackHitApplied = false;
                return;
            }

            if (attackHitApplied || target == null || target.IsDead || !animation.IsInHitWindow)
                return;
            if (Vector2.Distance(position, target.transform.position) > hitRange)
                return;

            attackHitApplied = target.TakeSharkHit(position);
            if (attackHitApplied && attackClip != null && audioSource != null)
                audioSource.PlayOneShot(attackClip, attackVolume);
        }


        /// <summary>
        /// Applies damage from any throwable using SodaCanProjectile. The creature
        /// has health but intentionally does not create or expose a health bar.
        /// </summary>
        public bool TakeThrownItemHit(int damage, Vector2 impactPosition)
        {
            if (defeated || arenaEntranceActive || damage <= 0)
                return false;

            // Boss armour only opens for one hit at a time. Returning false lets
            // SodaCanProjectile continue its normal ricochet instead of melting
            // the boss with a pile of projectiles in the same second.
            if (Time.time < nextVulnerableTime)
                return false;

            nextVulnerableTime = Time.time + Mathf.Max(0.05f, vulnerabilityCooldown);
            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, Mathf.Min(damage, thrownItemDamage)));
            ArenaHitAccepted?.Invoke(this);
            enragedUntil = Mathf.Max(enragedUntil, Time.time + hitAggressionDuration);

            // Immediately retaliate against the active player after being struck.
            TinyWaveSurfer retaliationTarget = FindBestTarget(transform.position);
            if (retaliationTarget != null)
            {
                target = retaliationTarget;
                state = CreatureState.Pursue;
                nextAttackTime = Mathf.Min(nextAttackTime, Time.time + 0.08f);
                FaceTarget(body != null ? body.position : (Vector2)transform.position);
            }

            if (hurtFlashRoutine != null)
                StopCoroutine(hurtFlashRoutine);
            hurtFlashRoutine = StartCoroutine(HurtFlash());

            if (hitReactionRoutine != null)
                StopCoroutine(hitReactionRoutine);
            hitReactionRoutine = StartCoroutine(HitReaction());

            TinySurferCinematicCamera hitCamera =
                FindFirstObjectByType<TinySurferCinematicCamera>();
            hitCamera?.BeginBossHitFocus(
                transform,
                hitCameraFocusDuration);

            if (hurtClip != null && audioSource != null)
                audioSource.PlayOneShot(hurtClip, hurtVolume);

            if (currentHealth <= 0)
                StartCoroutine(DefeatAfterFlash());

            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // SodaCanProjectile owns thrown-item hit detection and ricochet.
            // Keeping damage in one place prevents duplicate hits and prevents
            // this swimmer from absorbing the projectile before it can bounce.
        }

        private IEnumerator HitReaction()
        {
            Vector3 baseScale =
                normalLocalScale == Vector3.zero
                    ? transform.localScale
                    : normalLocalScale;

            float duration = Mathf.Max(0.08f, hitReactionDuration);
            float elapsed = 0f;

            while (elapsed < duration && !defeated)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t);
                float punch = 1f + wave * hitReactionScalePunch;

                transform.localScale = new Vector3(
                    baseScale.x * punch,
                    baseScale.y * (2f - punch),
                    baseScale.z);

                yield return null;
            }

            transform.localScale = baseScale;
            hitReactionRoutine = null;
        }

        private IEnumerator HurtFlash()
        {
            if (spriteRenderer == null)
                yield break;

            normalSpriteColour = spriteRenderer.color;
            spriteRenderer.color = new Color(
                hurtFlashRed,
                hurtFlashGreen,
                hurtFlashBlue,
                normalSpriteColour.a);

            yield return new WaitForSeconds(hurtFlashDuration);

            if (spriteRenderer != null && !defeated)
                spriteRenderer.color = normalSpriteColour;

            hurtFlashRoutine = null;
        }

        private IEnumerator DefeatAfterFlash()
        {
            if (defeated)
                yield break;

            defeated = true;

            if (reaperDeathClip != null && audioSource != null)
                audioSource.PlayOneShot(reaperDeathClip, reaperDeathVolume);
                
            TinySurferCinematicCamera deathCamera =
                FindFirstObjectByType<TinySurferCinematicCamera>();
            deathCamera?.BeginBossDeathFocus(transform);

            target = null;
            respondingToDeath = false;
            changingLane = false;
            attackHitApplied = false;
            state = CreatureState.Recover;

            if (hurtFlashRoutine != null)
            {
                StopCoroutine(hurtFlashRoutine);
                hurtFlashRoutine = null;
            }

            if (hitReactionRoutine != null)
            {
                StopCoroutine(hitReactionRoutine);
                hitReactionRoutine = null;
                transform.localScale =
                    normalLocalScale == Vector3.zero
                        ? transform.localScale
                        : normalLocalScale;
            }

            Collider2D[] colliders = GetComponents<Collider2D>();
            foreach (Collider2D hitbox in colliders)
            {
                if (hitbox != null)
                    hitbox.enabled = false;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            Vector2 startPosition = body != null
                ? body.position
                : (Vector2)transform.position;

            Quaternion startRotation = transform.rotation;
            Color originalColour = spriteRenderer != null
                ? normalSpriteColour
                : Color.white;
            Color deathRed = new Color(
                1f,
                bossDeathRedGreen,
                bossDeathRedBlue,
                originalColour.a);

            float duration = Mathf.Max(0.5f, bossDeathDuration);
            float elapsed = 0f;
            float nextFlashTime = 0f;
            bool showRed = true;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedSink = t * t * (3f - 2f * t);

                if (elapsed >= nextFlashTime)
                {
                    showRed = !showRed;
                    nextFlashTime = elapsed +
                        bossDeathFlashInterval * Mathf.Lerp(0.65f, 1.9f, t);

                    if (spriteRenderer != null)
                        spriteRenderer.color = showRed ? deathRed : originalColour;
                }

                float shakeFade = 1f - t;
                float shakeX = Mathf.Sin(elapsed * bossDeathShakeFrequency)
                    * bossDeathShakeAmount * shakeFade;
                float shakeY = Mathf.Cos(elapsed * bossDeathShakeFrequency * 0.73f)
                    * bossDeathShakeAmount * 0.55f * shakeFade;

                Vector2 deathPosition = startPosition + new Vector2(
                    shakeX,
                    shakeY - bossDeathSinkDistance * easedSink);

                SetPosition(deathPosition);

                float tiltDirection = direction >= 0f ? -1f : 1f;
                float tilt = bossDeathTiltDegrees * tiltDirection * easedSink;
                transform.rotation = Quaternion.Slerp(
                    startRotation,
                    Quaternion.Euler(0f, 0f, tilt),
                    easedSink);

                yield return null;
            }

            if (spriteRenderer != null)
                spriteRenderer.color = deathRed;

            yield return new WaitForSeconds(Mathf.Max(0f, deathDelay));

            deathCamera?.EndBossDeathFocus(transform);

            SurfDayProgressionDirector progression = FindFirstObjectByType<SurfDayProgressionDirector>();
            progression?.OnFinalBossDefeated();

            if (gameObject != null)
                Destroy(gameObject);
        }

        private void UpdateSkullSpawner()
        {
            if (defeated ||
                arenaEntranceActive ||
                respondingToDeath ||
                Time.time < nextSkullSpawnTime)
            {
                return;
            }

            Sprite[] frames = Resources.LoadAll<Sprite>("Godzilla/skull_move")
                .OrderBy(sprite =>
                {
                    int separator = sprite.name.LastIndexOf('_');
                    return separator >= 0 &&
                           int.TryParse(sprite.name[(separator + 1)..], out int number)
                        ? number
                        : int.MaxValue;
                })
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogWarning(
                    "Godzilla skull sheet could not be loaded from Resources/Godzilla/skull_move.",
                    this);
                ScheduleSkullWave();
                return;
            }

            int count = Mathf.Max(1, skullsPerWave);
            for (int i = 0; i < count; i++)
                SpawnSkull(frames, i, count);

            animation?.Attack();
            ScheduleSkullWave();
        }

        private void ScheduleSkullWave(float additionalDelay = 0f)
        {
            float minimum = Mathf.Max(0.75f, skullSpawnInterval.x);
            float maximum = Mathf.Max(minimum, skullSpawnInterval.y);
            nextSkullSpawnTime =
                Time.time +
                Mathf.Max(0f, additionalDelay) +
                UnityEngine.Random.Range(minimum, maximum);
        }

        private void SpawnSkull(Sprite[] frames, int index, int count)
        {
            float centredIndex = index - (count - 1) * 0.5f;
            float side = direction >= 0f ? 1f : -1f;

            GameObject skull = new("Godzilla Summoned Skull");
            skull.transform.position = transform.position + new Vector3(
                side * (0.45f + Mathf.Abs(centredIndex) * 0.12f),
                centredIndex * 0.22f,
                0f);
            skull.transform.localScale = Vector3.one;

            SpriteRenderer renderer = skull.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            if (spriteRenderer != null)
            {
                renderer.sortingLayerID = spriteRenderer.sortingLayerID;
                renderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }

            InterWaveRenderItem skullRenderItem =
                skull.AddComponent<InterWaveRenderItem>();
            skullRenderItem.SetLane(currentLane);

            // Hide the skull on the same frame it is created. Adding the fade
            // here avoids a visible one-frame flash before the global installer
            // has time to discover the new projectile.
            OceanSpawnFadeIn fade = skull.AddComponent<OceanSpawnFadeIn>();
            fade.Configure(0.45f);

            GodzillaSkullSwimmer swimmer =
                skull.AddComponent<GodzillaSkullSwimmer>();
            swimmer.Initialise(
                frames,
                currentLane,
                index,
                count,
                transform);
        }

        public int CurrentHealth => currentHealth;
        public int MaximumHealth => Mathf.Max(1, maximumHealth);
        public bool IsDefeated => defeated;
        public bool IsVulnerable => !defeated && Time.time >= nextVulnerableTime;
        public float VulnerabilityTimeRemaining => Mathf.Max(0f, nextVulnerableTime - Time.time);

        private int GetTargetLane(TinyWaveSurfer surfer)
        {
            return Mathf.Clamp(surfer.CurrentWaveIndex, 0, waterLayers.Count - 2);
        }

        private void BeginLaneChangeToward(int desiredLane)
        {
            desiredLane = Mathf.Clamp(desiredLane, 0, waterLayers.Count - 2);
            if (desiredLane == currentLane)
                return;

            targetLane = currentLane + (desiredLane > currentLane ? 1 : -1);
            changingLane = true;
            laneChangeElapsed = 0f;
        }

        private void BeginDistinctLaneShift()
        {
            int laneCount = waterLayers.Count - 1;
            if (laneCount <= 1)
                return;

            // Godzilla moves in two-lane sweeps when possible, unlike the shark's
            // frequent single-lane wandering.
            int step = UnityEngine.Random.value < 0.7f ? 2 : 1;
            int sign = UnityEngine.Random.value < 0.5f ? -1 : 1;
            targetLane = Mathf.Clamp(currentLane + sign * step, 0, laneCount - 1);
            if (targetLane == currentLane)
                targetLane = currentLane == 0 ? Mathf.Min(step, laneCount - 1) : Mathf.Max(0, currentLane - step);

            changingLane = targetLane != currentLane;
            laneChangeElapsed = 0f;
        }

        private float UpdateLaneTransition(float worldX)
        {
            if (!changingLane)
                return GetLaneCentreY(currentLane, worldX) + depthOffset;

            laneChangeElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(laneChangeElapsed / laneChangeDuration);
            float eased = t * t * (3f - 2f * t);
            float desired = Mathf.Lerp(
                GetLaneCentreY(currentLane, worldX),
                GetLaneCentreY(targetLane, worldX),
                eased) + depthOffset;

            if (t >= 0.5f)
                renderItem.SetLane(targetLane);
            if (t >= 1f)
            {
                currentLane = targetLane;
                changingLane = false;
                laneChangeElapsed = 0f;
                renderItem.SetLane(currentLane);
                ScheduleLaneShift();
            }

            return desired;
        }

        private float GetLaneCentreY(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(
                waterLayers[clamped].GetGameplaySurfaceHeight(worldX),
                waterLayers[clamped + 1].GetGameplaySurfaceHeight(worldX),
                0.5f);
        }

        private Vector2 GetLaneVelocity(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Vector2.Lerp(
                waterLayers[clamped].GetGameplayWaveVelocity(worldX),
                waterLayers[clamped + 1].GetGameplayWaveVelocity(worldX),
                0.5f);
        }

        private void KeepInsideGameArea(ref Vector2 position)
        {
            float halfWidth = spriteRenderer != null ? spriteRenderer.bounds.extents.x : 0.8f;
            float minX = waterLayers[0].TankMinimum.x + halfWidth;
            float maxX = waterLayers[0].TankMaximum.x - halfWidth;

            if (position.x >= maxX)
            {
                position.x = maxX;
                direction = -1f;
                lastFacingChangeTime = Time.time;
                if (spriteRenderer != null) spriteRenderer.flipX = true;
            }
            else if (position.x <= minX)
            {
                position.x = minX;
                direction = 1f;
                lastFacingChangeTime = Time.time;
                if (spriteRenderer != null) spriteRenderer.flipX = false;
            }
        }

        private void ApplyWaterTilt(float worldX, float follow)
        {
            float left = GetLaneCentreY(currentLane, worldX - slopeSampleDistance);
            float right = GetLaneCentreY(currentLane, worldX + slopeSampleDistance);
            float slope = Mathf.Atan2(right - left, slopeSampleDistance * 2f) * Mathf.Rad2Deg;
            float angle = Mathf.Clamp(slope * surfaceTilt, -maximumTilt, maximumTilt);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.Euler(0f, 0f, angle),
                follow);
        }

        private void SetPosition(Vector2 position)
        {
            if (body != null)
                body.position = position;
            else
                transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void ScheduleLaneShift()
        {
            float minimum = Mathf.Min(laneShiftDelayRange.x, laneShiftDelayRange.y);
            float maximum = Mathf.Max(laneShiftDelayRange.x, laneShiftDelayRange.y);
            nextLaneShiftTime = Time.time + UnityEngine.Random.Range(minimum, maximum);
        }
    }
}