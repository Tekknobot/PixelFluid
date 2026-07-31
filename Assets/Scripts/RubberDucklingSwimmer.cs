using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class RubberDucklingSwimmer : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float seekSpeed = 1.65f;
        [SerializeField, Min(0.1f)] private float turnResponsiveness = 2.6f;
        [SerializeField, Min(0.1f)] private float explosionRange = 0.58f;
        [SerializeField, Min(0.5f)] private float lifetime = 10f;
        [SerializeField, Min(1f)] private float framesPerSecond = 9f;
        [SerializeField, Min(0f)] private float bobAmount = 0.11f;
        [SerializeField, Min(0.1f)] private float bobSpeed = 4.5f;
        [SerializeField, Min(0.05f)] private float laneRefreshInterval = 0.12f;

        [Header("Spatial Quack Audio")]
        [SerializeField] private AudioClip quackClip;
        [SerializeField, Range(0f, 1f)] private float quackVolume = 0.7f;
        [SerializeField] private Vector2 quackInterval = new(2.2f, 4.8f);
        [SerializeField, Min(0.1f)] private float audioMinDistance = 3.5f;
        [SerializeField, Min(1f)] private float audioMaxDistance = 20f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Sprite[] frames;
        private TinyWaveSurfer target;
        private Vector2 velocity;
        private float frameClock;
        private float lifeRemaining;
        private float bobPhase;
        private float nextLaneRefreshTime;
        private int currentLane;
        private bool exploded;
        private AudioSource audioSource;
        private float nextQuackTime;

        public bool CanBeHit => !exploded;

        public void Initialise(Sprite[] movementFrames, int initialLane)
        {
            frames = movementFrames;
            lifeRemaining = lifetime;
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            target = FindFirstObjectByType<TinyWaveSurfer>();
            currentLane = Mathf.Max(0, initialLane);
            RefreshLaneOrdering(true);
            EnsurePhysics();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            EnsurePhysics();
            quackClip ??= Resources.Load<AudioClip>("Audio/SFX/duckling_quack");
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = audioMinDistance;
            audioSource.maxDistance = Mathf.Max(audioMinDistance + 0.1f, audioMaxDistance);
            audioSource.dopplerLevel = 0.1f;
            ScheduleQuack();
        }

        private void ScheduleQuack()
        {
            float minimum = Mathf.Max(0.25f, quackInterval.x);
            float maximum = Mathf.Max(minimum, quackInterval.y);
            nextQuackTime = Time.time + Random.Range(minimum, maximum);
        }

        private void EnsurePhysics()
        {
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.38f;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
        }

        private void Update()
        {
            if (exploded) return;

            if (Time.time >= nextQuackTime)
            {
                if (quackClip != null && audioSource != null)
                    audioSource.PlayOneShot(quackClip, quackVolume);
                ScheduleQuack();
            }

            lifeRemaining -= Time.deltaTime;
            if (lifeRemaining <= 0f)
            {
                Explode(false);
                return;
            }

            if (target == null || target.IsDead)
                target = FindFirstObjectByType<TinyWaveSurfer>();

            if (frames != null && frames.Length > 0)
            {
                frameClock += Time.deltaTime * framesPerSecond;
                spriteRenderer.sprite = frames[Mathf.FloorToInt(frameClock) % frames.Length];
            }

            if (target == null)
            {
                RefreshLaneOrdering(false);
                return;
            }

            Vector2 position = transform.position;
            Vector2 toTarget = (Vector2)target.transform.position - position;
            Vector2 desired = toTarget.normalized * seekSpeed;
            velocity = Vector2.Lerp(
                velocity,
                desired,
                1f - Mathf.Exp(-turnResponsiveness * Time.deltaTime));

            position += velocity * Time.deltaTime;
            position.y += Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmount * Time.deltaTime;
            transform.position = position;

            RefreshLaneOrdering(false);

            if (Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;

            if (toTarget.magnitude <= explosionRange)
            {
                target.TakeSharkHit(position);
                Explode(true);
            }
        }

        private void RefreshLaneOrdering(bool force)
        {
            if (!force && Time.time < nextLaneRefreshTime)
                return;

            nextLaneRefreshTime = Time.time + laneRefreshInterval;
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
            waterLayers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            waterLayers.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));

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
                float lower = waterLayers[lane].GetGameplaySurfaceHeight(worldX);
                float upper = waterLayers[lane + 1].GetGameplaySurfaceHeight(worldX);
                float laneCentre = (lower + upper) * 0.5f;
                float distance = Mathf.Abs(worldY - laneCentre);
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
