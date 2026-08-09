using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class DayTwoHelicopterController : MonoBehaviour
    {
        private enum State { Arrival, Patrol, Aim, Fire, Crashing, Disabled }

        [Header("Animation")]
        [SerializeField, Min(1f)] private float moveFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float attackFramesPerSecond = 16f;
        [SerializeField, Min(0.05f)] private float helicopterScale = 0.78f;
        [SerializeField] private int sortingOrder = 12010;

        [Header("Flight")]
        [SerializeField] private Vector2 skyViewportY = new(0.72f, 0.91f);
        [SerializeField] private Vector2 patrolSpeedRange = new(1.8f, 2.8f);
        [SerializeField] private Vector2 patrolDecisionRange = new(0.8f, 1.8f);
        [SerializeField] private float movementSmoothing = 6.5f;
        [SerializeField] private float bankAngle = 8f;
        [SerializeField, Range(0.1f, 0.45f)] private float patrolHorizontalRadius = 0.24f;
        [SerializeField, Range(0.15f, 0.5f)] private float maximumTargetViewportSeparation = 0.32f;

        [Header("Spatial Audio")]
        [SerializeField] private AudioClip movementClip;
        [SerializeField] private AudioClip missileLaunchClip;
        [SerializeField, Range(0f, 1f)] private float movementVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] private float missileLaunchVolume = 0.95f;
        [SerializeField, Min(0.1f)] private float audioMinDistance = 5f;
        [SerializeField, Min(1f)] private float audioMaxDistance = 28f;

        [Header("Missile Attack")]
        [SerializeField] private Vector2 firstAttackDelayRange = new(5f, 9f);
        [SerializeField] private Vector2 attackCooldownRange = new(8f, 13f);
        [SerializeField] private float aimDuration = 1.15f;
        [SerializeField] private float fireAnimationDuration = 0.7f;
        [SerializeField] private float missileSpawnOffsetX = 0.34f;
        [SerializeField] private float missileSpawnOffsetY = -0.16f;

        [Header("Crash and Respawn")]
        [SerializeField, Min(0.5f)] private float crashDuration = 3.4f;
        [SerializeField, Range(0.1f, 0.9f)] private float crashFadeBeginsAt = 0.52f;
        [SerializeField, Min(0f)] private float crashSinkDepth = 0.42f;
        [SerializeField] private Vector2 crashHorizontalDriftRange = new(-1.8f, 1.8f);
        [SerializeField] private Vector2 crashSpinSpeedRange = new(115f, 220f);
        [SerializeField] private Vector2 crashExplosionIntervalRange = new(0.16f, 0.32f);
        [SerializeField, Range(0.1f, 1.5f)] private float crashExplosionRadius = 0.62f;
        [SerializeField] private Vector2 returnDelayRange = new(10f, 16f);

        private SpriteRenderer spriteRenderer;
        private BoxCollider2D hitCollider;
        private Rigidbody2D body;
        private Camera worldCamera;
        private TinyWaveSurfer target;
        private Sprite[] moveFrames;
        private Sprite[] attackFrames;
        private State state;
        private Vector3 desiredPosition;
        private Vector3 velocity;
        private float frameClock;
        private int frameIndex;
        private float decisionTimer;
        private float nextAttackTime;
        private float stateClock;
        private float moveSpeed;
        private bool missileFired;
        private float returnAt;
        private Color baseTint = Color.white;
        private AudioSource movementAudioSource;
        private AudioSource oneShotAudioSource;
        private Vector3 crashStartPosition;
        private float crashTargetX;
        private float crashSpinSpeed;
        private float nextCrashExplosionAt;
        private int originalSortingLayerId;
        private InterWaveRenderItem crashRenderItem;
        private PixelWaterGPU crashForegroundWater;
        private PixelWaterGPU crashBackgroundWater;
        private int crashInterWaveLane = -1;

        public bool CanBeHit => isActiveAndEnabled && spriteRenderer != null &&
            spriteRenderer.enabled && state != State.Crashing && state != State.Disabled;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            hitCollider = GetComponent<BoxCollider2D>();
            body = GetComponent<Rigidbody2D>();
            worldCamera = Camera.main;

            moveFrames = LoadSheet("Helicopter/helicopter_move", 128, 32f, "helicopter_move");
            attackFrames = LoadSheet("Helicopter/helicopter_attack", 128, 32f, "helicopter_attack");
            if (moveFrames.Length > 0) spriteRenderer.sprite = moveFrames[0];
            spriteRenderer.sortingOrder = sortingOrder;
            originalSortingLayerId = spriteRenderer.sortingLayerID;
            baseTint = spriteRenderer.color;

            transform.localScale = Vector3.one * helicopterScale;
            hitCollider.isTrigger = true;
            if (spriteRenderer.sprite != null)
            {
                hitCollider.size = spriteRenderer.sprite.bounds.size * 0.72f;
                hitCollider.offset = spriteRenderer.sprite.bounds.center;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            movementClip ??= Resources.Load<AudioClip>("Audio/SFX/helicopter");
            missileLaunchClip ??= Resources.Load<AudioClip>("Audio/SFX/missile_launch");
            movementAudioSource = CreateSpatialAudioSource(true, movementVolume);
            movementAudioSource.clip = movementClip;
            if (movementAudioSource.clip != null)
                movementAudioSource.Play();
            oneShotAudioSource = CreateSpatialAudioSource(false, 1f);
        }

        private AudioSource CreateSpatialAudioSource(bool loop, float volume)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = audioMinDistance;
            source.maxDistance = Mathf.Max(audioMinDistance + 0.1f, audioMaxDistance);
            source.dopplerLevel = 0.15f;
            return source;
        }

        private IEnumerator Start()
        {
            yield return null;
            FindTarget();
            PlaceForArrival();
            nextAttackTime = Time.time + Random.Range(firstAttackDelayRange.x, firstAttackDelayRange.y);
            state = State.Arrival;
        }

        private void Update()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (target == null || target.IsDead || !target.IsPlayerControlled) FindTarget();
            Animate();
            if (worldCamera == null) return;

            switch (state)
            {
                case State.Arrival: UpdateArrival(); break;
                case State.Patrol: UpdatePatrol(); break;
                case State.Aim: UpdateAim(); break;
                case State.Fire: UpdateFire(); break;
                case State.Crashing: UpdateCrash(); return;
                case State.Disabled: UpdateDisabled(); return;
            }

            ApplyMovement();
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
            bool left = Random.value < 0.5f;
            float y = ViewportWorldY(Random.Range(skyViewportY.x, skyViewportY.y));
            transform.position = new Vector3(ViewportWorldX(left ? -0.18f : 1.18f), y, 0f);
            desiredPosition = new Vector3(ViewportWorldX(left ? 0.22f : 0.78f), y, 0f);
            moveSpeed = Random.Range(patrolSpeedRange.x, patrolSpeedRange.y);
            SetFacing(desiredPosition.x - transform.position.x);
        }

        private void UpdateArrival()
        {
            if (Vector2.Distance(transform.position, desiredPosition) < 0.25f)
            {
                state = State.Patrol;
                PickPatrolTarget();
            }
        }

        private void UpdatePatrol()
        {
            decisionTimer -= Time.deltaTime;
            if (target != null && Time.time >= nextAttackTime)
            {
                state = State.Aim;
                stateClock = 0f;
                frameClock = 0f;
                frameIndex = 0;
                return;
            }

            bool targetTooFar = false;
            if (target != null && worldCamera != null)
            {
                Vector3 helicopterViewport = worldCamera.WorldToViewportPoint(transform.position);
                Vector3 targetViewport = worldCamera.WorldToViewportPoint(target.transform.position);
                targetTooFar = Mathf.Abs(helicopterViewport.x - targetViewport.x) > maximumTargetViewportSeparation;
            }

            if (decisionTimer <= 0f || targetTooFar || Vector2.Distance(transform.position, desiredPosition) < 0.25f)
                PickPatrolTarget();
        }

        private void PickPatrolTarget()
        {
            float targetViewportX = 0.5f;
            if (target != null && worldCamera != null)
                targetViewportX = worldCamera.WorldToViewportPoint(target.transform.position).x;

            float patrolViewportX = Mathf.Clamp(
                targetViewportX + Random.Range(-patrolHorizontalRadius, patrolHorizontalRadius),
                0.10f,
                0.90f);

            desiredPosition = new Vector3(
                ViewportWorldX(patrolViewportX),
                ViewportWorldY(Random.Range(skyViewportY.x, skyViewportY.y)),
                0f);
            moveSpeed = Random.Range(patrolSpeedRange.x, patrolSpeedRange.y);
            decisionTimer = Random.Range(patrolDecisionRange.x, patrolDecisionRange.y);
            SetFacing(desiredPosition.x - transform.position.x);
        }

        private void UpdateAim()
        {
            if (target == null) { state = State.Patrol; PickPatrolTarget(); return; }
            stateClock += Time.deltaTime;
            float hoverX = Mathf.Clamp(target.transform.position.x, ViewportWorldX(0.15f), ViewportWorldX(0.85f));
            desiredPosition = new Vector3(hoverX, ViewportWorldY(0.82f), 0f);
            SetFacing(target.transform.position.x - transform.position.x);
            if (stateClock >= aimDuration)
            {
                state = State.Fire;
                stateClock = 0f;
                missileFired = false;
                frameClock = 0f;
                frameIndex = 0;
            }
        }

        private void UpdateFire()
        {
            stateClock += Time.deltaTime;
            if (!missileFired && stateClock >= fireAnimationDuration * 0.45f)
            {
                missileFired = true;
                FireMissile();
            }

            if (stateClock >= fireAnimationDuration)
            {
                nextAttackTime = Time.time + Random.Range(attackCooldownRange.x, attackCooldownRange.y);
                state = State.Patrol;
                PickPatrolTarget();
            }
        }

        private void FireMissile()
        {
            if (target == null) return;
            if (missileLaunchClip != null && oneShotAudioSource != null)
                oneShotAudioSource.PlayOneShot(missileLaunchClip, missileLaunchVolume);
            GameObject missile = new("Day 2 Helicopter Tracking Missile");
            missile.transform.position = transform.position + new Vector3(
                spriteRenderer.flipX ? -missileSpawnOffsetX : missileSpawnOffsetX,
                missileSpawnOffsetY,
                0f);
            missile.AddComponent<SpriteRenderer>();
            missile.AddComponent<CircleCollider2D>();
            missile.AddComponent<Rigidbody2D>();
            DayTwoHelicopterMissile controller = missile.AddComponent<DayTwoHelicopterMissile>();
            controller.Launch(target, this);
        }

        public void TakeThrownItemHit(Vector2 hitPosition)
        {
            if (!CanBeHit) return;

            foreach (DayTwoHelicopterMissile missile in FindObjectsByType<DayTwoHelicopterMissile>(FindObjectsSortMode.None))
                if (missile != null && missile.Owner == this) missile.Intercept(hitPosition);

            BeginCrash();
        }

        private void BeginCrash()
        {
            state = State.Crashing;
            stateClock = 0f;
            crashStartPosition = transform.position;
            crashTargetX = Mathf.Clamp(
                transform.position.x + Random.Range(
                    crashHorizontalDriftRange.x,
                    crashHorizontalDriftRange.y),
                ViewportWorldX(0.08f),
                ViewportWorldX(0.92f));
            crashSpinSpeed = Random.Range(crashSpinSpeedRange.x, crashSpinSpeedRange.y) *
                (Random.value < 0.5f ? -1f : 1f);
            velocity = Vector3.zero;
            hitCollider.enabled = false;

            AssignRandomInterWaveCrashLane();
            SpawnCrashExplosion();
            nextCrashExplosionAt = Time.time + Random.Range(
                Mathf.Min(crashExplosionIntervalRange.x, crashExplosionIntervalRange.y),
                Mathf.Max(crashExplosionIntervalRange.x, crashExplosionIntervalRange.y));
        }

        private void AssignRandomInterWaveCrashLane()
        {
            crashForegroundWater = null;
            crashBackgroundWater = null;
            crashInterWaveLane = -1;

            List<PixelWaterGPU> waterLayers = new(
                EndlessWaveSections.LayersNearest(transform.position.x));
            waterLayers.RemoveAll(water => water == null || !water.isActiveAndEnabled);
            waterLayers.Sort((a, b) =>
                a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));

            if (waterLayers.Count < 2)
                return;

            crashInterWaveLane = Random.Range(0, waterLayers.Count - 1);
            crashForegroundWater = waterLayers[crashInterWaveLane];
            crashBackgroundWater = waterLayers[crashInterWaveLane + 1];

            Renderer waterRenderer = crashForegroundWater.GetComponent<Renderer>();
            if (waterRenderer == null)
                waterRenderer = crashForegroundWater.GetComponentInChildren<Renderer>();
            if (waterRenderer != null)
                spriteRenderer.sortingLayerID = waterRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = 0;

            if (crashRenderItem == null)
                crashRenderItem = gameObject.AddComponent<InterWaveRenderItem>();
            crashRenderItem.enabled = true;
            crashRenderItem.SetWaterAndLane(
                crashForegroundWater,
                crashInterWaveLane);
        }

        private void UpdateCrash()
        {
            stateClock += Time.deltaTime;
            float duration = Mathf.Max(0.5f, crashDuration);
            float progress = Mathf.Clamp01(stateClock / duration);
            float fallProgress = progress * progress;

            float targetY = ViewportWorldY(0.02f);
            if (crashForegroundWater != null && crashBackgroundWater != null)
            {
                float frontSurface = crashForegroundWater.GetGameplaySurfaceHeight(crashTargetX);
                float backSurface = crashBackgroundWater.GetGameplaySurfaceHeight(crashTargetX);
                targetY = Mathf.Lerp(frontSurface, backSurface, 0.5f) - crashSinkDepth;
            }

            transform.position = new Vector3(
                Mathf.Lerp(crashStartPosition.x, crashTargetX, progress),
                Mathf.Lerp(crashStartPosition.y, targetY, fallProgress),
                crashStartPosition.z);
            transform.rotation = Quaternion.Euler(0f, 0f, crashSpinSpeed * stateClock);

            float fade = 1f - Mathf.InverseLerp(
                Mathf.Clamp01(crashFadeBeginsAt),
                1f,
                progress);
            bool redFlash = Mathf.FloorToInt(stateClock * 12f) % 2 == 0;
            Color tint = redFlash
                ? new Color(1f, 0.06f, 0.04f, baseTint.a)
                : baseTint;
            tint.a = baseTint.a * fade;
            spriteRenderer.color = tint;

            if (movementAudioSource != null)
                movementAudioSource.volume = movementVolume * fade;

            if (Time.time >= nextCrashExplosionAt)
            {
                SpawnCrashExplosion();
                nextCrashExplosionAt = Time.time + Random.Range(
                    Mathf.Min(crashExplosionIntervalRange.x, crashExplosionIntervalRange.y),
                    Mathf.Max(crashExplosionIntervalRange.x, crashExplosionIntervalRange.y));
            }

            if (progress >= 1f)
                FinishCrash();
        }

        private void SpawnCrashExplosion()
        {
            Vector2 offset = Random.insideUnitCircle * crashExplosionRadius;
            ExplosionBasicEffect.SpawnInterWave(
                transform.position + (Vector3)offset,
                spriteRenderer,
                crashForegroundWater,
                crashInterWaveLane);
        }

        private void FinishCrash()
        {
            SpawnCrashExplosion();
            spriteRenderer.enabled = false;
            spriteRenderer.color = baseTint;
            if (movementAudioSource != null)
                movementAudioSource.Stop();
            if (crashRenderItem != null)
                crashRenderItem.enabled = false;

            state = State.Disabled;
            returnAt = Time.time + Random.Range(
                Mathf.Min(returnDelayRange.x, returnDelayRange.y),
                Mathf.Max(returnDelayRange.x, returnDelayRange.y));
        }

        private void UpdateDisabled()
        {
            if (Time.time < returnAt)
                return;

            spriteRenderer.sortingLayerID = originalSortingLayerId;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = baseTint;
            spriteRenderer.enabled = true;
            hitCollider.enabled = true;
            transform.rotation = Quaternion.identity;
            velocity = Vector3.zero;
            crashForegroundWater = null;
            crashBackgroundWater = null;
            crashInterWaveLane = -1;

            if (movementAudioSource != null)
            {
                movementAudioSource.volume = movementVolume;
                if (movementAudioSource.clip != null && !movementAudioSource.isPlaying)
                    movementAudioSource.Play();
            }

            PlaceForArrival();
            nextAttackTime = Time.time + Random.Range(
                firstAttackDelayRange.x,
                firstAttackDelayRange.y);
            state = State.Arrival;
        }

        private void Animate()
        {
            Sprite[] active = state == State.Aim || state == State.Fire ? attackFrames : moveFrames;
            if (active == null || active.Length == 0) return;
            float fps = state == State.Aim || state == State.Fire ? attackFramesPerSecond : moveFramesPerSecond;
            frameClock += Time.deltaTime * fps;
            int next = Mathf.FloorToInt(frameClock) % active.Length;
            if (next != frameIndex || spriteRenderer.sprite == null)
            {
                frameIndex = next;
                spriteRenderer.sprite = active[frameIndex];
            }
        }

        private void ApplyMovement()
        {
            Vector3 delta = desiredPosition - transform.position;
            Vector3 desiredVelocity = delta.sqrMagnitude > 0.001f ? delta.normalized * moveSpeed : Vector3.zero;
            velocity = Vector3.Lerp(velocity, desiredVelocity, 1f - Mathf.Exp(-movementSmoothing * Time.deltaTime));
            transform.position += velocity * Time.deltaTime;
            float bank = Mathf.Clamp(-velocity.x * bankAngle * 0.22f, -bankAngle, bankAngle);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, 0f, bank), 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        private void SetFacing(float deltaX)
        {
            if (Mathf.Abs(deltaX) > 0.05f) spriteRenderer.flipX = deltaX < 0f;
        }

        private float ViewportWorldX(float x) => worldCamera.ViewportToWorldPoint(new Vector3(x, 0.5f, Mathf.Abs(worldCamera.transform.position.z))).x;
        private float ViewportWorldY(float y) => worldCamera.ViewportToWorldPoint(new Vector3(0.5f, y, Mathf.Abs(worldCamera.transform.position.z))).y;

        private static Sprite[] LoadSheet(string resourcePath, int frameSize, float pixelsPerUnit, string prefix)
        {
            Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
            if (sheet == null) return System.Array.Empty<Sprite>();
            int count = Mathf.Max(1, sheet.width / frameSize);
            Sprite[] result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Sprite.Create(sheet, new Rect(i * frameSize, 0, frameSize, sheet.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                result[i].name = $"{prefix}_{i:00}";
            }
            return result;
        }
    }
}
