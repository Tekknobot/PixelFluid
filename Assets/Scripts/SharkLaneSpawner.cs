using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Spawns exactly one animated Shark prefab after the independent wave layers
    /// have finished constructing. No procedural/random item population is used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SharkLaneSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject sharkPrefab;
        [SerializeField, Min(0)] private int startingLane = 2;
        [SerializeField] private bool spawnOnStart = false;

        private GameObject spawnedShark;

        private IEnumerator Start()
        {
            // Independent PixelWaterGPU clones are created during startup.
            yield return null;
            yield return null;

            if (spawnOnStart)
                SpawnShark();
        }

        [ContextMenu("Spawn Shark")]
        public void SpawnShark(bool spawnAtSectionEdge = false)
        {
            if (spawnedShark != null)
                return;

            GameObject prefab = sharkPrefab;
            if (prefab == null)
                prefab = Resources.Load<GameObject>("Shark");

            if (prefab == null)
            {
                Debug.LogError(
                    "SharkLaneSpawner could not load Resources/Shark.prefab.",
                    this);
                return;
            }

            // The Shark prefab can retain an obsolete/broken Animator Controller.
            // Clear it on the loaded prefab before Instantiate so Unity does not
            // validate and log the invalid controller during cloning. Sharks use
            // SharkSpriteAnimation, not Mecanim, for all runtime animation.
            Animator prefabAnimator = prefab.GetComponent<Animator>();
            if (prefabAnimator != null)
            {
                prefabAnimator.runtimeAnimatorController = null;
                prefabAnimator.enabled = false;
            }

            spawnedShark = Instantiate(prefab, transform);
            spawnedShark.name = "Shark - Inter-Wave Swimmer";

            Animator spawnedAnimator = spawnedShark.GetComponent<Animator>();
            if (spawnedAnimator != null)
                Destroy(spawnedAnimator);

            SharkLaneSwimmer swimmer = spawnedShark.GetComponent<SharkLaneSwimmer>();
            if (swimmer == null)
                swimmer = spawnedShark.AddComponent<SharkLaneSwimmer>();

            swimmer.Initialise(startingLane, spawnAtSectionEdge);
        }
    }
}
