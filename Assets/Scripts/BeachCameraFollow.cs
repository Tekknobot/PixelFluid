using UnityEngine;

namespace PixelOcean
{
    public sealed class BeachCameraFollow : MonoBehaviour
    {
        public Transform Target { get; set; }
        private Vector3 velocity;

        private void LateUpdate()
        {
            if (Target == null) return;
            Vector3 desired = new(Target.position.x + 0.15f, Mathf.Clamp(Target.position.y + 0.45f, -0.15f, 1.05f), -10f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.20f);
        }
    }
}
