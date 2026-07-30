using System.Collections;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class WhaleLaneSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 3;
        [SerializeField] private bool spawnOnStart = false;
        [SerializeField, Min(0.05f)] private float scale = 0.72f;

        private GameObject spawnedWhale;

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (spawnOnStart)
                SpawnWhale();
        }

        [ContextMenu("Spawn Whale")]
        public void SpawnWhale(bool spawnAtSectionEdge = false)
        {
            if (spawnedWhale != null)
                return;

            Sprite[] frames = Resources.LoadAll<Sprite>("Whales/whale_move")
                .OrderBy(sprite => sprite.name)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError("WhaleLaneSpawner could not load Resources/Whales/whale_move.", this);
                return;
            }

            spawnedWhale = new GameObject("Whale - Inter-Wave Swimmer");
            spawnedWhale.transform.SetParent(transform, false);
            spawnedWhale.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = spawnedWhale.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];

            spawnedWhale.AddComponent<InterWaveRenderItem>();
            WhaleSpriteAnimation animation = spawnedWhale.AddComponent<WhaleSpriteAnimation>();
            animation.SetFrames(frames);

            WhaleLaneSwimmer swimmer = spawnedWhale.AddComponent<WhaleLaneSwimmer>();
            swimmer.Initialise(startingLane, spawnAtSectionEdge);
        }
    }
}
