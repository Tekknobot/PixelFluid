using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class HeartLaneSpawner : MonoBehaviour
    {
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private Vector2 scaleRange = new(0.72f, 0.92f);
        [SerializeField, Min(0f)] private float respawnDelay = 2.5f;
        [SerializeField] private bool chooseRandomLaneAfterPickup = true;

        private GameObject spawnedHeart;
        private Coroutine respawnRoutine;

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (spawnOnStart) SpawnHeart();
        }

        [ContextMenu("Spawn Animated Heart")]
        public void SpawnHeart()
        {
            if (spawnedHeart != null) return;

            List<PixelWaterGPU> layers = EndlessWaveSections.LayersNearest(transform.position.x);
            if (layers.Count < 2)
            {
                Debug.LogError("HeartLaneSpawner requires at least two independent water layers.", this);
                return;
            }

            int laneCount = layers.Count - 1;
            int lane = chooseRandomLaneAfterPickup ? Random.Range(0, laneCount) : Mathf.Clamp(laneCount / 2, 0, laneCount - 1);
            spawnedHeart = new GameObject($"Animated Heart - Lane {lane + 1}");
            spawnedHeart.transform.SetParent(transform, false);
            spawnedHeart.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);

            SpriteRenderer renderer = spawnedHeart.AddComponent<SpriteRenderer>();
            renderer.sprite = HeartSpriteAnimation.LoadFirstFrame();
            InterWaveRenderItem renderItem = spawnedHeart.AddComponent<InterWaveRenderItem>();
            renderItem.SetLane(lane);
            Rigidbody2D body = spawnedHeart.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            CircleCollider2D trigger = spawnedHeart.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.32f;
            spawnedHeart.AddComponent<HeartSpriteAnimation>();
            HeartLaneDrifter drifter = spawnedHeart.AddComponent<HeartLaneDrifter>();
            drifter.Initialise(lane, this);
        }

        public void NotifyHeartCollected(GameObject heart)
        {
            if (heart != null && heart == spawnedHeart) spawnedHeart = null;
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnHeart());
        }

        private IEnumerator RespawnHeart()
        {
            yield return new WaitForSeconds(respawnDelay);
            respawnRoutine = null;
            SpawnHeart();
        }
    }
}
