using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class JellyfishSchoolController : MonoBehaviour
    {
        public enum TravelStyle
        {
            Patrol,
            SlowDrift,
            FastCurrent,
            Swaying
        }
        [SerializeField, Min(0.05f)] private float horizontalSpeed = 0.28f;
        [SerializeField, Range(0f, 0.3f)] private float currentInfluence = 0.04f;
        [SerializeField, Min(0.05f)] private float edgePadding = 0.55f;
        [SerializeField, Min(0.1f)] private float turnCooldown = 0.65f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private readonly List<JellyfishSwimmer> members = new();
        private int lane;
        private float anchorX;
        private float direction = 1f;
        private float nextAllowedTurnTime;
        private bool initialised;
        private float trackedSectionCentreX;
        private TravelStyle travelStyle;
        private float baseAnchorYPhase;
        private JellyfishSchoolSpawner owner;
        private float configuredHorizontalSpeed;
        private float configuredCurrentInfluence;
        private bool capturedConfiguration;

        public float AnchorX => anchorX;
        public float Direction => direction;
        public int Lane => lane;
        public IReadOnlyList<PixelWaterGPU> WaterLayers => waterLayers;

        public void SetOwner(JellyfishSchoolSpawner schoolOwner) => owner = schoolOwner;

        private void Awake()
        {
            CaptureConfiguration();
        }

        private void CaptureConfiguration()
        {
            if (capturedConfiguration) return;
            configuredHorizontalSpeed = horizontalSpeed;
            configuredCurrentInfluence = currentInfluence;
            capturedConfiguration = true;
        }

        private void OnEnable() => EndlessWaveSections.SectionRecycled += HandleSectionRecycled;

        private void OnDisable() => EndlessWaveSections.SectionRecycled -= HandleSectionRecycled;

        private void HandleSectionRecycled(IReadOnlyList<PixelWaterGPU> recycledLayers, float horizontalShift)
        {
            if (!initialised || recycledLayers == null || waterLayers.Count == 0)
                return;

            bool ownsRecycledSection = false;
            for (int i = 0; i < recycledLayers.Count && !ownsRecycledSection; i++)
                ownsRecycledSection = recycledLayers[i] != null && waterLayers.Contains(recycledLayers[i]);

            if (!ownsRecycledSection)
                return;

            anchorX += horizontalShift;
            transform.position += Vector3.right * horizontalShift;
            trackedSectionCentreX += horizontalShift;

            members.RemoveAll(member => member == null);
            foreach (JellyfishSwimmer member in members)
                member.ApplySectionShift(horizontalShift);
        }

        public void Initialise(int startingLane, float initialDirection, TravelStyle style = TravelStyle.Patrol)
        {
            CaptureConfiguration();
            horizontalSpeed = configuredHorizontalSpeed;
            currentInfluence = configuredCurrentInfluence;
            members.RemoveAll(member => member == null);
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
            if (waterLayers.Count < 2)
            {
                enabled = false;
                return;
            }

            lane = Mathf.Clamp(startingLane, 0, waterLayers.Count - 2);
            direction = initialDirection < 0f ? -1f : 1f;
            travelStyle = style;
            baseAnchorYPhase = Random.Range(0f, Mathf.PI * 2f);

            switch (travelStyle)
            {
                case TravelStyle.SlowDrift:
                    horizontalSpeed *= Random.Range(0.48f, 0.7f);
                    currentInfluence = Mathf.Max(currentInfluence, 0.10f);
                    break;
                case TravelStyle.FastCurrent:
                    horizontalSpeed *= Random.Range(1.5f, 2.05f);
                    currentInfluence *= 0.5f;
                    break;
                case TravelStyle.Swaying:
                    horizontalSpeed *= Random.Range(0.8f, 1.15f);
                    break;
            }

            float minX = waterLayers[0].TankMinimum.x + edgePadding;
            float maxX = waterLayers[0].TankMaximum.x - edgePadding;
            anchorX = Mathf.Clamp(transform.position.x, minX, maxX);
            if (Mathf.Abs(anchorX - transform.position.x) > 0.01f)
                transform.position = new Vector3(anchorX, transform.position.y, transform.position.z);

            trackedSectionCentreX = GetSectionCentreX();
            initialised = true;
        }

        public void Register(JellyfishSwimmer member)
        {
            if (member != null && !members.Contains(member))
                members.Add(member);
        }

        private void FixedUpdate()
        {
            if (!initialised || waterLayers.Count < 2)
                return;

            trackedSectionCentreX = GetSectionCentreX();

            Vector2 current = GetLaneVelocity(anchorX);
            float speed = Mathf.Max(0.04f, horizontalSpeed + current.x * direction * currentInfluence);
            if (travelStyle == TravelStyle.Swaying)
                speed *= 0.72f + Mathf.Abs(Mathf.Sin(Time.time * 0.9f + baseAnchorYPhase)) * 0.65f;
            anchorX += direction * speed * Time.fixedDeltaTime;

            float minX = waterLayers[0].TankMinimum.x + edgePadding;
            float maxX = waterLayers[0].TankMaximum.x - edgePadding;

            bool hitLeft = anchorX <= minX && direction < 0f;
            bool hitRight = anchorX >= maxX && direction > 0f;
            if ((hitLeft || hitRight) && Time.time >= nextAllowedTurnTime)
            {
                anchorX = Mathf.Clamp(anchorX, minX, maxX);
                direction = -direction;
                nextAllowedTurnTime = Time.time + turnCooldown;
            }
            else
            {
                anchorX = Mathf.Clamp(anchorX, minX, maxX);
            }
        }

        public float GetLaneCentreY(float x)
        {
            int c = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(
                waterLayers[c].GetGameplaySurfaceHeight(x),
                waterLayers[c + 1].GetGameplaySurfaceHeight(x),
                0.5f);
        }

        private float GetSectionCentreX()
        {
            if (waterLayers.Count == 0 || waterLayers[0] == null)
                return trackedSectionCentreX;

            return (waterLayers[0].TankMinimum.x + waterLayers[0].TankMaximum.x) * 0.5f;
        }

        private Vector2 GetLaneVelocity(float x)
        {
            int c = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Vector2.Lerp(
                waterLayers[c].GetGameplayWaveVelocity(x),
                waterLayers[c + 1].GetGameplayWaveVelocity(x),
                0.5f);
        }
    }
}
