using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class JellyfishSchoolController : MonoBehaviour
    {
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

        public float AnchorX => anchorX;
        public float Direction => direction;
        public int Lane => lane;
        public IReadOnlyList<PixelWaterGPU> WaterLayers => waterLayers;

        public void Initialise(int startingLane, float initialDirection)
        {
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
            if (waterLayers.Count < 2)
            {
                enabled = false;
                return;
            }

            lane = Mathf.Clamp(startingLane, 0, waterLayers.Count - 2);
            direction = initialDirection < 0f ? -1f : 1f;

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

            // EndlessWaveSections recycles a complete water section by shifting
            // its PixelWaterGPU layers several section widths in one frame. Keep
            // the school in the same recycled section instead of allowing its
            // world-space anchor to remain behind and pull every follower across
            // the map.
            float currentSectionCentreX = GetSectionCentreX();
            float sectionShift = currentSectionCentreX - trackedSectionCentreX;
            if (Mathf.Abs(sectionShift) > 0.5f)
            {
                anchorX += sectionShift;
                transform.position += Vector3.right * sectionShift;

                members.RemoveAll(member => member == null);
                foreach (JellyfishSwimmer member in members)
                    member.ApplySectionShift(sectionShift);
            }
            trackedSectionCentreX = currentSectionCentreX;

            Vector2 current = GetLaneVelocity(anchorX);
            float speed = Mathf.Max(0.04f, horizontalSpeed + current.x * direction * currentInfluence);
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
