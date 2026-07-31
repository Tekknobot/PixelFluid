using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Turns the existing sandbox systems into one escalating surf-day run.
    /// Installed automatically, so no scene setup is required.
    /// </summary>
    [DefaultExecutionOrder(-12000)]
    [DisallowMultipleComponent]
    public sealed class SurfDayProgressionDirector : MonoBehaviour
    {
        public enum Chapter
        {
            Dawn,
            FirstRescue,
            DangerousWater,
            StrangeTide,
            Storm,
            FinalWave,
            Complete
        }

        [Header("Run Length")]
        [SerializeField, Min(10f)] private float rescueBeginsAt = 40f;
        [SerializeField, Min(20f)] private float dangerBeginsAt = 120f;
        [SerializeField, Min(30f)] private float strangeTideBeginsAt = 260f;
        [SerializeField, Min(40f)] private float stormBeginsAt = 430f;
        [SerializeField, Min(50f)] private float finalWaveBeginsAt = 600f;
        [SerializeField, Min(60f)] private float dayEndsAt = 720f;

        [Header("Objectives")]
        [SerializeField, Min(1)] private int rescuesRequired = 3;
        [SerializeField, Min(1)] private int finalSurvivalSeconds = 60;

        private readonly List<GameObject> progressionSpawners = new();
        private Chapter chapter;
        private float runTime;
        private int rescues;
        private float bannerUntil;
        private string banner = string.Empty;
        private string objective = string.Empty;
        private bool finalWaveStarted;
        private ProceduralRainSystem rain;
        private GUIStyle titleStyle;
        private GUIStyle objectiveStyle;
        private GUIStyle panelStyle;

        public Chapter CurrentChapter => chapter;
        public int Rescues => rescues;
        public float RunTime => runTime;
        public float DayDuration => dayEndsAt;
        public float NormalizedDayProgress => dayEndsAt > 0f ? Mathf.Clamp01(runTime / dayEndsAt) : 0f;
        public string CurrentObjective => objective;
        public string CurrentBanner => banner;
        public bool IsBannerVisible => Time.unscaledTime < bannerUntil && !string.IsNullOrEmpty(banner);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurfDayProgressionDirector>() != null)
                return;

            GameObject host = new("Surf Day Progression Director");
            DontDestroyOnLoad(host);
            host.AddComponent<SurfDayProgressionDirector>();
        }

        private void OnEnable() => StrugglingSwimmerDrifter.SwimmerSaved += OnSwimmerSaved;
        private void OnDisable() => StrugglingSwimmerDrifter.SwimmerSaved -= OnSwimmerSaved;

        private IEnumerator Start()
        {
            yield return BeginRun(false);
        }

        public IEnumerator RestartRunInPlace()
        {
            yield return BeginRun(true);
        }

        private IEnumerator BeginRun(bool clearPreviousRun)
        {
            if (clearPreviousRun)
            {
                ClearRunObjects();
                yield return null; // allow deferred Destroy calls to finish
            }

            float deadline = Time.realtimeSinceStartup + 12f;
            while ((EndlessWaveSections.Instance == null || !EndlessWaveSections.Instance.IsReady) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            runTime = 0f;
            rescues = 0;
            finalWaveStarted = false;
            chapter = Chapter.Dawn;
            banner = string.Empty;
            objective = string.Empty;
            bannerUntil = 0f;

            rain = FindFirstObjectByType<ProceduralRainSystem>();
            rain?.ClearRain();
            BeginChapter(Chapter.Dawn, "DAWN PATROL", "SURF. STAY ALIVE. LEARN THE WATER.");
            SpawnPickupSet();
            SpawnOceanItems(12);
            SpawnJellyfishEncounter("Dawn Jellyfish", 1);
            SpawnMajor<SharkLaneSpawner>("Early Shark", spawner => spawner.SpawnShark(true));
        }

        private void ClearRunObjects()
        {
            foreach (GameObject holder in progressionSpawners)
                if (holder != null) Destroy(holder);
            progressionSpawners.Clear();

            DestroyAll<SharkLaneSwimmer>();
            DestroyAll<GiantSquidLaneSwimmer>();
            DestroyAll<GodzillaLaneSwimmer>();
            DestroyAll<JellyfishSwimmer>();
            DestroyAll<WhaleLaneSwimmer>();
            DestroyAll<StrugglingSwimmerDrifter>();
            DestroyAll<RescuedSurferExit>();
            DestroyAll<OceanItemBehaviour>();
            DestroyAll<SodaCanPickup>();
            DestroyAll<SodaCanProjectile>();
            DestroyAll<AlienUfoController>();
            DestroyAll<BoomboxSurferSwimmer>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            foreach (T item in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item != null) Destroy(item.gameObject);
            }
        }

        private void Update()
        {
            if (chapter == Chapter.Complete || EndlessWaveSections.Instance == null)
                return;

            TinyWaveSurfer player = FindFirstObjectByType<TinyWaveSurfer>();
            if (player == null || player.IsDead)
                return;

            runTime += Time.deltaTime;

            if (runTime >= dayEndsAt && finalWaveStarted)
            {
                BeginChapter(Chapter.Complete, "DAY COMPLETE", "THE OCEAN LETS YOU GO. FOR NOW.");
                rain?.ClearRain();
                return;
            }

            if (runTime >= finalWaveBeginsAt && chapter < Chapter.FinalWave)
            {
                finalWaveStarted = true;
                BeginChapter(Chapter.FinalWave, "THE LAST WAVE", $"SURVIVE {finalSurvivalSeconds} SECONDS.");
                SpawnMajor<GodzillaLaneSpawner>("Final Godzilla", spawner => spawner.SpawnGodzilla());
                return;
            }

            if (runTime >= stormBeginsAt && chapter < Chapter.Storm)
            {
                BeginChapter(Chapter.Storm, "STORM FRONT", "KEEP MOVING. RESCUE ANYONE LEFT OUT THERE.");
                EnsureRain().SetSituation(ProceduralRainSystem.RainSituation.HeavyRain);
                SpawnMajor<GiantSquidLaneSpawner>("Storm Squid", spawner => spawner.SpawnSquid(true));
                SpawnJellyfishEncounter("Storm Jellyfish", 3);
                return;
            }

            if (runTime >= strangeTideBeginsAt && chapter < Chapter.StrangeTide)
            {
                BeginChapter(Chapter.StrangeTide, "STRANGE TIDE", "SOMETHING IS WATCHING THE WATER.");
                SpawnBoombox();
                SpawnUfo();
                SpawnJellyfishEncounter("Strange Tide Jellyfish", 2);
                SpawnMajor<WhaleLaneSpawner>("Strange Tide Whale", spawner => spawner.SpawnWhale(true));
                return;
            }

            if (runTime >= dangerBeginsAt && chapter < Chapter.DangerousWater)
            {
                BeginChapter(Chapter.DangerousWater, "DANGEROUS WATER", "SAVE 3 SWIMMERS. USE CANS TO FIGHT BACK.");
                SpawnMajor<GiantSquidLaneSpawner>("First Squid", spawner => spawner.SpawnSquid(true));
                SpawnMajor<SharkLaneSpawner>("Second Shark", spawner => spawner.SpawnShark(true));
                SpawnRescueSet(2);
                return;
            }

            if (runTime >= rescueBeginsAt && chapter < Chapter.FirstRescue)
            {
                BeginChapter(Chapter.FirstRescue, "DISTRESS CALL", "FIND AND SAVE THE STRUGGLING SWIMMER.");
                SpawnRescueSet(1);
            }

            if (chapter == Chapter.DangerousWater)
                objective = $"RESCUES  {rescues}/{rescuesRequired}";
            else if (chapter == Chapter.FinalWave)
                objective = $"SURVIVE  {Mathf.Max(0, Mathf.CeilToInt(dayEndsAt - runTime))}s";
        }

        private void OnSwimmerSaved()
        {
            rescues++;
            ShowBanner("SWIMMER SAVED", $"RESCUES  {rescues}/{rescuesRequired}", 2.5f);

            if (rescues < rescuesRequired)
                SpawnRescueSet(1);
            else if (chapter == Chapter.DangerousWater)
                objective = "RESCUES COMPLETE — SURVIVE THE CHANGING TIDE";
        }

        private void BeginChapter(Chapter next, string heading, string newObjective)
        {
            chapter = next;
            objective = newObjective;
            ShowBanner(heading, newObjective, 4f);
        }

        private void ShowBanner(string heading, string subheading, float duration)
        {
            banner = heading + "\n" + subheading;
            bannerUntil = Time.unscaledTime + duration;
        }

        private void SpawnPickupSet()
        {
            foreach (float centre in GetCentres())
            {
                SpawnAt<SodaCanSpawner>("Progression Soda Can", centre, s => s.SpawnCan());
                SpawnAt<HeartLaneSpawner>("Progression Heart", centre, s => s.SpawnHeart());
            }
        }

        private void SpawnRescueSet(int count)
        {
            IReadOnlyList<float> centres = GetCentres();
            if (centres.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                float x = centres[Random.Range(0, centres.Count)];
                SpawnAt<StrugglingSwimmerSpawner>("Story Rescue", x, s => s.SpawnSwimmer());
            }
        }

        private void SpawnMajor<T>(string name, System.Action<T> spawn) where T : Component
        {
            IReadOnlyList<float> centres = GetCentres();
            float x = centres.Count > 0 ? centres[Random.Range(0, centres.Count)] : 0f;
            SpawnAt(name, x, spawn);
        }

        private void SpawnAt<T>(string name, float x, System.Action<T> spawn) where T : Component
        {
            GameObject holder = new(name);
            holder.transform.SetParent(transform, false);
            holder.transform.position = new Vector3(x, 0f, 0f);
            T component = holder.AddComponent<T>();
            if (component is Behaviour behaviour) behaviour.enabled = true;
            spawn(component);
            progressionSpawners.Add(holder);
        }


        private void SpawnOceanItems(int count)
        {
            if (FindFirstObjectByType<OceanItemSpawner>() is OceanItemSpawner existing)
            {
                existing.SpawnProgressionItems(count);
                return;
            }

            GameObject holder = new("Progression Ocean Items");
            holder.transform.SetParent(transform, false);
            OceanItemSpawner spawner = holder.AddComponent<OceanItemSpawner>();
            spawner.SpawnProgressionItems(count);
            progressionSpawners.Add(holder);
        }

        private void SpawnJellyfishEncounter(string name, int schools)
        {
            IReadOnlyList<float> centres = GetCentres();
            if (centres.Count == 0) return;

            for (int i = 0; i < schools; i++)
            {
                float x = centres[(i + Random.Range(0, centres.Count)) % centres.Count];
                GameObject holder = new(name + " " + (i + 1));
                holder.transform.SetParent(transform, false);
                holder.transform.position = new Vector3(x, 0f, 0f);
                JellyfishSchoolSpawner spawner = holder.AddComponent<JellyfishSchoolSpawner>();
                spawner.SpawnSchool();
                progressionSpawners.Add(holder);
            }
        }

        private void SpawnBoombox()
        {
            GameObject holder = new("Story Boombox Surfer");
            holder.transform.SetParent(transform, false);
            BoomboxSurferSpawner spawner = holder.AddComponent<BoomboxSurferSpawner>();
            spawner.enabled = true;
            spawner.SpawnOnce();
            progressionSpawners.Add(holder);
        }

        private void SpawnUfo()
        {
            if (FindFirstObjectByType<AlienUfoController>() != null) return;
            GameObject ufo = new("Alien UFO - Story Encounter");
            ufo.AddComponent<SpriteRenderer>();
            ufo.AddComponent<AlienUfoController>();
        }

        private ProceduralRainSystem EnsureRain()
        {
            if (rain != null) return rain;
            rain = FindFirstObjectByType<ProceduralRainSystem>();
            if (rain == null)
                rain = new GameObject("Progression Rain").AddComponent<ProceduralRainSystem>();
            return rain;
        }

        private static IReadOnlyList<float> GetCentres()
        {
            return EndlessWaveSections.Instance != null
                ? EndlessWaveSections.Instance.GetSectionCentres()
                : System.Array.Empty<float>();
        }

        // Progression presentation is handled by SurferSlugMinimalHud so every
        // gameplay HUD element shares one Canvas, layout and visual style.
    }
}
