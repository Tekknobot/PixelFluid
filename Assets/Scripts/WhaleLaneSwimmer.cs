using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class WhaleLaneSwimmer : MonoBehaviour
    {
        [Header("Swimming")]
        [SerializeField, Min(0.05f)] private float horizontalSpeed = 0.34f;
        [SerializeField] private bool startMovingRight = true;
        [SerializeField, Range(0f, 0.45f)] private float laneDepthBias = 0.35f;
        [SerializeField, Range(0f, 1f)] private float waveFollow = 0.82f;
        [SerializeField, Range(1f, 20f)] private float verticalResponsiveness = 5f;
        [SerializeField, Range(0f, 0.2f)] private float currentInfluence = 0.035f;

        [Header("Lane Changes")]
        [SerializeField] private Vector2 laneChangeDelayRange = new(7f, 14f);
        [SerializeField, Min(0.2f)] private float laneChangeDuration = 2.4f;

        [Header("Occasional Breach")]
        [SerializeField] private Vector2 breachDelayRange = new(12f, 25f);
        [SerializeField, Min(0.5f)] private float breachDuration = 2.1f;
        [SerializeField, Min(0.1f)] private float breachHeight = 1.15f;
        [SerializeField, Range(0f, 45f)] private float breachRotation = 18f;
        [SerializeField, Range(1f, 3f)] private float breachSpeedMultiplier = 1.35f;

        [Header("Water Tilt")]
        [SerializeField, Range(0f, 1f)] private float surfaceTilt = 0.2f;
        [SerializeField, Range(0f, 20f)] private float maximumTilt = 7f;
        [SerializeField, Range(0.05f, 0.8f)] private float slopeSampleDistance = 0.28f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;
        private Camera gameplayCamera;

        private int currentLane;
        private int targetLane;
        private float direction;
        private float depthOffset;
        private bool changingLane;
        private float laneChangeElapsed;
        private float nextLaneChangeTime;
        private bool breaching;
        private float breachElapsed;
        private float nextBreachTime;
        private bool initialised;

        public void Initialise(int requestedLane, bool spawnAtSectionEdge = false)
        {
            ResolveReferences();
            if (waterLayers.Count < 2)
            {
                Debug.LogError("WhaleLaneSwimmer requires at least two water layers.", this);
                enabled = false;
                return;
            }

            currentLane = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            targetLane = currentLane;
            direction = startMovingRight ? 1f : -1f;
            depthOffset = -Mathf.Abs(laneDepthBias);
            renderItem.SetLane(currentLane);

            Vector2 position = transform.position;
            if (spawnAtSectionEdge)
            {
                position.x = CameraSafeSpawnUtility.ChooseOffscreenEntryX(
                    waterLayers, spriteRenderer, out bool enterFromLeft);
                direction = enterFromLeft ? 1f : -1f;
                if (spriteRenderer != null)
                    spriteRenderer.flipX = !enterFromLeft;
            }
            else
            {
                position.x = GetVisibleHorizontalCentre();
            }
            position.y = GetLaneCentreY(currentLane, position.x) + depthOffset;
            SetPosition(position);

            ScheduleNextLaneChange();
            ScheduleNextBreach();
            initialised = true;
        }

        private void Awake() => ResolveReferences();
        private void Start() { if (!initialised) Initialise(0); }

        private void ResolveReferences()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            gameplayCamera = Camera.main;

            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
        }

        private void FixedUpdate()
        {
            if (!initialised || waterLayers.Count < 2)
                return;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            Vector2 waterVelocity = GetLaneVelocity(currentLane, position.x);
            float speedScale = breaching ? breachSpeedMultiplier : 1f;
            float swimSpeed = horizontalSpeed * speedScale + waterVelocity.x * currentInfluence;
            position.x += direction * Mathf.Max(0.05f, swimSpeed) * Time.fixedDeltaTime;
            KeepInsideGameArea(ref position);

            if (!breaching && !changingLane && Time.time >= nextLaneChangeTime)
                BeginRandomLaneChange();
            if (!breaching && !changingLane && Time.time >= nextBreachTime)
                BeginBreach();

            float laneY = UpdateLaneTransition(position.x);
            float breachOffset = UpdateBreach();
            float desiredY = laneY + breachOffset;
            float follow = 1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime);
            position.y = Mathf.Lerp(position.y, desiredY, follow * waveFollow);
            SetPosition(position);

            if (!breaching)
                ApplyWaterTilt(position.x, follow);
        }

        private void BeginRandomLaneChange()
        {
            int laneCount = waterLayers.Count - 1;
            if (laneCount <= 1)
                return;

            if (currentLane <= 0) targetLane = 1;
            else if (currentLane >= laneCount - 1) targetLane = laneCount - 2;
            else targetLane = currentLane + (Random.value < 0.5f ? -1 : 1);

            changingLane = targetLane != currentLane;
            laneChangeElapsed = 0f;
        }

        private float UpdateLaneTransition(float worldX)
        {
            if (!changingLane)
                return GetLaneCentreY(currentLane, worldX) + depthOffset;

            laneChangeElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(laneChangeElapsed / laneChangeDuration);
            float eased = t * t * (3f - 2f * t);
            float y = Mathf.Lerp(GetLaneCentreY(currentLane, worldX), GetLaneCentreY(targetLane, worldX), eased) + depthOffset;

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

            return y;
        }

        private void BeginBreach()
        {
            breaching = true;
            breachElapsed = 0f;
        }

        private float UpdateBreach()
        {
            if (!breaching)
                return 0f;

            breachElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(breachElapsed / breachDuration);
            float arc = Mathf.Sin(t * Mathf.PI);
            float signedRotation = direction > 0f ? -breachRotation : breachRotation;
            transform.rotation = Quaternion.Euler(0f, 0f, signedRotation * Mathf.Sin(t * Mathf.PI * 2f));

            if (t >= 1f)
            {
                breaching = false;
                breachElapsed = 0f;
                transform.rotation = Quaternion.identity;
                ScheduleNextBreach();
            }

            return arc * breachHeight;
        }

        private float GetLaneCentreY(int lane, float worldX)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(
                waterLayers[clamped].GetGameplaySurfaceHeight(worldX),
                waterLayers[clamped + 1].GetGameplaySurfaceHeight(worldX),
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

        private void KeepInsideGameArea(ref Vector2 position)
        {
            float minX = waterLayers[0].TankMinimum.x;
            float maxX = waterLayers[0].TankMaximum.x;
            float halfWidth = spriteRenderer != null ? spriteRenderer.bounds.extents.x : 1f;
            minX += halfWidth;
            maxX -= halfWidth;

            if (position.x > maxX)
            {
                position.x = maxX;
                direction = -1f;
                if (spriteRenderer != null) spriteRenderer.flipX = true;
            }
            else if (position.x < minX)
            {
                position.x = minX;
                direction = 1f;
                if (spriteRenderer != null) spriteRenderer.flipX = false;
            }
        }

        private float GetVisibleHorizontalCentre() => gameplayCamera != null && gameplayCamera.orthographic
            ? gameplayCamera.transform.position.x
            : (waterLayers[0].TankMinimum.x + waterLayers[0].TankMaximum.x) * 0.5f;

        private void ApplyWaterTilt(float worldX, float follow)
        {
            float left = GetLaneCentreY(currentLane, worldX - slopeSampleDistance);
            float right = GetLaneCentreY(currentLane, worldX + slopeSampleDistance);
            float slope = Mathf.Atan2(right - left, slopeSampleDistance * 2f) * Mathf.Rad2Deg;
            float angle = Mathf.Clamp(slope * surfaceTilt, -maximumTilt, maximumTilt);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, 0f, angle), follow);
        }

        private void SetPosition(Vector2 position)
        {
            if (body != null) body.position = position;
            else transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void ScheduleNextLaneChange()
        {
            float minimum = Mathf.Min(laneChangeDelayRange.x, laneChangeDelayRange.y);
            float maximum = Mathf.Max(laneChangeDelayRange.x, laneChangeDelayRange.y);
            nextLaneChangeTime = Time.time + Random.Range(minimum, maximum);
        }

        private void ScheduleNextBreach()
        {
            float minimum = Mathf.Max(1f, Mathf.Min(breachDelayRange.x, breachDelayRange.y));
            float maximum = Mathf.Max(minimum, Mathf.Max(breachDelayRange.x, breachDelayRange.y));
            nextBreachTime = Time.time + Random.Range(minimum, maximum);
        }
    }
}
