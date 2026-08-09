using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    public enum DaySixCreatureKind
    {
        Fishbowl,
        MushroomSquid,
        MustacheShark,
        Resort,
        Starfish,
        Toaster,
        Toilet
    }

    /// <summary>
    /// Shared controller for the seven Day 6 oddities. Every creature remains
    /// attached to an inter-wave lane, but each one owns a different telegraphed
    /// attack pattern so the new art is more than a cosmetic enemy reskin.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class DaySixCreature : MonoBehaviour
    {
        private enum State { Entering, Patrol, Telegraph, Attack, Recovery, Hit, Retreat }

        private readonly List<PixelWaterGPU> waterLayers = new();
        private DaySixCreatureKind kind;
        private DaySixEncounter encounter;
        private SpriteRenderer spriteRenderer;
        private BoxCollider2D hitCollider;
        private Rigidbody2D body;
        private InterWaveRenderItem renderItem;
        private AudioSource audioSource;
        private AudioClip attackClip;
        private AudioClip hitClip;
        private TinyWaveSurfer target;
        private Camera gameplayCamera;
        private Sprite[] moveFrames = System.Array.Empty<Sprite>();
        private Sprite[] attackFrames = System.Array.Empty<Sprite>();
        private State state;

        private int currentLane;
        private int targetLane;
        private int health;
        private int maximumHealth;
        private int entrySide;
        private int attackStep;
        private float direction;
        private float moveSpeed;
        private float attackInterval;
        private float telegraphDuration;
        private float attackDuration;
        private float recoveryDuration;
        private float nextAttackAt;
        private float nextLaneChangeAt;
        private float nextContactDamageAt;
        private float stateClock;
        private float frameClock;
        private float laneChangeClock;
        private float laneChangeDuration;
        private float floatPhase;
        private float attackDirection;
        private float retreatDirection;
        private float horizontalVelocity;
        private float smoothedLaneY;
        private float laneYVelocity;
        private bool changingLane;
        private bool laneYReady;
        private bool defeatAnimating;
        private bool initialised;
        private bool removalNotified;
        private Color baseColour;

        public DaySixCreatureKind Kind => kind;
        public bool CanBeHit => initialised && state != State.Retreat && health > 0 &&
                                isActiveAndEnabled && spriteRenderer != null && spriteRenderer.enabled;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            hitCollider = GetComponent<BoxCollider2D>();
            body = GetComponent<Rigidbody2D>();
            renderItem = GetComponent<InterWaveRenderItem>();
            if (renderItem == null)
                renderItem = gameObject.AddComponent<InterWaveRenderItem>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            hitCollider.isTrigger = true;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void Initialise(
            DaySixCreatureKind creatureKind,
            int requestedLane,
            int requestedEntrySide,
            DaySixEncounter owner)
        {
            kind = creatureKind;
            encounter = owner;
            entrySide = requestedEntrySide < 0 ? -1 : 1;
            direction = entrySide < 0 ? 1f : -1f;
            gameplayCamera = Camera.main;
            ConfigureProfile();
            RefreshWaterLayers(transform.position.x);

            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            currentLane = Mathf.Clamp(requestedLane, 0, laneCount - 1);
            targetLane = currentLane;
            ApplyLaneSorting();
            FindTarget();

            float spawnX = ChooseEntryX();
            smoothedLaneY = GetLaneY(currentLane, spawnX);
            laneYVelocity = 0f;
            laneYReady = true;
            horizontalVelocity = direction * moveSpeed;
            transform.position = new Vector3(spawnX, smoothedLaneY, 0f);
            spriteRenderer.flipX = direction < 0f;
            floatPhase = Random.Range(0f, Mathf.PI * 2f);
            nextLaneChangeAt = Time.time + Random.Range(1.5f, 3.2f);
            nextAttackAt = Time.time + Random.Range(1.1f, 2.4f);
            state = State.Entering;
            baseColour = spriteRenderer.color;
            initialised = true;
        }

        private void ConfigureProfile()
        {
            string movePath;
            string attackPath = null;
            string attackAudioPath;
            int frameSize = 64;

            switch (kind)
            {
                case DaySixCreatureKind.Fishbowl:
                    movePath = "Day6/fishbowl_move";
                    attackPath = "Day6/fishbowl_attack";
                    attackAudioPath = "Audio/SFX/ocean_item_pickup";
                    maximumHealth = 2;
                    moveSpeed = 1.7f;
                    attackInterval = 3.8f;
                    telegraphDuration = 0.75f;
                    attackDuration = 1.05f;
                    recoveryDuration = 0.75f;
                    laneChangeDuration = 0.52f;
                    transform.localScale = Vector3.one * 0.78f;
                    break;

                case DaySixCreatureKind.MushroomSquid:
                    movePath = "Day6/mushroom_squid_move";
                    attackPath = "Day6/mushroom_squid_attack";
                    attackAudioPath = "Audio/SFX/alien_ship";
                    maximumHealth = 3;
                    moveSpeed = 1.45f;
                    attackInterval = 4.4f;
                    telegraphDuration = 1.0f;
                    attackDuration = 1.15f;
                    recoveryDuration = 0.8f;
                    laneChangeDuration = 0.68f;
                    transform.localScale = Vector3.one * 0.82f;
                    break;

                case DaySixCreatureKind.MustacheShark:
                    movePath = "Day6/mustache_shark_move";
                    attackPath = "Day6/mustache_shark_attack";
                    attackAudioPath = "Audio/SFX/shark_attack";
                    maximumHealth = 3;
                    moveSpeed = 1.95f;
                    attackInterval = 3.5f;
                    telegraphDuration = 0.7f;
                    attackDuration = 1.35f;
                    recoveryDuration = 0.85f;
                    laneChangeDuration = 0.7f;
                    transform.localScale = Vector3.one * 0.86f;
                    break;

                case DaySixCreatureKind.Resort:
                    movePath = "Day6/resort_move";
                    attackPath = "Day6/resort_attack";
                    attackAudioPath = "Audio/SFX/rubber_duck_quack";
                    maximumHealth = 4;
                    moveSpeed = 0.9f;
                    attackInterval = 5.0f;
                    telegraphDuration = 1.1f;
                    attackDuration = 1.45f;
                    recoveryDuration = 1.0f;
                    laneChangeDuration = 1.1f;
                    transform.localScale = Vector3.one * 0.9f;
                    break;

                case DaySixCreatureKind.Starfish:
                    movePath = "Day6/starfish_move";
                    attackAudioPath = "Audio/SFX/ching";
                    maximumHealth = 1;
                    moveSpeed = 2.6f;
                    attackInterval = 2.6f;
                    telegraphDuration = 0.5f;
                    attackDuration = 1.7f;
                    recoveryDuration = 0.55f;
                    laneChangeDuration = 0.24f;
                    frameSize = 32;
                    transform.localScale = Vector3.one * 0.92f;
                    break;

                case DaySixCreatureKind.Toaster:
                    movePath = "Day6/toaster_move";
                    attackPath = "Day6/toaster_attack";
                    attackAudioPath = "Audio/SFX/explosion_8bit";
                    maximumHealth = 2;
                    moveSpeed = 1.25f;
                    attackInterval = 3.7f;
                    telegraphDuration = 0.85f;
                    attackDuration = 1.15f;
                    recoveryDuration = 0.8f;
                    laneChangeDuration = 0.65f;
                    transform.localScale = Vector3.one * 0.82f;
                    break;

                default:
                    movePath = "Day6/toilet_move";
                    attackPath = "Day6/toilet_attack";
                    attackAudioPath = "Audio/SFX/water_slash";
                    maximumHealth = 3;
                    moveSpeed = 1.15f;
                    attackInterval = 4.2f;
                    telegraphDuration = 1.0f;
                    attackDuration = 1.45f;
                    recoveryDuration = 0.9f;
                    laneChangeDuration = 0.45f;
                    transform.localScale = Vector3.one * 0.84f;
                    break;
            }

            moveFrames = SliceSheet(movePath, frameSize, 32f);
            attackFrames = string.IsNullOrEmpty(attackPath)
                ? moveFrames
                : SliceSheet(attackPath, frameSize, 32f);
            health = maximumHealth;
            attackClip = Resources.Load<AudioClip>(attackAudioPath);
            hitClip = Resources.Load<AudioClip>("Audio/SFX/shark_hit");
            if (moveFrames.Length > 0)
                spriteRenderer.sprite = moveFrames[0];
            RefreshCollider();
        }

        private void Update()
        {
            if (!initialised)
                return;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
            if (target == null || target.IsDead || !target.IsPlayerControlled)
                FindTarget();

            Animate();
            switch (state)
            {
                case State.Entering: UpdateEntering(); break;
                case State.Patrol: UpdatePatrol(); break;
                case State.Telegraph: UpdateTelegraph(); break;
                case State.Attack: UpdateAttack(); break;
                case State.Recovery: UpdateRecovery(); break;
                case State.Hit: UpdateHit(); break;
                case State.Retreat:
                    if (!defeatAnimating)
                        UpdateRetreat();
                    return;
            }

            Vector3 position = transform.position;
            float targetY = UpdateLanePosition(position.x) + CurrentBob();
            if (!laneYReady)
            {
                smoothedLaneY = targetY;
                laneYVelocity = 0f;
                laneYReady = true;
            }
            else
            {
                // Wave sections can exchange ownership at their seam. Damping the
                // sampled lane height prevents a one-frame seam change from making
                // a hostile visibly shake while it remains attached to that lane.
                smoothedLaneY = Mathf.SmoothDamp(
                    smoothedLaneY,
                    targetY,
                    ref laneYVelocity,
                    0.075f,
                    Mathf.Infinity,
                    Time.deltaTime);
            }
            position.y = smoothedLaneY;
            transform.position = position;
        }

        private void UpdateEntering()
        {
            float centreX = ViewportWorldX(entrySide < 0 ? 0.28f : 0.72f);
            MoveHorizontalToward(centreX, moveSpeed * 1.7f);
            if (Mathf.Abs(transform.position.x - centreX) <= 0.16f)
            {
                state = State.Patrol;
                stateClock = 0f;
            }
        }

        private void UpdatePatrol()
        {
            stateClock += Time.deltaTime;
            float left = ViewportWorldX(0.12f);
            float right = ViewportWorldX(0.88f);
            if (transform.position.x <= left)
            {
                direction = 1f;
                horizontalVelocity = Mathf.Max(0f, horizontalVelocity);
                SetX(left);
            }
            else if (transform.position.x >= right)
            {
                direction = -1f;
                horizontalVelocity = Mathf.Min(0f, horizontalVelocity);
                SetX(right);
            }

            MoveWithAcceleration(direction * moveSpeed, 8f);

            if (Time.time >= nextLaneChangeAt && !changingLane)
            {
                RequestAdjacentLane(Random.value < 0.5f ? -1 : 1);
                nextLaneChangeAt = Time.time + Random.Range(2.0f, 4.2f);
            }

            if (Time.time >= nextAttackAt)
                BeginTelegraph();
        }

        private void BeginTelegraph()
        {
            state = State.Telegraph;
            stateClock = 0f;
            attackStep = 0;
            attackDirection = target != null && target.transform.position.x < transform.position.x ? -1f : 1f;
            if (kind == DaySixCreatureKind.MustacheShark)
                horizontalVelocity = direction * Mathf.Min(Mathf.Abs(horizontalVelocity), moveSpeed);
            int targetWaterLane = FindTargetLane();

            switch (kind)
            {
                case DaySixCreatureKind.MushroomSquid:
                case DaySixCreatureKind.MustacheShark:
                case DaySixCreatureKind.Toilet:
                    RequestLane(targetWaterLane);
                    break;
                case DaySixCreatureKind.Fishbowl:
                    RequestAdjacentLane(targetWaterLane >= currentLane ? 1 : -1);
                    break;
            }
        }

        private void UpdateTelegraph()
        {
            stateClock += Time.deltaTime;
            SetFacing(attackDirection);

            float pulse = 0.72f + Mathf.PingPong(stateClock * 3.5f, 0.28f);
            spriteRenderer.color = Color.Lerp(baseColour, TelegraphColour(), pulse * 0.5f);
            if (kind == DaySixCreatureKind.MushroomSquid)
            {
                Color colour = spriteRenderer.color;
                colour.a = Mathf.Lerp(1f, 0.28f, Mathf.PingPong(stateClock * 2.4f, 1f));
                spriteRenderer.color = colour;
            }
            else if (kind == DaySixCreatureKind.MustacheShark)
            {
                // Let the shark visibly settle before its feint instead of
                // stopping on a single frame.
                MoveWithAcceleration(0f, 7.5f);
            }

            if (stateClock >= telegraphDuration)
                BeginAttack();
        }

        private void BeginAttack()
        {
            state = State.Attack;
            stateClock = 0f;
            attackStep = 0;
            spriteRenderer.color = baseColour;
            if (attackClip != null && audioSource != null)
                audioSource.PlayOneShot(attackClip, kind == DaySixCreatureKind.MushroomSquid ? 0.45f : 0.72f);

            if (kind == DaySixCreatureKind.MushroomSquid)
            {
                direction = attackDirection;
                SpawnLaneProjectile(DaySixHazardKind.Spore, currentLane, 3.8f);
                SpawnLaneProjectile(DaySixHazardKind.Spore, currentLane + 1, 3.35f);
            }
            else if (kind == DaySixCreatureKind.Toilet)
            {
                direction = attackDirection;
            }
        }

        private void UpdateAttack()
        {
            stateClock += Time.deltaTime;
            switch (kind)
            {
                case DaySixCreatureKind.Fishbowl:
                    SetX(transform.position.x + attackDirection * 6.2f * Time.deltaTime);
                    if (attackStep == 0 && stateClock >= 0.46f)
                    {
                        attackStep++;
                        RequestAdjacentLane(Random.value < 0.5f ? -1 : 1);
                    }
                    break;

                case DaySixCreatureKind.MushroomSquid:
                    SetX(transform.position.x + attackDirection * 4.7f * Time.deltaTime);
                    if (attackStep == 0 && stateClock >= 0.52f)
                    {
                        attackStep++;
                        SpawnLaneProjectile(DaySixHazardKind.Spore, currentLane - 1, 4.0f);
                    }
                    break;

                case DaySixCreatureKind.MustacheShark:
                    if (stateClock < 0.42f)
                    {
                        MoveWithAcceleration(-attackDirection * 2.2f, 9.5f);
                    }
                    else if (stateClock < 0.58f)
                    {
                        // A short eased turn keeps the feint readable without an
                        // instantaneous velocity reversal.
                        MoveWithAcceleration(0f, 16f);
                    }
                    else
                    {
                        MoveWithAcceleration(attackDirection * 7.4f, 13.5f);
                    }
                    break;

                case DaySixCreatureKind.Resort:
                    FireSteppedProjectiles(DaySixHazardKind.ResortWake, 3, 0.34f, 4.8f, true);
                    break;

                case DaySixCreatureKind.Starfish:
                    transform.Rotate(0f, 0f, 760f * Time.deltaTime);
                    SetX(transform.position.x + attackDirection * 5.0f * Time.deltaTime);
                    if (stateClock >= (attackStep + 1) * 0.28f)
                    {
                        attackStep++;
                        RequestAdjacentLane(attackStep % 2 == 0 ? -1 : 1);
                    }
                    break;

                case DaySixCreatureKind.Toaster:
                    FireSteppedProjectiles(DaySixHazardKind.Toast, 3, 0.28f, 5.8f, false);
                    break;

                case DaySixCreatureKind.Toilet:
                    FireSteppedProjectiles(DaySixHazardKind.Flush, 4, 0.24f, 5.0f, true);
                    if (stateClock > 0.82f)
                        SetX(transform.position.x + attackDirection * 3.7f * Time.deltaTime);
                    break;
            }

            SetFacing(attackDirection);
            if (stateClock >= attackDuration)
            {
                state = State.Recovery;
                stateClock = 0f;
                transform.rotation = Quaternion.identity;
            }
        }

        private void FireSteppedProjectiles(
            DaySixHazardKind hazard,
            int count,
            float stepDelay,
            float speed,
            bool alternateLanes)
        {
            if (attackStep >= count || stateClock < 0.08f + attackStep * stepDelay)
                return;

            int lane = currentLane;
            if (alternateLanes && attackStep > 0)
                lane += attackStep % 2 == 0 ? -1 : 1;
            SpawnLaneProjectile(hazard, lane, speed);
            attackStep++;
        }

        private void UpdateRecovery()
        {
            stateClock += Time.deltaTime;
            spriteRenderer.color = Color.Lerp(baseColour, Color.white, Mathf.Max(0f, 0.25f - stateClock));
            if (kind == DaySixCreatureKind.MustacheShark)
                MoveWithAcceleration(direction * moveSpeed * 0.35f, 7f);
            else
                SetX(transform.position.x + direction * moveSpeed * 0.35f * Time.deltaTime);
            if (stateClock >= recoveryDuration)
            {
                state = State.Patrol;
                stateClock = 0f;
                spriteRenderer.color = baseColour;
                nextAttackAt = Time.time + attackInterval * Random.Range(0.82f, 1.18f);
            }
        }

        private void UpdateHit()
        {
            stateClock += Time.deltaTime;
            spriteRenderer.color = Mathf.FloorToInt(stateClock * 20f) % 2 == 0
                ? Color.white
                : new Color(1f, 0.22f, 0.2f, baseColour.a);
            if (stateClock >= 0.28f)
            {
                state = State.Patrol;
                stateClock = 0f;
                spriteRenderer.color = baseColour;
                nextAttackAt = Time.time + Mathf.Max(0.75f, attackInterval * 0.55f);
            }
        }

        private void UpdateRetreat()
        {
            transform.position += Vector3.right * (retreatDirection * moveSpeed * 2.2f * Time.deltaTime);
            if (gameplayCamera == null ||
                transform.position.x < ViewportWorldX(-0.2f) ||
                transform.position.x > ViewportWorldX(1.2f))
            {
                Destroy(gameObject);
            }
        }

        private void SpawnLaneProjectile(DaySixHazardKind hazard, int requestedLane, float speed)
        {
            RefreshWaterLayers(transform.position.x);
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            int lane = Mathf.Clamp(requestedLane, 0, laneCount - 1);
            float travelDirection = target != null && target.transform.position.x < transform.position.x ? -1f : 1f;
            Vector3 position = transform.position;
            position.y = GetLaneY(lane, position.x);
            PixelWaterGPU water = waterLayers.Count > 0
                ? waterLayers[Mathf.Clamp(lane, 0, waterLayers.Count - 1)]
                : null;
            DaySixHazardProjectile.Spawn(hazard, position, travelDirection, speed, lane, water);
        }

        public bool TakeThrownItemHit(int damage, Vector2 impactPosition)
        {
            if (!CanBeHit)
                return false;

            health = Mathf.Max(0, health - Mathf.Max(1, damage));
            if (hitClip != null && audioSource != null)
                audioSource.PlayOneShot(hitClip, 0.75f);
            if (health <= 0)
            {
                NotifyRemoved(true);
                StartCoroutine(DefeatRoutine(impactPosition));
                return true;
            }

            state = State.Hit;
            stateClock = 0f;
            Vector2 away = ((Vector2)transform.position - impactPosition).normalized;
            transform.position += (Vector3)(away * 0.16f);
            return true;
        }

        private IEnumerator DefeatRoutine(Vector2 impactPosition)
        {
            state = State.Retreat;
            defeatAnimating = true;
            hitCollider.enabled = false;
            float directionAway = transform.position.x >= impactPosition.x ? 1f : -1f;
            float elapsed = 0f;
            while (elapsed < 0.38f)
            {
                elapsed += Time.deltaTime;
                transform.position += new Vector3(directionAway * 2.8f, 1.2f, 0f) * Time.deltaTime;
                transform.Rotate(0f, 0f, directionAway * 520f * Time.deltaTime);
                Color colour = Color.white;
                colour.a = 1f - Mathf.Clamp01(elapsed / 0.38f);
                spriteRenderer.color = colour;
                yield return null;
            }
            Destroy(gameObject);
        }

        public void BeginRetreat()
        {
            if (!initialised || state == State.Retreat)
                return;
            NotifyRemoved(false);
            state = State.Retreat;
            defeatAnimating = false;
            hitCollider.enabled = false;
            float centre = gameplayCamera != null ? gameplayCamera.transform.position.x : transform.position.x;
            retreatDirection = transform.position.x < centre ? -1f : 1f;
            spriteRenderer.color = baseColour;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (state == State.Retreat || Time.time < nextContactDamageAt || other == null)
                return;

            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || !surfer.IsPlayerControlled)
                return;

            bool dangerous = state == State.Attack || kind == DaySixCreatureKind.Starfish;
            if (dangerous && surfer.TakeSharkHit(transform.position))
                nextContactDamageAt = Time.time + 0.8f;
        }

        private void RequestAdjacentLane(int step)
        {
            RefreshWaterLayers(transform.position.x);
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            if (laneCount <= 1)
                return;

            int requested = currentLane + (step < 0 ? -1 : 1);
            if (requested < 0 || requested >= laneCount)
                requested = currentLane - (step < 0 ? -1 : 1);
            RequestLane(requested);
        }

        private void RequestLane(int lane)
        {
            RefreshWaterLayers(transform.position.x);
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            lane = Mathf.Clamp(lane, 0, laneCount - 1);
            if (lane == currentLane)
                return;
            targetLane = lane;
            laneChangeClock = 0f;
            changingLane = true;
        }

        private float UpdateLanePosition(float worldX)
        {
            RefreshWaterLayers(worldX);
            if (!changingLane)
                return GetLaneY(currentLane, worldX);

            laneChangeClock += Time.deltaTime;
            float t = Mathf.Clamp01(laneChangeClock / Mathf.Max(0.1f, laneChangeDuration));
            float smooth = t * t * (3f - 2f * t);
            float y = Mathf.Lerp(GetLaneY(currentLane, worldX), GetLaneY(targetLane, worldX), smooth);
            if (t >= 1f)
            {
                currentLane = targetLane;
                changingLane = false;
                ApplyLaneSorting();
            }
            return y;
        }

        private int FindTargetLane()
        {
            if (target == null)
                return currentLane;
            RefreshWaterLayers(target.transform.position.x);
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            int bestLane = currentLane;
            float bestDistance = float.PositiveInfinity;
            for (int lane = 0; lane < laneCount; lane++)
            {
                float distance = Mathf.Abs(target.transform.position.y - GetLaneY(lane, target.transform.position.x));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLane = lane;
                }
            }
            return bestLane;
        }

        private void RefreshWaterLayers(float worldX)
        {
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(worldX));
            waterLayers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            waterLayers.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            currentLane = Mathf.Clamp(currentLane, 0, laneCount - 1);
            targetLane = Mathf.Clamp(targetLane, 0, laneCount - 1);
        }

        private float GetLaneY(int lane, float worldX)
        {
            if (waterLayers.Count >= 2)
            {
                lane = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
                return Mathf.Lerp(
                    waterLayers[lane].GetGameplaySurfaceHeight(worldX),
                    waterLayers[lane + 1].GetGameplaySurfaceHeight(worldX),
                    0.5f);
            }
            return target != null ? target.transform.position.y : transform.position.y;
        }

        private void ApplyLaneSorting()
        {
            PixelWaterGPU water = waterLayers.Count > 0
                ? waterLayers[Mathf.Clamp(currentLane, 0, waterLayers.Count - 1)]
                : null;
            renderItem.SetWaterAndLane(water, currentLane);
        }

        private float ChooseEntryX()
        {
            if (waterLayers.Count > 0)
            {
                float chosen = CameraSafeSpawnUtility.ChooseOffscreenEntryX(
                    waterLayers,
                    spriteRenderer,
                    out bool fromLeft,
                    0.8f);
                entrySide = fromLeft ? -1 : 1;
                direction = fromLeft ? 1f : -1f;
                return chosen;
            }
            return ViewportWorldX(entrySide < 0 ? -0.12f : 1.12f);
        }

        private float ViewportWorldX(float viewportX)
        {
            if (gameplayCamera == null)
                return (target != null ? target.transform.position.x : 0f) + (viewportX - 0.5f) * 16f;
            float depth = Mathf.Abs(gameplayCamera.transform.position.z);
            return gameplayCamera.ViewportToWorldPoint(new Vector3(viewportX, 0.5f, depth)).x;
        }

        private void MoveHorizontalToward(float destination, float speed)
        {
            float x = Mathf.SmoothDamp(
                transform.position.x,
                destination,
                ref horizontalVelocity,
                0.16f,
                speed,
                Time.deltaTime);
            SetX(x);
            SetFacing(horizontalVelocity);
        }

        private void MoveWithAcceleration(float targetSpeed, float acceleration)
        {
            horizontalVelocity = Mathf.MoveTowards(
                horizontalVelocity,
                targetSpeed,
                Mathf.Max(0.1f, acceleration) * Time.deltaTime);
            SetX(transform.position.x + horizontalVelocity * Time.deltaTime);
            SetFacing(horizontalVelocity);
        }

        private void SetX(float x)
        {
            Vector3 position = transform.position;
            position.x = x;
            transform.position = position;
        }

        private void SetFacing(float horizontalDirection)
        {
            if (Mathf.Abs(horizontalDirection) > 0.02f)
                spriteRenderer.flipX = horizontalDirection < 0f;
        }

        private float CurrentBob()
        {
            float amount = kind == DaySixCreatureKind.Resort ? 0.035f :
                kind == DaySixCreatureKind.Starfish ? 0.09f : 0.065f;
            return Mathf.Sin(Time.time * 2.2f + floatPhase) * amount;
        }

        private Color TelegraphColour()
        {
            return kind switch
            {
                DaySixCreatureKind.MushroomSquid => new Color(0.78f, 0.34f, 1f, 1f),
                DaySixCreatureKind.Toaster => new Color(1f, 0.78f, 0.18f, 1f),
                DaySixCreatureKind.Toilet => new Color(0.28f, 0.88f, 1f, 1f),
                DaySixCreatureKind.Resort => new Color(0.32f, 1f, 0.76f, 1f),
                _ => new Color(1f, 0.35f, 0.3f, 1f)
            };
        }

        private void Animate()
        {
            Sprite[] activeFrames = state == State.Telegraph || state == State.Attack
                ? attackFrames
                : moveFrames;
            if (activeFrames == null || activeFrames.Length == 0)
                return;

            float fps = state == State.Attack ? 14f : state == State.Telegraph ? 11f : 9f;
            frameClock += Time.deltaTime * fps;
            int index = Mathf.FloorToInt(frameClock) % activeFrames.Length;
            if (spriteRenderer.sprite != activeFrames[index])
            {
                spriteRenderer.sprite = activeFrames[index];
                RefreshCollider();
            }
        }

        private void RefreshCollider()
        {
            if (spriteRenderer.sprite == null)
                return;
            hitCollider.size = spriteRenderer.sprite.bounds.size *
                (kind == DaySixCreatureKind.Starfish ? 0.60f : 0.64f);
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

        private void NotifyRemoved(bool defeated)
        {
            if (removalNotified)
                return;
            removalNotified = true;
            encounter?.NotifyCreatureRemoved(this, defeated);
        }

        private void OnDestroy()
        {
            NotifyRemoved(false);
        }

        private static Sprite[] SliceSheet(string path, int frameSize, float pixelsPerUnit)
        {
            Texture2D sheet = Resources.Load<Texture2D>(path);
            if (sheet == null)
            {
                Debug.LogWarning($"Day 6 sprite sheet was not found: {path}");
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
    }
}
