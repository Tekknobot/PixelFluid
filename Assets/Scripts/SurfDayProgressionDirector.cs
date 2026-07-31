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
        [SerializeField, Min(30f)] private float strangeTideBeginsAt = 260f; // 260
        [SerializeField, Min(40f)] private float stormBeginsAt = 430f;
        [SerializeField, Min(5f)] private float finalWaveBeginsAt = 480f; // 480
        [SerializeField, Min(60f)] private float dayEndsAt = 720f; // 720

        [Header("Objectives")]
        [SerializeField, Min(1)] private int rescuesRequired = 3;
        [SerializeField, Min(1)] private int finalSurvivalSeconds = 240;

        [Header("Boss Defeat Sunset")]
        [SerializeField, Min(10f)] private float acceleratedSunsetSeconds = 75f;
        [SerializeField, Min(1f)] private float retreatSpeedMultiplier = 1.75f;

        private bool bossDefeatedSunset;

        //[SerializeField] private bool startOnDayTwoForTesting = true; // true

        private readonly List<GameObject> progressionSpawners = new();
        private Chapter chapter;
        private float runTime;
        private int rescues;
        private float bannerUntil;
        private string banner = string.Empty;
        private string objective = string.Empty;
        private bool finalWaveStarted;
        private bool changingDay;
        private int currentDay = 1;
        private ProceduralRainSystem rain;
        private GUIStyle titleStyle;
        private GUIStyle objectiveStyle;
        private GUIStyle panelStyle;

        public Chapter CurrentChapter => chapter;
        public int Rescues => rescues;
        public float RunTime => runTime;
        public int CurrentDay => currentDay;
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

            //if (startOnDayTwoForTesting)
            //{
            //    StartCoroutine(BeginDayTwo());
            //    yield break;
            //}  
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
            currentDay = 1; // 1
            changingDay = false;
            rescues = 0;
            finalWaveStarted = false;
            bossDefeatedSunset = false;
            AirTrickScoreSystem.Instance?.BeginDay(1);
            chapter = Chapter.Dawn;
            banner = string.Empty;
            objective = string.Empty;
            bannerUntil = 0f;

            rain = FindFirstObjectByType<ProceduralRainSystem>();
            rain?.ClearRain();
            SpawnPickupSet();
            SpawnOceanItems(12);
            if (currentDay == 1)
            {
                BeginChapter(Chapter.Dawn, "DAWN PATROL", "SURF. STAY ALIVE. LEARN THE WATER.");
                SpawnJellyfishEncounter("Dawn Jellyfish", 1);
                SpawnMajor<SharkLaneSpawner>("Early Shark", spawner => spawner.SpawnShark(true));
            }
            else
            {
                BeginChapter(Chapter.Dawn, "DAY 2 — DEEP CURRENT", "NEW PREDATORS HAVE ENTERED THE WATER.");
                SpawnMajor<BloodSharkLaneSpawner>("Dawn Blood Shark", spawner => spawner.SpawnBloodShark(true));
                SpawnBloodfishEncounter("Dawn Bloodfish", 1);
            }
        }


        private IEnumerator BeginDayTwo()
        {
            AirTrickScoreSystem.Instance?.ShowDayRecap(1, 5f);
            yield return new WaitForSecondsRealtime(5f);

            ShowBanner("NIGHT PASSES", "DAY 2 — DEEP CURRENT", 4f);
            rain?.ClearRain();
            yield return new WaitForSeconds(4f);

            foreach (GameObject holder in progressionSpawners)
                if (holder != null) Destroy(holder);
            progressionSpawners.Clear();
            DestroyAll<SharkLaneSwimmer>();
            DestroyAll<GiantSquidLaneSwimmer>();
            DestroyAll<GodzillaLaneSwimmer>();
            DestroyAll<JellyfishSwimmer>();
            DestroyAll<BloodfishSwimmer>();
            DestroyAll<BloodSharkLaneSwimmer>();
            DestroyAll<TransparentSquidLaneSwimmer>();
            DestroyAll<StingrayLaneSwimmer>();
            DestroyAll<DayTwoHelicopterController>();
            DestroyAll<DayTwoHelicopterMissile>();
            DestroyAll<RubberDuckBossSwimmer>();
            DestroyAll<RubberDucklingSwimmer>();
            yield return null;

            currentDay = 2;
            AirTrickScoreSystem.Instance?.BeginDay(2);
            runTime = 0f;
            rescues = 0;
            finalWaveStarted = false;
            bossDefeatedSunset = false;
            chapter = Chapter.Dawn;
            changingDay = false;
            BeginChapter(Chapter.Dawn, "DAY 2 — DEEP CURRENT", "NEW PREDATORS HAVE ENTERED THE WATER.");
            SpawnPickupSet();
            SpawnOceanItems(12);
            SpawnMajor<BloodSharkLaneSpawner>("Dawn Blood Shark", spawner => spawner.SpawnBloodShark(true));
            SpawnBloodfishEncounter("Dawn Bloodfish", 1);
        }

        private void ClearRunObjects()
        {
            foreach (GameObject holder in progressionSpawners)
                if (holder != null) Destroy(holder);
            progressionSpawners.Clear();

            DestroyAll<SharkLaneSwimmer>();
            DestroyAll<GiantSquidLaneSwimmer>();
            DestroyAll<BloodSharkLaneSwimmer>();
            DestroyAll<TransparentSquidLaneSwimmer>();
            DestroyAll<StingrayLaneSwimmer>();
            DestroyAll<GodzillaLaneSwimmer>();
            DestroyAll<JellyfishSwimmer>();
            DestroyAll<BloodfishSwimmer>();
            DestroyAll<WhaleLaneSwimmer>();
            DestroyAll<StrugglingSwimmerDrifter>();
            DestroyAll<RescuedSurferExit>();
            DestroyAll<OceanItemBehaviour>();
            DestroyAll<SodaCanPickup>();
            DestroyAll<SodaCanProjectile>();
            DestroyAll<AlienUfoController>();
            DestroyAll<DayTwoHelicopterController>();
            DestroyAll<DayTwoHelicopterMissile>();
            DestroyAll<RubberDuckBossSwimmer>();
            DestroyAll<RubberDucklingSwimmer>();
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
                if (currentDay == 1 && !changingDay)
                {
                    changingDay = true;
                    StartCoroutine(BeginDayTwo());
                }
                else if (currentDay >= 2)
                {
                    BeginChapter(Chapter.Complete, "TWO DAYS SURVIVED", "THE DEEP WATER WILL RETURN.");
                    AirTrickScoreSystem.Instance?.ShowDayRecap(2, 10f);
                    rain?.ClearRain();
                }
                return;
            }

            if (runTime >= finalWaveBeginsAt && chapter < Chapter.FinalWave)
            {
                finalWaveStarted = true;
                BeginChapter(Chapter.FinalWave, "THE LAST WAVE", $"SURVIVE {finalSurvivalSeconds} SECONDS.");
                if (currentDay == 1)
                    SpawnMajor<GodzillaLaneSpawner>("Final Godzilla", spawner => spawner.SpawnGodzilla());
                else
                {
                    SpawnMajor<RubberDuckBossSpawner>("Day 2 Giant Rubber Duck Boss", spawner => spawner.SpawnRubberDuckBoss());
                }
                return;
            }

            if (runTime >= stormBeginsAt && chapter < Chapter.Storm)
            {
                BeginChapter(Chapter.Storm, "STORM FRONT", "KEEP MOVING. RESCUE ANYONE LEFT OUT THERE.");
                EnsureRain().SetSituation(ProceduralRainSystem.RainSituation.HeavyRain);
                if (currentDay == 1)
                    SpawnMajor<GiantSquidLaneSpawner>("Storm Squid", spawner => spawner.SpawnSquid(true));
                else
                {
                    SpawnMajor<BloodSharkLaneSpawner>("Storm Blood Shark", spawner => spawner.SpawnBloodShark(true));
                    SpawnMajor<TransparentSquidLaneSpawner>("Storm Transparent Squid", spawner => spawner.SpawnTransparentSquid(true));
                    SpawnMajor<StingrayLaneSpawner>("Storm Stingray", spawner => spawner.SpawnStingray(true));
                }
                if (currentDay == 1)
                    SpawnJellyfishEncounter("Storm Jellyfish", 3);
                else
                    SpawnBloodfishEncounter("Storm Bloodfish", 3);
                return;
            }

            if (runTime >= strangeTideBeginsAt && chapter < Chapter.StrangeTide)
            {
                BeginChapter(Chapter.StrangeTide, "STRANGE TIDE", "SOMETHING IS WATCHING THE WATER.");
                SpawnBoombox();
                if (currentDay == 1)
                    SpawnUfo();
                else
                    SpawnHelicopter();
                if (currentDay == 1)
                    SpawnJellyfishEncounter("Strange Tide Jellyfish", 2);
                else
                    SpawnBloodfishEncounter("Strange Tide Bloodfish", 2);
                if (currentDay == 1)
                    SpawnMajor<WhaleLaneSpawner>("Strange Tide Whale", spawner => spawner.SpawnWhale(true));
                else
                    SpawnMajor<TransparentSquidLaneSpawner>("Veiled Squid", spawner => spawner.SpawnTransparentSquid(true));
                return;
            }

            if (runTime >= dangerBeginsAt && chapter < Chapter.DangerousWater)
            {
                BeginChapter(Chapter.DangerousWater, "DANGEROUS WATER", "SAVE 3 SWIMMERS. USE CANS TO FIGHT BACK.");
                if (currentDay == 1)
                {
                    SpawnMajor<GiantSquidLaneSpawner>("First Squid", spawner => spawner.SpawnSquid(true));
                    SpawnMajor<SharkLaneSpawner>("Second Shark", spawner => spawner.SpawnShark(true));
                }
                else
                {
                    SpawnMajor<TransparentSquidLaneSpawner>("First Transparent Squid", spawner => spawner.SpawnTransparentSquid(true));
                    SpawnMajor<BloodSharkLaneSpawner>("Second Blood Shark", spawner => spawner.SpawnBloodShark(true));
                    SpawnMajor<StingrayLaneSpawner>("First Stingray", spawner => spawner.SpawnStingray(true));
                }
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
                objective = bossDefeatedSunset
                    ? $"SUNSET  {Mathf.Max(0, Mathf.CeilToInt(dayEndsAt - runTime))}s"
                    : $"SURVIVE  {Mathf.Max(0, Mathf.CeilToInt(dayEndsAt - runTime))}s";
        }


        public void OnFinalBossDefeated()
        {
            if (bossDefeatedSunset || chapter != Chapter.FinalWave)
                return;

            bossDefeatedSunset = true;
            StopHostileSpawners();
            RetreatAllHostileSeaCreatures();
            rain?.ClearRain();

            float shortenedStart = Mathf.Max(0f, dayEndsAt - acceleratedSunsetSeconds);
            runTime = Mathf.Max(runTime, shortenedStart);
            objective = $"SUNSET  {Mathf.Max(0, Mathf.CeilToInt(dayEndsAt - runTime))}s";
            ShowBanner("THE DEEP RETREATS", "THE OCEAN GROWS QUIET AS SUNSET FALLS.", 5f);
        }

        private void StopHostileSpawners()
        {
            DisableAll<SharkLaneSpawner>();
            DisableAll<GiantSquidLaneSpawner>();
            DisableAll<JellyfishSchoolSpawner>();
            DisableAll<WhaleLaneSpawner>();
            DisableAll<BloodSharkLaneSpawner>();
            DisableAll<TransparentSquidLaneSpawner>();
            DisableAll<StingrayLaneSpawner>();
            DisableAll<BloodfishSchoolSpawner>();
        }

        private static void DisableAll<T>() where T : Behaviour
        {
            foreach (T spawner in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (spawner != null) spawner.enabled = false;
        }

        private void RetreatAllHostileSeaCreatures()
        {
            BeginRetreat<SharkLaneSwimmer>();
            BeginRetreat<GiantSquidLaneSwimmer>();
            BeginRetreat<JellyfishSwimmer>();
            BeginRetreat<WhaleLaneSwimmer>();
            BeginRetreat<BloodSharkLaneSwimmer>();
            BeginRetreat<TransparentSquidLaneSwimmer>();
            BeginRetreat<StingrayLaneSwimmer>();
            BeginRetreat<BloodfishSwimmer>();
            BeginRetreat<RubberDucklingSwimmer>();
        }

        private void BeginRetreat<T>() where T : MonoBehaviour
        {
            foreach (T creature in FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (creature == null)
                    continue;

                GameObject creatureObject = creature.gameObject;
                SeaCreatureRetreatMover mover = creatureObject.GetComponent<SeaCreatureRetreatMover>();
                if (mover == null)
                    mover = creatureObject.AddComponent<SeaCreatureRetreatMover>();

                creature.enabled = false;
                mover.Begin(retreatSpeedMultiplier);
            }
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

        private void SpawnBloodfishEncounter(string name, int schools)
        {
            IReadOnlyList<float> centres = GetCentres();
            if (centres.Count == 0) return;

            for (int i = 0; i < schools; i++)
            {
                float x = centres[(i + Random.Range(0, centres.Count)) % centres.Count];
                GameObject holder = new(name + " " + (i + 1));
                holder.transform.SetParent(transform, false);
                holder.transform.position = new Vector3(x, 0f, 0f);
                BloodfishSchoolSpawner spawner = holder.AddComponent<BloodfishSchoolSpawner>();
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

        private void SpawnHelicopter()
        {
            if (FindFirstObjectByType<DayTwoHelicopterController>() != null) return;
            GameObject holder = new("Day 2 Helicopter Encounter");
            holder.transform.SetParent(transform, false);
            DayTwoHelicopterSpawner spawner = holder.AddComponent<DayTwoHelicopterSpawner>();
            spawner.SpawnHelicopter();
            progressionSpawners.Add(holder);
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
