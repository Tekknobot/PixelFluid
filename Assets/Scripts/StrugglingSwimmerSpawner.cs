using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class StrugglingSwimmerSpawner : MonoBehaviour
    {
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private Vector2 scaleRange = new(0.42f, 0.52f);
        [SerializeField, Min(0f)] private float respawnDelay = 7f;

        private GameObject spawnedSwimmer;
        private Coroutine respawnRoutine;

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (spawnOnStart)
                SpawnSwimmer();
        }

        [ContextMenu("Spawn Struggling Swimmer")]
        public void SpawnSwimmer()
        {
            if (spawnedSwimmer != null)
                return;

            List<PixelWaterGPU> layers = FindObjectsByType<PixelWaterGPU>(FindObjectsSortMode.None)
                .Where(layer => layer != null)
                .OrderBy(layer => layer.IndependentLayerIndex)
                .ToList();

            if (layers.Count < 2)
            {
                Debug.LogError("StrugglingSwimmerSpawner requires at least two independent water layers.", this);
                return;
            }

            int lane = Random.Range(0, layers.Count - 1);
            spawnedSwimmer = new GameObject($"Struggling Swimmer - Lane {lane + 1}");
            spawnedSwimmer.transform.SetParent(transform, false);
            spawnedSwimmer.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);

            SpriteRenderer renderer = spawnedSwimmer.AddComponent<SpriteRenderer>();
            renderer.sprite = StrugglingSwimmerAnimation.LoadFirstFrame();
            ApplyWaterBlendMaterial(renderer);

            InterWaveRenderItem renderItem = spawnedSwimmer.AddComponent<InterWaveRenderItem>();
            renderItem.SetLane(lane);

            Rigidbody2D body = spawnedSwimmer.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            CircleCollider2D trigger = spawnedSwimmer.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.32f;

            spawnedSwimmer.AddComponent<StrugglingSwimmerAnimation>();
            StrugglingSwimmerDrifter drifter = spawnedSwimmer.AddComponent<StrugglingSwimmerDrifter>();
            drifter.Initialise(lane, this);
        }

        private static void ApplyWaterBlendMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
                return;

            // Loading a material asset from Resources keeps a direct shader reference,
            // preventing the custom shader from being stripped from player builds.
            Material template = Resources.Load<Material>("Materials/SwimmerWaterBlend");
            if (template != null)
            {
                renderer.material = new Material(template)
                {
                    name = "Runtime Swimmer Water Blend"
                };
                return;
            }

            // Editor fallback in case the Resources material was removed accidentally.
            Shader shader = Shader.Find("PixelOcean/SwimmerWaterBlend");
            if (shader == null)
            {
                Debug.LogError(
                    "The swimmer water-blend material and shader could not be loaded. " +
                    "Expected Resources/Materials/SwimmerWaterBlend.mat.",
                    renderer);
                return;
            }

            renderer.material = new Material(shader)
            {
                name = "Runtime Swimmer Water Blend Fallback"
            };
        }

        public void NotifySaved(GameObject swimmer)
        {
            if (swimmer != null && swimmer == spawnedSwimmer)
                spawnedSwimmer = null;

            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);

            respawnRoutine = StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            respawnRoutine = null;
            SpawnSwimmer();
        }
    }
}
