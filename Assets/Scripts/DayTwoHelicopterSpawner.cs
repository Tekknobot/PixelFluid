using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class DayTwoHelicopterSpawner : MonoBehaviour
    {
        private GameObject spawned;

        [ContextMenu("Spawn Day 2 Helicopter")]
        public void SpawnHelicopter()
        {
            if (spawned != null || FindFirstObjectByType<DayTwoHelicopterController>() != null) return;
            spawned = new GameObject("Day 2 Missile Helicopter");
            spawned.AddComponent<SpriteRenderer>();
            spawned.AddComponent<BoxCollider2D>();
            spawned.AddComponent<Rigidbody2D>();
            spawned.AddComponent<DayTwoHelicopterController>();
        }
    }
}
