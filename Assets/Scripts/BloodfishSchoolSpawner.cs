using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class BloodfishSchoolSpawner : MonoBehaviour
    {
        private enum FormationStyle { TightCloud, HuntingLine, Arrowhead, Scatter }

        [SerializeField] private bool spawnOnStart = false;
        [SerializeField, Range(4, 18)] private int schoolSize = 9;
        [SerializeField, Min(0)] private int startingLane = 2;
        [SerializeField] private bool randomiseLane = true;
        [SerializeField, Min(0.05f)] private float scale = 0.58f;
        [SerializeField] private Vector2 schoolSpread = new(0.95f, 0.34f);
        [Header("Respawning")]
        [SerializeField] private bool respawnAfterDefeat = true;
        [SerializeField, Min(0f)] private float minimumRespawnDelay = 7f;
        [SerializeField, Min(0f)] private float maximumRespawnDelay = 12f;

        private readonly List<GameObject> spawned = new();
        private Coroutine respawnRoutine;
        private BloodfishSchoolController controller;
        private bool quitting;

        private void Awake()
        {
            controller = GetComponent<BloodfishSchoolController>();
            if (controller == null) controller = gameObject.AddComponent<BloodfishSchoolController>();
            controller.SetOwner(this);
        }

        private IEnumerator Start() { yield return null; yield return null; if (spawnOnStart) SpawnSchool(); }
        private void OnApplicationQuit() => quitting = true;

        [ContextMenu("Spawn Bloodfish School")]
        public void SpawnSchool()
        {
            spawned.RemoveAll(item => item == null);
            if (spawned.Count > 0) return;
            if (respawnRoutine != null) { StopCoroutine(respawnRoutine); respawnRoutine = null; }

            Sprite[] moveFrames = SliceSheet(Resources.Load<Texture2D>("Bloodfish/bloodfish_move"), "bloodfish_move");
            Sprite[] attackFrames = SliceSheet(Resources.Load<Texture2D>("Bloodfish/bloodfish_attack"), "bloodfish_attack");
            if (moveFrames.Length == 0 || attackFrames.Length == 0)
            {
                Debug.LogError("Could not load Bloodfish move/attack sheets from Resources/Bloodfish.", this);
                return;
            }

            IReadOnlyList<PixelWaterGPU> localLayers = EndlessWaveSections.LayersNearest(transform.position.x);
            int laneCount = Mathf.Max(1, localLayers.Count - 1);
            int chosenLane = randomiseLane ? Random.Range(0, laneCount) : Mathf.Clamp(startingLane, 0, laneCount - 1);
            float sharedDirection = Random.value < 0.5f ? -1f : 1f;
            BloodfishSchoolController.TravelStyle style = (BloodfishSchoolController.TravelStyle)Random.Range(0, 3);
            controller.SetOwner(this);
            controller.Initialise(chosenLane, sharedDirection, style);

            FormationStyle formation = (FormationStyle)Random.Range(0, 4);
            for (int i = 0; i < schoolSize; i++)
            {
                GameObject fish = new($"Bloodfish {i + 1}");
                fish.transform.SetParent(transform, true);
                fish.transform.localScale = Vector3.one * scale * Random.Range(0.86f, 1.12f);
                SpriteRenderer renderer = fish.AddComponent<SpriteRenderer>();
                renderer.sprite = moveFrames[i % moveFrames.Length];
                fish.AddComponent<InterWaveRenderItem>();
                BloodfishSwimmer swimmer = fish.AddComponent<BloodfishSwimmer>();
                swimmer.Initialise(controller, BuildFormationOffset(formation, i, schoolSize), moveFrames, attackFrames, i * 0.06f);
                spawned.Add(fish);
            }
        }

        public void NotifyBloodfishRemoved(GameObject fish)
        {
            spawned.RemoveAll(item => item == null || ReferenceEquals(item, fish));
            if (quitting || !isActiveAndEnabled || spawned.Count > 0 || !respawnAfterDefeat) return;
            if (respawnRoutine == null) respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            float low = Mathf.Max(0f, minimumRespawnDelay);
            float high = Mathf.Max(low, maximumRespawnDelay);
            yield return new WaitForSeconds(Random.Range(low, high));
            respawnRoutine = null;
            SpawnSchool();
        }

        private static Sprite[] SliceSheet(Texture2D sheet, string prefix)
        {
            if (sheet == null) return System.Array.Empty<Sprite>();
            const int frameSize = 32;
            int frameCount = Mathf.Max(1, sheet.width / frameSize);
            Sprite[] frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = Sprite.Create(sheet, new Rect(i * frameSize, 0, frameSize, frameSize), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{prefix}_{i:00}";
            }
            return frames;
        }

        private Vector2 BuildFormationOffset(FormationStyle formation, int index, int count)
        {
            float centred = index - (count - 1) * 0.5f;
            switch (formation)
            {
                case FormationStyle.HuntingLine:
                    return new Vector2(centred * (schoolSpread.x * 2f / Mathf.Max(1, count - 1)), Random.Range(-0.08f, 0.08f));
                case FormationStyle.Arrowhead:
                    float row = Mathf.Floor(Mathf.Sqrt(index));
                    float side = index % 2 == 0 ? -1f : 1f;
                    return new Vector2(-row * 0.22f, side * row * 0.15f);
                case FormationStyle.Scatter:
                    return new Vector2(Random.Range(-schoolSpread.x, schoolSpread.x), Random.Range(-schoolSpread.y, schoolSpread.y));
                default:
                    return new Vector2(Random.Range(-schoolSpread.x * 0.65f, schoolSpread.x * 0.65f), Random.Range(-schoolSpread.y, schoolSpread.y));
            }
        }
    }
}
