using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Scene-wide startup population controller.
    ///
    /// Waits for the horizontal ocean sections, then creates one of every
    /// enabled item and enemy type inside each section.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class SectionPopulationSpawner : MonoBehaviour
    {
        private enum SpawnKind
        {
            Heart,
            SodaCan,
            StrugglingSwimmer,
            Shark,
            GiantSquid,
            Whale
        }

        [Header("Startup")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField, Min(0.1f)] private float sectionReadyTimeout = 10f;

        [Header("Population Pool")]
        [SerializeField] private bool includeHearts = true;
        [SerializeField] private bool includeSodaCans = true;
        [SerializeField] private bool includeStrugglingSwimmers = true;
        [SerializeField] private bool includeSharks = true;
        [SerializeField] private bool includeGiantSquids = true;
        [SerializeField] private bool includeWhales = true;

        [Header("Selection")]
        [Tooltip("Prevents the same type from being selected twice until every enabled type has been used.")]
        [SerializeField] private bool avoidImmediateDuplicates = true;

        private readonly List<GameObject> sectionSpawners = new();
        private bool hasSpawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAutomatically()
        {
            if (FindFirstObjectByType<SectionPopulationSpawner>() != null)
                return;

            GameObject controller = new("Section Population Spawner");
            DontDestroyOnLoad(controller);
            controller.AddComponent<SectionPopulationSpawner>();
        }

        private void Awake()
        {
            DisableLegacyStartupSpawners();
        }

        private IEnumerator Start()
        {
            // Scene objects may not have completed Awake when this persistent
            // controller is created before the scene. Repeat once before any
            // normal-priority Start methods can run.
            DisableLegacyStartupSpawners();

            if (!spawnOnStart)
                yield break;

            float deadline = Time.realtimeSinceStartup + sectionReadyTimeout;
            while ((EndlessWaveSections.Instance == null ||
                    !EndlessWaveSections.Instance.IsReady) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            SpawnOnePerSection();
        }

        [ContextMenu("Spawn Each Enabled Type Per Section")]
        public void SpawnOnePerSection()
        {
            if (hasSpawned)
                return;

            EndlessWaveSections sections = EndlessWaveSections.Instance;
            if (sections == null || !sections.IsReady)
            {
                Debug.LogWarning(
                    "SectionPopulationSpawner could not spawn because the endless wave sections are not ready.",
                    this);
                return;
            }

            IReadOnlyList<float> centres = sections.GetSectionCentres();
            if (centres.Count == 0)
            {
                Debug.LogWarning("No ocean section centres were found.", this);
                return;
            }

            List<SpawnKind> enabledKinds = BuildSpawnPool();
            if (enabledKinds.Count == 0)
            {
                Debug.LogWarning("The section population pool is empty.", this);
                return;
            }

            // Spawn every enabled type once inside every section.
            for (int sectionIndex = 0; sectionIndex < centres.Count; sectionIndex++)
            {
                float sectionCentreX = centres[sectionIndex];

                foreach (SpawnKind kind in enabledKinds)
                {
                    CreateSectionSpawner(
                        sectionIndex,
                        sectionCentreX,
                        kind);
                }
            }

            hasSpawned = true;

            int totalSpawned = centres.Count * enabledKinds.Count;

            Debug.Log(
                $"Spawned {enabledKinds.Count} objects in each of " +
                $"{centres.Count} sections. Total: {totalSpawned}.",
                this);
        }

        private void CreateSectionSpawner(int sectionIndex, float sectionCentreX, SpawnKind kind)
        {
            GameObject holder = new($"Section {sectionIndex + 1} Population - {kind}");
            holder.transform.SetParent(transform, false);
            holder.transform.position = new Vector3(sectionCentreX, 0f, 0f);
            sectionSpawners.Add(holder);

            switch (kind)
            {
                case SpawnKind.Heart:
                    holder.AddComponent<HeartLaneSpawner>().SpawnHeart();
                    break;

                case SpawnKind.SodaCan:
                    holder.AddComponent<SodaCanSpawner>().SpawnCan();
                    break;

                case SpawnKind.StrugglingSwimmer:
                    holder.AddComponent<StrugglingSwimmerSpawner>().SpawnSwimmer();
                    break;

                case SpawnKind.Shark:
                    holder.AddComponent<SharkLaneSpawner>().SpawnShark();
                    break;

                case SpawnKind.GiantSquid:
                    holder.AddComponent<GiantSquidLaneSpawner>().SpawnSquid();
                    break;

                case SpawnKind.Whale:
                    holder.AddComponent<WhaleLaneSpawner>().SpawnWhale();
                    break;
            }
        }

        private List<SpawnKind> BuildSpawnPool()
        {
            List<SpawnKind> pool = new();
            if (includeHearts) pool.Add(SpawnKind.Heart);
            if (includeSodaCans) pool.Add(SpawnKind.SodaCan);
            if (includeStrugglingSwimmers) pool.Add(SpawnKind.StrugglingSwimmer);
            if (includeSharks) pool.Add(SpawnKind.Shark);
            if (includeGiantSquids) pool.Add(SpawnKind.GiantSquid);
            if (includeWhales) pool.Add(SpawnKind.Whale);
            return pool;
        }

        private static void DisableLegacyStartupSpawners()
        {
            DisableAll<GiantSquidLaneSpawner>();
            DisableAll<HeartLaneSpawner>();
            DisableAll<SharkLaneSpawner>();
            DisableAll<RandomInterWaveItemSpawner>();
            DisableAll<SodaCanSpawner>();
            DisableAll<StrugglingSwimmerSpawner>();
            DisableAll<WhaleLaneSpawner>();
        }

        private static void DisableAll<T>() where T : Behaviour
        {
            T[] spawners = FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (T spawner in spawners)
            {
                if (spawner != null)
                    spawner.enabled = false;
            }
        }
    }
}
