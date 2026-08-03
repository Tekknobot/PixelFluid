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

        [Header("Day 1 Mechanic Introduction")]
        [SerializeField, Min(0f)] private float handstandUnlockAt = 150f;
        [SerializeField, Min(0f)] private float throwingUnlockAt = 180f;
        [SerializeField, Min(0f)] private float waterSkidUnlockAt = 375f;
        [SerializeField, Min(0f)] private float waterSlashUnlockAt = 430f;

        [Header("Objectives")]
        [SerializeField, Min(1)] private int rescuesRequired = 3;
        [SerializeField, Min(1)] private int finalSurvivalSeconds = 240;

        [Header("Boss Defeat Sunset")]
        [SerializeField, Min(10f)] private float acceleratedSunsetSeconds = 75f;
        [SerializeField, Min(1f)] private float retreatSpeedMultiplier = 0.75f;

        private bool bossDefeatedSunset;

        //[SerializeField] private bool startOnDayTwoForTesting = true; // true

        private readonly List<GameObject> progressionSpawners = new();
        private Chapter chapter;
        private float runTime;
        private int rescues;
        private float bannerUntil;
        private string banner = string.Empty;
        private string objective = string.Empty;
        private string learningObjective = string.Empty;
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
        public string CurrentObjective => string.IsNullOrWhiteSpace(learningObjective)
            ? objective
            : objective + "\nLEARN  •  " + learningObjective;
        public string CurrentLearningObjective => learningObjective;
        public string CurrentBanner => banner;
        public bool IsBannerVisible => Time.unscaledTime < bannerUntil && !string.IsNullOrEmpty(banner);

        public SurfStageSaveSystem.SaveData CaptureSaveData() => new()
        {
            day = currentDay,
            chapter = (int)chapter,
            runTime = runTime,
            rescues = rescues,
            finalWaveStarted = finalWaveStarted,
            bossDefeatedSunset = bossDefeatedSunset
        };

        private Coroutine pendingCheckpoint;

        /// <summary>
        /// Saves after the current gameplay state has settled. This is important for
        /// developer jumps and chapter transitions because spawns/destructions are deferred.
        /// </summary>
        private void QueueCheckpoint()
        {
            if (!isActiveAndEnabled)
                return;

            if (pendingCheckpoint != null)
                StopCoroutine(pendingCheckpoint);
            pendingCheckpoint = StartCoroutine(SaveCheckpointAfterFrame());
        }

        private IEnumerator SaveCheckpointAfterFrame()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            SurfStageSaveSystem.Save(this);
            pendingCheckpoint = null;
        }

        /// <summary>
        /// Keeps the procedural sky tied to the run clock. Each surf day starts
        /// at 6:00 AM and reaches midnight at the end of the configured day.
        /// This also makes Continue loads and developer time jumps visually exact.
        /// </summary>
        private void SyncDayNightToRunTime()
        {
            ProceduralStarryNight sky = FindFirstObjectByType<ProceduralStarryNight>();
            if (sky == null)
                return;

            float progress = dayEndsAt > 0f
                ? Mathf.Clamp01(runTime / dayEndsAt)
                : 0f;

            // 0.25 = 6:00 AM. Advancing 0.75 of a full cycle reaches midnight.
            float visualTime = Mathf.Repeat(0.25f + progress * 0.75f, 1f);
            sky.SetTimeOfDay(visualTime);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurfDayProgressionDirector>() != null)
                return;

            GameObject host = new("Surf Day Progression Director");
            DontDestroyOnLoad(host);
            host.AddComponent<SurfDayProgressionDirector>();
        }

        private void OnEnable()
        {
            StrugglingSwimmerDrifter.SwimmerSaved += OnSwimmerSaved;
            AirTrickScoreSystem.CleanChainLanded += OnCleanChainLanded;
            AirTrickScoreSystem.OnFireActivated += OnFirstOnFireActivated;
        }

        private void OnDisable()
        {
            StrugglingSwimmerDrifter.SwimmerSaved -= OnSwimmerSaved;
            AirTrickScoreSystem.CleanChainLanded -= OnCleanChainLanded;
            AirTrickScoreSystem.OnFireActivated -= OnFirstOnFireActivated;
        }

        private IEnumerator Start()
        {
            yield return BeginRun(false);
            // Never overwrite an existing disk save merely because the title scene loaded.
            if (!SurfStageSaveSystem.HasSave)
                SurfStageSaveSystem.Save(this);

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

        public IEnumerator StartNewRunFromMenu()
        {
            SurfStageSaveSystem.Delete();
            yield return BeginRun(true);
            SurfStageSaveSystem.Save(this);
        }

        public IEnumerator LoadSavedRun(SurfStageSaveSystem.SaveData data)
        {
            if (data == null) yield break;
            ClearRunObjects();
            yield return null;

            float deadline = Time.realtimeSinceStartup + 12f;
            while ((EndlessWaveSections.Instance == null || !EndlessWaveSections.Instance.IsReady) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            currentDay = Mathf.Max(1, data.day);
            chapter = (Chapter)Mathf.Clamp(data.chapter, 0, (int)Chapter.FinalWave);
            runTime = Mathf.Max(0f, data.runTime);
            rescues = Mathf.Max(0, data.rescues);
            finalWaveStarted = data.finalWaveStarted || chapter >= Chapter.FinalWave;
            bossDefeatedSunset = data.bossDefeatedSunset;
            changingDay = false;
            banner = string.Empty;
            bannerUntil = 0f;
            rain = FindFirstObjectByType<ProceduralRainSystem>();
            rain?.ClearRain();
            AirTrickScoreSystem.Instance?.BeginDay(currentDay);
            AirTrickScoreSystem.Instance?.RestorePersistentStoke(data.totalStoke, data.dayStoke);
            if (SurfAbilityProgression.Instance != null)
            {
                if (data.unlockedAbilities != 0 || data.jumpUpgradeLevel != 0 ||
                    data.waterSlashUpgradeLevel != 0 || data.skidUpgradeLevel != 0)
                {
                    SurfAbilityProgression.Instance.RestoreExact(
                        (SurfAbility)data.unlockedAbilities,
                        data.jumpUpgradeLevel,
                        data.waterSlashUpgradeLevel,
                        data.skidUpgradeLevel);
                }
                else
                {
                    // Backward compatibility with saves created before staged abilities/upgrades.
                    SurfAbilityProgression.Instance.RestoreFor(currentDay, chapter);
                }

                // Day 2 is mastery-only: every core mechanic is always available,
                // including when continuing a save created by an older build.
                if (currentDay >= 2)
                    SurfAbilityProgression.Instance.DebugUnlockAll();

                // A save made after a developer chapter jump must contain every
                // mechanic that normal progression would have granted by this stage.
                SurfAbilityProgression.Instance.EnsureForStage(currentDay, chapter);
            }
            RefreshLearningObjectiveForStage();
            SyncDayNightToRunTime();

            SpawnPickupSet();
            SpawnOceanItems(12);
            RestoreChapterPopulation();
            RefreshLearningObjectiveForStage();
            SurfAbilityProgression.Instance?.ApplyUpgradesToAllPlayers();
            ShowBanner($"DAY {currentDay} CONTINUED", CurrentObjective, 4f);
        }

        private void RestoreChapterPopulation()
        {
            if (currentDay >= 3)
            {
                objective = chapter >= Chapter.FinalWave
                    ? "OUTSURF YOUR SHADOW. SURVIVE THE OCEAN."
                    : "THE OCEAN IS REPLAYING WHAT YOU SURVIVED.";
                if (chapter >= Chapter.FirstRescue) SpawnRescueSet(1);
                if (chapter >= Chapter.Storm)
                    EnsureRain().SetSituation(ProceduralRainSystem.RainSituation.HeavyRain);
                return;
            }

            bool dayTwo = currentDay == 2;
            if (!dayTwo)
            {
                objective = "Surf. Stay alive. Learn the water.";
                SpawnJellyfishEncounter("Dawn Jellyfish", 1);
                SpawnMajor<SharkLaneSpawner>("Early Shark", spawner => spawner.SpawnShark(true));
            }
            else
            {
                objective = "New predators have entered the water.";
                SpawnMajor<BloodSharkLaneSpawner>("Dawn Blood Shark", spawner => spawner.SpawnBloodShark(true));
                SpawnBloodfishEncounter("Dawn Bloodfish", 1);
            }

            if (chapter >= Chapter.FirstRescue) SpawnRescueSet(1);
            if (chapter >= Chapter.DangerousWater)
            {
                objective = $"RESCUES  {rescues}/{rescuesRequired}";
                if (!dayTwo)
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
                SpawnRescueSet(Mathf.Max(1, rescuesRequired - rescues));
            }
            if (chapter >= Chapter.StrangeTide)
            {
                objective = "Something is watching the water.";
                SpawnBoombox();
                if (!dayTwo) { SpawnUfo(); SpawnJellyfishEncounter("Strange Tide Jellyfish", 2); SpawnMajor<WhaleLaneSpawner>("Strange Tide Whale", s => s.SpawnWhale(true)); }
                else { SpawnHelicopter(); SpawnBloodfishEncounter("Strange Tide Bloodfish", 2); SpawnMajor<TransparentSquidLaneSpawner>("Veiled Squid", s => s.SpawnTransparentSquid(true)); }
            }
            if (chapter >= Chapter.Storm)
            {
                objective = "Keep moving. Rescue anyone left out there.";
                EnsureRain().SetSituation(ProceduralRainSystem.RainSituation.HeavyRain);
                if (!dayTwo) { SpawnMajor<GiantSquidLaneSpawner>("Storm Squid", s => s.SpawnSquid(true)); SpawnJellyfishEncounter("Storm Jellyfish", 3); }
                else { SpawnMajor<BloodSharkLaneSpawner>("Storm Blood Shark", s => s.SpawnBloodShark(true)); SpawnMajor<TransparentSquidLaneSpawner>("Storm Transparent Squid", s => s.SpawnTransparentSquid(true)); SpawnMajor<StingrayLaneSpawner>("Storm Stingray", s => s.SpawnStingray(true)); SpawnBloodfishEncounter("Storm Bloodfish", 3); }
            }
            if (chapter >= Chapter.FinalWave)
            {
                objective = $"SURVIVE  {Mathf.Max(0, Mathf.CeilToInt(dayEndsAt - runTime))}s";
                if (!bossDefeatedSunset)
                {
                    if (!dayTwo) SpawnMajor<GodzillaLaneSpawner>("Final Godzilla", s => s.SpawnGodzilla());
                    else SpawnMajor<RubberDuckBossSpawner>("Day 2 Giant Rubber Duck Boss", s => s.SpawnRubberDuckBoss());
                }
            }
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
            SurfAbilityProgression.Instance?.ResetForNewRun();
            chapter = Chapter.Dawn;
            banner = string.Empty;
            objective = string.Empty;
            learningObjective = string.Empty;
            bannerUntil = 0f;

            rain = FindFirstObjectByType<ProceduralRainSystem>();
            rain?.ClearRain();
            SyncDayNightToRunTime();
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
            if (SurfDayUpgradeScreen.Instance != null)
                yield return SurfDayUpgradeScreen.Instance.ShowAndWait();

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

            // The world stays paused behind the cutscene system's full black fade.
            // Day 2 enemies are not spawned until all six boards have finished.
            yield return StoryboardCutsceneSystem.PlayDayTwoOpening();

            currentDay = 2;
            AirTrickScoreSystem.Instance?.BeginDay(2);
            SurfAbilityProgression.Instance?.DebugUnlockAll();
            runTime = 0f;
            SyncDayNightToRunTime();
            rescues = 0;
            finalWaveStarted = false;
            bossDefeatedSunset = false;
            chapter = Chapter.Dawn;
            changingDay = false;
            BeginChapter(Chapter.Dawn, "DAY 2 — DEEP CURRENT", "NEW PREDATORS HAVE ENTERED THE WATER.");
            learningObjective = "Use the full moveset to build flow and survive.";
            SpawnPickupSet();
            SpawnOceanItems(12);
            SpawnMajor<BloodSharkLaneSpawner>("Dawn Blood Shark", spawner => spawner.SpawnBloodShark(true));
            SpawnBloodfishEncounter("Dawn Bloodfish", 1);
            SurfStageSaveSystem.Save(this);
        }

        private IEnumerator BeginDayThree()
        {
            AirTrickScoreSystem.Instance?.ShowDayRecap(2, 5f);
            yield return new WaitForSecondsRealtime(5f);
            if (SurfDayUpgradeScreen.Instance != null)
                yield return SurfDayUpgradeScreen.Instance.ShowAndWait();

            ShowBanner("THE NIGHT DOES NOT PASS", "DAY 3 — THE OCEAN REMEMBERS", 4f);
            rain?.ClearRain();
            yield return new WaitForSecondsRealtime(3f);

            ClearRunObjects();
            yield return null;

            currentDay = 3;
            AirTrickScoreSystem.Instance?.BeginDay(3);
            SurfAbilityProgression.Instance?.DebugUnlockAll();
            runTime = 0f;
            rescues = 0;
            finalWaveStarted = false;
            bossDefeatedSunset = false;
            chapter = Chapter.Dawn;
            changingDay = false;
            SyncDayNightToRunTime();
            BeginChapter(Chapter.Dawn, "DAY 3 — ECHOES", "THE OCEAN IS REPLAYING WHAT YOU SURVIVED.");
            learningObjective = "Watch the Shadow. Survive the changing water.";
            SpawnPickupSet();
            SpawnOceanItems(12);
            SurfStageSaveSystem.Save(this);
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
            DestroyAll<BossArenaPrison>();
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
            SyncDayNightToRunTime();
            UpdateDayOneMechanicUnlocks();

            if (runTime >= dayEndsAt && finalWaveStarted)
            {
                if (currentDay == 1 && !changingDay)
                {
                    changingDay = true;
                    StartCoroutine(BeginDayTwo());
                }
                else if (currentDay == 2 && !changingDay)
                {
                    changingDay = true;
                    StartCoroutine(BeginDayThree());
                }
                else if (currentDay >= 3)
                {
                    BeginChapter(Chapter.Complete, "THREE DAYS SURVIVED", "YOU OUTSURFED WHAT THE OCEAN REMEMBERED.");
                    AirTrickScoreSystem.Instance?.ShowDayRecap(3, 10f);
                    rain?.ClearRain();
                }
                return;
            }

            if (runTime >= finalWaveBeginsAt && chapter < Chapter.FinalWave)
            {
                finalWaveStarted = true;
                BeginChapter(Chapter.FinalWave,
                    currentDay >= 3 ? "OUTSURF YOUR SHADOW" : "THE LAST WAVE",
                    currentDay >= 3 ? "SURVIVE THE BLACK WATER." : $"SURVIVE {finalSurvivalSeconds} SECONDS.");
                if (currentDay == 1)
                    SpawnMajor<GodzillaLaneSpawner>("Final Godzilla", spawner => spawner.SpawnGodzilla());
                else if (currentDay == 2)
                    SpawnMajor<RubberDuckBossSpawner>("Day 2 Giant Rubber Duck Boss", spawner => spawner.SpawnRubberDuckBoss());
                // Day 3 has no giant boss: the Shadow Surfer and corrupted ocean are the encounter.
                return;
            }

            if (runTime >= stormBeginsAt && chapter < Chapter.Storm)
            {
                BeginChapter(Chapter.Storm,
                    currentDay >= 3 ? "BLACK WATER" : "STORM FRONT",
                    currentDay >= 3 ? "THE WAVES NO LONGER FOLLOW THE SKY." : "KEEP MOVING. RESCUE ANYONE LEFT OUT THERE.");
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
                BeginChapter(Chapter.StrangeTide,
                    currentDay >= 3 ? "THE OCEAN REMEMBERS" : "STRANGE TIDE",
                    currentDay >= 3 ? "YOUR SHADOW HAS ENTERED THE WATER." : "SOMETHING IS WATCHING THE WATER.");
                if (currentDay == 1)
                    UnlockAbility(SurfAbility.Rotation | SurfAbility.Flip |
                        SurfAbility.DoubleChain | SurfAbility.TripleChain,
                        "FULL TRICK CHAINS UNLOCKED",
                        "CHAIN EACH UNIQUE AIR TRICK ONCE BEFORE LANDING.");
                SpawnBoombox();
                if (currentDay == 1)
                    SpawnUfo();
                else if (currentDay == 2)
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
                BeginChapter(Chapter.DangerousWater,
                    currentDay >= 3 ? "THE CURRENT SHIFTS" : "DANGEROUS WATER",
                    currentDay >= 3 ? "FAMILIAR THREATS RETURN IN THE WRONG ORDER." : "SAVE 3 SWIMMERS. USE CANS TO FIGHT BACK.");
                if (currentDay == 1)
                    UnlockAbility(SurfAbility.ChargedJump,
                        "CHARGED JUMP UNLOCKED",
                        "HOLD JUMP WHILE MOVING, THEN RELEASE TO LAUNCH.");
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
            SyncDayNightToRunTime();
            objective = $"SUNSET  {Mathf.Max(0, Mathf.CeilToInt(dayEndsAt - runTime))}s";
            ShowBanner("THE DEEP RETREATS", "THE OCEAN GROWS QUIET AS SUNSET FALLS.", 5f);
            QueueCheckpoint();
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

        public void DebugNextChapter()
        {
            Chapter targetChapter = chapter;
            switch (chapter)
            {
                case Chapter.Dawn:
                    targetChapter = Chapter.FirstRescue;
                    runTime = Mathf.Max(runTime, rescueBeginsAt + 0.05f);
                    break;
                case Chapter.FirstRescue:
                    targetChapter = Chapter.DangerousWater;
                    runTime = Mathf.Max(runTime, dangerBeginsAt + 0.05f);
                    break;
                case Chapter.DangerousWater:
                    targetChapter = Chapter.StrangeTide;
                    runTime = Mathf.Max(runTime, strangeTideBeginsAt + 0.05f);
                    break;
                case Chapter.StrangeTide:
                    targetChapter = Chapter.Storm;
                    runTime = Mathf.Max(runTime, stormBeginsAt + 0.05f);
                    break;
                case Chapter.Storm:
                    targetChapter = Chapter.FinalWave;
                    runTime = Mathf.Max(runTime, finalWaveBeginsAt + 0.05f);
                    break;
                case Chapter.FinalWave:
                    targetChapter = Chapter.Complete;
                    runTime = Mathf.Max(runTime, dayEndsAt + 0.05f);
                    break;
            }

            // Apply the destination chapter's mechanics immediately, before the
            // deferred checkpoint is captured. Update() will still perform the
            // normal population/banner transition on the following frame.
            SurfAbilityProgression.Instance?.EnsureForStage(currentDay, targetChapter);
            SyncDayNightToRunTime();
            QueueCheckpoint();
        }

        public void DebugSpawnBoss()
        {
            if (chapter < Chapter.FinalWave)
                runTime = Mathf.Max(runTime, finalWaveBeginsAt + 0.05f);

            SurfAbilityProgression.Instance?.EnsureForStage(currentDay, Chapter.FinalWave);
            SyncDayNightToRunTime();
            QueueCheckpoint();
        }

        public void DebugResetCurrentDay()
        {
            StartCoroutine(LoadSavedRun(new SurfStageSaveSystem.SaveData
            {
                day = currentDay,
                chapter = (int)Chapter.Dawn,
                runTime = 0f,
                rescues = 0,
                finalWaveStarted = false,
                bossDefeatedSunset = false,
                unlockedAbilities = SurfAbilityProgression.Instance != null ? (int)SurfAbilityProgression.Instance.Unlocked : 0,
                jumpUpgradeLevel = SurfAbilityProgression.Instance != null ? SurfAbilityProgression.Instance.JumpUpgradeLevel : 0,
                waterSlashUpgradeLevel = SurfAbilityProgression.Instance != null ? SurfAbilityProgression.Instance.WaterSlashUpgradeLevel : 0,
                skidUpgradeLevel = SurfAbilityProgression.Instance != null ? SurfAbilityProgression.Instance.SkidUpgradeLevel : 0
            }));
        }

        public void DebugNextDay()
        {
            // Developer advancement from Day 1 uses the real transition so the
            // Day 2 storyboard, pause, black fade and spawn timing are tested too.
            if (currentDay == 1 && !changingDay)
            {
                changingDay = true;
                StartCoroutine(BeginDayTwo());
                return;
            }
            if (currentDay == 2 && !changingDay)
            {
                changingDay = true;
                StartCoroutine(BeginDayThree());
                return;
            }

            StartCoroutine(LoadSavedRun(new SurfStageSaveSystem.SaveData
            {
                day = currentDay + 1,
                chapter = (int)Chapter.Dawn,
                runTime = 0f,
                rescues = 0,
                finalWaveStarted = false,
                bossDefeatedSunset = false,
                unlockedAbilities = SurfAbilityProgression.Instance != null ? (int)SurfAbilityProgression.Instance.Unlocked : 0,
                jumpUpgradeLevel = SurfAbilityProgression.Instance != null ? SurfAbilityProgression.Instance.JumpUpgradeLevel : 0,
                waterSlashUpgradeLevel = SurfAbilityProgression.Instance != null ? SurfAbilityProgression.Instance.WaterSlashUpgradeLevel : 0,
                skidUpgradeLevel = SurfAbilityProgression.Instance != null ? SurfAbilityProgression.Instance.SkidUpgradeLevel : 0
            }));
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

        private void UpdateDayOneMechanicUnlocks()
        {
            if (currentDay != 1 || SurfAbilityProgression.Instance == null)
                return;

            if (runTime >= handstandUnlockAt &&
                !SurfAbilityProgression.Instance.Has(SurfAbility.Handstand))
                UnlockAbility(SurfAbility.Handstand,
                    "HANDSTAND UNLOCKED",
                    "PRESS A / SPACE AGAIN WHILE AIRBORNE.");

            if (runTime >= throwingUnlockAt &&
                !SurfAbilityProgression.Instance.Has(SurfAbility.ThrowItems))
                UnlockAbility(SurfAbility.ThrowItems,
                    "THROWING UNLOCKED",
                    "PRESS X / F ON THE WATER TO THROW A COLLECTED OBJECT.");

            if (runTime >= waterSkidUnlockAt &&
                !SurfAbilityProgression.Instance.Has(SurfAbility.WaterSkid))
                UnlockAbility(SurfAbility.WaterSkid,
                    "WATER SKID UNLOCKED",
                    "HOLD B / E ON THE WATER, THEN RELEASE.");

            if (runTime >= waterSlashUnlockAt &&
                !SurfAbilityProgression.Instance.Has(SurfAbility.WaterSlash))
                UnlockAbility(SurfAbility.WaterSlash,
                    "WATER SLASH UNLOCKED",
                    "PRESS RB / R WHILE RIDING.");
        }

        private void OnCleanChainLanded(int chainLength)
        {
            if (currentDay != 1 || chapter < Chapter.StrangeTide || chainLength < 2)
                return;

            UnlockAbility(SurfAbility.Flow,
                "FLOW METER UNLOCKED",
                "CLEAN TRICK CHAINS BUILD FLOW. KEEP THE RHYTHM GOING.");
            RefreshLearningObjectiveForStage();
        }

        private void OnFirstOnFireActivated()
        {
            if (currentDay != 1 || SurfAbilityProgression.Instance == null ||
                !SurfAbilityProgression.Instance.Has(SurfAbility.Flow))
                return;

            UnlockAbility(SurfAbility.FlowFinisher,
                "FLOW FINISHER UNLOCKED",
                "WHILE ON FIRE, PRESS RB / R ON THE WATER.");
            RefreshLearningObjectiveForStage();
        }

        private void OnSwimmerSaved()
        {
            rescues++;
            ShowBanner("SWIMMER SAVED", $"RESCUES  {rescues}/{rescuesRequired}", 2.5f);
            QueueCheckpoint();

            if (rescues < rescuesRequired)
                SpawnRescueSet(1);
            else if (chapter == Chapter.DangerousWater)
                objective = "RESCUES COMPLETE — SURVIVE THE CHANGING TIDE";
        }

        private void BeginChapter(Chapter next, string heading, string newObjective)
        {
            chapter = next;
            objective = newObjective;
            RefreshLearningObjectiveForStage();
            ShowBanner(heading, CurrentObjective, 4f);
            QueueCheckpoint();
        }

        private void UnlockAbility(SurfAbility ability, string heading, string instruction)
        {
            if (SurfAbilityProgression.Instance == null)
                return;

            if (!SurfAbilityProgression.Instance.Unlock(ability))
                return;

            learningObjective = instruction;
            ShowBanner(heading, instruction, 5f);
            SurfStageSaveSystem.Save(this);
        }

        private void RefreshLearningObjectiveForStage()
        {
            SurfAbilityProgression abilities = SurfAbilityProgression.Instance;

            if (currentDay >= 2)
            {
                learningObjective = "Use the full moveset to build Flow and survive.";
                return;
            }

            if (chapter == Chapter.Dawn)
            {
                learningObjective = "Surf left/right. Hold Up/Down + Jump to change waves.";
                return;
            }

            if (chapter == Chapter.FirstRescue)
            {
                learningObjective = "Reach struggling swimmers. A rescue restores 1 life.";
                return;
            }

            if (abilities == null || !abilities.Has(SurfAbility.ChargedJump))
                learningObjective = "Hold Jump while moving, then release to launch.";
            else if (!abilities.Has(SurfAbility.Handstand))
                learningObjective = "Press A / Space again while airborne for a handstand.";
            else if (!abilities.Has(SurfAbility.ThrowItems))
                learningObjective = "Press X / F on the water to throw a collected object.";
            else if (!abilities.Has(SurfAbility.TripleChain))
                learningObjective = "Chain different air tricks before landing.";
            else if (!abilities.Has(SurfAbility.Flow))
                learningObjective = "Land a clean multi-trick chain to reveal Flow.";
            else if (!abilities.Has(SurfAbility.WaterSkid))
                learningObjective = "Clean chains build Flow. Keep the rhythm going.";
            else if (!abilities.Has(SurfAbility.WaterSlash))
                learningObjective = "Hold B / E on the water, then release for a skid.";
            else if (!abilities.Has(SurfAbility.FlowFinisher))
                learningObjective = "Build Flow to 100% and enter ON FIRE.";
            else if (chapter >= Chapter.FinalWave)
                learningObjective = "Tip • Build Flow and use the finisher against the boss.";
            else
                learningObjective = "While ON FIRE, press RB / R on the water for Flow Finisher.";
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
                IReadOnlyList<PixelWaterGPU> layers = EndlessWaveSections.LayersNearest(x);
                x = CameraSafeSpawnUtility.ChooseOffscreenEntryX(layers, null, out _);
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
                IReadOnlyList<PixelWaterGPU> layers = EndlessWaveSections.LayersNearest(x);
                x = CameraSafeSpawnUtility.ChooseOffscreenEntryX(layers, null, out _);
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
