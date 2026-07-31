using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RubberDucklingSwimmer : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float seekSpeed = 1.65f;
        [SerializeField, Min(0.1f)] private float turnResponsiveness = 2.6f;
        [SerializeField, Min(0.1f)] private float explosionRange = 0.58f;
        [SerializeField, Min(0.5f)] private float lifetime = 10f;
        [SerializeField, Min(1f)] private float framesPerSecond = 9f;
        [SerializeField, Min(0f)] private float bobAmount = 0.11f;
        [SerializeField, Min(0.1f)] private float bobSpeed = 4.5f;

        [Header("Layer Ordering")]
        [SerializeField] private string waterSortingLayer = "Default";
        [SerializeField] private int sortingOrderOffset = 4;
        [SerializeField, Min(1f)] private float sortingPrecision = 100f;

        private int baseSortingOrder;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private TinyWaveSurfer target;
        private Vector2 velocity;
        private float frameClock;
        private float lifeRemaining;
        private float bobPhase;
        private bool exploded;

        public bool CanBeHit => !exploded;

        public void Initialise(Sprite[] movementFrames, SpriteRenderer motherRenderer = null)
        {
            frames = movementFrames;
            lifeRemaining = lifetime;
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            target = FindFirstObjectByType<TinyWaveSurfer>();

            if (motherRenderer != null)
            {
                spriteRenderer.sortingLayerID = motherRenderer.sortingLayerID;
                baseSortingOrder = motherRenderer.sortingOrder;
            }
            else
            {
                spriteRenderer.sortingLayerName = waterSortingLayer;
                baseSortingOrder = spriteRenderer.sortingOrder;
            }

            UpdateLayerOrdering();
            EnsurePhysics();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsurePhysics();
        }

        private void EnsurePhysics()
        {
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.72f;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
        }

        private void UpdateLayerOrdering()
        {
            if (spriteRenderer == null)
                return;

            // Objects lower on the screen render in front.
            int verticalOrder = Mathf.RoundToInt(-transform.position.y * sortingPrecision);

            spriteRenderer.sortingOrder =
                baseSortingOrder +
                verticalOrder +
                sortingOrderOffset;
        }

        private void Update()
        {
            if (exploded) return;
            lifeRemaining -= Time.deltaTime;
            if (lifeRemaining <= 0f) { Explode(false); return; }
            if (target == null || target.IsDead) target = FindFirstObjectByType<TinyWaveSurfer>();

            if (frames != null && frames.Length > 0)
            {
                frameClock += Time.deltaTime * framesPerSecond;
                spriteRenderer.sprite = frames[Mathf.FloorToInt(frameClock) % frames.Length];
            }

            if (target == null) return;
            Vector2 position = transform.position;
            Vector2 toTarget = (Vector2)target.transform.position - position;
            Vector2 desired = toTarget.normalized * seekSpeed;
            velocity = Vector2.Lerp(velocity, desired, 1f - Mathf.Exp(-turnResponsiveness * Time.deltaTime));
            position += velocity * Time.deltaTime;
            position.y += Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmount * Time.deltaTime;
            transform.position = position;
            UpdateLayerOrdering();
            if (Mathf.Abs(velocity.x) > 0.05f) spriteRenderer.flipX = velocity.x < 0f;

            if (toTarget.magnitude <= explosionRange)
            {
                target.TakeSharkHit(position);
                Explode(true);
            }
        }

        public bool TakeThrownItemHit(Vector2 impactPosition)
        {
            if (exploded) return false;
            Explode(false);
            return true;
        }

        private void Explode(bool damaging)
        {
            if (exploded) return;
            exploded = true;
            ExplosionBasicEffect.Spawn(transform.position);
            Destroy(gameObject);
        }
    }
}
