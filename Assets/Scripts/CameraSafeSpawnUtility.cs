using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>Chooses an entry point fully outside the gameplay camera while remaining inside its water section.</summary>
    public static class CameraSafeSpawnUtility
    {
        public static float ChooseOffscreenEntryX(
            IReadOnlyList<PixelWaterGPU> waterLayers,
            SpriteRenderer renderer,
            out bool enterFromLeft,
            float extraMargin = 0.6f)
        {
            float sectionMin = waterLayers != null && waterLayers.Count > 0 ? waterLayers[0].TankMinimum.x : -20f;
            float sectionMax = waterLayers != null && waterLayers.Count > 0 ? waterLayers[0].TankMaximum.x : 20f;
            float halfWidth = renderer != null ? Mathf.Max(0.1f, renderer.bounds.extents.x) : 0.45f;
            float margin = halfWidth + Mathf.Max(0.35f, extraMargin);

            Camera camera = Camera.main;
            if (camera == null)
            {
                enterFromLeft = Random.value < 0.5f;
                return enterFromLeft ? sectionMin + margin : sectionMax - margin;
            }

            float cameraLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, Mathf.Abs(camera.transform.position.z))).x;
            float cameraRight = camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, Mathf.Abs(camera.transform.position.z))).x;

            float leftX = Mathf.Clamp(cameraLeft - margin, sectionMin + margin, sectionMax - margin);
            float rightX = Mathf.Clamp(cameraRight + margin, sectionMin + margin, sectionMax - margin);
            bool leftIsHidden = leftX + halfWidth < cameraLeft;
            bool rightIsHidden = rightX - halfWidth > cameraRight;

            if (leftIsHidden && rightIsHidden)
                enterFromLeft = Random.value < 0.5f;
            else if (leftIsHidden)
                enterFromLeft = true;
            else if (rightIsHidden)
                enterFromLeft = false;
            else
            {
                // The selected section is currently filling the view. Use the farther
                // section edge; this guarantees the largest possible hidden approach.
                float leftDistance = Mathf.Abs(camera.transform.position.x - (sectionMin + margin));
                float rightDistance = Mathf.Abs((sectionMax - margin) - camera.transform.position.x);
                enterFromLeft = leftDistance >= rightDistance;
                leftX = sectionMin + margin;
                rightX = sectionMax - margin;
            }

            return enterFromLeft ? leftX : rightX;
        }
    }
}
