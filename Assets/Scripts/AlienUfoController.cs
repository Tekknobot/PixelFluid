using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class AlienUfoController : MonoBehaviour
    {
        private enum UfoState { Arrival, Roaming, Swooping, Hunting, Beaming, Retreat }

        [Header("Sprite")]
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;
        [SerializeField] private float shipScale = 1.0f;
        [SerializeField] private int sortingOrder = 12000;

        [Header("Sky Movement")]
        [SerializeField] private Vector2 skyHeightViewport = new Vector2(0.82f, 0.94f);
        [SerializeField] private Vector2 roamSpeedRange = new Vector2(1.6f, 3.2f);
        [SerializeField] private Vector2 decisionTimeRange = new Vector2(1.2f, 3.1f);
        [SerializeField] private float swoopDepth = 0.35f;
        [SerializeField] private float swoopDuration = 1.15f;
        [SerializeField] private float bankAngle = 13f;
        [SerializeField] private float movementSmoothing = 0.5f;

        [Header("Abduction")]
        [SerializeField] private Vector2 firstAttackDelayRange = new Vector2(5f, 10f);
        [SerializeField] private Vector2 attackCooldownRange = new Vector2(7f, 13f);
        [SerializeField] private float trackingSpeed = 4.8f;
        [SerializeField] private float beamHalfWidth = 0.9f;
        [SerializeField] private float beamBreakPadding = 0.3f;
        [SerializeField] private float abductionSeconds = 3f;
        [SerializeField] private float hoverAbovePlayer = 3.25f;
        [SerializeField, Range(0.5f, 0.9f)] private float lowestSkyViewportY = 0.5f;
        [SerializeField, Range(0.75f, 1.05f)] private float highestSkyViewportY = 0.98f;
        [SerializeField] private float visibleEdgePadding = 0.2f;

        [Header("Can Hit Response")]
        [SerializeField] private Vector2 hitRetreatCooldownRange = new Vector2(8f, 14f);
        [SerializeField] private float hitFlashDuration = 0.22f;
        [SerializeField] private float hitKickSpeed = 4.5f;

        [Header("Spatial Movement Audio")]
        [SerializeField] private AudioClip movementClip;
        [SerializeField, Range(0f, 1f)] private float movementVolume = 0.72f;
        [SerializeField, Min(0.1f)] private float audioMinDistance = 5f;
        [SerializeField, Min(1f)] private float audioMaxDistance = 28f;

        [Header("Beam Juice")]
        [SerializeField] private Color beamOuterColor = new Color(0.35f, 1f, 0.85f, 0.36f);
        [SerializeField] private Color beamInnerColor = new Color(0.85f, 1f, 0.95f, 0.82f);
        [SerializeField] private float beamOuterWidth = 0.12f;
        [SerializeField] private float beamInnerWidth = 0.035f;
        [SerializeField] private float beamPulseSpeed = 11f;
        [SerializeField] private float shakeAmount = 0.035f;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private Camera worldCamera;
        private TinyWaveSurfer target;
        private UfoState state;
        private Vector3 velocity;
        private Vector3 desiredPosition;
        private float frameClock;
        private int frameIndex;
        private float decisionTimer;
        private float nextAttackTime;
        private float beamTimer;
        private float swoopClock;
        private Vector3 swoopStart;
        private Vector3 swoopEnd;
        private float roamDirection;
        private LineRenderer beamLeft;
        private LineRenderer beamRight;
        private LineRenderer beamCore;
        private LineRenderer beamFloor;
        private Material lineMaterial;
        private Vector3 baseScale;
        private float currentMoveSpeed;
        private bool forcedRetreatFromHit;
        private bool waitingOffscreen;
        private float returnAfterTime;
        private float hitFlashUntil;
        private AudioSource movementAudioSource;

        public bool CanBeHit =>
            isActiveAndEnabled &&
            spriteRenderer != null &&
            spriteRenderer.enabled &&
            !waitingOffscreen;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
            frames = Resources.LoadAll<Sprite>("Alien/alien_ship_idle");
            if (frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[0];

            BoxCollider2D hitCollider = GetComponent<BoxCollider2D>();
            hitCollider.isTrigger = true;
            if (spriteRenderer.sprite != null)
            {
                hitCollider.size = spriteRenderer.sprite.bounds.size * 0.78f;
                hitCollider.offset = spriteRenderer.sprite.bounds.center;
            }

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            baseScale = Vector3.one * Mathf.Max(0.05f, shipScale);
            transform.localScale = baseScale;
            worldCamera = Camera.main;
            BuildBeam();
            SetBeamVisible(false);

            movementAudioSource = gameObject.AddComponent<AudioSource>();
            movementAudioSource.playOnAwake = false;
            movementAudioSource.loop = true;
            movementAudioSource.spatialBlend = 1f;
            movementAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            movementAudioSource.minDistance = audioMinDistance;
            movementAudioSource.maxDistance = Mathf.Max(audioMinDistance + 0.1f, audioMaxDistance);
            movementAudioSource.dopplerLevel = 0.15f;
            movementAudioSource.volume = movementVolume;
            movementAudioSource.clip = movementClip != null
                ? movementClip
                : Resources.Load<AudioClip>("Audio/SFX/alien_ship");
            if (movementAudioSource.clip != null)
                movementAudioSource.Play();
        }

        private IEnumerator Start()
        {
            yield return null;
            FindTarget();
            PlaceForArrival();
            nextAttackTime = Time.time + Random.Range(firstAttackDelayRange.x, firstAttackDelayRange.y);
            state = UfoState.Arrival;
        }

        private void Update()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (target == null || !target.IsPlayerControlled) FindTarget();
            AnimateSprite();
            if (worldCamera == null) return;

            switch (state)
            {
                case UfoState.Arrival: UpdateArrival(); break;
                case UfoState.Roaming: UpdateRoaming(); break;
                case UfoState.Swooping: UpdateSwoop(); break;
                case UfoState.Hunting: UpdateHunting(); break;
                case UfoState.Beaming: UpdateBeamAttack(); break;
                case UfoState.Retreat: UpdateRetreat(); break;
            }

            ApplyMovementAndJuice();
        }

        private void FindTarget()
        {
            TinyWaveSurfer[] surfers = GameplayTargetCache.Surfers;
            for (int i = 0; i < surfers.Length; i++)
            {
                if (surfers[i] != null && surfers[i].IsPlayerControlled)
                {
                    target = surfers[i];
                    return;
                }
            }
        }

        private void PlaceForArrival()
        {
            float y = ViewportWorldY(Random.Range(skyHeightViewport.x, skyHeightViewport.y));
            bool enterLeft = Random.value < 0.5f;
            float x = ViewportWorldX(enterLeft ? -0.15f : 1.15f);
            transform.position = new Vector3(x, y, 0f);
            roamDirection = enterLeft ? 1f : -1f;
            currentMoveSpeed = Random.Range(roamSpeedRange.x, roamSpeedRange.y);
            desiredPosition = new Vector3(ViewportWorldX(enterLeft ? 0.2f : 0.8f), y, 0f);
        }

        private void UpdateArrival()
        {
            if (Vector2.Distance(transform.position, desiredPosition) < 0.3f)
            {
                state = UfoState.Roaming;
                PickRoamTarget();
            }
        }

        private void UpdateRoaming()
        {
            decisionTimer -= Time.deltaTime;
            if (Time.time >= nextAttackTime && target != null)
            {
                state = UfoState.Hunting;
                return;
            }

            if (decisionTimer <= 0f || Vector2.Distance(transform.position, desiredPosition) < 0.25f)
            {
                if (Random.value < 0.38f)
                    BeginSwoop();
                else
                    PickRoamTarget();
            }
        }

        private void PickRoamTarget()
        {
            float x = ViewportWorldX(Random.Range(0.08f, 0.92f));
            float y = ViewportWorldY(Random.Range(skyHeightViewport.x, skyHeightViewport.y));
            desiredPosition = new Vector3(x, y, 0f);
            decisionTimer = Random.Range(decisionTimeRange.x, decisionTimeRange.y);
            currentMoveSpeed = Random.Range(roamSpeedRange.x, roamSpeedRange.y);
            roamDirection = Mathf.Sign(desiredPosition.x - transform.position.x);
        }

        private void BeginSwoop()
        {
            state = UfoState.Swooping;
            swoopClock = 0f;
            swoopStart = transform.position;
            float x = ViewportWorldX(Random.Range(0.12f, 0.88f));
            float y = ViewportWorldY(Random.Range(skyHeightViewport.x, skyHeightViewport.y));
            swoopEnd = new Vector3(x, y, 0f);
            currentMoveSpeed = Random.Range(roamSpeedRange.x, roamSpeedRange.y) * 1.25f;
            roamDirection = Mathf.Sign(x - transform.position.x);
        }

        private void UpdateSwoop()
        {
            swoopClock += Time.deltaTime;
            float t = Mathf.Clamp01(swoopClock / Mathf.Max(0.1f, swoopDuration));
            Vector3 p = Vector3.Lerp(swoopStart, swoopEnd, t);
            p.y -= Mathf.Sin(t * Mathf.PI) * swoopDepth;
            desiredPosition = p;
            if (t >= 1f)
            {
                state = UfoState.Roaming;
                PickRoamTarget();
            }
        }

        private void UpdateHunting()
        {
            if (target == null) { state = UfoState.Roaming; return; }
            Vector3 tp = target.transform.position;
            float attackY = Mathf.Max(tp.y + hoverAbovePlayer, ViewportWorldY(lowestSkyViewportY));
            desiredPosition = new Vector3(tp.x, attackY, 0f);
            float dx = Mathf.Abs(transform.position.x - tp.x);
            float dy = Mathf.Abs(transform.position.y - desiredPosition.y);
            if (dx < 0.22f && dy < 0.35f)
            {
                beamTimer = 0f;
                state = UfoState.Beaming;
                SetBeamVisible(true);
            }
        }

        private void UpdateBeamAttack()
        {
            if (target == null)
            {
                EndBeam(false);
                return;
            }

            Vector3 tp = target.transform.position;
            float attackY = Mathf.Max(tp.y + hoverAbovePlayer, ViewportWorldY(lowestSkyViewportY));
            desiredPosition = new Vector3(tp.x, attackY, 0f);
            float horizontalDistance = Mathf.Abs(transform.position.x - tp.x);
            bool locked = horizontalDistance <= beamHalfWidth + beamBreakPadding;

            if (locked)
                beamTimer += Time.deltaTime;
            else
                beamTimer = Mathf.Max(0f, beamTimer - Time.deltaTime * 1.7f);

            DrawBeam(tp, locked);

            if (beamTimer >= abductionSeconds)
            {
                target.DieFromAbduction(transform.position);
                EndBeam(true);
            }
        }

        public void TakeSodaCanHit(Vector2 hitPosition)
        {
            if (!CanBeHit)
                return;

            // Any successful upward can shot interrupts the current abduction
            // sequence, clears its timer and forces the ship out of the scene.
            SetBeamVisible(false);
            beamTimer = 0f;
            hitFlashUntil = Time.time + Mathf.Max(0.05f, hitFlashDuration);
            forcedRetreatFromHit = true;
            waitingOffscreen = false;
            returnAfterTime = Time.time + Random.Range(
                hitRetreatCooldownRange.x,
                hitRetreatCooldownRange.y);

            Vector2 away = ((Vector2)transform.position - hitPosition).normalized;
            if (away.sqrMagnitude < 0.01f)
                away = Vector2.up;
            velocity = away * hitKickSpeed;

            float exitSide = transform.position.x < worldCamera.transform.position.x
                ? -0.3f
                : 1.3f;
            desiredPosition = new Vector3(
                ViewportWorldX(exitSide),
                ViewportWorldY(1.08f),
                0f);
            currentMoveSpeed = Mathf.Max(currentMoveSpeed, trackingSpeed * 1.35f);
            state = UfoState.Retreat;
        }

        private void EndBeam(bool successful)
        {
            SetBeamVisible(false);
            beamTimer = 0f;
            nextAttackTime = Time.time + Random.Range(attackCooldownRange.x, attackCooldownRange.y);
            state = successful ? UfoState.Retreat : UfoState.Roaming;
            if (!successful) PickRoamTarget();
        }

        private void UpdateRetreat()
        {
            if (waitingOffscreen)
            {
                if (Time.time < returnAfterTime)
                    return;

                waitingOffscreen = false;
                forcedRetreatFromHit = false;
                spriteRenderer.enabled = true;
                GetComponent<BoxCollider2D>().enabled = true;
                PlaceForArrival();
                nextAttackTime = Time.time + Random.Range(
                    firstAttackDelayRange.x,
                    firstAttackDelayRange.y);
                state = UfoState.Arrival;
                return;
            }

            float side = transform.position.x < worldCamera.transform.position.x ? -0.3f : 1.3f;
            desiredPosition = new Vector3(ViewportWorldX(side), ViewportWorldY(1.12f), 0f);

            if (!spriteRenderer.isVisible)
            {
                if (forcedRetreatFromHit)
                {
                    waitingOffscreen = true;
                    spriteRenderer.enabled = false;
                    GetComponent<BoxCollider2D>().enabled = false;
                    velocity = Vector3.zero;
                }
                else
                {
                    PlaceForArrival();
                    nextAttackTime = Time.time + Random.Range(
                        firstAttackDelayRange.x,
                        firstAttackDelayRange.y);
                    state = UfoState.Arrival;
                }
            }
        }

        private void ApplyMovementAndJuice()
        {
            float speed = state == UfoState.Hunting || state == UfoState.Beaming
                ? trackingSpeed
                : Mathf.Max(0.1f, currentMoveSpeed);

            Vector3 nextPosition = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref velocity,
                1f / Mathf.Max(0.1f, movementSmoothing),
                speed,
                Time.deltaTime);

            // Shake must never write to localPosition on this root object. The old
            // implementation reset the whole UFO to (0,0,0) every frame, overriding
            // all arrival, roaming and swoop movement.
            if (state == UfoState.Beaming)
            {
                nextPosition += new Vector3(
                    Random.Range(-shakeAmount, shakeAmount),
                    Random.Range(-shakeAmount, shakeAmount),
                    0f);
            }

            transform.position = nextPosition;
            KeepShipInSkyBand();

            float bank = Mathf.Clamp(-velocity.x * bankAngle * 0.28f, -bankAngle, bankAngle);
            float beamPulse = state == UfoState.Beaming ? 1f + Mathf.Sin(Time.time * beamPulseSpeed) * 0.045f : 1f;
            float hoverPulse = 1f + Mathf.Sin(Time.time * 3.2f) * 0.018f;
            transform.localScale = baseScale * beamPulse * hoverPulse;
            transform.rotation = Quaternion.Euler(0f, 0f, bank);
            spriteRenderer.color = Time.time < hitFlashUntil
                ? Color.white * 1.35f
                : Color.white;
        }

        private void KeepShipInSkyBand()
        {
            if (worldCamera == null || spriteRenderer == null) return;

            // The UFO always lives in a camera-relative sky band. It can descend for
            // an abduction pass, but it is never allowed to settle into the waves.
            // lowestSkyViewportY is the minimum visible BOTTOM edge of the ship,
            // not merely its centre. This guarantees the artwork itself remains
            // above the ocean even when the sprite is tall.
            float minimumVisibleBottom = ViewportWorldY(lowestSkyViewportY);
            float maximumVisibleTop = ViewportWorldY(highestSkyViewportY);
            float halfSpriteHeight = spriteRenderer.bounds.extents.y;
            float paddedHalfHeight = Mathf.Max(0.05f, halfSpriteHeight + visibleEdgePadding);

            float minimumY = minimumVisibleBottom + paddedHalfHeight;
            float maximumY = maximumVisibleTop - paddedHalfHeight;
            if (maximumY < minimumY) maximumY = minimumY;

            Vector3 position = transform.position;
            position.y = Mathf.Clamp(position.y, minimumY, maximumY);
            transform.position = position;

            // Prevent a stale target from dragging the ship back toward the ocean
            // after the camera or endless section has moved.
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minimumY, maximumY);
        }

        private void AnimateSprite()
        {
            if (frames == null || frames.Length == 0) return;
            frameClock += Time.deltaTime * Mathf.Max(1f, framesPerSecond);
            int next = Mathf.FloorToInt(frameClock) % frames.Length;
            if (next != frameIndex)
            {
                frameIndex = next;
                spriteRenderer.sprite = frames[frameIndex];
            }
        }

        private void BuildBeam()
        {
            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = shader != null ? new Material(shader) : null;
            beamLeft = CreateLine("Beam Left", beamOuterWidth, beamOuterColor, 2);
            beamRight = CreateLine("Beam Right", beamOuterWidth, beamOuterColor, 2);
            beamCore = CreateLine("Beam Core", beamInnerWidth, beamInnerColor, 2);
            beamFloor = CreateLine("Beam Floor", beamInnerWidth * 0.8f, beamInnerColor, 18);
        }

        private LineRenderer CreateLine(string objectName, float width, Color color, int positions)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = positions;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, color.a * 0.45f);
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.sortingOrder = sortingOrder - 1;
            if (lineMaterial != null) lr.material = lineMaterial;
            return lr;
        }

        private void DrawBeam(Vector3 targetPosition, bool locked)
        {
            float pulse = 1f + Mathf.Sin(Time.time * beamPulseSpeed) * 0.12f;
            float width = beamHalfWidth * pulse;
            Vector3 origin = transform.position + Vector3.down * 0.28f;
            Vector3 floor = new Vector3(targetPosition.x, targetPosition.y + 0.05f, 0f);
            beamLeft.SetPosition(0, origin + Vector3.left * 0.18f);
            beamLeft.SetPosition(1, floor + Vector3.left * width);
            beamRight.SetPosition(0, origin + Vector3.right * 0.18f);
            beamRight.SetPosition(1, floor + Vector3.right * width);
            beamCore.SetPosition(0, origin);
            beamCore.SetPosition(1, floor);

            int count = beamFloor.positionCount;
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)(count - 1)) * Mathf.PI * 2f;
                beamFloor.SetPosition(i, floor + new Vector3(Mathf.Cos(a) * width, Mathf.Sin(a) * 0.18f, 0f));
            }

            float alpha = locked ? 0.9f : 0.35f;
            Color inner = beamInnerColor; inner.a *= alpha;
            beamCore.startColor = inner;
            beamCore.endColor = new Color(inner.r, inner.g, inner.b, inner.a * 0.25f);
        }

        private void SetBeamVisible(bool visible)
        {
            if (beamLeft != null) beamLeft.enabled = visible;
            if (beamRight != null) beamRight.enabled = visible;
            if (beamCore != null) beamCore.enabled = visible;
            if (beamFloor != null) beamFloor.enabled = visible;
        }

        private float ViewportWorldX(float viewportX)
        {
            float z = Mathf.Abs(worldCamera.transform.position.z);
            return worldCamera.ViewportToWorldPoint(new Vector3(viewportX, 0.5f, z)).x;
        }

        private float ViewportWorldY(float viewportY)
        {
            float z = Mathf.Abs(worldCamera.transform.position.z);
            return worldCamera.ViewportToWorldPoint(new Vector3(0.5f, viewportY, z)).y;
        }

        private void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
        }
    }
}
