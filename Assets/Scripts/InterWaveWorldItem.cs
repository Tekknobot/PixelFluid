using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Interactive object placed between two interleaved water render bands.
    /// It keeps its original lane depth while following the sampled GPU wave,
    /// water current and local surface slope.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class InterWaveWorldItem : MonoBehaviour
    {
        public enum ItemKind
        {
            Shark,
            Whale,
            Buoy,
            Crate,
            Bottle,
            Treasure
        }

        [SerializeField] private ItemKind kind;
        [SerializeField, Min(1)] private int hitPoints = 1;
        [SerializeField] private float driftSpeed = 0.2f;
        [SerializeField] private float bobHeight = 0.02f;
        [SerializeField] private float bobSpeed = 1.1f;
        [SerializeField] private bool wrapHorizontally = true;
        [SerializeField] private bool destroyWhenInteracted = true;

        [Header("Water Response")]
        [SerializeField, Range(0f, 1.5f)] private float waveFollow = 0.9f;
        [SerializeField, Range(0f, 1f)] private float currentInfluence = 0.08f;
        [SerializeField, Range(0.5f, 20f)] private float buoyancyResponsiveness = 7f;
        [SerializeField, Range(0f, 1f)] private float surfaceTilt = 0.45f;
        [SerializeField, Range(0.05f, 0.8f)] private float slopeSampleDistance = 0.22f;
        [SerializeField, Range(0f, 30f)] private float maximumTiltDegrees = 14f;

        private PixelWaterGPU foregroundWater;
        private PixelWaterGPU backgroundWater;
        private RandomInterWaveItemSpawner owner;
        private Rigidbody2D body;
        private float surfaceDepthOffset;
        private float bobPhase;
        private bool waterOffsetReady;
        private bool interacted;

        public ItemKind Kind => kind;

        public void Initialise(
            ItemKind newKind,
            PixelWaterGPU newForegroundWater,
            PixelWaterGPU newBackgroundWater,
            RandomInterWaveItemSpawner newOwner,
            float newDriftSpeed,
            float newBobHeight,
            float newBobSpeed,
            int newHitPoints)
        {
            kind = newKind;
            foregroundWater = newForegroundWater;
            backgroundWater = newBackgroundWater;
            owner = newOwner;
            driftSpeed = newDriftSpeed;
            bobHeight = newBobHeight;
            bobSpeed = newBobSpeed;
            hitPoints = Mathf.Max(1, newHitPoints);
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            CacheWaterDepthOffset();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            if (foregroundWater == null)
                foregroundWater = FindFirstObjectByType<PixelWaterGPU>();
            if (backgroundWater == null)
                backgroundWater = foregroundWater;

            CacheWaterDepthOffset();
        }

        private void FixedUpdate()
        {
            if (foregroundWater == null || backgroundWater == null)
                return;

            if (!waterOffsetReady)
                CacheWaterDepthOffset();

            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            Vector2 waveVelocity = Vector2.Lerp(
                foregroundWater.GetGameplayWaveVelocity(position.x),
                backgroundWater.GetGameplayWaveVelocity(position.x),
                0.5f);

            float horizontalSpeed = driftSpeed + waveVelocity.x * currentInfluence;
            position.x += horizontalSpeed * Time.fixedDeltaTime;

            if (wrapHorizontally)
            {
                float padding = 0.5f;
                float minX = Mathf.Max(foregroundWater.TankMinimum.x, backgroundWater.TankMinimum.x);
                float maxX = Mathf.Min(foregroundWater.TankMaximum.x, backgroundWater.TankMaximum.x);
                if (horizontalSpeed >= 0f && position.x > maxX + padding)
                    position.x = minX - padding;
                else if (horizontalSpeed < 0f && position.x < minX - padding)
                    position.x = maxX + padding;
            }

            float sampledSurface = Mathf.Lerp(
                foregroundWater.GetGameplaySurfaceHeight(position.x),
                backgroundWater.GetGameplaySurfaceHeight(position.x),
                0.5f);
            float naturalBob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
            float targetY = sampledSurface + surfaceDepthOffset * waveFollow + naturalBob;
            float follow = 1f - Mathf.Exp(-buoyancyResponsiveness * Time.fixedDeltaTime);
            position.y = Mathf.Lerp(position.y, targetY, follow);

            if (body != null)
                body.MovePosition(position);
            else
                transform.position = new Vector3(position.x, position.y, transform.position.z);

            ApplyWaterTilt(position.x, follow);
        }

        private void CacheWaterDepthOffset()
        {
            if (foregroundWater == null || backgroundWater == null)
                return;

            float middleSurface = Mathf.Lerp(
                foregroundWater.GetGameplaySurfaceHeight(transform.position.x),
                backgroundWater.GetGameplaySurfaceHeight(transform.position.x),
                0.5f);
            surfaceDepthOffset = transform.position.y - middleSurface;
            waterOffsetReady = true;
        }

        private void ApplyWaterTilt(float worldX, float follow)
        {
            float left = Mathf.Lerp(
                foregroundWater.GetGameplaySurfaceHeight(worldX - slopeSampleDistance),
                backgroundWater.GetGameplaySurfaceHeight(worldX - slopeSampleDistance),
                0.5f);
            float right = Mathf.Lerp(
                foregroundWater.GetGameplaySurfaceHeight(worldX + slopeSampleDistance),
                backgroundWater.GetGameplaySurfaceHeight(worldX + slopeSampleDistance),
                0.5f);
            float slopeDegrees = Mathf.Atan2(right - left, slopeSampleDistance * 2f) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Clamp(slopeDegrees * surfaceTilt,
                -maximumTiltDegrees, maximumTiltDegrees);

            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, follow);
        }

        private void OnMouseDown()
        {
            Interact(1);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.isTrigger)
                return;

            Interact(1);
        }

        public void Interact(int strength)
        {
            if (interacted)
                return;

            hitPoints -= Mathf.Max(1, strength);
            Pulse();

            if (hitPoints > 0)
                return;

            interacted = true;
            owner?.NotifyItemInteracted(this);

            if (destroyWhenInteracted)
                Destroy(gameObject);
        }

        private void Pulse()
        {
            SpriteRenderer sprite = GetComponent<SpriteRenderer>();
            if (sprite != null)
                sprite.flipY = !sprite.flipY;
        }
    }
}
