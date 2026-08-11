using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelOcean
{
    /// <summary>
    /// Day 7's complete final boss. AION is a smooth inter-wave lane swimmer with
    /// authored move/charge/attack sheets, reused hostile projectile pools,
    /// lane-aware laser patterns, diving ambushes, hue phases, and a full HUD.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(CapsuleCollider2D), typeof(Rigidbody2D))]
    public sealed class AionFinalBoss : MonoBehaviour
    {
        private enum BossState
        {
            WaitingForWorld,
            Entrance,
            Patrol,
            Charge,
            Attack,
            Dive,
            PhaseShift,
            Banished
        }

        [Header("Health")]
        [SerializeField, Min(12)] private int maximumHealth = 72;
        [SerializeField, Min(0.02f)] private float damageCooldown = 0.10f;

        [Header("Animation")]
        [SerializeField, Min(1f)] private float movementFps = 8f;
        [SerializeField, Min(1f)] private float chargeFps = 10f;
        [SerializeField, Min(1f)] private float attackFps = 12f;
        [SerializeField, Min(0.1f)] private float spriteScale = 0.68f;

        [Header("Lane Swimming")]
        [SerializeField, Min(0.1f)] private float followDistance = 5.3f;
        [SerializeField, Min(0.1f)] private float horizontalSmoothTime = 0.55f;
        [SerializeField, Min(0.1f)] private float verticalSmoothTime = 0.35f;
        [SerializeField, Min(0.1f)] private float laneChangeDuration = 0.82f;

        [Header("Attack Rhythm")]
        [SerializeField, Min(0.2f)] private float openingAttackDelay = 1.35f;
        [SerializeField, Min(0.2f)] private float phaseOneAttackInterval = 2.1f;
        [SerializeField, Min(0.2f)] private float phaseFourAttackInterval = 0.95f;

        [Header("Hue Manifestation")]
        [SerializeField, Min(0f)] private float hueCyclesPerSecond = 0.055f;
        [SerializeField, Range(0f, 1f)] private float normalHueStrength = 0.24f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SurfDayProgressionDirector director;
        private DaySevenEncounter encounter;
        private TinyWaveSurfer target;
        private SpriteRenderer spriteRenderer;
        private CapsuleCollider2D hitCollider;
        private Rigidbody2D body;
        private InterWaveRenderItem renderItem;
        private AudioSource audioSource;
        private AudioClip chargeClip;
        private AudioClip launchClip;
        private AudioClip hitClip;
        private AudioClip defeatClip;
        private Sprite[] moveFrames = Array.Empty<Sprite>();
        private Sprite[] chargeFrames = Array.Empty<Sprite>();
        private Sprite[] attackFrames = Array.Empty<Sprite>();
        private Sprite[] activeFrames = Array.Empty<Sprite>();
        private AionBossHealthHud healthHud;
        private Coroutine encounterRoutine;
        private BossState state = BossState.WaitingForWorld;
        private int currentHealth;
        private int manifestedPhase = 1;
        private int currentLane;
        private int sourceLane;
        private int targetLane;
        private int lastPattern = -1;
        private float animationClock;
        private float animationFps;
        private float nextDamageAt;
        private float nextContactDamageAt;
        private float hueOffset;
        private float hitFlashUntil;
        private float visibilityAlpha = 1f;
        private float horizontalVelocity;
        private float verticalVelocity;
        private float laneChangeClock;
        private float laneChangeStartTime;
        private bool animationLoop;
        private bool animationPingPong;
        private bool changingLane;
        private bool phaseShiftRequested;
        private bool defeated;
        private bool configured;
        private Vector3 normalScale;

        public int CurrentHealth => currentHealth;
        public int MaximumHealth => maximumHealth;
        public bool IsDefeated => defeated;
        public bool CanBeHit => configured && !defeated &&
                                state != BossState.WaitingForWorld &&
                                state != BossState.Entrance &&
                                state != BossState.Dive &&
                                state != BossState.PhaseShift;
        public int ManifestationPhase => manifestedPhase;
        public Color CurrentManifestationColour => ManifestationColour();

        public static AionFinalBoss Spawn(
            SurfDayProgressionDirector progression,
            DaySevenEncounter owner)
        {
            AionFinalBoss existing = FindFirstObjectByType<AionFinalBoss>();
            if (existing != null)
                return existing;

            GameObject bossObject = new("AION - The Tide Beyond");
            SpriteRenderer renderer = bossObject.AddComponent<SpriteRenderer>();
            CapsuleCollider2D collider = bossObject.AddComponent<CapsuleCollider2D>();
            Rigidbody2D rigidbody = bossObject.AddComponent<Rigidbody2D>();
            InterWaveRenderItem interWave = bossObject.AddComponent<InterWaveRenderItem>();
            AudioSource source = bossObject.AddComponent<AudioSource>();
            AionFinalBoss boss = bossObject.AddComponent<AionFinalBoss>();

            renderer.sortingOrder = 18;
            collider.isTrigger = true;
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(3.25f, 3.35f);
            collider.offset = new Vector2(0f, -0.12f);
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            boss.Configure(progression, owner, renderer, collider, rigidbody, interWave, source);
            return boss;
        }

        private void Configure(
            SurfDayProgressionDirector progression,
            DaySevenEncounter owner,
            SpriteRenderer renderer,
            CapsuleCollider2D collider,
            Rigidbody2D rigidbody,
            InterWaveRenderItem interWave,
            AudioSource source)
        {
            director = progression;
            encounter = owner;
            spriteRenderer = renderer;
            hitCollider = collider;
            body = rigidbody;
            renderItem = interWave;
            audioSource = source;
            currentHealth = maximumHealth;
            normalScale = Vector3.one * spriteScale;
            transform.localScale = normalScale;
            hitCollider.enabled = false;

            moveFrames = LoadFrames("Day7/aion_move");
            chargeFrames = LoadFrames("Day7/aion_charge");
            attackFrames = LoadFrames("Day7/aion_attack");
            PlayAnimation(moveFrames, movementFps, true, true);

            chargeClip = Resources.Load<AudioClip>("Audio/SFX/alien_ship");
            launchClip = Resources.Load<AudioClip>("Audio/SFX/missile_launch");
            hitClip = Resources.Load<AudioClip>("Audio/SFX/reaper_hurt");
            defeatClip = Resources.Load<AudioClip>("Audio/SFX/flow_finish");

            if (!BossSpawnAuthority.RegisterBoss(this))
                return;

            healthHud = AionBossHealthHud.Create(this);
            configured = true;
            encounterRoutine = StartCoroutine(BeginEncounterWhenReady());
        }

        private IEnumerator BeginEncounterWhenReady()
        {
            state = BossState.WaitingForWorld;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                AcquireTarget();
                RefreshWater(target != null ? target.transform.position.x : transform.position.x);
                if (target != null && waterLayers.Count >= 2)
                    break;
                yield return null;
            }

            if (target == null || waterLayers.Count < 2)
            {
                Debug.LogError("AION could not find the player and inter-wave stack.", this);
                yield break;
            }

            yield return EntranceRoutine();
            yield return new WaitForSeconds(openingAttackDelay);

            while (!defeated)
            {
                if (phaseShiftRequested)
                    yield return PhaseShiftRoutine();

                int pattern = PickPatternForPhase();
                yield return ExecutePattern(pattern);

                if (defeated)
                    yield break;

                state = BossState.Patrol;
                PlayAnimation(moveFrames, movementFps, true, true);
                ScheduleLaneChange(false);
                float interval = Mathf.Lerp(
                    phaseOneAttackInterval,
                    phaseFourAttackInterval,
                    (manifestedPhase - 1f) / 3f);
                yield return new WaitForSeconds(interval);
            }
        }

        private void Update()
        {
            if (!configured)
                return;

            UpdateAnimation();
            UpdateHueAndScale();
        }

        private void FixedUpdate()
        {
            if (!configured)
                return;
            if (state == BossState.Patrol || state == BossState.Charge ||
                state == BossState.Attack)
            {
                UpdateLaneSwimming(Time.fixedDeltaTime);
            }
        }

        private void UpdateAnimation()
        {
            if (spriteRenderer == null || activeFrames == null || activeFrames.Length == 0)
                return;

            animationClock += Time.deltaTime * animationFps;
            int frame;
            if (animationPingPong && activeFrames.Length > 1)
            {
                int span = activeFrames.Length * 2 - 2;
                int position = Mathf.FloorToInt(animationClock) % span;
                frame = position < activeFrames.Length ? position : span - position;
            }
            else if (animationLoop)
            {
                frame = Mathf.FloorToInt(animationClock) % activeFrames.Length;
            }
            else
            {
                frame = Mathf.Min(activeFrames.Length - 1, Mathf.FloorToInt(animationClock));
            }

            spriteRenderer.sprite = activeFrames[Mathf.Clamp(frame, 0, activeFrames.Length - 1)];
        }

        private void PlayAnimation(Sprite[] frames, float fps, bool loop, bool pingPong = false)
        {
            activeFrames = frames != null && frames.Length > 0 ? frames : moveFrames;
            animationFps = Mathf.Max(1f, fps);
            animationLoop = loop;
            animationPingPong = pingPong;
            animationClock = 0f;
            if (activeFrames != null && activeFrames.Length > 0 && spriteRenderer != null)
                spriteRenderer.sprite = activeFrames[0];
        }

        private void UpdateHueAndScale()
        {
            if (spriteRenderer == null)
                return;

            Color tint = Time.time < hitFlashUntil
                ? Color.white
                : ManifestationColour();
            tint.a = visibilityAlpha;
            spriteRenderer.color = tint;

            if (state != BossState.Entrance && state != BossState.Dive &&
                state != BossState.PhaseShift && state != BossState.Banished)
            {
                float pulse = Time.time < hitFlashUntil
                    ? 1f + Mathf.Sin(Time.time * 65f) * 0.035f
                    : 1f + Mathf.Sin(Time.time * 2.4f) * 0.012f;
                transform.localScale = normalScale * pulse;
            }
        }

        private Color ManifestationColour()
        {
            float hue = Mathf.Repeat(
                Time.time * hueCyclesPerSecond + hueOffset + (manifestedPhase - 1) * 0.11f,
                1f);
            Color spectrum = Color.HSVToRGB(hue, 0.68f, 1f);
            float strength = Mathf.Clamp01(normalHueStrength + (manifestedPhase - 1) * 0.035f);
            return Color.Lerp(Color.white, spectrum, strength);
        }

        private IEnumerator EntranceRoutine()
        {
            state = BossState.Entrance;
            PlayAnimation(moveFrames, movementFps, true, true);
            AcquireTarget();
            RefreshWater(target.transform.position.x);

            int lanes = Mathf.Max(1, waterLayers.Count - 1);
            currentLane = Mathf.Clamp(lanes / 2, 0, lanes - 1);
            sourceLane = targetLane = currentLane;
            float spawnX = target.transform.position.x + followDistance + 1.2f;
            float laneY = ResolveLaneY(currentLane, spawnX);
            float deepY = LowestSurfaceY(spawnX) - 4.2f;
            body.position = new Vector2(spawnX, deepY);
            visibilityAlpha = 0f;
            transform.localScale = normalScale * 0.32f;
            SetRenderLane(0);

            SurferSlugMinimalHud.ShowNotice(
                "AION — THE TIDE BEYOND\nTHE OCEAN HAS OPENED ITS EYE",
                4.2f);
            PlayOneShot(chargeClip, 0.72f);

            float duration = 3.1f;
            float elapsed = 0f;
            while (elapsed < duration && !defeated)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                body.MovePosition(new Vector2(spawnX, Mathf.Lerp(deepY, laneY, eased)));
                visibilityAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.66f, t));
                transform.localScale = Vector3.Lerp(normalScale * 0.32f, normalScale, eased);
                if (t >= 0.52f)
                    SetRenderLane(currentLane);
                yield return null;
            }

            body.position = new Vector2(spawnX, ResolveLaneY(currentLane, spawnX));
            visibilityAlpha = 1f;
            transform.localScale = normalScale;
            hitCollider.enabled = true;
            state = BossState.Patrol;
            PlayAnimation(moveFrames, movementFps, true, true);
        }

        private void UpdateLaneSwimming(float deltaTime)
        {
            AcquireTarget();
            if (target == null)
                return;

            Vector2 position = body.position;
            float desiredX = target.transform.position.x + followDistance +
                             Mathf.Sin(Time.time * 0.55f) * 0.48f;
            float x = Mathf.SmoothDamp(
                position.x,
                desiredX,
                ref horizontalVelocity,
                horizontalSmoothTime,
                7.5f,
                deltaTime);

            float desiredY;
            if (changingLane)
            {
                laneChangeClock = Time.time - laneChangeStartTime;
                float t = Mathf.Clamp01(laneChangeClock / laneChangeDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                desiredY = Mathf.Lerp(
                    ResolveLaneY(sourceLane, x),
                    ResolveLaneY(targetLane, x),
                    eased);
                if (t >= 0.5f)
                    SetRenderLane(targetLane);
                if (t >= 1f)
                {
                    currentLane = targetLane;
                    changingLane = false;
                    SetRenderLane(currentLane);
                }
            }
            else
            {
                desiredY = ResolveLaneY(currentLane, x);
            }

            float y = Mathf.SmoothDamp(
                position.y,
                desiredY,
                ref verticalVelocity,
                verticalSmoothTime,
                9f,
                deltaTime);
            body.MovePosition(new Vector2(x, y));
        }

        private void ScheduleLaneChange(bool forceDifferent)
        {
            RefreshWater(transform.position.x);
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            if (laneCount <= 1)
                return;

            int chosen = UnityEngine.Random.Range(0, laneCount);
            if (forceDifferent || chosen == currentLane)
            {
                int step = UnityEngine.Random.Range(1, laneCount);
                chosen = (currentLane + step) % laneCount;
            }

            sourceLane = currentLane;
            targetLane = chosen;
            laneChangeStartTime = Time.time;
            laneChangeClock = 0f;
            changingLane = targetLane != sourceLane;
        }

        private int PickPatternForPhase()
        {
            int[] patternPool = manifestedPhase switch
            {
                1 => new[] { 0, 1, 2, 5 },
                2 => new[] { 0, 1, 2, 3, 5 },
                3 => new[] { 0, 1, 2, 3, 4, 5 },
                _ => new[] { 0, 1, 2, 3, 4, 5, 6 }
            };
            int selected = patternPool[UnityEngine.Random.Range(0, patternPool.Length)];
            if (patternPool.Length > 1 && selected == lastPattern)
            {
                int index = Array.IndexOf(patternPool, selected);
                selected = patternPool[(index + UnityEngine.Random.Range(1, patternPool.Length)) %
                                       patternPool.Length];
            }
            lastPattern = selected;
            return selected;
        }

        private IEnumerator ExecutePattern(int pattern)
        {
            switch (pattern)
            {
                case 0:
                    yield return ProjectileFanPattern();
                    break;
                case 1:
                    yield return StaircaseVolleyPattern();
                    break;
                case 2:
                    yield return LaserSweepPattern();
                    break;
                case 3:
                    yield return SafeLaneGridPattern();
                    break;
                case 4:
                    yield return PortalCrossfirePattern();
                    break;
                case 5:
                    yield return DiveAmbushPattern();
                    break;
                default:
                    yield return CrossingLaserPattern();
                    break;
            }
        }

        private IEnumerator BeginCharge(float seconds)
        {
            state = BossState.Charge;
            PlayAnimation(chargeFrames, chargeFps, false);
            PlayOneShot(chargeClip, 0.42f);
            yield return new WaitForSeconds(seconds);
        }

        private IEnumerator BeginAttack(float seconds)
        {
            state = BossState.Attack;
            PlayAnimation(attackFrames, attackFps, false);
            PlayOneShot(launchClip, 0.74f);
            yield return new WaitForSeconds(seconds);
        }

        private IEnumerator ProjectileFanPattern()
        {
            yield return BeginCharge(0.72f);
            int volleys = manifestedPhase >= 3 ? 3 : 2;
            for (int volley = 0; volley < volleys && !defeated; volley++)
            {
                state = BossState.Attack;
                PlayAnimation(attackFrames, attackFps, false);
                int lanes = LaneCount;
                int safe = UnityEngine.Random.Range(0, lanes);
                for (int lane = 0; lane < lanes; lane++)
                {
                    if (manifestedPhase == 1 && lane == safe)
                        continue;
                    SpawnLaneProjectile(lane, DirectionToPlayer(), ProjectileKind(volley + lane));
                }
                PlayOneShot(launchClip, 0.72f);
                yield return new WaitForSeconds(0.52f);
            }
        }

        private IEnumerator StaircaseVolleyPattern()
        {
            yield return BeginCharge(0.62f);
            int lanes = LaneCount;
            bool reverse = UnityEngine.Random.value < 0.5f;
            int passes = manifestedPhase >= 3 ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < lanes && !defeated; i++)
                {
                    int lane = reverse ? lanes - 1 - i : i;
                    state = BossState.Attack;
                    PlayAnimation(attackFrames, attackFps, false);
                    SpawnLaneProjectile(lane, DirectionToPlayer(), ProjectileKind(i + pass));
                    PlayOneShot(launchClip, 0.52f);
                    yield return new WaitForSeconds(Mathf.Lerp(0.34f, 0.20f, (manifestedPhase - 1f) / 3f));
                }
                reverse = !reverse;
            }
        }

        private IEnumerator LaserSweepPattern()
        {
            yield return BeginCharge(0.84f);
            state = BossState.Attack;
            PlayAnimation(attackFrames, attackFps, false);
            int lanes = LaneCount;
            bool reverse = UnityEngine.Random.value < 0.5f;
            Color colour = LaserColour();
            for (int i = 0; i < lanes && !defeated; i++)
            {
                int lane = reverse ? lanes - 1 - i : i;
                AionLaneLaser.Spawn(
                    this,
                    lane,
                    lane,
                    Mathf.Lerp(0.70f, 0.46f, (manifestedPhase - 1f) / 3f),
                    0.58f,
                    colour);
                PlayOneShot(launchClip, 0.36f);
                yield return new WaitForSeconds(0.31f);
            }
            yield return new WaitForSeconds(0.70f);
        }

        private IEnumerator SafeLaneGridPattern()
        {
            yield return BeginCharge(0.92f);
            int repetitions = manifestedPhase >= 4 ? 3 : manifestedPhase >= 3 ? 2 : 1;
            int lanes = LaneCount;
            int safeLane = FindPlayerLane();
            for (int repetition = 0; repetition < repetitions && !defeated; repetition++)
            {
                state = BossState.Attack;
                PlayAnimation(attackFrames, attackFps, false);
                safeLane = (safeLane + UnityEngine.Random.Range(1, lanes)) % lanes;
                for (int lane = 0; lane < lanes; lane++)
                {
                    if (lane == safeLane)
                        continue;
                    AionLaneLaser.Spawn(this, lane, lane, 0.88f, 0.62f, LaserColour());
                }
                SurferSlugMinimalHud.ShowNotice($"SAFE CURRENT  {safeLane + 1}", 1.1f);
                PlayOneShot(chargeClip, 0.46f);
                yield return new WaitForSeconds(1.62f);
            }
        }

        private IEnumerator PortalCrossfirePattern()
        {
            yield return BeginCharge(0.70f);
            Camera camera = Camera.main;
            float centreX = camera != null ? camera.transform.position.x : transform.position.x;
            float halfWidth = camera != null && camera.orthographic
                ? camera.orthographicSize * camera.aspect + 1.1f
                : 9f;
            int rounds = manifestedPhase >= 4 ? 4 : 3;
            for (int round = 0; round < rounds && !defeated; round++)
            {
                state = BossState.Attack;
                PlayAnimation(attackFrames, attackFps, false);
                int firstLane = round % LaneCount;
                int secondLane = (LaneCount - 1 - round + LaneCount) % LaneCount;
                SpawnLaneProjectile(firstLane, 1f, ProjectileKind(round), centreX - halfWidth);
                SpawnLaneProjectile(secondLane, -1f, ProjectileKind(round + 2), centreX + halfWidth);
                PlayOneShot(launchClip, 0.62f);
                yield return new WaitForSeconds(0.48f);
            }
        }

        private IEnumerator DiveAmbushPattern()
        {
            state = BossState.Dive;
            hitCollider.enabled = false;
            PlayAnimation(moveFrames, movementFps * 1.35f, true, true);
            Vector2 start = body.position;
            float deepY = LowestSurfaceY(start.x) - 4.4f;
            float elapsed = 0f;
            const float sinkDuration = 0.72f;
            while (elapsed < sinkDuration && !defeated)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / sinkDuration);
                body.MovePosition(new Vector2(start.x, Mathf.Lerp(start.y, deepY, t)));
                visibilityAlpha = 1f - t;
                transform.localScale = Vector3.Lerp(normalScale, normalScale * 0.44f, t);
                yield return null;
            }

            AcquireTarget();
            int newLane = Mathf.Clamp(
                FindPlayerLane() + UnityEngine.Random.Range(-1, 2),
                0,
                LaneCount - 1);
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float emergeX = target != null
                ? target.transform.position.x + side * UnityEngine.Random.Range(2.3f, 4.2f)
                : start.x;
            float emergeY = ResolveLaneY(newLane, emergeX);
            body.position = new Vector2(emergeX, LowestSurfaceY(emergeX) - 4.4f);
            currentLane = sourceLane = targetLane = newLane;
            changingLane = false;
            SetRenderLane(newLane);
            PlayAnimation(attackFrames, attackFps, false);
            PlayOneShot(chargeClip, 0.58f);

            elapsed = 0f;
            const float riseDuration = 0.92f;
            Vector2 deepStart = body.position;
            bool burstSpawned = false;
            while (elapsed < riseDuration && !defeated)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                body.MovePosition(new Vector2(emergeX, Mathf.Lerp(deepStart.y, emergeY, eased)));
                visibilityAlpha = Mathf.SmoothStep(0f, 1f, t);
                transform.localScale = Vector3.Lerp(normalScale * 0.44f, normalScale * 1.08f, eased);
                if (!burstSpawned && t >= 0.58f)
                {
                    burstSpawned = true;
                    hitCollider.enabled = true;
                    ExplosionBasicEffect.SpawnInterWave(
                        body.position,
                        spriteRenderer,
                        GetSortingWater(newLane),
                        newLane);
                    SpawnLaneProjectile(newLane, -1f, ProjectileKind(manifestedPhase));
                    SpawnLaneProjectile(newLane, 1f, ProjectileKind(manifestedPhase + 1));
                    if (manifestedPhase >= 3)
                    {
                        SpawnLaneProjectile((newLane + 1) % LaneCount, -1f, ProjectileKind(2));
                        SpawnLaneProjectile((newLane + LaneCount - 1) % LaneCount, 1f, ProjectileKind(3));
                    }
                    PlayOneShot(launchClip, 0.85f);
                }
                yield return null;
            }

            body.position = new Vector2(emergeX, emergeY);
            visibilityAlpha = 1f;
            transform.localScale = normalScale;
            hitCollider.enabled = true;
            state = BossState.Patrol;
            PlayAnimation(moveFrames, movementFps, true, true);
        }

        private IEnumerator CrossingLaserPattern()
        {
            yield return BeginCharge(1.02f);
            state = BossState.Attack;
            PlayAnimation(attackFrames, attackFps, false);
            int top = LaneCount - 1;
            AionLaneLaser.Spawn(this, 0, top, 0.95f, 0.72f, LaserColour());
            AionLaneLaser.Spawn(this, top, 0, 0.95f, 0.72f,
                Color.Lerp(LaserColour(), Color.magenta, 0.36f));
            PlayOneShot(launchClip, 0.82f);
            yield return new WaitForSeconds(1.82f);
        }

        private IEnumerator PhaseShiftRoutine()
        {
            phaseShiftRequested = false;
            state = BossState.PhaseShift;
            hitCollider.enabled = false;
            PlayAnimation(chargeFrames, chargeFps * 1.25f, true, true);
            healthHud?.SetPhase(manifestedPhase);
            SurferSlugMinimalHud.ShowNotice(
                $"MANIFESTATION {manifestedPhase}\nREALITY COHESION FAILING",
                2.1f);
            PlayOneShot(chargeClip, 0.82f);

            float elapsed = 0f;
            const float duration = 1.55f;
            while (elapsed < duration && !defeated)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t);
                transform.localScale = new Vector3(
                    normalScale.x * (1f + wave * 0.16f),
                    normalScale.y * (1f - wave * 0.12f),
                    normalScale.z);
                hueOffset += Time.deltaTime * 0.34f;
                visibilityAlpha = 0.72f + Mathf.Abs(Mathf.Sin(t * Mathf.PI * 5f)) * 0.28f;
                yield return null;
            }

            visibilityAlpha = 1f;
            transform.localScale = normalScale;
            hitCollider.enabled = true;
            state = BossState.Patrol;
            PlayAnimation(moveFrames, movementFps, true, true);
            ScheduleLaneChange(true);
        }

        public bool TakeThrownItemHit(int damage, Vector2 impactPosition)
        {
            if (!CanBeHit || damage <= 0 || Time.time < nextDamageAt)
                return false;

            nextDamageAt = Time.time + damageCooldown;
            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, damage));
            hitFlashUntil = Time.time + 0.13f;
            hueOffset = Mathf.Repeat(hueOffset + 0.083f, 1f);
            PlayOneShot(hitClip, 0.55f);
            ExplosionBasicEffect.SpawnInterWave(
                impactPosition,
                spriteRenderer,
                GetSortingWater(currentLane),
                currentLane);
            healthHud?.RefreshImmediate();

            int newPhase = DeterminePhase();
            if (newPhase > manifestedPhase)
            {
                manifestedPhase = newPhase;
                phaseShiftRequested = true;
            }

            if (currentHealth <= 0 && !defeated)
                StartCoroutine(BanishRoutine());

            return true;
        }

        private int DeterminePhase()
        {
            float ratio = (float)currentHealth / Mathf.Max(1, maximumHealth);
            if (ratio <= 0.25f) return 4;
            if (ratio <= 0.50f) return 3;
            if (ratio <= 0.75f) return 2;
            return 1;
        }

        private IEnumerator BanishRoutine()
        {
            if (defeated)
                yield break;

            defeated = true;
            state = BossState.Banished;
            hitCollider.enabled = false;
            AionLaneLaser[] lasers = FindObjectsByType<AionLaneLaser>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (AionLaneLaser laser in lasers)
                if (laser != null && laser.Owner == this) Destroy(laser.gameObject);

            PlayAnimation(attackFrames, attackFps * 0.72f, false);
            PlayOneShot(defeatClip, 0.9f);
            healthHud?.BeginVictory();
            SurferSlugMinimalHud.ShowNotice(
                "THE OTHER SHORE REMEMBERS YOU",
                4f);

            Vector3 startScale = normalScale;
            Vector2 startPosition = body.position;
            float elapsed = 0f;
            const float duration = 3.2f;
            float nextBurstAt = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float fold = Mathf.SmoothStep(0f, 1f, t);
                hueOffset += Time.deltaTime * (0.55f + t);
                visibilityAlpha = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.62f, 1f, t));
                transform.localScale = new Vector3(
                    Mathf.Lerp(startScale.x, startScale.x * 0.08f, fold),
                    Mathf.Lerp(startScale.y, startScale.y * 1.28f, fold),
                    1f);
                body.MovePosition(startPosition + Vector2.up * (fold * 0.85f));

                if (elapsed >= nextBurstAt)
                {
                    nextBurstAt += 0.42f;
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * 1.15f;
                    ExplosionBasicEffect.SpawnInterWave(
                        body.position + offset,
                        spriteRenderer,
                        GetSortingWater(currentLane),
                        currentLane);
                }
                yield return null;
            }

            visibilityAlpha = 0f;
            encounter?.NotifyAionDefeated(this);
            BossSpawnAuthority.UnregisterBoss(this);
            Destroy(gameObject);
        }

        private void SpawnLaneProjectile(
            int lane,
            float direction,
            DaySixHazardKind kind,
            float? spawnX = null)
        {
            int clampedLane = Mathf.Clamp(lane, 0, LaneCount - 1);
            float x = spawnX ?? transform.position.x;
            float y = ResolveLaneY(clampedLane, x);
            float speed = Mathf.Lerp(4.1f, 6.2f, (manifestedPhase - 1f) / 3f);
            DaySixHazardProjectile.Spawn(
                kind,
                new Vector3(x, y, transform.position.z),
                direction,
                speed,
                clampedLane,
                GetSortingWater(clampedLane),
                0f);
        }

        private static DaySixHazardKind ProjectileKind(int index)
        {
            DaySixHazardKind[] palette =
            {
                DaySixHazardKind.Spore,
                DaySixHazardKind.Toast,
                DaySixHazardKind.ResortWake,
                DaySixHazardKind.Flush
            };
            return palette[Mathf.Abs(index) % palette.Length];
        }

        private Color LaserColour()
        {
            return Color.Lerp(
                new Color(0.04f, 0.94f, 1f, 1f),
                Color.HSVToRGB(Mathf.Repeat(hueOffset + manifestedPhase * 0.17f, 1f), 0.72f, 1f),
                0.62f);
        }

        private float DirectionToPlayer()
        {
            AcquireTarget();
            return target != null && target.transform.position.x < transform.position.x ? -1f : 1f;
        }

        private int FindPlayerLane()
        {
            AcquireTarget();
            if (target == null)
                return currentLane;

            int bestLane = 0;
            float bestDistance = float.MaxValue;
            for (int lane = 0; lane < LaneCount; lane++)
            {
                float distance = Mathf.Abs(
                    ResolveLaneY(lane, target.transform.position.x) - target.transform.position.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLane = lane;
                }
            }
            return bestLane;
        }

        private int LaneCount
        {
            get
            {
                RefreshWater(transform.position.x);
                return Mathf.Max(1, waterLayers.Count - 1);
            }
        }

        private float ResolveLaneY(int lane, float worldX)
        {
            RefreshWater(worldX);
            if (waterLayers.Count < 2)
                return transform.position.y;
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(
                waterLayers[clamped].GetGameplaySurfaceHeight(worldX),
                waterLayers[clamped + 1].GetGameplaySurfaceHeight(worldX),
                0.5f);
        }

        private float LowestSurfaceY(float worldX)
        {
            RefreshWater(worldX);
            if (waterLayers.Count == 0)
                return transform.position.y - 3f;
            float lowest = float.PositiveInfinity;
            foreach (PixelWaterGPU water in waterLayers)
                lowest = Mathf.Min(lowest, water.GetGameplaySurfaceHeight(worldX));
            return lowest;
        }

        private PixelWaterGPU GetSortingWater(int lane)
        {
            RefreshWater(transform.position.x);
            return waterLayers.Count == 0
                ? null
                : waterLayers[Mathf.Clamp(lane, 0, waterLayers.Count - 1)];
        }

        private void SetRenderLane(int lane)
        {
            if (renderItem == null)
                return;

            int clampedLane = Mathf.Max(0, lane);
            PixelWaterGPU correspondingWater = GetSortingWater(clampedLane);
            renderItem.SetWaterAndLane(correspondingWater, clampedLane);

            // Keep AION on the ocean's own sorting layer as well as between its
            // procedural render queues. Without this, the sprite can still be
            // composited like a foreground object even though its material is
            // correctly assigned to an inter-wave lane.
            Renderer waterRenderer = correspondingWater != null
                ? correspondingWater.GetComponent<Renderer>()
                : null;
            if (waterRenderer == null && correspondingWater != null)
                waterRenderer = correspondingWater.GetComponentInChildren<Renderer>();

            spriteRenderer.sortingOrder = 0;
            if (waterRenderer != null)
                spriteRenderer.sortingLayerID = waterRenderer.sortingLayerID;
        }

        private void RefreshWater(float worldX)
        {
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(worldX));
            waterLayers.RemoveAll(water => water == null || !water.isActiveAndEnabled);
            waterLayers.Sort((left, right) =>
                left.IndependentLayerIndex.CompareTo(right.IndependentLayerIndex));
        }

        private void AcquireTarget()
        {
            if (target != null && !target.IsDead && target.IsPlayerControlled)
                return;
            target = GameplayTargetCache.Surfers
                .Where(surfer => surfer != null && !surfer.IsDead && surfer.IsPlayerControlled)
                .OrderBy(surfer => Vector2.Distance(transform.position, surfer.transform.position))
                .FirstOrDefault();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (defeated || other == null || Time.time < nextContactDamageAt)
                return;
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || !surfer.IsPlayerControlled)
                return;
            nextContactDamageAt = Time.time + 0.8f;
            surfer.TakeSharkHit(transform.position);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static Sprite[] LoadFrames(string path)
        {
            Sprite[] frames = Resources.LoadAll<Sprite>(path);
            return frames
                .Where(frame => frame != null)
                .OrderBy(frame => FrameNumber(frame.name))
                .ToArray();
        }

        private static int FrameNumber(string value)
        {
            int split = value.LastIndexOf('_');
            return split >= 0 && int.TryParse(value.Substring(split + 1), out int number)
                ? number
                : 0;
        }

        private void OnDestroy()
        {
            BossSpawnAuthority.UnregisterBoss(this);
            if (healthHud != null)
                Destroy(healthHud.gameObject);
        }
    }

    /// <summary>Fixed, minimal final-boss HUD using the project's TMP font.</summary>
    [DisallowMultipleComponent]
    internal sealed class AionBossHealthHud : MonoBehaviour
    {
        private AionFinalBoss boss;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform fill;
        private TextMeshProUGUI title;
        private TextMeshProUGUI value;
        private int displayedPhase = 1;
        private bool victory;
        private static Sprite whiteSprite;

        public static AionBossHealthHud Create(AionFinalBoss target)
        {
            GameObject host = new("AION Boss HUD");
            Canvas hudCanvas = host.AddComponent<Canvas>();
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            host.AddComponent<GraphicRaycaster>();
            AionBossHealthHud hud = host.AddComponent<AionBossHealthHud>();
            hud.boss = target;
            hud.canvas = hudCanvas;

            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.overrideSorting = true;
            hudCanvas.sortingOrder = 32450;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            RectTransform panel = CreateRect("AION Health", transform, new Vector2(820f, 94f));
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            // The regular HUD occupies roughly the first 188 reference pixels.
            // Leave a small gap so the boss title and health never cover it.
            panel.anchoredPosition = new Vector2(0f, -204f);
            group = panel.gameObject.AddComponent<CanvasGroup>();

            title = CreateText(panel, "AION — THE TIDE BEYOND", 24f, TextAlignmentOptions.Center);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.58f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

            RectTransform shadow = CreateRect("Health Shadow", panel, new Vector2(0f, 0f));
            shadow.anchorMin = new Vector2(0.02f, 0.14f);
            shadow.anchorMax = new Vector2(0.98f, 0.50f);
            shadow.offsetMin = new Vector2(4f, -4f);
            shadow.offsetMax = new Vector2(4f, -4f);
            AddImage(shadow, new Color(0f, 0f, 0f, 0.78f));

            RectTransform background = CreateRect("Health Background", panel, Vector2.zero);
            background.anchorMin = new Vector2(0.02f, 0.14f);
            background.anchorMax = new Vector2(0.98f, 0.50f);
            background.offsetMin = background.offsetMax = Vector2.zero;
            AddImage(background, new Color(0.015f, 0.025f, 0.07f, 0.86f));

            fill = CreateRect("Manifestation Fill", background, Vector2.zero);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            AddImage(fill, new Color(0.04f, 0.88f, 1f, 0.96f));

            value = CreateText(background, string.Empty, 18f, TextAlignmentOptions.Center);
            value.rectTransform.anchorMin = Vector2.zero;
            value.rectTransform.anchorMax = Vector2.one;
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
            RefreshImmediate();
        }

        private void LateUpdate()
        {
            if (boss == null)
            {
                Destroy(gameObject);
                return;
            }

            RefreshImmediate();
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, victory ? 0f : 1f,
                    Time.unscaledDeltaTime * (victory ? 0.32f : 2.5f));
        }

        public void RefreshImmediate()
        {
            if (boss == null || fill == null)
                return;

            float ratio = Mathf.Clamp01((float)boss.CurrentHealth /
                                        Mathf.Max(1, boss.MaximumHealth));
            Vector3 scale = fill.localScale;
            scale.x = ratio;
            fill.localScale = scale;
            Image image = fill.GetComponent<Image>();
            if (image != null)
                image.color = boss.CurrentManifestationColour;
            if (value != null)
                value.text = $"THE VEIL  {boss.CurrentHealth} / {boss.MaximumHealth}   •   MANIFESTATION {displayedPhase}";
        }

        public void SetPhase(int phase)
        {
            displayedPhase = Mathf.Clamp(phase, 1, 4);
            RefreshImmediate();
        }

        public void BeginVictory()
        {
            victory = true;
            if (title != null)
                title.text = "THE VEIL IS CLOSING";
            if (value != null)
                value.text = "AION RETURNS BEYOND THE OTHER SHORE";
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return rect;
        }

        private static void AddImage(RectTransform rect, Color colour)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = GetWhiteSprite();
            image.color = colour;
            image.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string text,
            float size,
            TextAlignmentOptions alignment)
        {
            GameObject go = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.outlineColor = Color.black;
            label.outlineWidth = 0.24f;
            PixelFontLibrary.Apply(label, true, true);
            return label;
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "AION HUD Pixel"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            whiteSprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);
            return whiteSprite;
        }
    }
}
