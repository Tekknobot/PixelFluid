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
            Whale,
            JellyfishSchool
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
        [SerializeField] private bool includeJellyfishSchools = true;
        [SerializeField] private bool includeGodzilla = true;

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

            // Ordinary pickups/NPCs still appear in every section. The three
            // major sea creatures are shuffled and distributed exactly one per
            // section, so no section receives a shark, squid and whale together.
            int jellyfishSectionIndex = Random.Range(0, centres.Count);
            List<SpawnKind> majorCreatures = BuildMajorCreaturePool();
            Shuffle(majorCreatures);

            for (int sectionIndex = 0; sectionIndex < centres.Count; sectionIndex++)
            {
                float sectionCentreX = centres[sectionIndex];

                foreach (SpawnKind kind in enabledKinds)
                {
                    if (IsMajorCreature(kind))
                        continue;

                    if (kind == SpawnKind.JellyfishSchool &&
                        sectionIndex != jellyfishSectionIndex)
                        continue;

                    CreateSectionSpawner(sectionIndex, sectionCentreX, kind);
                }

                if (sectionIndex < majorCreatures.Count)
                {
                    CreateSectionSpawner(
                        sectionIndex,
                        sectionCentreX,
                        majorCreatures[sectionIndex],
                        spawnAtSectionEdge: true);
                }
            }

            if (includeGodzilla && FindFirstObjectByType<GodzillaLaneSwimmer>() == null)
            {
                GameObject godzillaHolder = new("Unique Godzilla Spawner");
                godzillaHolder.transform.SetParent(transform, false);
                godzillaHolder.AddComponent<GodzillaLaneSpawner>().SpawnGodzilla();
                sectionSpawners.Add(godzillaHolder);
            }

            if (FindFirstObjectByType<OceanItemSpawner>() == null)
            {
                GameObject oceanItems = new("All Ocean Items");
                oceanItems.transform.SetParent(transform, false);
                oceanItems.AddComponent<OceanItemSpawner>();
            }

            hasSpawned = true;

            int ordinaryKindCount = enabledKinds.Count -
                (enabledKinds.Contains(SpawnKind.JellyfishSchool) ? 1 : 0);
            int totalSpawned = centres.Count * ordinaryKindCount +
                (enabledKinds.Contains(SpawnKind.JellyfishSchool) ? 1 : 0);

            Debug.Log(
                $"Spawned the section population with one jellyfish school total. " +
                $"Spawner count: {totalSpawned}.",
                this);
        }

        private void CreateSectionSpawner(int sectionIndex, float sectionCentreX, SpawnKind kind, bool spawnAtSectionEdge = false)
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
                    holder.AddComponent<SharkLaneSpawner>().SpawnShark(spawnAtSectionEdge);
                    break;

                case SpawnKind.GiantSquid:
                    holder.AddComponent<GiantSquidLaneSpawner>().SpawnSquid(spawnAtSectionEdge);
                    break;

                case SpawnKind.Whale:
                    holder.AddComponent<WhaleLaneSpawner>().SpawnWhale(spawnAtSectionEdge);
                    break;

                case SpawnKind.JellyfishSchool:
                    holder.AddComponent<JellyfishSchoolSpawner>().SpawnSchool();
                    break;
            }
        }


        private List<SpawnKind> BuildMajorCreaturePool()
        {
            List<SpawnKind> pool = new();
            if (includeSharks) pool.Add(SpawnKind.Shark);
            if (includeGiantSquids) pool.Add(SpawnKind.GiantSquid);
            if (includeWhales) pool.Add(SpawnKind.Whale);
            return pool;
        }

        private static bool IsMajorCreature(SpawnKind kind) =>
            kind == SpawnKind.Shark ||
            kind == SpawnKind.GiantSquid ||
            kind == SpawnKind.Whale;

        private static void Shuffle<T>(IList<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
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
            if (includeJellyfishSchools) pool.Add(SpawnKind.JellyfishSchool);
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
            DisableAll<GodzillaLaneSpawner>();
            DisableAll<JellyfishSchoolSpawner>();
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
