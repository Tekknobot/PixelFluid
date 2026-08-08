using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    public enum DayFiveEnemyKind
    {
        Searchlight,
        SurveillanceBuoy,
        Drone,
        SignalRelay,
        Warden
    }

    /// <summary>
    /// Shared Day 5 enemy controller. Every security unit enters from a camera
    /// edge, floats in place, telegraphs its attack and can be hit by the same
    /// thrown items and water slashes as the earlier enemies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class DayFiveCombatant : MonoBehaviour
    {
        private enum State { Entering, Patrol, Charging, Recovery, Hit, Shutdown }

        private DayFiveEnemyKind kind;
        private SurfDayProgressionDirector director;
        private SpriteRenderer spriteRenderer;
        private BoxCollider2D hitCollider;
        private Rigidbody2D body;
        private Camera worldCamera;
        private TinyWaveSurfer target;
        private InterWaveRenderItem waterRenderItem;
        private PixelWaterGPU sharedWaterSortingSource;
        private readonly List<PixelWaterGPU> waterLayers = new();
        private State state;

        private Sprite[] idleFrames = System.Array.Empty<Sprite>();
        private Sprite[] detectFrames = System.Array.Empty<Sprite>();
        private Sprite[] attackFrames = System.Array.Empty<Sprite>();
        private Sprite[] hitFrames = System.Array.Empty<Sprite>();
        private Sprite[] shutdownFrames = System.Array.Empty<Sprite>();
        private Sprite[] disabledFrames = System.Array.Empty<Sprite>();

        private int currentHealth;
        private int maximumHealth;
        private int entrySide;
        private int waterLaneIndex;
        private int targetWaterLaneIndex;
        private int requestedWaterLaneIndex;
        private bool changingWaterLane;
        private float waterLaneChangeElapsed;
        private float nextWaterLaneRefreshTime;
        private float viewportY;
        private float patrolCentreX;
        private float moveSpeed;
        private float floatPhase;
        private float frameClock;
        private int frameIndex;
        private State animatedState;
        private bool animationStateInitialised;
        private float nextAttackAt;
        private float stateClock;
        private float chargeDuration;
        private float recoveryDuration;
        private float nextDamageAt;
        private Vector3 desiredPosition;
        private Vector3 velocity;
        private Vector3 lockedAimPoint;
        private bool wardenUsesBeam;
        private bool initialised;
        private Color baseColour = Color.white;

        private LineRenderer beamGlow;
        private LineRenderer beamCore;
        private Material beamMaterial;

        public DayFiveEnemyKind Kind => kind;
        public int CurrentHealth => currentHealth;
        public int MaximumHealth => maximumHealth;
        public bool IsBoss => kind == DayFiveEnemyKind.SignalRelay || kind == DayFiveEnemyKind.Warden;
        public bool IsDefeated => state == State.Shutdown || currentHealth <= 0;
        public bool CanBeHit => initialised && !IsDefeated && isActiveAndEnabled &&
                                spriteRenderer != null && spriteRenderer.enabled;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            hitCollider = GetComponent<BoxCollider2D>();
            body = GetComponent<Rigidbody2D>();
            worldCamera = Camera.main;

            spriteRenderer.sortingOrder = 12012;
            hitCollider.isTrigger = true;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.freezeRotation = true;
        }

        public void Initialise(
            DayFiveEnemyKind enemyKind,
            int side,
            SurfDayProgressionDirector progression,
            PixelWaterGPU sortingSource,
            int sharedLane)
        {
            kind = enemyKind;
            entrySide = side < 0 ? -1 : 1;
            director = progression;
            sharedWaterSortingSource = sortingSource;
            waterLaneIndex = Mathf.Max(0, sharedLane);
            targetWaterLaneIndex = waterLaneIndex;
            requestedWaterLaneIndex = waterLaneIndex;
            ConfigureProfile();
            FindTarget();
            ConfigureWaterLane();

            float spawnX = entrySide < 0 ? -0.18f : 1.18f;
            patrolCentreX = entrySide < 0
                ? Random.Range(0.22f, 0.42f)
                : Random.Range(0.58f, 0.78f);
            transform.position = ResolvePatrolPosition(spawnX, 0f);
            desiredPosition = ResolvePatrolPosition(patrolCentreX, 0f);
            spriteRenderer.flipX = entrySide > 0;
            floatPhase = Random.Range(0f, Mathf.PI * 2f);
            state = State.Entering;
            initialised = true;
        }

        private void ConfigureProfile()
        {
            switch (kind)
            {
                case DayFiveEnemyKind.Searchlight:
                    maximumHealth = 1;
                    viewportY = 0.70f;
                    moveSpeed = 3.1f;
                    chargeDuration = 1.15f;
                    recoveryDuration = 0.28f;
                    transform.localScale = Vector3.one * 0.78f;
                    idleFrames = SliceSheet("Day5/Searchlight/searchlight_normal", 64, 32f);
                    detectFrames = SliceSheet("Day5/Searchlight/searchlight_sweep", 64, 32f);
                    attackFrames = SliceSheet("Day5/Searchlight/searchlight_activate", 64, 32f);
                    break;

                case DayFiveEnemyKind.SurveillanceBuoy:
                    maximumHealth = 2;
                    viewportY = 0.43f;
                    moveSpeed = 2.0f;
                    chargeDuration = 1.35f;
                    recoveryDuration = 0.32f;
                    transform.localScale = Vector3.one * 0.72f;
                    idleFrames = SliceSheet("Day5/SurveillanceBuoy/surveillance_buoy_normal", 64, 32f);
                    detectFrames = SliceSheet("Day5/SurveillanceBuoy/surveillance_buoy_scan", 64, 32f);
                    attackFrames = SliceSheet("Day5/SurveillanceBuoy/surveillance_buoy_detect", 64, 32f);
                    break;

                case DayFiveEnemyKind.Drone:
                    maximumHealth = 2;
                    viewportY = 0.80f;
                    moveSpeed = 3.8f;
                    // The drone replaces its pulse volley with the complete
                    // yellow searchlight telegraph and firing cycle.
                    chargeDuration = 1.15f;
                    recoveryDuration = 0.28f;
                    transform.localScale = Vector3.one * 0.82f;
                    idleFrames = SliceSheet("Day5/Drone/drone_patrol", 64, 32f);
                    detectFrames = SliceSheet("Day5/Drone/drone_detect", 64, 32f);
                    attackFrames = SliceSheet("Day5/Drone/drone_attack", 64, 32f);
                    break;

                case DayFiveEnemyKind.SignalRelay:
                    maximumHealth = 8;
                    viewportY = 0.52f;
                    moveSpeed = 1.7f;
                    chargeDuration = 0.9f;
                    recoveryDuration = 0.34f;
                    transform.localScale = Vector3.one * 0.72f;
                    idleFrames = SliceSheet("Day5/SignalRelay/signalrelay_active", 128, 32f);
                    detectFrames = idleFrames;
                    attackFrames = idleFrames;
                    hitFrames = SliceSheet("Day5/SignalRelay/signalrelay_hit", 128, 32f);
                    shutdownFrames = SliceSheet("Day5/SignalRelay/signalrelay_shutdown", 128, 32f);
                    disabledFrames = SliceSheet("Day5/SignalRelay/signalrelay_disabled", 128, 32f);
                    break;

                case DayFiveEnemyKind.Warden:
                    maximumHealth = 18;
                    viewportY = 0.58f;
                    moveSpeed = 2.1f;
                    chargeDuration = 1.0f;
                    recoveryDuration = 0.4f;
                    transform.localScale = Vector3.one * 0.78f;
                    idleFrames = SliceSheet("Day5/Warden/warden_idle", 128, 32f);
                    detectFrames = SliceSheet("Day5/Warden/warden_scan", 128, 32f);
                    attackFrames = SliceSheet("Day5/Warden/warden_attack", 128, 32f);
                    hitFrames = SliceSheet("Day5/Warden/warden_hit", 128, 32f);
                    shutdownFrames = SliceSheet("Day5/Warden/warden_shutdown", 128, 32f);
                    disabledFrames = SliceSheet("Day5/Warden/warden_diabled", 128, 32f);
                    break;
            }

            if (kind == DayFiveEnemyKind.SurveillanceBuoy)
            {
                const float actionAnimationRate = 14f;
                if (detectFrames.Length > 0)
                    chargeDuration = Mathf.Max(
                        chargeDuration,
                        detectFrames.Length / actionAnimationRate);
                if (attackFrames.Length > 0)
                    recoveryDuration = Mathf.Max(
                        recoveryDuration,
                        attackFrames.Length / actionAnimationRate);
            }

            currentHealth = maximumHealth;
            spriteRenderer.sortingOrder = UsesWaterGap() ? 0 : 12012;
            if (idleFrames.Length > 0)
                spriteRenderer.sprite = idleFrames[0];
            baseColour = spriteRenderer.color;
            RefreshCollider();

            if (IsBoss)
            {
                BossHealthBar bar = gameObject.GetComponent<BossHealthBar>();
                if (bar == null)
                    bar = gameObject.AddComponent<BossHealthBar>();
                bar.Bind(this);
            }
        }

        private void Update()
        {
            if (!initialised || state == State.Shutdown)
                return;

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (target == null || target.IsDead || !target.IsPlayerControlled)
                FindTarget();

            Animate();

            switch (state)
            {
                case State.Entering:
                    UpdateEntering();
                    break;
                case State.Patrol:
                    UpdatePatrol();
                    break;
                case State.Charging:
                    UpdateCharging();
                    break;
                case State.Recovery:
                    UpdateRecovery();
                    break;
                case State.Hit:
                    UpdateHit();
                    break;
            }

            // Water-mounted security must remain in the moving gap between its
            // two assigned simulations during patrols, charges and hit reactions.
            if (UsesWaterGap())
            {
                RefreshWaterLaneOrdering(false);
                desiredPosition.y = UpdateWaterLaneTransition(desiredPosition.x) + CurrentWaterBob();
            }

            ApplyMovement();
        }

        private void UpdateEntering()
        {
            desiredPosition = ResolvePatrolPosition(patrolCentreX, CurrentWaterBob());
            if (Vector2.Distance(transform.position, desiredPosition) <= 0.22f)
            {
                state = State.Patrol;
                nextAttackAt = Time.time + (IsSearchlightAttacker()
                    ? Random.Range(0.9f, 1.6f)
                    : Random.Range(1.4f, 3.0f));
            }
        }

        private void UpdatePatrol()
        {
            float horizontal = Mathf.Sin(Time.time * (kind == DayFiveEnemyKind.Drone ? 0.85f : 0.46f) + floatPhase);
            float x = Mathf.Clamp(patrolCentreX + horizontal * (kind == DayFiveEnemyKind.Drone ? 0.18f : 0.10f), 0.12f, 0.88f);
            float viewportBob = UsesWaterGap()
                ? 0f
                : Mathf.Sin(Time.time * 1.55f + floatPhase) * 0.018f;
            desiredPosition = ResolvePatrolPosition(x, UsesWaterGap() ? CurrentWaterBob() : viewportBob);
            SetFacing(desiredPosition.x - transform.position.x);

            if (target != null && Time.time >= nextAttackAt)
                BeginAttack();
        }

        private void BeginAttack()
        {
            if (target == null)
                return;

            state = State.Charging;
            stateClock = 0f;
            frameClock = 0f;
            frameIndex = 0;
            lockedAimPoint = target.transform.position;
            velocity *= 0.25f;

            if (kind == DayFiveEnemyKind.Warden)
                wardenUsesBeam = !wardenUsesBeam;

            if (UsesBeamAttack())
                EnsureBeam();
        }

        private bool IsSearchlightAttacker() =>
            kind == DayFiveEnemyKind.Searchlight ||
            kind == DayFiveEnemyKind.Drone;

        private bool UsesBeamAttack() =>
            IsSearchlightAttacker() ||
            kind == DayFiveEnemyKind.SurveillanceBuoy ||
            (kind == DayFiveEnemyKind.Warden && wardenUsesBeam);

        private void UpdateCharging()
        {
            stateClock += Time.deltaTime;
            if (target != null && stateClock < chargeDuration * 0.58f)
                lockedAimPoint = target.transform.position;

            if (UsesBeamAttack())
                DrawBeam(false);

            if (stateClock < chargeDuration)
                return;

            if (UsesBeamAttack())
                FireBeam();
            else
                FireSecurityPulseVolley();

            state = State.Recovery;
            stateClock = 0f;
            frameClock = 0f;
            frameIndex = 0;
        }

        private void UpdateRecovery()
        {
            stateClock += Time.deltaTime;
            if (UsesBeamAttack() && stateClock < 0.16f)
                DrawBeam(true);
            else
                SetBeamVisible(false);

            if (stateClock < recoveryDuration)
                return;

            state = State.Patrol;
            nextAttackAt = Time.time + AttackCooldown();
        }

        private void UpdateHit()
        {
            stateClock += Time.deltaTime;
            if (stateClock < 0.24f)
                return;

            spriteRenderer.color = baseColour;
            state = State.Patrol;
            nextAttackAt = Mathf.Max(nextAttackAt, Time.time + 0.65f);
        }

        private float AttackCooldown()
        {
            return kind switch
            {
                DayFiveEnemyKind.Searchlight => Random.Range(3.8f, 5.6f),
                DayFiveEnemyKind.SurveillanceBuoy => Random.Range(4.4f, 6.2f),
                DayFiveEnemyKind.Drone => Random.Range(3.8f, 5.6f),
                DayFiveEnemyKind.SignalRelay => Random.Range(2.5f, 3.7f),
                _ => Random.Range(1.9f, 2.9f)
            };
        }

        private void FireBeam()
        {
            DrawBeam(true);
            ExplosionBasicEffect.Spawn(lockedAimPoint);
            DayFiveSecurityImpact.Spawn(lockedAimPoint, BeamColour(true));

            if (target == null || target.IsDead)
                return;

            float hitRadius = kind == DayFiveEnemyKind.Warden ? 0.82f : 0.64f;
            if (Vector2.Distance(target.transform.position, lockedAimPoint) <= hitRadius)
                target.TakeSharkHit(transform.position);
        }

        private void FireSecurityPulseVolley()
        {
            if (target == null)
                return;

            int count = kind switch
            {
                DayFiveEnemyKind.SignalRelay => 3,
                DayFiveEnemyKind.Warden => 5,
                _ => 1
            };
            float spread = count <= 1 ? 0f : 13f;

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                float centred = i - (count - 1) * 0.5f;
                direction = Quaternion.Euler(0f, 0f, centred * spread) * direction;

                GameObject pulse = new("Day 5 Security Pulse");
                pulse.transform.position = transform.position;
                pulse.AddComponent<SpriteRenderer>();
                pulse.AddComponent<CircleCollider2D>();
                pulse.AddComponent<Rigidbody2D>();
                pulse.AddComponent<DayFiveSecurityPulse>().Launch(
                    this,
                    target,
                    direction,
                    kind == DayFiveEnemyKind.Warden ? 7.2f : 6.1f);
            }
        }

        public bool TakeThrownItemHit(int damage, Vector2 impactPosition)
        {
            if (!CanBeHit || Time.time < nextDamageAt)
                return false;

            nextDamageAt = Time.time + (IsBoss ? 0.16f : 0.08f);
            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, damage));
            SetBeamVisible(false);

            Vector2 away = ((Vector2)transform.position - impactPosition).normalized;
            if (away.sqrMagnitude < 0.01f)
                away = Vector2.up;
            velocity += (Vector3)(away * (IsBoss ? 0.8f : 2.0f));

            if (currentHealth <= 0)
            {
                StartCoroutine(Shutdown());
                return true;
            }

            state = State.Hit;
            stateClock = 0f;
            frameClock = 0f;
            frameIndex = 0;
            spriteRenderer.color = new Color(1f, 0.12f, 0.12f, baseColour.a);
            DayFiveSecurityImpact.Spawn(transform.position, new Color(1f, 0.2f, 0.16f, 1f));
            return true;
        }

        private IEnumerator Shutdown()
        {
            state = State.Shutdown;
            hitCollider.enabled = false;
            SetBeamVisible(false);
            velocity = Vector3.zero;
            frameClock = 0f;
            frameIndex = 0;
            spriteRenderer.color = baseColour;

            float duration = shutdownFrames.Length > 0 ? shutdownFrames.Length / 10f : 0.36f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (shutdownFrames.Length > 0)
                {
                    int index = Mathf.Clamp(
                        Mathf.FloorToInt(elapsed * 10f),
                        0,
                        shutdownFrames.Length - 1);
                    spriteRenderer.sprite = shutdownFrames[index];
                }
                transform.position += Vector3.down * (0.32f * Time.deltaTime);
                yield return null;
            }

            if (disabledFrames.Length > 0)
                spriteRenderer.sprite = disabledFrames[0];
            ExplosionBasicEffect.Spawn(transform.position);
            DayFiveSecurityImpact.Spawn(transform.position, new Color(1f, 0.08f, 0.08f, 1f));

            FindFirstObjectByType<DayFiveEncounter>()?.NotifyCombatantDefeated(this);

            Destroy(gameObject, 0.08f);
        }

        public void BeginRetreat(bool includeBoss = false)
        {
            if (!initialised || (IsBoss && !includeBoss))
                return;

            StopAllCoroutines();
            SetBeamVisible(false);
            hitCollider.enabled = false;
            StartCoroutine(RetreatRoutine());
        }

        private IEnumerator RetreatRoutine()
        {
            float side = transform.position.x < (worldCamera != null ? worldCamera.transform.position.x : 0f)
                ? -0.22f
                : 1.22f;
            Vector3 exit = ViewportWorld(side, Mathf.Min(1.08f, viewportY + 0.18f));
            while (Vector2.Distance(transform.position, exit) > 0.18f)
            {
                transform.position = Vector3.MoveTowards(transform.position, exit, moveSpeed * 1.6f * Time.deltaTime);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void ApplyMovement()
        {
            if (state == State.Shutdown)
                return;

            float activeSpeed = state == State.Entering ? moveSpeed * 1.45f : moveSpeed;
            Vector3 delta = desiredPosition - transform.position;
            Vector3 wanted = delta.sqrMagnitude > 0.002f ? delta.normalized * activeSpeed : Vector3.zero;
            velocity = Vector3.Lerp(velocity, wanted, 1f - Mathf.Exp(-5.5f * Time.deltaTime));
            transform.position += velocity * Time.deltaTime;

            float bank = Mathf.Clamp(-velocity.x * 2.2f, -8f, 8f);
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.Euler(0f, 0f, bank),
                1f - Mathf.Exp(-7f * Time.deltaTime));
        }

        private void SetFacing(float deltaX)
        {
            if (Mathf.Abs(deltaX) > 0.05f)
                spriteRenderer.flipX = deltaX < 0f;
        }

        private void Animate()
        {
            if (!animationStateInitialised || animatedState != state)
            {
                animatedState = state;
                animationStateInitialised = true;
                frameClock = 0f;
                frameIndex = -1;
            }

            Sprite[] active = state switch
            {
                State.Charging => detectFrames.Length > 0 ? detectFrames : idleFrames,
                State.Recovery => attackFrames.Length > 0 ? attackFrames : idleFrames,
                State.Hit => hitFrames.Length > 0 ? hitFrames : idleFrames,
                _ => idleFrames
            };

            if (active == null || active.Length == 0)
                return;

            frameClock += Time.deltaTime * (state == State.Charging || state == State.Recovery ? 14f : 10f);
            int next = Mathf.FloorToInt(frameClock) % active.Length;
            if (spriteRenderer.sprite == null || next != frameIndex)
            {
                frameIndex = next;
                spriteRenderer.sprite = active[next];
                RefreshCollider();
            }
        }

        private void RefreshCollider()
        {
            if (spriteRenderer.sprite == null || hitCollider == null)
                return;
            hitCollider.size = spriteRenderer.sprite.bounds.size * (IsBoss ? 0.70f : 0.62f);
            hitCollider.offset = spriteRenderer.sprite.bounds.center;
        }

        private void FindTarget()
        {
            foreach (TinyWaveSurfer surfer in GameplayTargetCache.Surfers)
            {
                if (surfer != null && surfer.IsPlayerControlled && !surfer.IsDead)
                {
                    target = surfer;
                    return;
                }
            }
            target = null;
        }

        private bool UsesWaterGap() =>
            kind == DayFiveEnemyKind.SurveillanceBuoy;

        private bool UsesSharedWaveSorting() =>
            kind == DayFiveEnemyKind.SurveillanceBuoy ||
            kind == DayFiveEnemyKind.Drone;

        private void ConfigureWaterLane()
        {
            if (!UsesSharedWaveSorting())
                return;

            if (UsesWaterGap())
            {
                float sampleX = target != null
                    ? target.transform.position.x
                    : ViewportWorld(0.5f, 0.5f).x;
                RefreshWaterLayers(sampleX);
                waterLaneIndex = waterLayers.Count >= 2
                    ? Mathf.Clamp(waterLaneIndex, 0, waterLayers.Count - 2)
                    : 0;
                targetWaterLaneIndex = waterLaneIndex;
                requestedWaterLaneIndex = waterLaneIndex;
                if (sharedWaterSortingSource == null && waterLayers.Count > 0)
                    sharedWaterSortingSource = waterLayers[0];
            }

            waterRenderItem = GetComponent<InterWaveRenderItem>();
            if (waterRenderItem == null)
                waterRenderItem = gameObject.AddComponent<InterWaveRenderItem>();
            if (UsesWaterGap())
                ApplyWaterLaneSorting(waterLaneIndex);
            else
                waterRenderItem.SetWaterAndLane(sharedWaterSortingSource, waterLaneIndex);
            nextWaterLaneRefreshTime = Time.time + 0.12f;
        }

        public void SetSharedWaveSorting(PixelWaterGPU sortingSource, int sharedLane)
        {
            if (!UsesSharedWaveSorting())
                return;

            sharedWaterSortingSource = sortingSource;
            if (waterRenderItem == null)
                waterRenderItem = GetComponent<InterWaveRenderItem>();
            if (waterRenderItem == null)
                waterRenderItem = gameObject.AddComponent<InterWaveRenderItem>();

            if (!UsesWaterGap())
            {
                waterLaneIndex = Mathf.Max(0, sharedLane);
                waterRenderItem.SetWaterAndLane(sharedWaterSortingSource, waterLaneIndex);
                return;
            }

            RefreshWaterLayers(transform.position.x);
            requestedWaterLaneIndex = waterLayers.Count >= 2
                ? Mathf.Clamp(sharedLane, 0, waterLayers.Count - 2)
                : 0;
            if (!changingWaterLane && requestedWaterLaneIndex != waterLaneIndex)
                BeginWaterLaneChangeToward(requestedWaterLaneIndex);
            ApplyWaterLaneSorting(
                changingWaterLane && waterLaneChangeElapsed >= 0.625f
                    ? targetWaterLaneIndex
                    : waterLaneIndex);
        }

        private void RefreshWaterLaneOrdering(bool force)
        {
            if (!UsesWaterGap() || (!force && Time.time < nextWaterLaneRefreshTime))
                return;

            nextWaterLaneRefreshTime = Time.time + 0.12f;
            RefreshWaterLayers(transform.position.x);
            if (waterLayers.Count < 2)
                return;

            waterLaneIndex = Mathf.Clamp(waterLaneIndex, 0, waterLayers.Count - 2);
            targetWaterLaneIndex = Mathf.Clamp(targetWaterLaneIndex, 0, waterLayers.Count - 2);
            requestedWaterLaneIndex = Mathf.Clamp(requestedWaterLaneIndex, 0, waterLayers.Count - 2);
            if (!changingWaterLane && requestedWaterLaneIndex != waterLaneIndex)
                BeginWaterLaneChangeToward(requestedWaterLaneIndex);
            ApplyWaterLaneSorting(
                changingWaterLane && waterLaneChangeElapsed >= 0.625f
                    ? targetWaterLaneIndex
                    : waterLaneIndex);
        }

        private void RefreshWaterLayers(float worldX)
        {
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(worldX));
            waterLayers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            waterLayers.Sort((a, b) =>
                a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
        }

        private Vector3 ResolvePatrolPosition(float viewportX, float verticalOffset)
        {
            Vector3 viewportPoint = ViewportWorld(viewportX, viewportY + verticalOffset);
            if (!UsesWaterGap())
                return viewportPoint;

            viewportPoint.y = ResolveWaterGapY(viewportPoint.x) + verticalOffset;
            return viewportPoint;
        }

        private float ResolveWaterGapY(float worldX)
        {
            if (waterLayers.Count < 2)
            {
                RefreshWaterLayers(worldX);
                if (waterLayers.Count < 2)
                    return ViewportWorld(0.5f, viewportY).y;
            }

            int lane = Mathf.Clamp(waterLaneIndex, 0, waterLayers.Count - 2);
            float backSurface = waterLayers[lane].GetGameplaySurfaceHeight(worldX);
            float frontSurface = waterLayers[lane + 1].GetGameplaySurfaceHeight(worldX);
            return Mathf.Lerp(backSurface, frontSurface, 0.5f);
        }

        private void BeginWaterLaneChangeToward(int desiredLane)
        {
            if (waterLayers.Count < 2)
                return;

            desiredLane = Mathf.Clamp(desiredLane, 0, waterLayers.Count - 2);
            if (desiredLane == waterLaneIndex)
                return;

            targetWaterLaneIndex = waterLaneIndex +
                                   (desiredLane > waterLaneIndex ? 1 : -1);
            changingWaterLane = true;
            waterLaneChangeElapsed = 0f;
        }

        private float UpdateWaterLaneTransition(float worldX)
        {
            if (waterLayers.Count < 2)
                return ResolveWaterGapY(worldX);

            if (!changingWaterLane)
            {
                if (requestedWaterLaneIndex != waterLaneIndex)
                    BeginWaterLaneChangeToward(requestedWaterLaneIndex);
                return GetWaterLaneCentreY(waterLaneIndex, worldX);
            }

            const float laneChangeDuration = 1.25f;
            waterLaneChangeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(waterLaneChangeElapsed / laneChangeDuration);
            float eased = t * t * (3f - 2f * t);
            float laneY = Mathf.Lerp(
                GetWaterLaneCentreY(waterLaneIndex, worldX),
                GetWaterLaneCentreY(targetWaterLaneIndex, worldX),
                eased);

            if (t >= 0.5f)
                ApplyWaterLaneSorting(targetWaterLaneIndex);
            if (t >= 1f)
            {
                waterLaneIndex = targetWaterLaneIndex;
                changingWaterLane = false;
                waterLaneChangeElapsed = 0f;
                ApplyWaterLaneSorting(waterLaneIndex);
            }

            return laneY;
        }

        private float GetWaterLaneCentreY(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(
                waterLayers[clamped].GetGameplaySurfaceHeight(worldX),
                waterLayers[clamped + 1].GetGameplaySurfaceHeight(worldX),
                0.5f);
        }

        private void ApplyWaterLaneSorting(int lane)
        {
            if (waterRenderItem == null || waterLayers.Count == 0)
                return;

            int clampedLane = Mathf.Clamp(lane, 0, Mathf.Max(0, waterLayers.Count - 2));
            PixelWaterGPU correspondingWater = waterLayers[
                Mathf.Clamp(clampedLane, 0, waterLayers.Count - 1)];
            sharedWaterSortingSource = correspondingWater;
            waterRenderItem.SetWaterAndLane(correspondingWater, clampedLane);

            Renderer waterRenderer = correspondingWater.GetComponent<Renderer>();
            if (waterRenderer == null)
                waterRenderer = correspondingWater.GetComponentInChildren<Renderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 0;
                if (waterRenderer != null)
                    spriteRenderer.sortingLayerID = waterRenderer.sortingLayerID;
            }

            RefreshBeamSorting(clampedLane);
        }

        private float CurrentWaterBob() =>
            Mathf.Sin(Time.time * 1.55f + floatPhase) * 0.055f;

        private void EnsureBeam()
        {
            if (beamCore != null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            beamMaterial = new Material(shader)
            {
                name = "Runtime Day 5 Beam",
                hideFlags = HideFlags.HideAndDontSave
            };
            beamGlow = CreateLine("Beam Glow", 0.17f, 12018);
            beamCore = CreateLine("Beam Core", 0.045f, 12019);
            RefreshBeamSorting();
            SetBeamVisible(false);
        }

        private LineRenderer CreateLine(string objectName, float width, int order)
        {
            GameObject lineObject = new(objectName);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.sharedMaterial = beamMaterial;
            line.sortingOrder = order;
            if (spriteRenderer != null)
                line.sortingLayerID = spriteRenderer.sortingLayerID;
            return line;
        }

        private void RefreshBeamSorting(int laneOverride = -1)
        {
            if (beamGlow == null || beamCore == null)
                return;

            int sortingLayer = spriteRenderer != null
                ? spriteRenderer.sortingLayerID
                : 0;
            beamGlow.sortingLayerID = sortingLayer;
            beamCore.sortingLayerID = sortingLayer;

            // InterWaveRenderItem captures the buoy before its runtime beam
            // children exist. Assign the beam to the same interleaved water
            // queue explicitly so it cannot disappear behind every wave.
            if (UsesSharedWaveSorting() &&
                beamMaterial != null && sharedWaterSortingSource != null)
            {
                beamMaterial.renderQueue =
                    sharedWaterSortingSource.GetInterleavedObjectRenderQueue(
                        laneOverride >= 0
                            ? laneOverride
                            : Mathf.Max(0, waterLaneIndex)) + 1;
            }
        }

        private void DrawBeam(bool firing)
        {
            EnsureBeam();
            if (beamCore == null || beamGlow == null)
                return;

            Vector3 start;
            Vector3 end = lockedAimPoint;
            if (kind == DayFiveEnemyKind.SurveillanceBuoy ||
                kind == DayFiveEnemyKind.Warden)
            {
                float skyY = worldCamera != null
                    ? ViewportWorld(0.5f, 1.12f).y
                    : lockedAimPoint.y + 8f;
                start = new Vector3(lockedAimPoint.x, skyY, 0f);
            }
            else
            {
                start = transform.position;
            }

            Color colour = BeamColour(firing);
            float pulse = 0.78f + Mathf.Sin(Time.time * 18f) * 0.18f;
            Color glow = colour;
            glow.a = firing ? 0.72f : 0.34f * pulse;
            Color core = Color.Lerp(colour, Color.white, firing ? 0.72f : 0.25f);
            core.a = firing ? 1f : 0.72f;

            beamGlow.startColor = beamGlow.endColor = glow;
            beamCore.startColor = beamCore.endColor = core;
            beamGlow.SetPosition(0, start);
            beamGlow.SetPosition(1, end);
            beamCore.SetPosition(0, start);
            beamCore.SetPosition(1, end);
            SetBeamVisible(true);
        }

        private Color BeamColour(bool firing)
        {
            if (kind == DayFiveEnemyKind.Searchlight ||
                kind == DayFiveEnemyKind.Drone)
                return firing
                    ? new Color(1f, 0.45f, 0.10f, 1f)
                    : new Color(1f, 0.82f, 0.24f, 1f);
            return firing
                ? new Color(1f, 0.02f, 0.02f, 1f)
                : new Color(1f, 0.08f, 0.08f, 1f);
        }

        private void SetBeamVisible(bool visible)
        {
            if (beamGlow != null)
                beamGlow.enabled = visible;
            if (beamCore != null)
                beamCore.enabled = visible;
        }

        private Vector3 ViewportWorld(float x, float y)
        {
            if (worldCamera == null)
            {
                // Keep sky enemies overhead even if they initialise one frame
                // before Camera.main becomes available.
                Vector3 centre = target != null ? target.transform.position : transform.position;
                return new Vector3(
                    centre.x + (x - 0.5f) * 16f,
                    centre.y + Mathf.Lerp(-2.5f, 6.5f, y),
                    0f);
            }
            float depth = Mathf.Abs(worldCamera.transform.position.z);
            Vector3 point = worldCamera.ViewportToWorldPoint(new Vector3(x, y, depth));
            point.z = 0f;
            return point;
        }

        private static Sprite[] SliceSheet(string resourcePath, int frameSize, float pixelsPerUnit)
        {
            Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
            if (sheet == null)
            {
                Debug.LogWarning($"Day 5 sprite sheet was not found: {resourcePath}");
                return System.Array.Empty<Sprite>();
            }

            int columns = Mathf.Max(1, sheet.width / frameSize);
            int rows = Mathf.Max(1, sheet.height / frameSize);
            Sprite[] result = new Sprite[columns * rows];
            int index = 0;
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < columns; column++)
                {
                    result[index] = Sprite.Create(
                        sheet,
                        new Rect(column * frameSize, row * frameSize, frameSize, frameSize),
                        new Vector2(0.5f, 0.5f),
                        pixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);
                    result[index].name = $"{sheet.name}_{index:00}";
                    index++;
                }
            }
            return result;
        }

        private void OnDestroy()
        {
            if (beamMaterial != null)
                Destroy(beamMaterial);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    public sealed class DayFiveSecurityPulse : MonoBehaviour
    {
        private DayFiveCombatant owner;
        private TinyWaveSurfer target;
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private Vector2 velocity;
        private float speed;
        private float age;
        private float frameClock;

        public void Launch(DayFiveCombatant pulseOwner, TinyWaveSurfer surfer, Vector2 direction, float travelSpeed)
        {
            owner = pulseOwner;
            target = surfer;
            speed = Mathf.Max(1f, travelSpeed);
            velocity = direction.sqrMagnitude > 0.01f ? direction.normalized * speed : Vector2.down * speed;

            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 12024;
            frames = SliceSheet("Day5/SecurityPulse/security_pulse_flying", 32, 32f);
            if (frames.Length > 0)
                spriteRenderer.sprite = frames[0];
            transform.localScale = Vector3.one * 0.78f;

            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.32f;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= 5f || owner == null)
            {
                Destroy(gameObject);
                return;
            }

            if (target != null && !target.IsDead && age < 1.4f)
            {
                Vector2 desired = ((Vector2)target.transform.position - (Vector2)transform.position).normalized * speed;
                velocity = Vector2.Lerp(velocity, desired, 1f - Mathf.Exp(-1.8f * Time.deltaTime));
            }

            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);

            if (frames != null && frames.Length > 0)
            {
                frameClock += Time.deltaTime * 16f;
                spriteRenderer.sprite = frames[Mathf.FloorToInt(frameClock) % frames.Length];
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.GetComponentInParent<DayFiveCombatant>() != null)
                return;

            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || !surfer.IsPlayerControlled)
                return;

            surfer.TakeSharkHit(transform.position);
            DayFiveSecurityImpact.Spawn(transform.position, new Color(1f, 0.08f, 0.12f, 1f));
            ExplosionBasicEffect.Spawn(transform.position);
            Destroy(gameObject);
        }

        private static Sprite[] SliceSheet(string path, int frameSize, float ppu)
        {
            Texture2D sheet = Resources.Load<Texture2D>(path);
            if (sheet == null)
                return System.Array.Empty<Sprite>();
            int count = Mathf.Max(1, sheet.width / frameSize);
            Sprite[] result = new Sprite[count];
            for (int i = 0; i < count; i++)
                result[i] = Sprite.Create(sheet, new Rect(i * frameSize, 0, frameSize, frameSize),
                    new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            return result;
        }
    }

    [DisallowMultipleComponent]
    public sealed class DayFiveSecurityImpact : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float clock;
        private Color tint;

        public static void Spawn(Vector3 position, Color colour)
        {
            GameObject impact = new("Day 5 Security Impact");
            impact.transform.position = position;
            DayFiveSecurityImpact effect = impact.AddComponent<DayFiveSecurityImpact>();
            effect.tint = colour;
        }

        private void Start()
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 12028;
            Texture2D sheet = Resources.Load<Texture2D>("Day5/SecurityPulse/security_impact");
            if (sheet == null)
            {
                Destroy(gameObject);
                return;
            }

            const int frameSize = 32;
            int count = Mathf.Max(1, sheet.width / frameSize);
            frames = new Sprite[count];
            for (int i = 0; i < count; i++)
                frames[i] = Sprite.Create(sheet, new Rect(i * frameSize, 0, frameSize, frameSize),
                    new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            spriteRenderer.sprite = frames[0];
            spriteRenderer.color = tint == default ? Color.white : tint;
            transform.localScale = Vector3.one * 1.18f;
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
                return;
            clock += Time.deltaTime * 18f;
            int index = Mathf.FloorToInt(clock);
            if (index >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }
            spriteRenderer.sprite = frames[index];
            transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.35f, index / (float)frames.Length);
        }
    }
}
