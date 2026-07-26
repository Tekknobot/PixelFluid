using UnityEngine;

namespace PixelOcean
{
    public sealed class BeachCameraFollow : MonoBehaviour
    {
        [Header("Follow Offset")]
        [SerializeField] private float horizontalOffset = 0.25f;
        [SerializeField] private float verticalOffset = 0.45f;

        [Header("Vertical Camera Limits")]
        [SerializeField] private float minimumCameraY = -0.30f;
        [SerializeField] private float maximumCameraY = 1.05f;

        [Header("Smoothing")]
        [SerializeField] private float smoothTime = 0.20f;

        public Transform Target { get; set; }

        private Vector3 velocity;

        private void LateUpdate()
        {
            if (Target == null)
                return;

            float desiredY = Mathf.Clamp(
                Target.position.y + verticalOffset,
                minimumCameraY,
                maximumCameraY
            );

            Vector3 desired = new(
                Target.position.x + horizontalOffset,
                desiredY,
                -10f
            );

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desired,
                ref velocity,
                smoothTime
            );
        }
    }
}