using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class SodaCanSpawner : MonoBehaviour
    {
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private Vector2 scaleRange = new(0.29f, 0.36f);
        [SerializeField, Min(0f)] private float respawnDelay = 4f;
        private GameObject spawnedCan;

        private IEnumerator Start()
        {
            yield return null; yield return null;
            if (spawnOnStart) SpawnCan();
        }

        public void SpawnCan()
        {
            if (spawnedCan != null) return;
            List<PixelWaterGPU> layers = EndlessWaveSections.LayersNearest(transform.position.x);
            if (layers.Count < 2) return;
            int lane = Random.Range(0, layers.Count - 1);
            spawnedCan = new GameObject($"Soda Can - Lane {lane + 1}");
            spawnedCan.transform.SetParent(transform, false);
            spawnedCan.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
            SpriteRenderer r = spawnedCan.AddComponent<SpriteRenderer>();
            r.sprite = Resources.Load<Sprite>("Items/soda_can");
            spawnedCan.AddComponent<InterWaveRenderItem>().SetLane(lane);
            Rigidbody2D rb = spawnedCan.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0f;
            CircleCollider2D c = spawnedCan.AddComponent<CircleCollider2D>();
            c.isTrigger = true; c.radius = 0.3f;
            spawnedCan.AddComponent<SodaCanPickup>().Initialise(lane, this);
        }

        public void NotifyCollected(GameObject can)
        {
            if (can == spawnedCan) spawnedCan = null;
            StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            SpawnCan();
        }
    }
}
