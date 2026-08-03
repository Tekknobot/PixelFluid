using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class GodzillaSkullSwimmer : MonoBehaviour
    {
        public static event Action<GodzillaSkullSwimmer> DestroyedByProjectile;

        [Header("Unique Skull Motion")]
        [SerializeField, Min(0.1f)] private float orbitDuration = 1.35f;
        [SerializeField, Min(0.1f)] private float orbitAngularSpeed = 4.8f;
        [SerializeField, Min(0.1f)] private float orbitRadius = 0.72f;
        [SerializeField, Min(0.1f)] private float pursuitSpeed = 1.45f;
        [SerializeField, Min(0.1f)] private float turnResponsiveness = 2.2f;
        [SerializeField, Min(0f)] private float weavingAmount = 0.34f;
        [SerializeField, Min(0.1f)] private float weavingSpeed = 5.2f;
        [SerializeField, Min(0.1f)] private float hitRange = 0.52f;
        [SerializeField, Min(0.5f)] private float lifetime = 11f;
        [SerializeField, Min(1f)] private float framesPerSecond = 10f;
        [SerializeField, Min(0.05f)] private float laneRefreshInterval = 0.16f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Sprite[] frames;
        private TinyWaveSurfer target;
        private Transform summoner;
        private Vector2 velocity;
        private float frameClock;
        private float lifeRemaining;
        private float elapsed;
        private float orbitPhase;
        private float wavePhase;
        private float nextLaneRefreshTime;
        private int currentLane;
        private bool destroyed;

        public bool CanBeHit => !destroyed;

        public void Initialise(
            Sprite[] movementFrames,
            int initialLane,
            int index,
            int count,
            Transform owner)
        {
            frames = movementFrames;
            currentLane = Mathf.Max(0, initialLane);
            summoner = owner;
            lifeRemaining = lifetime;

            float spread = Mathf.Max(1, count);
            orbitPhase =
                (index / spread) * Mathf.PI * 2f +
                UnityEngine.Random.Range(-0.18f, 0.18f);
            wavePhase = orbitPhase * 1.7f;

            target = FindPreferredPlayer();
            RefreshLaneOrdering(true);
            EnsurePhysics();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            EnsurePhysics();
        }

        private void EnsurePhysics()
        {
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
                collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.30f;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void Update()
        {
            if (destroyed)
                return;

            elapsed += Time.deltaTime;
            lifeRemaining -= Time.deltaTime;
            if (lifeRemaining <= 0f)
            {
                Burst();
                return;
            }

            if (target == null || target.IsDead)
                target = FindPreferredPlayer();

            Animate();
            Move();
            RefreshLaneOrdering(false);

            if (target != null &&
                Vector2.Distance(transform.position, target.transform.position) <= hitRange)
            {
                target.TakeSharkHit(transform.position);
                Burst();
            }
        }

        private void Animate()
        {
            if (frames == null || frames.Length == 0 || spriteRenderer == null)
                return;

            frameClock += Time.deltaTime * framesPerSecond;
            spriteRenderer.sprite =
                frames[Mathf.FloorToInt(frameClock) % frames.Length];
        }

        private void Move()
        {
            Vector2 position = transform.position;

            if (elapsed < orbitDuration && summoner != null)
            {
                float angle = orbitPhase + elapsed * orbitAngularSpeed;
                float radius = orbitRadius *
                    Mathf.Lerp(0.72f, 1.15f, elapsed / orbitDuration);

                Vector2 desiredPosition =
                    (Vector2)summoner.position +
                    new Vector2(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius * 0.58f);

                Vector2 desiredVelocity =
                    (desiredPosition - position) *
                    Mathf.Max(1f, turnResponsiveness * 2.2f);

                velocity = Vector2.Lerp(
                    velocity,
                    desiredVelocity,
                    1f - Mathf.Exp(-turnResponsiveness * Time.deltaTime));
            }
            else if (target != null)
            {
                Vector2 toTarget =
                    (Vector2)target.transform.position - position;

                Vector2 direction = toTarget.sqrMagnitude > 0.0001f
                    ? toTarget.normalized
                    : Vector2.right;

                Vector2 perpendicular =
                    new Vector2(-direction.y, direction.x);

                float weave =
                    Mathf.Sin(elapsed * weavingSpeed + wavePhase) *
                    weavingAmount;

                Vector2 desired =
                    direction * pursuitSpeed +
                    perpendicular * weave;

                velocity = Vector2.Lerp(
                    velocity,
                    desired,
                    1f - Mathf.Exp(-turnResponsiveness * Time.deltaTime));
            }

            position += velocity * Time.deltaTime;
            transform.position = position;

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.06f)
                spriteRenderer.flipX = velocity.x < 0f;
        }

        private TinyWaveSurfer FindPreferredPlayer()
        {
            TinyWaveSurfer[] surfers = FindObjectsByType<TinyWaveSurfer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            TinyWaveSurfer closest = null;
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < surfers.Length; i++)
            {
                TinyWaveSurfer surfer = surfers[i];
                if (surfer == null || surfer.IsDead)
                    continue;

                if (surfer.IsPlayerControlled)
                    return surfer;

                float distance = Vector2.SqrMagnitude(
                    (Vector2)surfer.transform.position -
                    (Vector2)transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = surfer;
                }
            }

            return closest;
        }

        private void RefreshLaneOrdering(bool force)
        {
            if (!force && Time.time < nextLaneRefreshTime)
                return;

            nextLaneRefreshTime = Time.time + laneRefreshInterval;
            waterLayers.Clear();
            waterLayers.AddRange(
                EndlessWaveSections.LayersNearest(transform.position.x));
            waterLayers.RemoveAll(
                layer => layer == null || !layer.isActiveAndEnabled);
            waterLayers.Sort(
                (a, b) =>
                    a.IndependentLayerIndex.CompareTo(
                        b.IndependentLayerIndex));

            if (waterLayers.Count < 2)
            {
                renderItem?.SetLane(currentLane);
                return;
            }

            int closestLane = 0;
            float closestDistance = float.PositiveInfinity;
            float worldX = transform.position.x;
            float worldY = transform.position.y;

            for (int lane = 0; lane < waterLayers.Count - 1; lane++)
            {
                float lower =
                    waterLayers[lane].GetGameplaySurfaceHeight(worldX);
                float upper =
                    waterLayers[lane + 1].GetGameplaySurfaceHeight(worldX);
                float centre = (lower + upper) * 0.5f;
                float distance = Mathf.Abs(worldY - centre);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestLane = lane;
                }
            }

            if (closestLane != currentLane || force)
            {
                currentLane = closestLane;
                renderItem?.SetLane(currentLane);
            }
        }

        public bool TakeThrownItemHit(Vector2 impactPosition)
        {
            if (destroyed)
                return false;

            DestroyedByProjectile?.Invoke(this);
            Burst();
            return true;
        }

        private void Burst()
        {
            if (destroyed)
                return;

            destroyed = true;
            ExplosionBasicEffect.Spawn(transform.position);
            Destroy(gameObject);
        }
    }
}
