using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(InterWaveRenderItem))]
    public sealed class JellyfishSwimmer : MonoBehaviour
    {
        [Header("School Swimming")]
        [SerializeField, Min(0.1f)] private float formationResponsiveness = 2.4f;
        [SerializeField, Min(0f)] private float driftStrength = 0.10f;
        [SerializeField, Min(0.1f)] private float bobFrequency = 1.25f;
        [SerializeField, Min(0f)] private float bobHeight = 0.16f;

        [Header("Combat")]
        [SerializeField, Min(0.1f)] private float contactRadius = 0.34f;
        [SerializeField, Min(0.1f)] private float damageCooldown = 1.1f;
        [SerializeField, Min(1)] private int hitsToDefeat = 1;

        [Header("Animation")]
        [SerializeField, Min(1f)] private float framesPerSecond = 10f;

        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;
        private JellyfishSchoolController school;
        private Sprite[] frames;
        private Vector2 formationOffset;
        private float phase;
        private float animationTime;
        private float nextDamageTime;
        private int hitCount;
        private bool initialised;
        private float lastFacingDirection = 1f;

        public void Initialise(
            JellyfishSchoolController schoolController,
            Vector2 offset,
            Sprite[] animationFrames,
            float animationOffset)
        {
            ResolveReferences();
            school = schoolController;
            if (school == null || school.WaterLayers.Count < 2)
            {
                enabled = false;
                return;
            }

            school.Register(this);
            formationOffset = offset;
            frames = animationFrames;
            animationTime = animationOffset;
            phase = Random.Range(0f, Mathf.PI * 2f);
            renderItem.SetLane(school.Lane);
            lastFacingDirection = school.Direction;

            float x = school.AnchorX + formationOffset.x;
            float y = school.GetLaneCentreY(x) + formationOffset.y;
            SetInitialPosition(new Vector2(x, y));
            initialised = true;
        }

        private void Awake() => ResolveReferences();

        private void ResolveReferences()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            CircleCollider2D col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = contactRadius;
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0 || spriteRenderer == null) return;
            animationTime += Time.deltaTime * framesPerSecond;
            spriteRenderer.sprite = frames[Mathf.FloorToInt(animationTime) % frames.Length];
        }

        private void FixedUpdate()
        {
            if (!initialised || school == null || body == null) return;

            float time = Time.time;
            float drift = Mathf.Sin(time * 0.63f + phase) * driftStrength;
            float desiredX = school.AnchorX + formationOffset.x + drift;
            float desiredY = school.GetLaneCentreY(desiredX)
                + formationOffset.y
                + Mathf.Sin(time * bobFrequency + phase) * bobHeight;

            float follow = 1f - Mathf.Exp(-formationResponsiveness * Time.fixedDeltaTime);
            Vector2 next = Vector2.Lerp(body.position, new Vector2(desiredX, desiredY), follow);
            SetPosition(next);

            // Facing comes only from the shared leader, never from tiny local movement.
            if (Mathf.Abs(school.Direction - lastFacingDirection) > 0.5f)
                lastFacingDirection = school.Direction;
            spriteRenderer.flipX = lastFacingDirection < 0f;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Time.time < nextDamageTime || other == null) return;
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || surfer.IsDead || surfer.HasObstacleClearance) return;
            if (Vector2.Distance(transform.position, surfer.transform.position) > contactRadius + 0.28f) return;
            if (surfer.TakeSharkHit(transform.position))
                nextDamageTime = Time.time + damageCooldown;
        }

        public void TakeThrownItemHit(Vector2 hitPosition)
        {
            hitCount++;
            if (hitCount >= hitsToDefeat)
            {
                Destroy(gameObject);
                return;
            }
            StartCoroutine(HitFlash());
        }

        private IEnumerator HitFlash()
        {
            Color original = spriteRenderer.color;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.12f);
            if (spriteRenderer != null) spriteRenderer.color = original;
        }


        public void ApplySectionShift(float horizontalDistance)
        {
            if (Mathf.Abs(horizontalDistance) <= Mathf.Epsilon)
                return;

            Vector2 shifted = new(
                transform.position.x + horizontalDistance,
                transform.position.y);

            // This is an intentional endless-world recycle teleport, not normal
            // swimming. Move both representations immediately so Rigidbody2D does
            // not interpolate from the old section and create visible jitter.
            transform.position = new Vector3(shifted.x, shifted.y, transform.position.z);
            if (body != null)
            {
                body.position = shifted;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void SetInitialPosition(Vector2 position)
        {
            // MovePosition is deferred until the next physics step. Using it during
            // spawning makes the jellyfish briefly appear at the spawner and then
            // visibly race into formation. Place both the transform and rigidbody
            // immediately before enabling normal school following.
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void SetPosition(Vector2 position)
        {
            if (body != null) body.MovePosition(position);
            else transform.position = position;
        }
    }
}
