using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class StingrayLaneSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 3;
        [SerializeField] private bool randomiseLane = true;
        [SerializeField] private bool spawnOnStart;
        [SerializeField, Min(0.05f)] private float scale = 0.82f;

        private GameObject spawned;

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (spawnOnStart) SpawnStingray();
        }

        [ContextMenu("Spawn Stingray")]
        public void SpawnStingray(bool spawnAtSectionEdge = false)
        {
            if (spawned != null) return;

            Texture2D sheet = Resources.Load<Texture2D>("Stingray/stingray_move");
            Sprite[] frames = SliceSheet(sheet);
            if (frames.Length == 0)
            {
                Debug.LogError("Missing Resources/Stingray/stingray_move.", this);
                return;
            }

            int lane = startingLane;
            var layers = EndlessWaveSections.LayersNearest(transform.position.x);
            int laneCount = Mathf.Max(1, layers.Count - 1);
            if (randomiseLane) lane = Random.Range(0, laneCount);
            else lane = Mathf.Clamp(lane, 0, laneCount - 1);

            spawned = new GameObject("Stingray - Day 2 Charger");
            spawned.transform.SetParent(transform, false);
            spawned.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = spawned.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            spawned.AddComponent<InterWaveRenderItem>();

            StingrayLaneSwimmer swimmer = spawned.AddComponent<StingrayLaneSwimmer>();
            swimmer.Initialise(lane, frames, spawnAtSectionEdge);
        }

        private static Sprite[] SliceSheet(Texture2D sheet)
        {
            if (sheet == null) return System.Array.Empty<Sprite>();
            const int frameSize = 64;
            int frameCount = Mathf.Max(1, sheet.width / frameSize);
            Sprite[] frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = Sprite.Create(
                    sheet,
                    new Rect(i * frameSize, 0, frameSize, frameSize),
                    new Vector2(0.5f, 0.5f),
                    32f,
                    0,
                    SpriteMeshType.FullRect);
                frames[i].name = $"stingray_move_{i:00}";
            }
            return frames;
        }
    }
}
