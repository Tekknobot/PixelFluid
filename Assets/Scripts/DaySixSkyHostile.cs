using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    public enum DaySixSkyHostileKind
    {
        ClawUfo,
        RacerUfo,
        RetroUfo
    }

    /// <summary>
    /// Shared, smooth-moving controller for the three Day 6 sky oddities.
    /// Each craft owns a distinct attack while sharing targeting, animation,
    /// thrown-item damage, retreat and the helicopter-style water crash.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class DaySixSkyHostile : MonoBehaviour
    {
        private enum State
        {
            Arrival,
            Patrol,
            Telegraph,
            Attack,
            Carry,
            Recover,
            Hidden,
            Crashing,
            Retreat
        }

        [Header("Animation")]
        [SerializeField, Min(1f)] private float moveFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float attackFramesPerSecond = 16f;
        [SerializeField, Min(0.1f)] private float craftScale = 0.86f;
        [SerializeField] private int skySortingOrder = 12014;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float movementSmoothing = 9f;
        [SerializeField, Min(0.1f)] private float flightSpeed = 5.8f;
        [SerializeField, Min(0f)] private float bankAngle = 7f;

        [Header("Combat")]
        // Six ordinary hits leaves enough of Day 6's shared item supply for the
        // sea roster and the other simultaneous UFOs. A Flow Finisher deals 3.
        [SerializeField, Min(1)] private int maximumHealth = 6;
        [SerializeField, Min(0.1f)] private float contactRadius = 0.58f;
        [SerializeField] private Vector2 firstAttackDelayRange = new(2.2f, 4.2f);
        [SerializeField] private Vector2 attackCooldownRange = new(4.2f, 7.2f);

        [Header("Crash")]
        [SerializeField, Min(0.5f)] private float crashDuration = 2.8f;
        [SerializeField, Range(0.1f, 0.95f)] private float crashFadeBeginsAt = 0.55f;
        [SerializeField] private Vector2 crashExplosionIntervalRange = new(0.17f, 0.31f);

        private DaySixSkyHostileKind kind;
        private DaySixEncounter owner;
        private SpriteRenderer spriteRenderer;
        private BoxCollider2D hitCollider;
        private Rigidbody2D body;
        private InterWaveRenderItem interWaveRenderItem;
        private Camera worldCamera;
        private TinyWaveSurfer target;
        private Sprite[] moveFrames = System.Array.Empty<Sprite>();
        private Sprite[] attackFrames = System.Array.Empty<Sprite>();
        private State state;
        private Vector3 desiredPosition;
        private Vector3 smoothedVelocity;
        private float stateClock;
        private float frameClock;
        private int frameIndex;
        private float nextAttackAt;
        private float hitFlashUntil;
        private int health;
        private int entrySide = 1;
        private bool contactConsumed;
        private float bobSeed;
        private float raceSide;
        private Vector3 carryReleasePosition;
        private bool playerCarried;
        private int originalSortingLayerId;
        private Color baseTint = Color.white;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private PixelWaterGPU interWaveForeground;
        private PixelWaterGPU interWaveBackground;
        private int interWaveLane = -1;
        private int previousRetroLane = -1;
        private Vector3 crashStart;
        private float crashTargetX;
        private float crashSpinSpeed;
        private float nextCrashExplosionAt;

        public DaySixSkyHostileKind Kind => kind;
        public bool CanBeHit => isActiveAndEnabled && spriteRenderer != null &&
            spriteRenderer.enabled && state != State.Crashing && state != State.Retreat;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            hitCollider = GetComponent<BoxCollider2D>();
            body = GetComponent<Rigidbody2D>();
            interWaveRenderItem = GetComponent<InterWaveRenderItem>();
            worldCamera = Camera.main;

            originalSortingLayerId = spriteRenderer.sortingLayerID;
            baseTint = spriteRenderer.color;
            spriteRenderer.sortingOrder = skySortingOrder;

            hitCollider.isTrigger = true;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.freezeRotation = true;

            transform.localScale = Vector3.one * craftScale;
            bobSeed = Random.Range(0f, 20f);
        }

        public void Initialise(
            DaySixSkyHostileKind newKind,
            int spawnSide,
            DaySixEncounter encounter)
        {
            kind = newKind;
            owner = encounter;
            entrySide = spawnSide >= 0 ? 1 : -1;
            health = Mathf.Max(1, maximumHealth);
            LoadKindSprites();
            ResizeCollider();
            FindTarget();

            if (kind == DaySixSkyHostileKind.RetroUfo)
                EnterRetroHidden(true);
            else
                PlaceForArrival();
        }

        private void Update()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (target == null || target.IsDead || !target.IsPlayerControlled)
                FindTarget();

            if (state != State.Crashing && Time.time >= hitFlashUntil)
                spriteRenderer.color = baseTint;

            Animate();
            stateClock += Time.deltaTime;

            switch (state)
            {
                case State.Arrival: UpdateArrival(); break;
                case State.Patrol: UpdatePatrol(); break;
                case State.Telegraph: UpdateTelegraph(); break;
                case State.Attack: UpdateAttack(); break;
                case State.Carry: UpdateCarry(); break;
                case State.Recover: UpdateRecover(); break;
                case State.Hidden: UpdateHidden(); return;
                case State.Crashing: UpdateCrash(); return;
                case State.Retreat: UpdateRetreat(); return;
            }

            ApplySmoothMovement();
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

        private void PlaceForArrival()
        {
            RestoreSkySorting();
            spriteRenderer.enabled = true;
            hitCollider.enabled = true;
            state = State.Arrival;
            stateClock = 0f;
            smoothedVelocity = Vector3.zero;

            float startX = ViewportWorldX(entrySide < 0 ? -0.15f : 1.15f);
            float destinationX = ViewportWorldX(entrySide < 0 ? 0.25f : 0.75f);
            float y = ViewportWorldY(kind == DaySixSkyHostileKind.ClawUfo ? 0.83f : 0.72f);
            transform.position = new Vector3(startX, y, 0f);
            desiredPosition = new Vector3(destinationX, y, 0f);
            SetFacing(desiredPosition.x - transform.position.x);
            nextAttackAt = Time.time + Random.Range(firstAttackDelayRange.x, firstAttackDelayRange.y);
        }

        private void UpdateArrival()
        {
            if (Vector2.Distance(transform.position, desiredPosition) > 0.22f)
                return;

            EnterState(State.Patrol);
            PickPatrolPosition();
        }

        private void UpdatePatrol()
        {
            if (target != null && Time.time >= nextAttackAt)
            {
                if (kind != DaySixSkyHostileKind.ClawUfo ||
                    (!target.IsAirborneDoingTricks && !target.IsSwitchingWave))
                {
                    BeginAttackTelegraph();
                    return;
                }
            }

            if (target != null)
            {
                float followX = Mathf.Clamp(
                    target.transform.position.x + entrySide * 2.2f,
                    ViewportWorldX(0.12f),
                    ViewportWorldX(0.88f));
                float y = ViewportWorldY(kind == DaySixSkyHostileKind.ClawUfo ? 0.82f : 0.70f);
                desiredPosition = new Vector3(followX, y + Mathf.Sin(Time.time * 2.1f + bobSeed) * 0.12f, 0f);
            }
            else if (Vector2.Distance(transform.position, desiredPosition) < 0.25f)
            {
                PickPatrolPosition();
            }
        }

        private void PickPatrolPosition()
        {
            float yMin = kind == DaySixSkyHostileKind.ClawUfo ? 0.74f : 0.62f;
            float yMax = kind == DaySixSkyHostileKind.ClawUfo ? 0.89f : 0.78f;
            desiredPosition = new Vector3(
                ViewportWorldX(Random.Range(0.18f, 0.82f)),
                ViewportWorldY(Random.Range(yMin, yMax)),
                0f);
            SetFacing(desiredPosition.x - transform.position.x);
        }

        private void BeginAttackTelegraph()
        {
            contactConsumed = false;
            frameClock = 0f;
            frameIndex = 0;
            EnterState(State.Telegraph);

            if (kind == DaySixSkyHostileKind.ClawUfo)
            {
                desiredPosition = target.transform.position + Vector3.up * 2.25f;
            }
            else
            {
                raceSide = target.transform.position.x >= transform.position.x ? -1f : 1f;
                desiredPosition = target.transform.position + new Vector3(-raceSide * 2.25f, 0.35f, 0f);
                SetFacing(raceSide);
            }
        }

        private void UpdateTelegraph()
        {
            if (target == null)
            {
                FinishAttackCycle();
                return;
            }

            if (kind == DaySixSkyHostileKind.ClawUfo)
            {
                desiredPosition = target.transform.position + Vector3.up * 2.25f;
                if (target.IsAirborneDoingTricks || target.IsSwitchingWave)
                {
                    FinishAttackCycle();
                    return;
                }

                if (stateClock >= 0.55f)
                    EnterState(State.Attack);
            }
            else if (stateClock >= 0.42f)
            {
                if (kind == DaySixSkyHostileKind.RetroUfo)
                    hitCollider.enabled = true;
                EnterState(State.Attack);
            }
        }

        private void UpdateAttack()
        {
            if (target == null)
            {
                FinishAttackCycle();
                return;
            }

            if (kind == DaySixSkyHostileKind.ClawUfo)
                UpdateClawSwoop();
            else if (kind == DaySixSkyHostileKind.RacerUfo)
                UpdateRacerPass();
            else
                UpdateRetroPass();
        }

        private void UpdateClawSwoop()
        {
            if (target.IsAirborneDoingTricks || target.IsSwitchingWave)
            {
                FinishAttackCycle();
                return;
            }

            desiredPosition = target.transform.position + Vector3.up * 0.48f;
            if (Vector2.Distance(transform.position, desiredPosition) <= contactRadius &&
                target.TryBeginExternalCarry(transform, Vector3.down * 0.72f))
            {
                playerCarried = true;
                carryReleasePosition = target.transform.position;
                hitCollider.enabled = false;
                EnterState(State.Carry);
                return;
            }

            if (stateClock >= 1.05f)
                FinishAttackCycle();
        }

        private void UpdateRacerPass()
        {
            desiredPosition = target.transform.position + new Vector3(raceSide * 0.08f, 0.25f, 0f);
            SetFacing(desiredPosition.x - transform.position.x);
            TryContactHit();

            if (stateClock >= 1.65f)
                FinishAttackCycle();
        }

        private void UpdateRetroPass()
        {
            desiredPosition = target.transform.position + new Vector3(raceSide * 0.06f, 0.16f, 0f);
            SetFacing(desiredPosition.x - transform.position.x);
            TryContactHit();

            if (stateClock >= 1.25f)
            {
                hitCollider.enabled = false;
                EnterState(State.Recover);
                SetRetroSubmergeTarget();
            }
        }

        private void TryContactHit()
        {
            if (contactConsumed || target == null ||
                Vector2.Distance(transform.position, target.transform.position) > contactRadius)
                return;

            if (target.TakeSharkHit(transform.position))
                contactConsumed = true;
        }

        private void UpdateCarry()
        {
            if (!playerCarried || target == null || target.IsDead)
            {
                ReleaseCarriedPlayer(false);
                FinishAttackCycle();
                return;
            }

            desiredPosition = new Vector3(
                Mathf.Clamp(target.transform.position.x, ViewportWorldX(0.15f), ViewportWorldX(0.85f)),
                ViewportWorldY(0.84f),
                0f);

            if (stateClock < 1.20f)
                return;

            // The burst happens first; the controller then returns the surfer to
            // the saved wave position and applies one normal protected hit.
            ExplosionBasicEffect.Spawn(transform.position + Vector3.down * 0.45f);
            ReleaseCarriedPlayer(true);
            FinishAttackCycle();
        }

        private void ReleaseCarriedPlayer(bool applyHit)
        {
            if (!playerCarried)
                return;

            playerCarried = false;
            hitCollider.enabled = state != State.Crashing && state != State.Retreat;
            if (target == null)
                return;

            Vector3 release = carryReleasePosition;
            release.x = Mathf.Clamp(transform.position.x, ViewportWorldX(0.10f), ViewportWorldX(0.90f));
            target.EndExternalCarry(release, applyHit ? 0f : 0.35f);
            if (applyHit && !target.IsDead)
                target.TakeSharkHit(transform.position);
        }

        private void UpdateRecover()
        {
            if (kind == DaySixSkyHostileKind.RetroUfo)
            {
                if (Vector2.Distance(transform.position, desiredPosition) <= 0.18f || stateClock >= 0.85f)
                    EnterRetroHidden(false);
                return;
            }

            desiredPosition = new Vector3(
                ViewportWorldX(entrySide < 0 ? 0.20f : 0.80f),
                ViewportWorldY(0.80f),
                0f);
            if (stateClock >= 0.85f)
            {
                EnterState(State.Patrol);
                nextAttackAt = Time.time + Random.Range(attackCooldownRange.x, attackCooldownRange.y);
                hitCollider.enabled = true;
                PickPatrolPosition();
            }
        }

        private void FinishAttackCycle()
        {
            if (kind == DaySixSkyHostileKind.RetroUfo)
            {
                hitCollider.enabled = false;
                EnterState(State.Recover);
                SetRetroSubmergeTarget();
                return;
            }

            EnterState(State.Recover);
            desiredPosition = new Vector3(transform.position.x, ViewportWorldY(0.80f), 0f);
        }

        private void EnterRetroHidden(bool immediate)
        {
            ReleaseCarriedPlayer(false);
            state = State.Hidden;
            stateClock = 0f;
            smoothedVelocity = Vector3.zero;
            spriteRenderer.enabled = false;
            hitCollider.enabled = false;
            nextAttackAt = Time.time + (immediate
                ? Random.Range(1.2f, 2.2f)
                : Random.Range(attackCooldownRange.x, attackCooldownRange.y));
        }

        private void UpdateHidden()
        {
            if (target == null || Time.time < nextAttackAt)
                return;

            AssignDifferentRetroLane();
            float emergeX = Mathf.Clamp(
                target.transform.position.x + Random.Range(-1.35f, 1.35f),
                ViewportWorldX(0.10f),
                ViewportWorldX(0.90f));
            float hiddenY = InterWaveMidY(emergeX) - 0.72f;
            transform.position = new Vector3(emergeX, hiddenY, 0f);
            desiredPosition = new Vector3(emergeX, InterWaveMidY(emergeX) + 0.22f, 0f);
            raceSide = target.transform.position.x >= emergeX ? 1f : -1f;
            spriteRenderer.enabled = true;
            hitCollider.enabled = false;
            contactConsumed = false;
            EnterState(State.Telegraph);
        }

        private void SetRetroSubmergeTarget()
        {
            desiredPosition = new Vector3(
                transform.position.x,
                InterWaveMidY(transform.position.x) - 0.78f,
                transform.position.z);
        }

        private void AssignDifferentRetroLane()
        {
            RefreshWaterLayers();
            if (waterLayers.Count < 2)
            {
                RestoreSkySorting();
                return;
            }

            int laneCount = waterLayers.Count - 1;
            int nearPlayer = target != null
                ? Mathf.Clamp(target.CurrentWaveIndex, 0, laneCount - 1)
                : Random.Range(0, laneCount);
            int lane = nearPlayer;
            if (laneCount > 1 && lane == previousRetroLane)
                lane = (lane + (Random.value < 0.5f ? 1 : laneCount - 1)) % laneCount;

            previousRetroLane = lane;
            AssignInterWaveLane(lane);
        }

        private void AssignRandomCrashLane()
        {
            RefreshWaterLayers();
            if (waterLayers.Count < 2)
                return;

            int lane = Random.Range(0, waterLayers.Count - 1);
            AssignInterWaveLane(lane);
        }

        private void AssignInterWaveLane(int lane)
        {
            lane = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            interWaveLane = lane;
            interWaveForeground = waterLayers[lane];
            interWaveBackground = waterLayers[lane + 1];

            Renderer waterRenderer = interWaveForeground.GetComponent<Renderer>();
            if (waterRenderer == null)
                waterRenderer = interWaveForeground.GetComponentInChildren<Renderer>();
            if (waterRenderer != null)
                spriteRenderer.sortingLayerID = waterRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = 0;

            if (interWaveRenderItem == null)
                interWaveRenderItem = gameObject.AddComponent<InterWaveRenderItem>();
            interWaveRenderItem.enabled = true;
            interWaveRenderItem.SetWaterAndLane(interWaveForeground, lane);
        }

        private void RefreshWaterLayers()
        {
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
            waterLayers.RemoveAll(water => water == null || !water.isActiveAndEnabled);
            waterLayers.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
        }

        private float InterWaveMidY(float worldX)
        {
            if (interWaveForeground == null || interWaveBackground == null)
                return target != null ? target.transform.position.y : ViewportWorldY(0.42f);
            return Mathf.Lerp(
                interWaveForeground.GetGameplaySurfaceHeight(worldX),
                interWaveBackground.GetGameplaySurfaceHeight(worldX),
                0.5f);
        }

        public bool TakeThrownItemHit(int damage, Vector2 hitPosition)
        {
            if (!CanBeHit)
                return false;

            health = Mathf.Max(0, health - Mathf.Max(1, damage));
            spriteRenderer.color = new Color(1f, 0.22f, 0.16f, baseTint.a);
            hitFlashUntil = Time.time + 0.12f;
            if (health <= 0)
                BeginCrash();
            return true;
        }

        private void BeginCrash()
        {
            ReleaseCarriedPlayer(false);
            state = State.Crashing;
            stateClock = 0f;
            crashStart = transform.position;
            crashTargetX = Mathf.Clamp(
                transform.position.x + Random.Range(-1.6f, 1.6f),
                ViewportWorldX(0.08f),
                ViewportWorldX(0.92f));
            crashSpinSpeed = Random.Range(140f, 235f) * (Random.value < 0.5f ? -1f : 1f);
            hitCollider.enabled = false;
            smoothedVelocity = Vector3.zero;

            if (kind != DaySixSkyHostileKind.RetroUfo || interWaveForeground == null)
                AssignRandomCrashLane();
            SpawnCrashExplosion();
            nextCrashExplosionAt = Time.time + Random.Range(
                crashExplosionIntervalRange.x,
                crashExplosionIntervalRange.y);
        }

        private void UpdateCrash()
        {
            float progress = Mathf.Clamp01(stateClock / Mathf.Max(0.5f, crashDuration));
            float targetY = InterWaveMidY(crashTargetX) - 0.52f;
            transform.position = new Vector3(
                Mathf.Lerp(crashStart.x, crashTargetX, progress),
                Mathf.Lerp(crashStart.y, targetY, progress * progress),
                crashStart.z);
            transform.rotation = Quaternion.Euler(0f, 0f, crashSpinSpeed * stateClock);

            float fade = 1f - Mathf.InverseLerp(crashFadeBeginsAt, 1f, progress);
            bool flash = Mathf.FloorToInt(stateClock * 12f) % 2 == 0;
            Color colour = flash ? new Color(1f, 0.08f, 0.04f, 1f) : baseTint;
            colour.a = baseTint.a * fade;
            spriteRenderer.color = colour;

            if (Time.time >= nextCrashExplosionAt)
            {
                SpawnCrashExplosion();
                nextCrashExplosionAt = Time.time + Random.Range(
                    crashExplosionIntervalRange.x,
                    crashExplosionIntervalRange.y);
            }

            if (progress >= 1f)
            {
                SpawnCrashExplosion();
                owner?.NotifySkyHostileRemoved(this, true);
                Destroy(gameObject);
            }
        }

        private void SpawnCrashExplosion()
        {
            Vector2 offset = Random.insideUnitCircle * 0.55f;
            ExplosionBasicEffect.SpawnInterWave(
                transform.position + (Vector3)offset,
                spriteRenderer,
                interWaveForeground,
                interWaveLane);
        }

        public void BeginRetreat()
        {
            if (state == State.Crashing || state == State.Retreat)
                return;

            ReleaseCarriedPlayer(false);
            RestoreSkySorting();
            state = State.Retreat;
            stateClock = 0f;
            hitCollider.enabled = false;
            desiredPosition = new Vector3(
                ViewportWorldX(entrySide < 0 ? -0.25f : 1.25f),
                ViewportWorldY(0.83f),
                0f);
        }

        private void UpdateRetreat()
        {
            ApplySmoothMovement();
            if (stateClock >= 2.5f ||
                transform.position.x < ViewportWorldX(-0.18f) ||
                transform.position.x > ViewportWorldX(1.18f))
            {
                owner?.NotifySkyHostileRemoved(this, false);
                Destroy(gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (state != State.Attack || contactConsumed || other == null)
                return;
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || surfer != target || kind == DaySixSkyHostileKind.ClawUfo)
                return;
            if (surfer.TakeSharkHit(transform.position))
                contactConsumed = true;
        }

        private void OnDestroy()
        {
            ReleaseCarriedPlayer(false);
            ReleaseSprites(moveFrames);
            ReleaseSprites(attackFrames);
        }

        private void EnterState(State newState)
        {
            state = newState;
            stateClock = 0f;
            frameClock = 0f;
            frameIndex = 0;
        }

        private void Animate()
        {
            bool attacking = state == State.Telegraph || state == State.Attack || state == State.Carry;
            Sprite[] frames = attacking ? attackFrames : moveFrames;
            if (frames == null || frames.Length == 0 || spriteRenderer == null)
                return;

            frameClock += Time.deltaTime * (attacking ? attackFramesPerSecond : moveFramesPerSecond);
            int nextFrame = Mathf.FloorToInt(frameClock) % frames.Length;
            if (spriteRenderer.sprite == null || nextFrame != frameIndex)
            {
                frameIndex = nextFrame;
                spriteRenderer.sprite = frames[frameIndex];
            }
        }

        private void ApplySmoothMovement()
        {
            float dt = Time.deltaTime;
            Vector3 delta = desiredPosition - transform.position;
            Vector3 wantedVelocity = delta.sqrMagnitude > 0.0001f
                ? Vector3.ClampMagnitude(delta * 4.2f, flightSpeed)
                : Vector3.zero;
            smoothedVelocity = Vector3.Lerp(
                smoothedVelocity,
                wantedVelocity,
                1f - Mathf.Exp(-movementSmoothing * dt));

            Vector3 step = smoothedVelocity * dt;
            if (step.sqrMagnitude > delta.sqrMagnitude && Vector3.Dot(step, delta) > 0f)
            {
                transform.position = desiredPosition;
                smoothedVelocity = Vector3.zero;
            }
            else
            {
                transform.position += step;
            }

            float bank = Mathf.Clamp(-smoothedVelocity.x * bankAngle * 0.2f, -bankAngle, bankAngle);
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.Euler(0f, 0f, bank),
                1f - Mathf.Exp(-8f * dt));
        }

        private void RestoreSkySorting()
        {
            if (interWaveRenderItem != null)
                interWaveRenderItem.enabled = false;
            spriteRenderer.sortingLayerID = originalSortingLayerId;
            spriteRenderer.sortingOrder = skySortingOrder;
            interWaveForeground = null;
            interWaveBackground = null;
            interWaveLane = -1;
        }

        private void SetFacing(float deltaX)
        {
            if (spriteRenderer != null && Mathf.Abs(deltaX) > 0.04f)
                spriteRenderer.flipX = deltaX < 0f;
        }

        private void LoadKindSprites()
        {
            ReleaseSprites(moveFrames);
            ReleaseSprites(attackFrames);
            string movePath;
            string attackPath;
            string prefix;
            switch (kind)
            {
                case DaySixSkyHostileKind.RacerUfo:
                    movePath = "Day6/SkyHostiles/racer_ufo_move";
                    attackPath = "Day6/SkyHostiles/racer_ufo_attack";
                    prefix = "racer_ufo";
                    break;
                case DaySixSkyHostileKind.RetroUfo:
                    movePath = "Day6/SkyHostiles/retro_ufo_move";
                    attackPath = "Day6/SkyHostiles/retro_ufo_attack";
                    prefix = "retro_ufo";
                    break;
                default:
                    movePath = "Day6/SkyHostiles/50s_ufo_move";
                    attackPath = "Day6/SkyHostiles/50s_ufo_claw";
                    prefix = "50s_ufo";
                    break;
            }

            moveFrames = LoadSheet(movePath, prefix + "_move");
            attackFrames = LoadSheet(attackPath, prefix + "_attack");
            if (moveFrames.Length > 0)
                spriteRenderer.sprite = moveFrames[0];
        }

        private void ResizeCollider()
        {
            if (spriteRenderer.sprite == null)
                return;
            hitCollider.size = spriteRenderer.sprite.bounds.size * 0.62f;
            hitCollider.offset = spriteRenderer.sprite.bounds.center;
        }

        private static Sprite[] LoadSheet(string resourcePath, string prefix)
        {
            Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
            if (sheet == null)
            {
                Debug.LogWarning($"DaySixSkyHostile could not load Resources/{resourcePath}.");
                return System.Array.Empty<Sprite>();
            }

            const int frameSize = 64;
            const float pixelsPerUnit = 32f;
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
                    result[index].name = $"{prefix}_{index:00}";
                    index++;
                }
            }
            return result;
        }

        private static void ReleaseSprites(Sprite[] sprites)
        {
            if (sprites == null)
                return;
            foreach (Sprite sprite in sprites)
                if (sprite != null)
                    Destroy(sprite);
        }

        private float ViewportWorldX(float x)
        {
            if (worldCamera == null)
                return transform.position.x;
            return worldCamera.ViewportToWorldPoint(
                new Vector3(x, 0.5f, Mathf.Abs(worldCamera.transform.position.z))).x;
        }

        private float ViewportWorldY(float y)
        {
            if (worldCamera == null)
                return transform.position.y;
            return worldCamera.ViewportToWorldPoint(
                new Vector3(0.5f, y, Mathf.Abs(worldCamera.transform.position.z))).y;
        }
    }
}
