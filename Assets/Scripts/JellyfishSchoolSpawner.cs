using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class JellyfishSchoolSpawner : MonoBehaviour
    {
        [SerializeField] private bool spawnOnStart = false;
        [SerializeField, Range(3, 14)] private int schoolSize = 7;
        [SerializeField, Min(0)] private int startingLane = 2;
        [SerializeField, Min(0.05f)] private float scale = 0.72f;
        [SerializeField] private Vector2 schoolSpread = new(1.25f, 0.42f);

        private readonly List<GameObject> spawned = new();

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (spawnOnStart) SpawnSchool();
        }

        [ContextMenu("Spawn Jellyfish School")]
        public void SpawnSchool()
        {
            spawned.RemoveAll(item => item == null);
            if (spawned.Count > 0) return;

            Texture2D sheet = Resources.Load<Texture2D>("Jellyfish/jellyfish_move");
            if (sheet == null)
            {
                Debug.LogError("Could not load Resources/Jellyfish/jellyfish_move.", this);
                return;
            }

            const int frameSize = 32;
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
                frames[i].name = $"jellyfish_move_{i:00}";
            }

            float sharedDirection = Random.value < 0.5f ? -1f : 1f;
            JellyfishSchoolController controller = GetComponent<JellyfishSchoolController>();
            if (controller == null)
                controller = gameObject.AddComponent<JellyfishSchoolController>();
            controller.Initialise(startingLane, sharedDirection);

            for (int i = 0; i < schoolSize; i++)
            {
                GameObject jelly = new($"Jellyfish {i + 1}");
                jelly.transform.SetParent(transform, true);
                jelly.transform.localScale = Vector3.one * scale * Random.Range(0.88f, 1.12f);

                SpriteRenderer renderer = jelly.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[i % frames.Length];
                jelly.AddComponent<InterWaveRenderItem>();

                Vector2 offset = new(
                    Random.Range(-schoolSpread.x, schoolSpread.x),
                    Random.Range(-schoolSpread.y, schoolSpread.y));

                JellyfishSwimmer swimmer = jelly.AddComponent<JellyfishSwimmer>();
                swimmer.Initialise(controller, offset, frames, i * 0.075f);
                spawned.Add(jelly);
            }
        }
    }
}
