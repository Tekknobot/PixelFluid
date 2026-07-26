using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Keeps one animated shark inside the visible camera, follows the sampled GPU
    /// waves and periodically crosses between adjacent inter-wave lanes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class SharkLaneSwimmer : MonoBehaviour
    {
        [Header("Swimming")]
        [SerializeField, Min(0.05f)] private float horizontalSpeed = 0.75f;
        [SerializeField] private bool startMovingRight = true;
        [SerializeField, Range(0f, 0.35f)] private float viewportPadding = 0.045f;

        [Header("Lane Changes")]
        [SerializeField] private Vector2 laneChangeDelayRange = new(2.8f, 5.5f);
        [SerializeField, Min(0.2f)] private float laneChangeDuration = 1.25f;
        [SerializeField, Range(0f, 0.45f)] private float laneDepthBias = 0.16f;

        [Header("Water Response")]
        [SerializeField, Range(0f, 1f)] private float waveFollow = 0.9f;
        [SerializeField, Range(0f, 0.35f)] private float currentInfluence = 0.055f;
        [SerializeField, Range(1f, 20f)] private float verticalResponsiveness = 8f;
        [SerializeField, Range(0f, 1f)] private float surfaceTilt = 0.32f;
        [SerializeField, Range(0f, 25f)] private float maximumTilt = 10f;
        [SerializeField, Range(0.05f, 0.8f)] private float slopeSampleDistance = 0.24f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;
        private Camera gameplayCamera;
        private SharkSpriteAnimation sharkAnimation;

        private int currentLane;
        private int targetLane;
        private bool changingLane;
        private float laneChangeElapsed;
        private float nextLaneChangeTime;
        private float direction;
        private float depthOffset;
        private bool initialised;

        public void Initialise(int requestedLane)
        {
            ResolveReferences();
            if (waterLayers.Count < 2)
            {
                Debug.LogError("SharkLaneSwimmer requires at least two water layers.", this);
                enabled = false;
                return;
            }

            currentLane = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            targetLane = currentLane;
            direction = startMovingRight ? 1f : -1f;
            renderItem.SetLane(currentLane);

            Vector2 position = transform.position;
            position.x = GetVisibleHorizontalCentre();
            float laneY = GetLaneCentreY(currentLane, position.x);
            depthOffset = -Mathf.Abs(laneDepthBias);
            position.y = laneY + depthOffset;
            SetPosition(position);

            ScheduleNextLaneChange();
            initialised = true;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (!initialised)
                Initialise(0);
        }

        private void ResolveReferences()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            gameplayCamera = Camera.main;
            sharkAnimation = GetComponent<SharkSpriteAnimation>();

            body = GetComponent<Rigidbody2D>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            Collider2D existingCollider = GetComponent<Collider2D>();
            if (existingCollider == null)
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = spriteRenderer != null && spriteRenderer.sprite != null
                    ? spriteRenderer.sprite.bounds.size * 0.72f
                    : new Vector2(1.2f, 0.45f);
            }

            waterLayers.Clear();
            waterLayers.AddRange(
                FindObjectsByType<PixelWaterGPU>(FindObjectsSortMode.None)
                    .Where(layer => layer != null)
                    .OrderBy(layer => layer.IndependentLayerIndex));
        }

        private void FixedUpdate()
        {
            if (!initialised || waterLayers.Count < 2)
                return;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            Vector2 position = body != null ? body.position : (Vector2)transform.position;

            Vector2 velocity = GetLaneVelocity(currentLane, position.x);
            float attackMultiplier = sharkAnimation != null ? sharkAnimation.MovementSpeedMultiplier : 1f;
            float swimSpeed = horizontalSpeed * attackMultiplier + velocity.x * currentInfluence;
            position.x += direction * Mathf.Max(0.08f, swimSpeed) * Time.fixedDeltaTime;

            KeepInsideVisibleScreen(ref position);

            if (!changingLane && (sharkAnimation == null || !sharkAnimation.IsAttacking) && Time.time >= nextLaneChangeTime)
                BeginLaneChange();

            float desiredY;
            if (changingLane)
            {
                laneChangeElapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(laneChangeElapsed / laneChangeDuration);
                float eased = t * t * (3f - 2f * t);

                float fromY = GetLaneCentreY(currentLane, position.x);
                float toY = GetLaneCentreY(targetLane, position.x);
                desiredY = Mathf.Lerp(fromY, toY, eased) + depthOffset;

                // The shark crosses the intervening full water layer at the middle
                // of the transition, so change its queue at the same point.
                if (t >= 0.5f)
                    renderItem.SetLane(targetLane);

                if (t >= 1f)
                {
                    currentLane = targetLane;
                    changingLane = false;
                    laneChangeElapsed = 0f;
                    renderItem.SetLane(currentLane);
                    ScheduleNextLaneChange();
                }
            }
            else
            {
                desiredY = GetLaneCentreY(currentLane, position.x) + depthOffset;
            }

            float follow = 1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime);
            position.y = Mathf.Lerp(position.y, desiredY, follow * waveFollow);
            SetPosition(position);
            ApplyWaterTilt(position.x, follow);
        }

        private void BeginLaneChange()
        {
            int laneCount = waterLayers.Count - 1;
            if (laneCount <= 1)
                return;

            if (currentLane <= 0)
                targetLane = 1;
            else if (currentLane >= laneCount - 1)
                targetLane = laneCount - 2;
            else
                targetLane = currentLane + (Random.value < 0.5f ? -1 : 1);

            changingLane = targetLane != currentLane;
            laneChangeElapsed = 0f;
        }

        private float GetLaneCentreY(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            PixelWaterGPU foreground = waterLayers[clamped];
            PixelWaterGPU background = waterLayers[clamped + 1];
            return Mathf.Lerp(
                foreground.GetGameplaySurfaceHeight(worldX),
                background.GetGameplaySurfaceHeight(worldX),
                0.5f);
        }

        private Vector2 GetLaneVelocity(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Vector2.Lerp(
                waterLayers[clamped].GetGameplayWaveVelocity(worldX),
                waterLayers[clamped + 1].GetGameplayWaveVelocity(worldX),
                0.5f);
        }

        private void KeepInsideVisibleScreen(ref Vector2 position)
        {
            float minX;
            float maxX;

            PixelWaterGPU first = waterLayers[0];
            minX = first.TankMinimum.x;
            maxX = first.TankMaximum.x;

            if (gameplayCamera != null && gameplayCamera.orthographic)
            {
                float zDistance = Mathf.Abs(gameplayCamera.transform.position.z - transform.position.z);
                float cameraMin = gameplayCamera.ViewportToWorldPoint(
                    new Vector3(viewportPadding, 0.5f, zDistance)).x;
                float cameraMax = gameplayCamera.ViewportToWorldPoint(
                    new Vector3(1f - viewportPadding, 0.5f, zDistance)).x;
                minX = Mathf.Max(minX, cameraMin);
                maxX = Mathf.Min(maxX, cameraMax);
            }

            float halfWidth = spriteRenderer != null ? spriteRenderer.bounds.extents.x : 0.45f;
            minX += halfWidth;
            maxX -= halfWidth;

            if (position.x >= maxX)
            {
                position.x = maxX;
                direction = -1f;
                if (spriteRenderer != null)
                    spriteRenderer.flipX = true;
            }
            else if (position.x <= minX)
            {
                position.x = minX;
                direction = 1f;
                if (spriteRenderer != null)
                    spriteRenderer.flipX = false;
            }
        }

        private float GetVisibleHorizontalCentre()
        {
            if (gameplayCamera != null && gameplayCamera.orthographic)
                return gameplayCamera.transform.position.x;

            return (waterLayers[0].TankMinimum.x + waterLayers[0].TankMaximum.x) * 0.5f;
        }

        private void ApplyWaterTilt(float worldX, float follow)
        {
            float left = GetLaneCentreY(currentLane, worldX - slopeSampleDistance);
            float right = GetLaneCentreY(currentLane, worldX + slopeSampleDistance);
            float slope = Mathf.Atan2(right - left, slopeSampleDistance * 2f) * Mathf.Rad2Deg;
            float angle = Mathf.Clamp(slope * surfaceTilt, -maximumTilt, maximumTilt);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.Euler(0f, 0f, angle),
                follow);
        }

        private void SetPosition(Vector2 position)
        {
            if (body != null)
                body.position = position;
            else
                transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void ScheduleNextLaneChange()
        {
            float minimum = Mathf.Min(laneChangeDelayRange.x, laneChangeDelayRange.y);
            float maximum = Mathf.Max(laneChangeDelayRange.x, laneChangeDelayRange.y);
            nextLaneChangeTime = Time.time + Random.Range(minimum, maximum);
        }
    }
}
