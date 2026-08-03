using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class BloodfishSchoolController : MonoBehaviour
    {
        public enum TravelStyle { Hunt, Darting, Swaying }

        [SerializeField, Min(0.05f)] private float horizontalSpeed = 0.46f;
        [SerializeField, Range(0f, 0.3f)] private float currentInfluence = 0.035f;
        [SerializeField, Min(0.05f)] private float edgePadding = 0.65f;
        [SerializeField, Min(0.1f)] private float turnCooldown = 0.45f;
        [SerializeField, Min(0.5f)] private float playerAwarenessRange = 2.6f;
        [SerializeField, Range(1f, 4f)] private float pursuitSpeedMultiplier = 1.8f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private readonly List<BloodfishSwimmer> members = new();
        private int lane;
        private float anchorX;
        private float direction = 1f;
        private float nextAllowedTurnTime;
        private bool initialised;
        private float trackedSectionCentreX;
        private TravelStyle travelStyle;
        private BloodfishSchoolSpawner owner;
        private TinyWaveSurfer target;

        public float AnchorX => anchorX;
        public float Direction => direction;
        public int Lane => lane;
        public IReadOnlyList<PixelWaterGPU> WaterLayers => waterLayers;
        public TinyWaveSurfer Target => target;
        public bool IsHunting => target != null && !target.IsDead;

        public void SetOwner(BloodfishSchoolSpawner schoolOwner) => owner = schoolOwner;
        private void OnEnable() => EndlessWaveSections.SectionRecycled += HandleSectionRecycled;
        private void OnDisable() => EndlessWaveSections.SectionRecycled -= HandleSectionRecycled;

        private void HandleSectionRecycled(IReadOnlyList<PixelWaterGPU> recycledLayers, float horizontalShift)
        {
            if (!initialised || recycledLayers == null || waterLayers.Count == 0) return;
            bool owns = false;
            for (int i = 0; i < recycledLayers.Count && !owns; i++)
                owns = recycledLayers[i] != null && waterLayers.Contains(recycledLayers[i]);
            if (!owns) return;

            anchorX += horizontalShift;
            transform.position += Vector3.right * horizontalShift;
            trackedSectionCentreX += horizontalShift;
            members.RemoveAll(member => member == null);
            foreach (BloodfishSwimmer member in members)
                member.ApplySectionShift(horizontalShift);
        }

        public void Initialise(int startingLane, float initialDirection, TravelStyle style)
        {
            members.RemoveAll(member => member == null);
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
            if (waterLayers.Count < 2) { enabled = false; return; }

            lane = Mathf.Clamp(startingLane, 0, waterLayers.Count - 2);
            direction = initialDirection < 0f ? -1f : 1f;
            travelStyle = style;
            float minX = waterLayers[0].TankMinimum.x + edgePadding;
            float maxX = waterLayers[0].TankMaximum.x - edgePadding;
            anchorX = Mathf.Clamp(transform.position.x, minX, maxX);
            trackedSectionCentreX = GetSectionCentreX();
            initialised = true;
        }

        public void Register(BloodfishSwimmer member)
        {
            if (member != null && !members.Contains(member)) members.Add(member);
        }

        private void FixedUpdate()
        {
            if (!initialised || waterLayers.Count < 2) return;
            trackedSectionCentreX = GetSectionCentreX();
            AcquireTarget();

            float speedMultiplier = 1f;
            if (target != null)
            {
                float dx = target.transform.position.x - anchorX;
                if (Mathf.Abs(dx) > 0.18f) direction = Mathf.Sign(dx);
                speedMultiplier = pursuitSpeedMultiplier;
            }
            else if (travelStyle == TravelStyle.Darting)
                speedMultiplier = 1.15f + Mathf.PingPong(Time.time * 0.8f, 0.9f);
            else if (travelStyle == TravelStyle.Swaying)
                speedMultiplier = 0.65f + Mathf.Abs(Mathf.Sin(Time.time * 1.1f)) * 0.75f;

            Vector2 current = GetLaneVelocity(anchorX);
            float speed = Mathf.Max(0.08f, horizontalSpeed * speedMultiplier + current.x * direction * currentInfluence);
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
            else anchorX = Mathf.Clamp(anchorX, minX, maxX);
        }

        private void AcquireTarget()
        {
            if (target != null && !target.IsDead &&
                Vector2.Distance(new Vector2(anchorX, GetLaneCentreY(anchorX)), target.transform.position) <= playerAwarenessRange * 1.35f)
                return;

            target = null;
            float best = playerAwarenessRange;
            foreach (TinyWaveSurfer surfer in GameplayTargetCache.Surfers)
            {
                if (surfer == null || surfer.IsDead) continue;
                float d = Vector2.Distance(new Vector2(anchorX, GetLaneCentreY(anchorX)), surfer.transform.position);
                if (d <= best)
                {
                    target = surfer;
                    best = d;
                    if (surfer.IsPlayerControlled) break;
                }
            }
        }

        public float GetLaneCentreY(float x)
        {
            int c = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(waterLayers[c].GetGameplaySurfaceHeight(x), waterLayers[c + 1].GetGameplaySurfaceHeight(x), 0.5f);
        }

        private float GetSectionCentreX() => waterLayers.Count == 0 || waterLayers[0] == null
            ? trackedSectionCentreX
            : (waterLayers[0].TankMinimum.x + waterLayers[0].TankMaximum.x) * 0.5f;

        private Vector2 GetLaneVelocity(float x)
        {
            int c = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Vector2.Lerp(waterLayers[c].GetGameplayWaveVelocity(x), waterLayers[c + 1].GetGameplayWaveVelocity(x), 0.5f);
        }
    }
}
