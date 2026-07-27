using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Spawns one animated heart in the middle gap between the independent waves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeartLaneSpawner : MonoBehaviour
    {
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private Vector2 scaleRange = new(0.72f, 0.92f);

        private GameObject spawnedHeart;

        private IEnumerator Start()
        {
            yield return null;
            yield return null;

            if (spawnOnStart)
                SpawnHeart();
        }

        [ContextMenu("Spawn Animated Heart")]
        public void SpawnHeart()
        {
            if (spawnedHeart != null)
                return;

            List<PixelWaterGPU> layers = FindObjectsByType<PixelWaterGPU>(FindObjectsSortMode.None)
                .Where(layer => layer != null)
                .OrderBy(layer => layer.IndependentLayerIndex)
                .ToList();

            if (layers.Count < 2)
            {
                Debug.LogError("HeartLaneSpawner requires at least two independent water layers.", this);
                return;
            }

            int laneCount = layers.Count - 1;
            int middleLane = Mathf.Clamp(laneCount / 2, 0, laneCount - 1);

            spawnedHeart = new GameObject("Animated Heart - Middle Inter-Wave Lane");
            spawnedHeart.transform.SetParent(transform, false);
            spawnedHeart.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);

            SpriteRenderer renderer = spawnedHeart.AddComponent<SpriteRenderer>();
            renderer.sprite = HeartSpriteAnimation.LoadFirstFrame();

            InterWaveRenderItem renderItem = spawnedHeart.AddComponent<InterWaveRenderItem>();
            renderItem.SetLane(middleLane);

            Rigidbody2D body = spawnedHeart.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            CircleCollider2D trigger = spawnedHeart.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.28f;

            spawnedHeart.AddComponent<HeartSpriteAnimation>();
            HeartLaneDrifter drifter = spawnedHeart.AddComponent<HeartLaneDrifter>();
            drifter.Initialise(middleLane);
        }
    }
}
