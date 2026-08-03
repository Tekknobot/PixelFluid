using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(InterWaveRenderItem))]
    public sealed class HeartLaneDrifter : MonoBehaviour
    {
        [Header("Random Movement")]
        [SerializeField] private Vector2 horizontalSpeedRange = new(0.18f, 0.42f);
        [SerializeField] private Vector2 directionChangeDelayRange = new(1.2f, 3.8f);
        [SerializeField, Range(0f, 0.35f)] private float horizontalPadding = 0.07f;
        [SerializeField, Range(0f, 0.4f)] private float laneWander = 0.18f;
        [SerializeField, Min(0.1f)] private float verticalResponsiveness = 3.8f;
        [Header("Pickup")]
        [SerializeField, Min(0.05f)] private float pickupRadius = 0.48f;
        [SerializeField, Min(1)] private int healingAmount = 1;
        [SerializeField, Min(0.03f)] private float pickupReactionDuration = 0.22f;
        [Header("Floating")]
        [SerializeField] private Vector2 bobHeightRange = new(0.025f, 0.07f);
        [SerializeField] private Vector2 bobSpeedRange = new(1.2f, 2.4f);
        [SerializeField, Range(0f, 12f)] private float maximumTilt = 4f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private HeartLaneSpawner owner;
        private int laneIndex;
        private float direction, speed, nextDirectionChange, targetLaneOffset, bobHeight, bobSpeed, bobPhase;
        private bool initialised, collected;

        public void Initialise(int requestedLane, HeartLaneSpawner spawner = null)
        {
            owner = spawner;
            ResolveReferences();
            if (waterLayers.Count < 2) { enabled = false; return; }
            laneIndex = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            GetComponent<InterWaveRenderItem>().SetLane(laneIndex);
            direction = Random.value < 0.5f ? -1f : 1f;
            speed = Random.Range(horizontalSpeedRange.x, horizontalSpeedRange.y);
            bobHeight = Random.Range(bobHeightRange.x, bobHeightRange.y);
            bobSpeed = Random.Range(bobSpeedRange.x, bobSpeedRange.y);
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            ChooseNewWanderTarget();
            Vector2 position = body.position;
            position.x = Random.Range(GetMinimumX(), GetMaximumX());
            position.y = GetLaneCentreY(position.x);
            body.position = position; transform.position = position; initialised = true;
        }

        private void Awake() => ResolveReferences();
        private void Start() { if (!initialised) Initialise(0); }
        private void ResolveReferences()
        {
            body = GetComponent<Rigidbody2D>(); spriteRenderer = GetComponent<SpriteRenderer>();
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
        }

        private void FixedUpdate()
        {
            if (!initialised || collected || waterLayers.Count < 2) return;
            if (Time.time >= nextDirectionChange) { if (Random.value < 0.68f) direction *= -1f; speed = Random.Range(horizontalSpeedRange.x, horizontalSpeedRange.y); ChooseNewWanderTarget(); }
            Vector2 position = body.position; position.x += direction * speed * Time.fixedDeltaTime;
            float minX = GetMinimumX(), maxX = GetMaximumX();
            if (position.x <= minX) { position.x = minX; direction = 1f; } else if (position.x >= maxX) { position.x = maxX; direction = -1f; }
            float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
            float desiredY = GetLaneCentreY(position.x) + targetLaneOffset + bob;
            position.y = Mathf.Lerp(position.y, desiredY, 1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime));
            body.MovePosition(position); spriteRenderer.flipX = direction < 0f;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * bobSpeed + bobPhase) * maximumTilt);
            TryCollect(position);
        }

        private void TryCollect(Vector2 heartPosition)
        {
            foreach (TinyWaveSurfer surfer in GameplayTargetCache.Surfers)
            {
                if (surfer == null || surfer.IsDead || surfer.IsSwitchingWave) continue;
                // An inter-wave heart can be reached from either water row bordering its lane.
                if (surfer.CurrentWaveIndex != laneIndex && surfer.CurrentWaveIndex != laneIndex + 1) continue;
                if (Vector2.Distance(heartPosition, surfer.transform.position) > pickupRadius) continue;
                if (surfer.HealFromHeart(healingAmount)) { Collect(surfer.transform.position); return; }
            }
        }

        private void Collect(Vector3 targetPosition)
        {
            if (collected) return; collected = true;
            Collider2D c = GetComponent<Collider2D>(); if (c != null) c.enabled = false;
            owner?.NotifyHeartCollected(gameObject);
            StartCoroutine(PickupReaction(targetPosition));
        }

        private System.Collections.IEnumerator PickupReaction(Vector3 targetPosition)
        {
            float elapsed = 0f; Vector3 start = transform.position; Vector3 initialScale = transform.localScale;
            while (elapsed < pickupReactionDuration)
            {
                elapsed += Time.deltaTime; float t = Mathf.Clamp01(elapsed / pickupReactionDuration);
                transform.position = Vector3.Lerp(start, targetPosition + Vector3.up * 0.18f, t);
                transform.localScale = initialScale * (1f + t * 0.75f);
                Color color = spriteRenderer.color; color.a = 1f - t; spriteRenderer.color = color;
                yield return null;
            }
            Destroy(gameObject);
        }

        private void ChooseNewWanderTarget() { targetLaneOffset = Random.Range(-laneWander, laneWander); nextDirectionChange = Time.time + Random.Range(Mathf.Max(0.1f, Mathf.Min(directionChangeDelayRange.x, directionChangeDelayRange.y)), Mathf.Max(0.1f, Mathf.Max(directionChangeDelayRange.x, directionChangeDelayRange.y))); }
        private float GetLaneCentreY(float x) => Mathf.Lerp(waterLayers[laneIndex].GetGameplaySurfaceHeight(x), waterLayers[laneIndex + 1].GetGameplaySurfaceHeight(x), 0.5f);
        private float GetMinimumX() { float min = Mathf.Max(waterLayers[laneIndex].TankMinimum.x, waterLayers[laneIndex + 1].TankMinimum.x); float max = Mathf.Min(waterLayers[laneIndex].TankMaximum.x, waterLayers[laneIndex + 1].TankMaximum.x); return Mathf.Lerp(min, max, horizontalPadding); }
        private float GetMaximumX() { float min = Mathf.Max(waterLayers[laneIndex].TankMinimum.x, waterLayers[laneIndex + 1].TankMinimum.x); float max = Mathf.Min(waterLayers[laneIndex].TankMaximum.x, waterLayers[laneIndex + 1].TankMaximum.x); return Mathf.Lerp(max, min, horizontalPadding); }
    }
}
