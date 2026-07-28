using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class GiantSquidLaneSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 4;
        [SerializeField] private bool spawnOnStart = false;
        [SerializeField, Min(0.05f)] private float scale = 0.52f;

        private GameObject spawnedSquid;

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (spawnOnStart)
                SpawnSquid();
        }

        [ContextMenu("Spawn Giant Squid")]
        public void SpawnSquid()
        {
            if (spawnedSquid != null)
                return;

            Sprite[] frames = Resources.LoadAll<Sprite>("Squid/giant_squid_move");
            if (frames == null || frames.Length == 0)
            {
                Debug.LogError("GiantSquidLaneSpawner could not load Resources/Squid/giant_squid_move.", this);
                return;
            }

            spawnedSquid = new GameObject("Giant Squid - Inter-Wave Predator");
            spawnedSquid.transform.SetParent(transform, false);
            spawnedSquid.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = spawnedSquid.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];

            spawnedSquid.AddComponent<InterWaveRenderItem>();
            spawnedSquid.AddComponent<GiantSquidSpriteAnimation>();
            GiantSquidLaneSwimmer swimmer = spawnedSquid.AddComponent<GiantSquidLaneSwimmer>();
            swimmer.Initialise(startingLane);
        }
    }
}
