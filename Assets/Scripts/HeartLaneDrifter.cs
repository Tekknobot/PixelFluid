using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(InterWaveRenderItem))]
    public sealed class HeartLaneDrifter : MonoBehaviour
    {
        [Header("Random Movement")]
        [SerializeField] private Vector2 horizontalSpeedRange = new(0.18f, 0.42f);
        [SerializeField] private Vector2 directionChangeDelayRange = new(1.2f, 3.8f);
        [SerializeField, Range(0f, 0.35f)] private float horizontalPadding = 0.07f;
        [SerializeField, Range(0f, 0.4f)] private float laneWander = 0.18f;
        [SerializeField, Min(0.1f)] private float verticalResponsiveness = 3.8f;

        [Header("Floating")]
        [SerializeField] private Vector2 bobHeightRange = new(0.025f, 0.07f);
        [SerializeField] private Vector2 bobSpeedRange = new(1.2f, 2.4f);
        [SerializeField, Range(0f, 12f)] private float maximumTilt = 4f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private int laneIndex;
        private float direction;
        private float speed;
        private float nextDirectionChange;
        private float targetLaneOffset;
        private float bobHeight;
        private float bobSpeed;
        private float bobPhase;
        private bool initialised;

        public void Initialise(int requestedLane)
        {
            ResolveReferences();
            if (waterLayers.Count < 2)
            {
                enabled = false;
                return;
            }

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
            body.position = position;
            transform.position = position;
            initialised = true;
        }

        private void Awake() => ResolveReferences();
        private void Start() { if (!initialised) Initialise(0); }

        private void ResolveReferences()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            waterLayers.Clear();
            waterLayers.AddRange(FindObjectsByType<PixelWaterGPU>(FindObjectsSortMode.None)
                .Where(layer => layer != null)
                .OrderBy(layer => layer.IndependentLayerIndex));
        }

        private void FixedUpdate()
        {
            if (!initialised || waterLayers.Count < 2)
                return;

            if (Time.time >= nextDirectionChange)
            {
                if (Random.value < 0.68f)
                    direction *= -1f;

                speed = Random.Range(horizontalSpeedRange.x, horizontalSpeedRange.y);
                ChooseNewWanderTarget();
            }

            Vector2 position = body.position;
            position.x += direction * speed * Time.fixedDeltaTime;

            float minX = GetMinimumX();
            float maxX = GetMaximumX();
            if (position.x <= minX)
            {
                position.x = minX;
                direction = 1f;
            }
            else if (position.x >= maxX)
            {
                position.x = maxX;
                direction = -1f;
            }

            float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
            float desiredY = GetLaneCentreY(position.x) + targetLaneOffset + bob;
            float verticalBlend = 1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime);
            position.y = Mathf.Lerp(position.y, desiredY, verticalBlend);

            body.MovePosition(position);
            spriteRenderer.flipX = direction < 0f;

            float tilt = Mathf.Sin(Time.time * bobSpeed + bobPhase) * maximumTilt;
            transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void ChooseNewWanderTarget()
        {
            targetLaneOffset = Random.Range(-laneWander, laneWander);
            float minimum = Mathf.Min(directionChangeDelayRange.x, directionChangeDelayRange.y);
            float maximum = Mathf.Max(directionChangeDelayRange.x, directionChangeDelayRange.y);
            nextDirectionChange = Time.time + Random.Range(Mathf.Max(0.1f, minimum), Mathf.Max(0.1f, maximum));
        }

        private float GetLaneCentreY(float x)
        {
            float lower = waterLayers[laneIndex].GetGameplaySurfaceHeight(x);
            float upper = waterLayers[laneIndex + 1].GetGameplaySurfaceHeight(x);
            return Mathf.Lerp(lower, upper, 0.5f);
        }

        private float GetMinimumX()
        {
            float min = Mathf.Max(waterLayers[laneIndex].TankMinimum.x, waterLayers[laneIndex + 1].TankMinimum.x);
            float max = Mathf.Min(waterLayers[laneIndex].TankMaximum.x, waterLayers[laneIndex + 1].TankMaximum.x);
            return Mathf.Lerp(min, max, horizontalPadding);
        }

        private float GetMaximumX()
        {
            float min = Mathf.Max(waterLayers[laneIndex].TankMinimum.x, waterLayers[laneIndex + 1].TankMinimum.x);
            float max = Mathf.Min(waterLayers[laneIndex].TankMaximum.x, waterLayers[laneIndex + 1].TankMaximum.x);
            return Mathf.Lerp(max, min, horizontalPadding);
        }
    }
}
