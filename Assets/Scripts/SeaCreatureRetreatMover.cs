using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Takes control of a hostile sea creature after a boss is defeated and
    /// carries it toward the nearest visible world edge before despawning it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeaCreatureRetreatMover : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float retreatSpeed = 5.5f;
        [SerializeField, Min(0.1f)] private float offscreenPadding = 2.5f;

        private float direction;
        private float despawnX;
        private SpriteRenderer[] renderers;

        public void Begin(float speedMultiplier = 1f)
        {
            Camera camera = Camera.main;
            float left = transform.position.x - 12f;
            float right = transform.position.x + 12f;

            if (camera != null)
            {
                float z = Mathf.Abs(camera.transform.position.z - transform.position.z);
                left = camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, z)).x;
                right = camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, z)).x;
            }

            float distanceLeft = Mathf.Abs(transform.position.x - left);
            float distanceRight = Mathf.Abs(right - transform.position.x);
            direction = distanceLeft <= distanceRight ? -1f : 1f;
            despawnX = direction < 0f ? left - offscreenPadding : right + offscreenPadding;
            retreatSpeed *= Mathf.Max(0.1f, speedMultiplier);

            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (Collider2D hitbox in GetComponentsInChildren<Collider2D>(true))
                hitbox.enabled = false;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            enabled = true;
        }

        private void Update()
        {
            Vector3 position = transform.position;
            position.x += direction * retreatSpeed * Time.deltaTime;
            transform.position = position;

            if (renderers != null)
            {
                bool faceLeft = direction < 0f;
                foreach (SpriteRenderer renderer in renderers)
                    if (renderer != null) renderer.flipX = faceLeft;
            }

            if ((direction < 0f && position.x <= despawnX) ||
                (direction > 0f && position.x >= despawnX))
            {
                Destroy(gameObject);
            }
        }
    }
}
