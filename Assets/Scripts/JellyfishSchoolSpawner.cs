using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class JellyfishSchoolSpawner : MonoBehaviour
    {
        private enum FormationStyle
        {
            LooseCloud,
            HorizontalRibbon,
            VerticalColumn,
            DiagonalTrail
        }

        [SerializeField] private bool spawnOnStart = false;
        [SerializeField, Range(3, 14)] private int schoolSize = 7;
        [Tooltip("Used only when randomiseLane is disabled.")]
        [SerializeField, Min(0)] private int startingLane = 2;
        [SerializeField] private bool randomiseLane = true;
        [SerializeField, Min(0.05f)] private float scale = 0.72f;
        [SerializeField] private Vector2 schoolSpread = new(1.25f, 0.42f);

        [Header("Respawning")]
        [SerializeField] private bool respawnAfterDefeat = true;
        [SerializeField, Min(0f)] private float minimumRespawnDelay = 5f;
        [SerializeField, Min(0f)] private float maximumRespawnDelay = 8f;

        private readonly List<GameObject> spawned = new();
        private Coroutine respawnRoutine;
        private JellyfishSchoolController controller;
        private bool quitting;

        private void Awake()
        {
            controller = GetComponent<JellyfishSchoolController>();
            if (controller == null)
                controller = gameObject.AddComponent<JellyfishSchoolController>();
            controller.SetOwner(this);
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (spawnOnStart) SpawnSchool();
        }

        private void OnApplicationQuit() => quitting = true;

        [ContextMenu("Spawn Jellyfish School")]
        public void SpawnSchool()
        {
            spawned.RemoveAll(item => item == null);
            if (spawned.Count > 0) return;

            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }

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

            IReadOnlyList<PixelWaterGPU> localLayers =
                EndlessWaveSections.LayersNearest(transform.position.x);
            int laneCount = Mathf.Max(1, localLayers.Count - 1);
            int chosenLane = randomiseLane
                ? Random.Range(0, laneCount)
                : Mathf.Clamp(startingLane, 0, laneCount - 1);

            float sharedDirection = Random.value < 0.5f ? -1f : 1f;
            JellyfishSchoolController.TravelStyle travelStyle =
                (JellyfishSchoolController.TravelStyle)Random.Range(
                    0,
                    System.Enum.GetValues(typeof(JellyfishSchoolController.TravelStyle)).Length);
            controller.SetOwner(this);
            controller.Initialise(chosenLane, sharedDirection, travelStyle);

            FormationStyle formation = (FormationStyle)Random.Range(0, 4);
            for (int i = 0; i < schoolSize; i++)
            {
                GameObject jelly = new($"Jellyfish {i + 1}");
                jelly.transform.SetParent(transform, true);
                jelly.transform.localScale = Vector3.one * scale * Random.Range(0.82f, 1.18f);

                SpriteRenderer renderer = jelly.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[i % frames.Length];
                jelly.AddComponent<InterWaveRenderItem>();

                Vector2 offset = BuildFormationOffset(formation, i, schoolSize);
                JellyfishSwimmer swimmer = jelly.AddComponent<JellyfishSwimmer>();
                swimmer.Initialise(controller, offset, frames, i * 0.075f);
                spawned.Add(jelly);
            }
        }

        public void NotifyJellyfishRemoved(GameObject jellyfish)
        {
            spawned.RemoveAll(item => item == null || ReferenceEquals(item, jellyfish));

            if (quitting || !isActiveAndEnabled || spawned.Count > 0 || !respawnAfterDefeat)
                return;

            if (respawnRoutine == null)
                respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            float low = Mathf.Max(0f, minimumRespawnDelay);
            float high = Mathf.Max(low, maximumRespawnDelay);
            yield return new WaitForSeconds(Random.Range(low, high));
            respawnRoutine = null;

            // Re-resolve the section at the holder's current recycled position.
            // This allows a defeated school to return even if its section was
            // recycled while the respawn timer was running.
            SpawnSchool();
        }

        private Vector2 BuildFormationOffset(FormationStyle formation, int index, int count)
        {
            float centred = index - (count - 1) * 0.5f;
            switch (formation)
            {
                case FormationStyle.HorizontalRibbon:
                    return new Vector2(
                        centred * (schoolSpread.x * 2f / Mathf.Max(1, count - 1)),
                        Random.Range(-schoolSpread.y * 0.35f, schoolSpread.y * 0.35f));

                case FormationStyle.VerticalColumn:
                    return new Vector2(
                        Random.Range(-schoolSpread.x * 0.25f, schoolSpread.x * 0.25f),
                        centred * (schoolSpread.y * 2f / Mathf.Max(1, count - 1)));

                case FormationStyle.DiagonalTrail:
                    float t = count <= 1 ? 0f : index / (float)(count - 1);
                    return new Vector2(
                        Mathf.Lerp(-schoolSpread.x, schoolSpread.x, t),
                        Mathf.Lerp(-schoolSpread.y, schoolSpread.y, t) + Random.Range(-0.08f, 0.08f));

                default:
                    return new Vector2(
                        Random.Range(-schoolSpread.x, schoolSpread.x),
                        Random.Range(-schoolSpread.y, schoolSpread.y));
            }
        }
    }
}
