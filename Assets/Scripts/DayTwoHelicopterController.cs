using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class DayTwoHelicopterController : MonoBehaviour
    {
        private enum State { Arrival, Patrol, Aim, Fire, Retreat, Disabled }

        [Header("Animation")]
        [SerializeField, Min(1f)] private float moveFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float attackFramesPerSecond = 16f;
        [SerializeField, Min(0.05f)] private float helicopterScale = 0.78f;
        [SerializeField] private int sortingOrder = 12010;

        [Header("Flight")]
        [SerializeField] private Vector2 skyViewportY = new(0.72f, 0.91f);
        [SerializeField] private Vector2 patrolSpeedRange = new(1.15f, 2.05f);
        [SerializeField] private Vector2 patrolDecisionRange = new(1.4f, 3.2f);
        [SerializeField] private float movementSmoothing = 4.2f;
        [SerializeField] private float bankAngle = 8f;

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

        [Header("Thrown Item Response")]
        [SerializeField] private float hitRetreatSpeed = 5.2f;
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
        private bool waitingOffscreen;
        private float returnAt;
        private Color baseTint = Color.white;
        private AudioSource movementAudioSource;
        private AudioSource oneShotAudioSource;

        public bool CanBeHit => isActiveAndEnabled && spriteRenderer != null && spriteRenderer.enabled && !waitingOffscreen;

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
                case State.Retreat: UpdateRetreat(); break;
                case State.Disabled: break;
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

            if (decisionTimer <= 0f || Vector2.Distance(transform.position, desiredPosition) < 0.25f)
                PickPatrolTarget();
        }

        private void PickPatrolTarget()
        {
            desiredPosition = new Vector3(
                ViewportWorldX(Random.Range(0.08f, 0.92f)),
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

            StopAllCoroutines();
            StartCoroutine(HitFlash());
            state = State.Retreat;
            waitingOffscreen = false;
            returnAt = Time.time + Random.Range(returnDelayRange.x, returnDelayRange.y);
            Vector2 away = ((Vector2)transform.position - hitPosition).normalized;
            if (away.sqrMagnitude < 0.01f) away = Vector2.up;
            velocity = away * hitRetreatSpeed;
            desiredPosition = new Vector3(
                ViewportWorldX(transform.position.x < worldCamera.transform.position.x ? -0.25f : 1.25f),
                ViewportWorldY(1.08f), 0f);
            moveSpeed = hitRetreatSpeed;
        }

        private IEnumerator HitFlash()
        {
            for (int i = 0; i < 6; i++)
            {
                spriteRenderer.color = i % 2 == 0 ? new Color(1f, 0.08f, 0.08f, baseTint.a) : baseTint;
                yield return new WaitForSeconds(0.055f);
            }
            spriteRenderer.color = baseTint;
        }

        private void UpdateRetreat()
        {
            if (waitingOffscreen)
            {
                if (Time.time < returnAt) return;
                waitingOffscreen = false;
                spriteRenderer.enabled = true;
                hitCollider.enabled = true;
                PlaceForArrival();
                nextAttackTime = Time.time + Random.Range(firstAttackDelayRange.x, firstAttackDelayRange.y);
                state = State.Arrival;
                return;
            }

            if (!spriteRenderer.isVisible)
            {
                waitingOffscreen = true;
                spriteRenderer.enabled = false;
                hitCollider.enabled = false;
                velocity = Vector3.zero;
            }
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
