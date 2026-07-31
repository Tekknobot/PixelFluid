using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    public sealed class DayTwoHelicopterMissile : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float animationFramesPerSecond = 28f;
        [SerializeField, Min(0.1f)] private float speed = 3.9f;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 6.2f;
        [SerializeField, Min(0.1f)] private float acceleration = 0.75f;
        [SerializeField, Min(1f)] private float turnResponsiveness = 2.0f;
        [SerializeField, Min(0.1f)] private float lifetime = 8f;
        [SerializeField, Min(0.05f)] private float hitRadius = 0.34f;
        [SerializeField] private int sortingOrder = 12020;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private TinyWaveSurfer target;
        private Vector2 velocity;
        private float frameClock;
        private float life;
        private bool resolved;

        public DayTwoHelicopterController Owner { get; private set; }
        public bool CanBeHit => !resolved && isActiveAndEnabled;

        public void Launch(TinyWaveSurfer requestedTarget, DayTwoHelicopterController owner)
        {
            target = requestedTarget;
            Owner = owner;
            life = lifetime;
            spriteRenderer = GetComponent<SpriteRenderer>();
            frames = LoadFrames();
            if (frames.Length > 0) spriteRenderer.sprite = frames[0];
            spriteRenderer.sortingOrder = sortingOrder;
            transform.localScale = Vector3.one * 0.72f;

            CircleCollider2D col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = hitRadius;
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            Vector2 initialDirection = target != null
                ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized
                : Vector2.down;
            velocity = initialDirection * speed;
        }

        private void Update()
        {
            if (resolved) return;
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            if (frames != null && frames.Length > 0)
            {
                frameClock += Time.deltaTime * animationFramesPerSecond;
                spriteRenderer.sprite = frames[Mathf.FloorToInt(frameClock) % frames.Length];
            }

            if (target != null && !target.IsDead)
            {
                Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
                if (toTarget.sqrMagnitude <= hitRadius * hitRadius * 1.8f)
                {
                    HitPlayer();
                    return;
                }
                float currentSpeed = Mathf.Min(maximumSpeed, velocity.magnitude + acceleration * Time.deltaTime);
                Vector2 desired = toTarget.normalized * currentSpeed;
                velocity = Vector2.Lerp(velocity, desired, 1f - Mathf.Exp(-turnResponsiveness * Time.deltaTime));
            }

            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved || other == null) return;
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer != null && surfer == target) HitPlayer();
        }

        private void HitPlayer()
        {
            if (resolved) return;
            resolved = true;
            Vector3 impactPosition = transform.position;
            if (target != null && !target.IsDead) target.TakeSharkHit(impactPosition);
            ExplosionBasicEffect.Spawn(impactPosition);
            Destroy(gameObject);
        }

        public void Intercept(Vector2 hitPosition)
        {
            if (!CanBeHit) return;
            resolved = true;
            ExplosionBasicEffect.Spawn(hitPosition);
            Destroy(gameObject);
        }

        private static Sprite[] LoadFrames()
        {
            Texture2D sheet = Resources.Load<Texture2D>("Helicopter/helicopter_missile");
            if (sheet == null) return System.Array.Empty<Sprite>();
            const int frameSize = 32;
            int count = Mathf.Max(1, sheet.width / frameSize);
            Sprite[] result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Sprite.Create(sheet, new Rect(i * frameSize, 0, frameSize, frameSize), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
                result[i].name = $"helicopter_missile_{i:00}";
            }
            return result;
        }
    }
}
