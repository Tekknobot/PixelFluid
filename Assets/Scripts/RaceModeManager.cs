using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    [DefaultExecutionOrder(-25000)]
    [DisallowMultipleComponent]
    public sealed class RaceModeManager : MonoBehaviour
    {
        private sealed class Racer
        {
            public string Name;
            public TinyWaveSurfer Surfer;
            public float Distance;
            public float StartX;
            public float LastX;
            public bool Player;
        }

        public static RaceModeManager Instance { get; private set; }
        public static bool RaceActive { get; private set; }

        private static readonly string[] Roster = { "Chuck", "Fred", "Josh", "Jason" };
        private readonly List<Racer> racers = new();
        private Canvas canvas;
        private GameObject selectionRoot;
        private GameObject raceHud;
        private TextMeshProUGUI timerLabel;
        private TextMeshProUGUI standingsLabel;
        private float raceTimeRemaining;
        private const float PrototypeRaceSeconds = 180f;
        private AudioSource musicSource;
        private Coroutine musicFadeCoroutine;
        private float nextRaceWeatherChangeTime;
        private ProceduralRainSystem raceRain;
        private ProceduralStarryNight raceSky;
        [Header("Race Atmosphere")]
        [SerializeField, Min(0.1f)] private float raceMusicFadeOutSeconds = 2.5f;
        [SerializeField, Min(0.1f)] private float raceTimeTransitionSeconds = 3.5f;
        [SerializeField, Min(5f)] private float minimumRaceWeatherDuration = 24f;
        [SerializeField, Min(5f)] private float maximumRaceWeatherDuration = 52f;
        [SerializeField, Range(0f, 1f)] private float raceClearWeatherChance = 0.30f;
        private SurferSlugPauseMenu selectionMenu;
        private GameObject ecosystemRoot;
        private float nextEcosystemSpawnTime;

        [Header("Race Bosses — No Arenas")]
        [SerializeField, Min(0f)] private float reaperSpawnAfterSeconds = 55f;
        [SerializeField, Min(0f)] private float rubberDuckSpawnAfterSeconds = 115f;
        [SerializeField, Min(0.5f)] private float raceBossFollowDistance = 3.25f;
        [SerializeField, Min(0.5f)] private float raceBossMaximumSpeed = 8.5f;
        [SerializeField, Min(0.1f)] private float raceBossAcceleration = 12f;
        [SerializeField, Min(0.5f)] private float raceBossVisibleSpawnOffset = 4.5f;
        [SerializeField, Min(1f)] private float raceBossChildRecycleDistance = 14f;
        private bool raceReaperSpawned;
        private bool raceRubberDuckSpawned;
        private bool raceTeardownInProgress;

        [Header("Race Ecosystem Difficulty")]
        [SerializeField, Range(1, 12)] private int openingEnemyCap = 6;
        [SerializeField, Range(1, 16)] private int earlyEnemyCap = 9;
        [SerializeField, Range(1, 20)] private int midEnemyCap = 12;
        [SerializeField, Range(1, 24)] private int finalEnemyCap = 15;
        [SerializeField, Min(0.5f)] private float openingSpawnInterval = 4.5f;
        [SerializeField, Min(0.5f)] private float earlySpawnInterval = 3.5f;
        [SerializeField, Min(0.5f)] private float midSpawnInterval = 2.6f;
        [SerializeField, Min(0.5f)] private float finalSpawnInterval = 1.8f;
        [SerializeField, Range(0f, 1f)] private float openingPhaseEnd = 0.10f;
        [SerializeField, Range(0f, 1f)] private float earlyPhaseEnd = 0.27f;
        [SerializeField, Range(0f, 1f)] private float midPhaseEnd = 0.55f;
        [SerializeField, Range(1, 6)] private int maximumSpawnsPerPulse = 3;
        [SerializeField, Min(0.05f)] private float raceCreatureFadeInDuration = 0.85f;
        [SerializeField, Range(2, 8)] private int raceTurtleSchoolMinimum = 3;
        [SerializeField, Range(3, 12)] private int raceTurtleSchoolMaximum = 6;
        private bool hasStoryProgressionSnapshot;
        private SurfAbility storyUnlockedSnapshot;
        private int storyJumpUpgradeSnapshot;
        private int storySlashUpgradeSnapshot;
        private int storySkidUpgradeSnapshot;

        public bool IsSelectionVisible => selectionRoot != null;

        public static RaceModeManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            RaceModeManager existing = FindFirstObjectByType<RaceModeManager>();
            if (existing != null) return existing;
            return new GameObject("Race Mode Manager").AddComponent<RaceModeManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ShowSelection(SurferSlugPauseMenu menu)
        {
            selectionMenu = menu;

            if (selectionMenu != null)
                selectionMenu.SetRaceSelectionPresentation(true);

            BuildSelection(menu);
        }

        private void BuildSelection(SurferSlugPauseMenu menu)
        {
            if (selectionRoot != null) Destroy(selectionRoot);
            EnsureCanvas();

            selectionRoot = CreatePanel(
                canvas.transform,
                "Race Surfer Selection",
                new Color(0f, 0.015f, 0.025f, 0.82f));

            GameObject window = new GameObject(
                "Selection Window",
                typeof(RectTransform),
                typeof(Image));
            window.transform.SetParent(selectionRoot.transform, false);
            RectTransform windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(1024f, 412f);

            Image windowImage = window.GetComponent<Image>();
            windowImage.sprite = Resources.Load<Sprite>("SurferSlugUI/Panels/race_mode_panel");
            windowImage.type = Image.Type.Simple;
            windowImage.preserveAspect = true;
            windowImage.color = Color.white;

            TextMeshProUGUI title = CreateText(
                window.transform,
                "SELECT SURFER",
                32,
                TextAlignmentOptions.Center);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.15f, 0.72f);
            titleRect.anchorMax = new Vector2(0.85f, 0.84f);
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

            GameObject row = new GameObject(
                "Roster",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            row.transform.SetParent(window.transform, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.08f, 0.24f);
            rowRect.anchorMax = new Vector2(0.92f, 0.76f);
            rowRect.offsetMin = rowRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            List<Button> buttons = new();
            foreach (string racer in Roster)
            {
                string capturedRacer = racer;
                Button button = CreateRosterButton(
                    row.transform,
                    capturedRacer,
                    GetPortraitSprite(capturedRacer),
                    () =>
                    {
                        if (selectionRoot != null)
                        {
                            Destroy(selectionRoot);
                            selectionRoot = null;
                        }

                        selectionMenu?.SetRaceSelectionPresentation(false);
                        selectionMenu?.BeginRaceMode(capturedRacer);
                    });
                buttons.Add(button);
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                Navigation nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = buttons[(i - 1 + buttons.Count) % buttons.Count],
                    selectOnRight = buttons[(i + 1) % buttons.Count],
                    selectOnUp = buttons[i],
                    selectOnDown = buttons[i]
                };
                buttons[i].navigation = nav;
            }

            TextMeshProUGUI help = CreateText(
                window.transform,
                "CHOOSE  •  ENTER / A TO START  •  ESC / B TO BACK",
                18,
                TextAlignmentOptions.Center);
            RectTransform helpRect = help.rectTransform;
            helpRect.anchorMin = new Vector2(0.08f, 0.08f);
            helpRect.anchorMax = new Vector2(0.92f, 0.18f);
            helpRect.offsetMin = helpRect.offsetMax = Vector2.zero;
            help.color = new Color(0.72f, 0.88f, 0.92f, 1f);

            EventSystem.current?.SetSelectedGameObject(
                buttons.Count > 0 ? buttons[0].gameObject : null);
        }

        public void BeginRace(string selectedSurfer, bool showHudImmediately = true)
        {
            GameModeSession.SelectRaceMode();
            ExitRaceMode(false);
            DestroyExistingSurfers(false);
            raceTeardownInProgress = false;
            CaptureStoryProgression();
            RaceActive = true;
            raceTimeRemaining = PrototypeRaceSeconds;
            SurfAbilityProgression.Instance?.DebugUnlockAll();
            DisableStoryAndSpawners();
            SpawnRoster(selectedSurfer);
            BindCameraToSelectedRacer();
            SetupRaceEcosystem();
            RandomizeRaceAtmosphere();
            BuildRaceHud();
            SetRaceHudVisible(showHudImmediately);
            StartMusic();
        }

        public void SetRaceHudVisible(bool visible)
        {
            if (raceHud != null)
                raceHud.SetActive(visible);
        }

        private void Update()
        {
            if (selectionRoot != null && CancelPressed())
            {
                Destroy(selectionRoot);
                selectionRoot = null;
                selectionMenu?.SetRaceSelectionPresentation(false);
                return;
            }

            if (!RaceActive) return;
            raceTimeRemaining = Mathf.Max(0f, raceTimeRemaining - Time.deltaTime);
            foreach (Racer racer in racers)
            {
                if (racer.Surfer == null) continue;
                float x = racer.Surfer.transform.position.x;

                // Race progress only increases when reaching a new furthest-right position.
                // Moving left does not add distance, and returning over old ground does not
                // count the same distance twice.
                float forwardProgress = Mathf.Max(0f, x - racer.StartX);
                racer.Distance = Mathf.Max(racer.Distance, forwardProgress);

                racer.LastX = x;
            }
            RefreshHud();
            UpdateRaceEcosystem();
            UpdateRaceBosses();
            UpdateRaceWeatherPattern();
            if (raceTimeRemaining <= 0f) FinishRace();
        }


        private void LateUpdate()
        {
            // The pause menu and EventSystem can process B/Escape before this
            // manager's normal Update. Check again at the end of the frame so
            // character select always honours the Back command.
            if (selectionRoot == null || !CancelPressed())
                return;

            Destroy(selectionRoot);
            selectionRoot = null;
            selectionMenu?.SetRaceSelectionPresentation(false);
        }

        private static bool CancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null &&
                gamepad.buttonEast.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                return true;
            }
#endif
            return false;
        }

        private void SpawnRoster(string selected)
        {
            PixelWaterGPU master = FindFirstObjectByType<PixelWaterGPU>();
            float speed = master != null ? master.SinglePlayerScrollSpeed : 2.5f;
            float boost = master != null ? master.SinglePlayerBoostMultiplier : 1.6f;

            List<PixelWaterGPU> nearbyLayers = EndlessWaveSections.LayersNearest(0f);
            int waveCount = Mathf.Max(1, nearbyLayers != null ? nearbyLayers.Count : 0);
            List<int> shuffledWaves = Enumerable.Range(0, waveCount)
                .OrderBy(_ => UnityEngine.Random.value)
                .ToList();

            string[] spawnOrder = Roster.OrderByDescending(n => string.Equals(n, selected, StringComparison.OrdinalIgnoreCase)).ToArray();
            float startX = DetermineRaceStartX(master);

            for (int i = 0; i < spawnOrder.Length; i++)
            {
                string name = spawnOrder[i];
                bool player = string.Equals(name, selected, StringComparison.OrdinalIgnoreCase);
                int randomWave = i < shuffledWaves.Count
                    ? shuffledWaves[i]
                    : UnityEngine.Random.Range(0, waveCount);

                GameObject go = new GameObject(player ? "Race Player - " + name : "Race AI - " + name);
                TinyWaveSurfer surfer = go.AddComponent<TinyWaveSurfer>();
                surfer.ConfigureGeneratedSurfer(randomWave, true, 0.95f, Color.white, Color.white, 100 + i, 0.2f + i * 0.1f, i * 0.08f);
                surfer.ConfigureRaceSurfer(!player, speed * (player ? 1f : UnityEngine.Random.Range(0.93f, 1.07f)), boost);
                surfer.ForceRaceStartingLine(startX, randomWave);

                RaceSurferSkin skin = go.AddComponent<RaceSurferSkin>();
                skin.Configure(name);
                racers.Add(new Racer
                {
                    Name = name,
                    Surfer = surfer,
                    StartX = startX,
                    LastX = startX,
                    Distance = 0f,
                    Player = player
                });
            }
        }

        private void BindCameraToSelectedRacer()
        {
            Racer selected = racers.FirstOrDefault(r => r.Player && r.Surfer != null);
            if (selected == null)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            BeachCameraFollow legacyFollow = camera.GetComponent<BeachCameraFollow>();
            if (legacyFollow != null)
            {
                legacyFollow.Target = null;
                legacyFollow.enabled = false;
            }

            TinySurferCinematicCamera follow = camera.GetComponent<TinySurferCinematicCamera>();
            if (follow != null)
            {
                follow.enabled = true;
                follow.SetFollowTarget(selected.Surfer, true);
            }
        }

        private static float DetermineRaceStartX(PixelWaterGPU master)
        {
            if (master != null)
                return Mathf.Lerp(master.TankMinimum.x, master.TankMaximum.x, 0.28f);

            Camera camera = Camera.main;
            return camera != null ? camera.transform.position.x - 2f : -2f;
        }

        public void ExitRaceMode(bool destroyRacers)
        {
            RaceActive = false;
            raceTeardownInProgress = destroyRacers;

            DisableAndDestroy(ref selectionRoot);
            DisableAndDestroy(ref raceHud);
            DisableAndDestroy(ref ecosystemRoot);

            ClearRaceCameraTarget();

            if (raceSky == null)
                raceSky = FindFirstObjectByType<ProceduralStarryNight>();
            raceSky?.ClearExternalTimeOverride();
            raceSky = null;
            StopMusicImmediately();

            if (destroyRacers)
            {
                DestroyExistingSurfers(true);
                StartCoroutine(CompleteRaceTeardown());
            }
            else
            {
                raceTeardownInProgress = false;
            }

            RestoreStoryProgression();
            GameplayTargetCache.Refresh();

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour == this)
                    continue;

                string n = behaviour.GetType().Name;
                if (n.Contains("Spawner", StringComparison.OrdinalIgnoreCase) ||
                    n.Contains("ProgressionDirector", StringComparison.OrdinalIgnoreCase))
                {
                    behaviour.enabled = GameModeSession.IsStory;
                }
            }
        }

        public IEnumerator WaitForRaceTeardown()
        {
            float timeoutAt = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < timeoutAt &&
                   (raceTeardownInProgress || HasRaceOwnedObjects()))
            {
                yield return null;
            }

            // One final end-of-frame boundary prevents deferred Destroy calls from
            // crossing into the destination mode's construction frame.
            yield return new WaitForEndOfFrame();
        }

        private IEnumerator CompleteRaceTeardown()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;
            raceTeardownInProgress = HasRaceOwnedObjects();

            if (raceTeardownInProgress)
            {
                // Disable any orphaned race object immediately and give Unity one
                // more frame to process its deferred destruction.
                DestroyExistingSurfers(true);
                yield return null;
                yield return new WaitForEndOfFrame();
            }

            raceTeardownInProgress = false;
            GameplayTargetCache.Refresh();
        }

        private static void DisableAndDestroy(ref GameObject target)
        {
            if (target == null)
                return;

            target.SetActive(false);
            Destroy(target);
            target = null;
        }

        private static bool HasRaceOwnedObjects()
        {
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (surfer == null)
                    continue;

                string objectName = surfer.gameObject.name;
                if (objectName.StartsWith("Race Player -", StringComparison.Ordinal) ||
                    objectName.StartsWith("Race AI -", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return FindFirstObjectByType<RaceBossHasteFollower>(
                       FindObjectsInactive.Include) != null ||
                   GameObject.Find("Race Mode Random Ecosystem") != null;
        }

        private void ClearRaceCameraTarget()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            TinySurferCinematicCamera follow =
                camera.GetComponent<TinySurferCinematicCamera>();
            follow?.SetFollowTarget(null, false);
        }

        private void CaptureStoryProgression()
        {
            SurfAbilityProgression progression = SurfAbilityProgression.Instance;
            if (progression == null)
                return;

            storyUnlockedSnapshot = progression.Unlocked;
            storyJumpUpgradeSnapshot = progression.JumpUpgradeLevel;
            storySlashUpgradeSnapshot = progression.WaterSlashUpgradeLevel;
            storySkidUpgradeSnapshot = progression.SkidUpgradeLevel;
            hasStoryProgressionSnapshot = true;
        }

        private void RestoreStoryProgression()
        {
            if (!hasStoryProgressionSnapshot || SurfAbilityProgression.Instance == null)
                return;

            SurfAbilityProgression.Instance.RestoreExact(
                storyUnlockedSnapshot,
                storyJumpUpgradeSnapshot,
                storySlashUpgradeSnapshot,
                storySkidUpgradeSnapshot);
            hasStoryProgressionSnapshot = false;
        }

        private void FinishRace()
        {
            RaceActive = false;
            racers.Sort((a, b) => b.Distance.CompareTo(a.Distance));
            if (timerLabel != null) timerLabel.text = "RACE COMPLETE";
            if (standingsLabel != null)
                standingsLabel.text = string.Join("\n", racers.Select((r, i) => $"{i + 1}. {r.Name.ToUpperInvariant()}  {r.Distance:0.0}m"));
            BeginMusicFadeOut();
            if (ecosystemRoot != null)
            {
                Destroy(ecosystemRoot);
                ecosystemRoot = null;
            }
        }

        private void RandomizeRaceAtmosphere()
        {
            raceSky = FindFirstObjectByType<ProceduralStarryNight>();
            if (raceSky != null)
            {
                // Any point in the full day/night cycle can be selected for a race.
                float randomTime = UnityEngine.Random.value;
                raceSky.BeginExternalTimeTransition(
                    randomTime,
                    raceTimeTransitionSeconds);
            }

            raceRain = FindFirstObjectByType<ProceduralRainSystem>();
            if (raceRain == null)
            {
                raceRain = new GameObject("Race Weather System")
                    .AddComponent<ProceduralRainSystem>();
            }

            ApplyRandomRaceWeather();
        }

        private void UpdateRaceWeatherPattern()
        {
            if (!RaceActive || Time.time < nextRaceWeatherChangeTime)
                return;

            if (raceRain == null)
                raceRain = FindFirstObjectByType<ProceduralRainSystem>();

            ApplyRandomRaceWeather();
        }

        private void ApplyRandomRaceWeather()
        {
            if (raceRain == null)
                return;

            ProceduralRainSystem.RainSituation situation;
            if (UnityEngine.Random.value < raceClearWeatherChance)
            {
                situation = ProceduralRainSystem.RainSituation.Clear;
            }
            else
            {
                int count = Enum.GetValues(
                    typeof(ProceduralRainSystem.RainSituation)).Length;
                situation = (ProceduralRainSystem.RainSituation)
                    UnityEngine.Random.Range(1, count);
            }

            raceRain.SetSituation(situation);
            nextRaceWeatherChangeTime = Time.time + UnityEngine.Random.Range(
                Mathf.Min(minimumRaceWeatherDuration, maximumRaceWeatherDuration),
                Mathf.Max(minimumRaceWeatherDuration, maximumRaceWeatherDuration));
        }

        private void SetupRaceEcosystem()
        {
            if (ecosystemRoot != null)
                Destroy(ecosystemRoot);

            ecosystemRoot = new GameObject("Race Mode Random Ecosystem");
            DontDestroyOnLoad(ecosystemRoot);
            nextEcosystemSpawnTime = Time.time + 0.75f;
            raceReaperSpawned = false;
            raceRubberDuckSpawned = false;
            SuppressRaceBossArenas();

            // Guarantee the signature Race Mode ecosystem appears immediately.
            // These used to be hidden behind a ten-way random roll, so an entire
            // race could pass without showing one of them.
            SpawnSpecificRaceCreature(3); // Jellyfish school
            SpawnSpecificRaceCreature(4); // Blood shark
            SpawnSpecificRaceCreature(7); // Bloodfish school
            SpawnSpecificRaceCreature(8); // Baby sea turtle school
            SpawnSpecificRaceCreature(9); // Giant turtle


            // Add two random creatures so the opening still changes each race.
            SpawnRandomWaterEnemy();
            SpawnRandomWaterEnemy();
        }

        private void UpdateRaceEcosystem()
        {
            if (ecosystemRoot == null || Time.time < nextEcosystemSpawnTime)
                return;

            GetRaceEcosystemDifficulty(out int enemyCap, out float spawnInterval);
            int activeEnemies = CountActiveRaceEnemies();
            int missing = Mathf.Max(0, enemyCap - activeEnemies);
            int spawnCount = Mathf.Min(missing, Mathf.Max(1, maximumSpawnsPerPulse));

            for (int i = 0; i < spawnCount; i++)
                SpawnRandomWaterEnemy();

            float jitter = UnityEngine.Random.Range(0.82f, 1.18f);
            nextEcosystemSpawnTime = Time.time + spawnInterval * jitter;
        }

        private void GetRaceEcosystemDifficulty(out int enemyCap, out float spawnInterval)
        {
            float progress = 1f - raceTimeRemaining / Mathf.Max(1f, PrototypeRaceSeconds);
            progress = Mathf.Clamp01(progress);

            float openingEnd = Mathf.Clamp01(openingPhaseEnd);
            float earlyEnd = Mathf.Max(openingEnd, Mathf.Clamp01(earlyPhaseEnd));
            float midEnd = Mathf.Max(earlyEnd, Mathf.Clamp01(midPhaseEnd));

            if (progress < openingEnd)
            {
                enemyCap = openingEnemyCap;
                spawnInterval = openingSpawnInterval;
            }
            else if (progress < earlyEnd)
            {
                enemyCap = earlyEnemyCap;
                spawnInterval = earlySpawnInterval;
            }
            else if (progress < midEnd)
            {
                enemyCap = midEnemyCap;
                spawnInterval = midSpawnInterval;
            }
            else
            {
                enemyCap = finalEnemyCap;
                spawnInterval = finalSpawnInterval;
            }

            enemyCap = Mathf.Max(1, enemyCap);
            spawnInterval = Mathf.Max(0.5f, spawnInterval);
        }

        private static int CountActiveRaceEnemies()
        {
            return
                FindObjectsByType<SharkLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<GiantSquidLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<WhaleLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<JellyfishSchoolController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<BloodSharkLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<TransparentSquidLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<StingrayLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<BloodfishSchoolController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<SeaTurtleSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<GiantTurtleSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        }

        private void SpawnRandomWaterEnemy()
        {
            SpawnSpecificRaceCreature(UnityEngine.Random.Range(0, 10));
        }

        private void SpawnSpecificRaceCreature(int creatureIndex)
        {
            if (ecosystemRoot == null)
                return;

            Transform holder = new GameObject("Race Ecosystem Spawn").transform;
            holder.SetParent(ecosystemRoot.transform, false);

            EndlessWaveSections sections = EndlessWaveSections.Instance;
            if (sections != null && sections.IsReady)
            {
                IReadOnlyList<float> centres = sections.GetSectionCentres();
                if (centres.Count > 0)
                    holder.position = new Vector3(centres[UnityEngine.Random.Range(0, centres.Count)], 0f, 0f);
            }

            // Every ordinary sea creature is eligible. Bosses, boss minions,
            // aircraft, UFOs and boombox surfers are intentionally excluded.
            switch (Mathf.Clamp(creatureIndex, 0, 9))
            {
                case 0:
                    holder.gameObject.AddComponent<SharkLaneSpawner>().SpawnShark(true);
                    break;
                case 1:
                    holder.gameObject.AddComponent<GiantSquidLaneSpawner>().SpawnSquid(true);
                    break;
                case 2:
                    holder.gameObject.AddComponent<WhaleLaneSpawner>().SpawnWhale(true);
                    break;
                case 3:
                    holder.gameObject.AddComponent<JellyfishSchoolSpawner>().SpawnSchool();
                    break;
                case 4:
                    holder.gameObject.AddComponent<BloodSharkLaneSpawner>().SpawnBloodShark(true);
                    break;
                case 5:
                    holder.gameObject.AddComponent<TransparentSquidLaneSpawner>().SpawnTransparentSquid(true);
                    break;
                case 6:
                    holder.gameObject.AddComponent<StingrayLaneSpawner>().SpawnStingray(true);
                    break;
                case 7:
                    holder.gameObject.AddComponent<BloodfishSchoolSpawner>().SpawnSchool();
                    break;
                case 8:
                    SpawnRaceSeaTurtleSchool(holder);
                    break;
                default:
                    SpawnRaceGiantTurtle(holder);
                    break;
            }

            EnsureRaceCreatureFade(holder.gameObject);
        }

        private void SpawnRaceSeaTurtleSchool(Transform holder)
        {
            int laneCount = Mathf.Max(1, EndlessWaveSections.LayersNearest(holder.position.x).Count - 1);
            int lane = UnityEngine.Random.Range(0, laneCount);
            int low = Mathf.Max(2, Mathf.Min(raceTurtleSchoolMinimum, raceTurtleSchoolMaximum));
            int high = Mathf.Max(low, Mathf.Max(raceTurtleSchoolMinimum, raceTurtleSchoolMaximum));
            int count = UnityEngine.Random.Range(low, high + 1);
            float direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            Transform leader = null;

            for (int i = 0; i < count; i++)
            {
                GameObject turtle = new GameObject($"Race Sea Turtle {i + 1}");
                turtle.transform.SetParent(holder, false);
                turtle.AddComponent<SpriteRenderer>();
                turtle.AddComponent<InterWaveRenderItem>();
                turtle.AddComponent<Rigidbody2D>();
                turtle.AddComponent<CircleCollider2D>();

                Vector2 offset = new(
                    -direction * i * 0.38f,
                    (i % 2 == 0 ? 1f : -1f) * 0.12f * Mathf.Ceil(i * 0.5f));

                SeaTurtleSwimmer swimmer = turtle.AddComponent<SeaTurtleSwimmer>();
                swimmer.Initialise(
                    Mathf.Clamp(lane + (i == count - 1 && count > 3 ? 1 : 0), 0, laneCount - 1),
                    leader,
                    offset,
                    direction);

                if (i == 0)
                    leader = turtle.transform;
            }
        }

        private static void SpawnRaceGiantTurtle(Transform holder)
        {
            int laneCount = Mathf.Max(1, EndlessWaveSections.LayersNearest(holder.position.x).Count - 1);
            GameObject turtle = new GameObject("Race Giant Turtle");
            turtle.transform.SetParent(holder, false);
            turtle.AddComponent<SpriteRenderer>();
            turtle.AddComponent<InterWaveRenderItem>();
            turtle.AddComponent<Rigidbody2D>();
            turtle.AddComponent<BoxCollider2D>();
            turtle.AddComponent<GiantTurtleSwimmer>().Initialise(
                UnityEngine.Random.Range(0, laneCount));
        }

        private void EnsureRaceCreatureFade(GameObject root)
        {
            if (root == null)
                return;

            OceanSpawnFadeIn fade = root.GetComponent<OceanSpawnFadeIn>();
            if (fade == null)
                fade = root.AddComponent<OceanSpawnFadeIn>();

            fade.Configure(raceCreatureFadeInDuration);
        }

        private void UpdateRaceBosses()
        {
            if (!RaceActive)
                return;

            // Story-mode boss encounters build BossArenaPrison objects. Race mode
            // never uses those arenas; bosses remain free-moving hazards.
            SuppressRaceBossArenas();
            RemoveDuplicateRaceBosses();

            float elapsed = PrototypeRaceSeconds - raceTimeRemaining;

            if (!raceReaperSpawned &&
                elapsed >= Mathf.Max(0f, reaperSpawnAfterSeconds))
            {
                raceReaperSpawned = true;
                SpawnRaceBoss<GodzillaLaneSwimmer>(
                    "Race Reaper",
                    raceBossVisibleSpawnOffset);
            }

            if (!raceRubberDuckSpawned &&
                elapsed >= Mathf.Max(0f, rubberDuckSpawnAfterSeconds))
            {
                raceRubberDuckSpawned = true;
                SpawnRaceBoss<RubberDuckBossSwimmer>(
                    "Race Rubber Duck",
                    -raceBossVisibleSpawnOffset);
            }

            AttachRaceFollowToExistingBosses();
        }

        private void SpawnRaceBoss<TBoss>(
            string objectName,
            float horizontalOffset)
            where TBoss : MonoBehaviour
        {
            if (ecosystemRoot == null)
                return;

            // A story spawner, developer command, or a second race pulse may have
            // created this boss already. Adopt the existing instance rather than
            // creating another one.
            TBoss existingBoss = FindObjectsByType<TBoss>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault();

            if (existingBoss != null)
            {
                EnsureRaceBossFollower(existingBoss);
                return;
            }

            Racer targetRacer = racers.FirstOrDefault(
                racer =>
                    racer.Player &&
                    racer.Surfer != null &&
                    !racer.Surfer.IsDead);

            if (targetRacer == null)
            {
                targetRacer = racers.FirstOrDefault(
                    racer =>
                        racer.Surfer != null &&
                        !racer.Surfer.IsDead);
            }

            Vector3 spawnPosition =
                targetRacer != null && targetRacer.Surfer != null
                    ? targetRacer.Surfer.transform.position
                    : Vector3.zero;

            spawnPosition = FindSafeRaceBossSpawn(
                spawnPosition,
                horizontalOffset);

            GameObject bossObject = new GameObject(objectName);
            bossObject.transform.SetParent(ecosystemRoot.transform, false);
            bossObject.transform.position = spawnPosition;

            // RequireComponent attributes on each boss add their normal renderer,
            // rigidbody and collider dependencies. Their ordinary attack logic stays
            // active; only arena confinement is omitted.
            bossObject.AddComponent<TBoss>();

            RaceBossHasteFollower follower =
                bossObject.AddComponent<RaceBossHasteFollower>();

            follower.Configure(
                targetRacer != null ? targetRacer.Surfer : null,
                raceBossFollowDistance,
                raceBossMaximumSpeed,
                raceBossAcceleration);

            RaceBossChildRecycler recycler =
                bossObject.AddComponent<RaceBossChildRecycler>();

            recycler.Configure(
                targetRacer != null ? targetRacer.Surfer : null,
                raceBossChildRecycleDistance);

            OceanSpawnFadeIn fade =
                bossObject.GetComponent<OceanSpawnFadeIn>();

            if (fade == null)
                fade = bossObject.AddComponent<OceanSpawnFadeIn>();

            fade.Configure(raceCreatureFadeInDuration);
        }

        private static Vector3 FindSafeRaceBossSpawn(
            Vector3 racerPosition,
            float requestedOffset)
        {
            float side = Mathf.Approximately(requestedOffset, 0f)
                ? 1f
                : Mathf.Sign(requestedOffset);

            float safeOffset = Mathf.Max(3.5f, Mathf.Abs(requestedOffset));
            Vector3 result = racerPosition;
            result.x += side * safeOffset;

            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                float halfWidth = camera.orthographicSize * camera.aspect;
                float cameraLeft = camera.transform.position.x - halfWidth + 0.75f;
                float cameraRight = camera.transform.position.x + halfWidth - 0.75f;
                result.x = Mathf.Clamp(result.x, cameraLeft, cameraRight);

                // If clamping made the boss too close, use the opposite safe side.
                if (Mathf.Abs(result.x - racerPosition.x) < 2.75f)
                {
                    float opposite = racerPosition.x - side * safeOffset;
                    result.x = Mathf.Clamp(opposite, cameraLeft, cameraRight);
                }
            }

            // Keep the boss in the active racer water band instead of allowing its
            // own Initialise/Start path to leave it above or below the visible lane.
            result.y = racerPosition.y;
            return result;
        }

        private void AttachRaceFollowToExistingBosses()
        {
            foreach (GodzillaLaneSwimmer boss in
                     FindObjectsByType<GodzillaLaneSwimmer>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                EnsureRaceBossFollower(boss);
            }

            foreach (RubberDuckBossSwimmer boss in
                     FindObjectsByType<RubberDuckBossSwimmer>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                EnsureRaceBossFollower(boss);
            }
        }

        private void EnsureRaceBossFollower(MonoBehaviour boss)
        {
            if (boss == null)
                return;

            RaceBossHasteFollower follower =
                boss.GetComponent<RaceBossHasteFollower>();

            if (follower == null)
                follower = boss.gameObject.AddComponent<RaceBossHasteFollower>();

            Racer targetRacer = racers.FirstOrDefault(
                racer =>
                    racer.Player &&
                    racer.Surfer != null &&
                    !racer.Surfer.IsDead);

            follower.Configure(
                targetRacer != null ? targetRacer.Surfer : null,
                raceBossFollowDistance,
                raceBossMaximumSpeed,
                raceBossAcceleration);

            RaceBossChildRecycler recycler =
                boss.GetComponent<RaceBossChildRecycler>();

            if (recycler == null)
                recycler = boss.gameObject.AddComponent<RaceBossChildRecycler>();

            recycler.Configure(
                targetRacer != null ? targetRacer.Surfer : null,
                raceBossChildRecycleDistance);
        }

        private static void RemoveDuplicateRaceBosses()
        {
            KeepSingleBoss(
                FindObjectsByType<GodzillaLaneSwimmer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None));

            KeepSingleBoss(
                FindObjectsByType<RubberDuckBossSwimmer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None));
        }

        private static void KeepSingleBoss<TBoss>(TBoss[] bosses)
            where TBoss : MonoBehaviour
        {
            if (bosses == null || bosses.Length <= 1)
                return;

            // Prefer the boss owned by the race ecosystem.
            // Otherwise keep the first valid boss Unity returned.
            TBoss keeper = bosses
                .OrderByDescending(boss =>
                    boss != null &&
                    boss.transform.parent != null &&
                    boss.transform.parent.name.Contains(
                        "Race Mode Random Ecosystem",
                        StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(boss => boss != null);

            foreach (TBoss boss in bosses)
            {
                if (boss != null && boss != keeper)
                    Destroy(boss.gameObject);
            }
        }

        private static void SuppressRaceBossArenas()
        {
            if (!RaceActive && !GameModeSession.IsRace)
                return;

            foreach (BossArenaPrison arena in
                     FindObjectsByType<BossArenaPrison>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (arena != null)
                    Destroy(arena.gameObject);
            }
        }

        private void DisableStoryAndSpawners()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour == this) continue;
                string n = behaviour.GetType().Name;
                if (n.Contains("Spawner", StringComparison.OrdinalIgnoreCase) || n.Contains("ProgressionDirector", StringComparison.OrdinalIgnoreCase))
                    behaviour.enabled = false;
            }
            foreach (BoomboxSurferSwimmer box in FindObjectsByType<BoomboxSurferSwimmer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Destroy(box.gameObject);

            foreach (GodzillaLaneSwimmer boss in FindObjectsByType<GodzillaLaneSwimmer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Destroy(boss.gameObject);
            foreach (RubberDuckBossSwimmer boss in FindObjectsByType<RubberDuckBossSwimmer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Destroy(boss.gameObject);
        }

        private void DestroyExistingSurfers(bool raceOwnedOnly)
        {
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (surfer == null)
                    continue;

                string objectName = surfer.gameObject.name;
                bool raceOwned =
                    objectName.StartsWith("Race Player -", StringComparison.Ordinal) ||
                    objectName.StartsWith("Race AI -", StringComparison.Ordinal) ||
                    racers.Any(racer => racer.Surfer == surfer);

                if (raceOwnedOnly && !raceOwned)
                    continue;

                surfer.gameObject.SetActive(false);
                Destroy(surfer.gameObject);
            }

            foreach (TinyWaveSurferSpawnListener listener in
                     FindObjectsByType<TinyWaveSurferSpawnListener>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (listener == null)
                    continue;

                if (raceOwnedOnly &&
                    !listener.gameObject.name.Contains(
                        "Race",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                listener.gameObject.SetActive(false);
                Destroy(listener.gameObject);
            }

            racers.Clear();
        }

        private void StartMusic()
        {
            if (musicSource == null)
                musicSource = gameObject.AddComponent<AudioSource>();

            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
                musicFadeCoroutine = null;
            }

            musicSource.clip = Resources.Load<AudioClip>(
                "Audio/Music/Death Surfer");
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0.8f;

            if (musicSource.clip != null)
                musicSource.Play();
        }

        private void BeginMusicFadeOut()
        {
            if (musicSource == null || !musicSource.isPlaying)
                return;

            if (musicFadeCoroutine != null)
                StopCoroutine(musicFadeCoroutine);

            musicFadeCoroutine = StartCoroutine(FadeOutRaceMusic());
        }

        private IEnumerator FadeOutRaceMusic()
        {
            float duration = Mathf.Max(0.05f, raceMusicFadeOutSeconds);
            float startingVolume = musicSource != null
                ? musicSource.volume
                : 0f;
            float elapsed = 0f;

            while (musicSource != null && musicSource.isPlaying && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(
                    startingVolume,
                    0f,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.volume = 0.8f;
            }

            musicFadeCoroutine = null;
        }

        private void StopMusicImmediately()
        {
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
                musicFadeCoroutine = null;
            }

            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.volume = 0.8f;
            }
        }

        private void BuildRaceHud()
        {
            EnsureCanvas();

            if (raceHud != null)
                Destroy(raceHud);

            raceHud = new GameObject(
                "Race HUD",
                typeof(RectTransform),
                typeof(CanvasGroup));

            raceHud.transform.SetParent(canvas.transform, false);

            RectTransform root = raceHud.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            // Header
            TextMeshProUGUI modeLabel =
                CreateText(raceHud.transform, "RACE MODE", 24, TextAlignmentOptions.Top);

            modeLabel.rectTransform.anchorMin = new Vector2(0.40f, 0.955f);
            modeLabel.rectTransform.anchorMax = new Vector2(0.60f, 0.995f);
            modeLabel.rectTransform.offsetMin = Vector2.zero;
            modeLabel.rectTransform.offsetMax = Vector2.zero;

            // Timer (lowered to create spacing)
            timerLabel =
                CreateText(raceHud.transform, "1:15", 40, TextAlignmentOptions.Top);

            timerLabel.rectTransform.anchorMin = new Vector2(0.35f, 0.81f);
            timerLabel.rectTransform.anchorMax = new Vector2(0.65f, 0.91f);
            timerLabel.rectTransform.offsetMin = Vector2.zero;
            timerLabel.rectTransform.offsetMax = Vector2.zero;

            // Standings
            standingsLabel =
                CreateText(raceHud.transform, string.Empty, 23, TextAlignmentOptions.TopLeft);

            standingsLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.68f);
            standingsLabel.rectTransform.anchorMax = new Vector2(0.28f, 0.94f);
            standingsLabel.rectTransform.offsetMin = Vector2.zero;
            standingsLabel.rectTransform.offsetMax = Vector2.zero;
        }

        private void RefreshHud()
        {
            if (timerLabel != null)
            {
                int seconds = Mathf.CeilToInt(raceTimeRemaining);
                timerLabel.text = $"{seconds / 60}:{seconds % 60:00}";
            }
            if (standingsLabel != null)
            {
                var ordered = racers.OrderByDescending(r => r.Distance).ToArray();
                standingsLabel.text = string.Join("\n", ordered.Select((r, i) => $"{i + 1}. {(r.Player ? "> " : string.Empty)}{r.Name.ToUpperInvariant()}  {r.Distance:0.0}m"));
            }
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            GameObject go = new GameObject("Race Mode Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 32767;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color; return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.enableWordWrapping = false;
            PixelFontLibrary.Apply(label, size >= 28f, size >= 20f);
            return label;
        }

        private static Sprite GetPortraitSprite(string racer)
        {
            string lower = racer.ToLowerInvariant();
            string[] paths = string.Equals(racer, "Chuck", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    "Surfers/chuck",
                    "RaceSurfers/Chuck/chuck_idle",
                    "SurferSlug/Chuck/chuck_idle",
                    "Surfers/chuck_idle",
                    "chuck_idle"
                }
                : new[] { $"RaceSurfers/{racer}/{lower}_idle" };

            foreach (string path in paths)
            {
                Sprite[] frames = Resources.LoadAll<Sprite>(path);
                if (frames != null && frames.Length > 0)
                {
                    Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                    return frames[0];
                }
                Sprite single = Resources.Load<Sprite>(path);
                if (single != null) return single;
            }
            return null;
        }

        private static Button CreateRosterButton(Transform parent, string racer, Sprite portrait, UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject(
                racer,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement),
                typeof(Outline),
                typeof(EventTrigger));
            go.transform.SetParent(parent, false);

            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 220f;
            le.preferredHeight = 220f;

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.015f, 0.055f, 0.075f, 0.82f);

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;
            outline.enabled = false;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(action);

            EventTrigger trigger = go.GetComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();
            AddSelectionTrigger(trigger, EventTriggerType.Select, _ => outline.enabled = true);
            AddSelectionTrigger(trigger, EventTriggerType.Deselect, _ => outline.enabled = false);
            AddSelectionTrigger(trigger, EventTriggerType.PointerEnter, _ =>
            {
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(go);
            });

            if (portrait != null)
            {
                GameObject portraitObject = new GameObject(
                    "Single Frame Portrait",
                    typeof(RectTransform),
                    typeof(Image));
                portraitObject.transform.SetParent(go.transform, false);
                Image portraitImage = portraitObject.GetComponent<Image>();
                portraitImage.sprite = portrait;
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;
                RectTransform pr = portraitImage.rectTransform;
                pr.anchorMin = new Vector2(0.18f, 0.27f);
                pr.anchorMax = new Vector2(0.82f, 0.88f);
                pr.offsetMin = pr.offsetMax = Vector2.zero;
            }

            TextMeshProUGUI label = CreateText(
                go.transform,
                racer.ToUpperInvariant(),
                20,
                TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = new Vector2(0f, 0.05f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.25f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            PixelFontLibrary.Apply(label, false, true);
            return button;
        }

        private static void AddSelectionTrigger(
            EventTrigger trigger,
            EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = type,
                callback = new EventTrigger.TriggerEvent()
            };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }
    }

    [DefaultExecutionOrder(25000)]
    [DisallowMultipleComponent]
    internal sealed class RaceBossHasteFollower : MonoBehaviour
    {
        private TinyWaveSurfer target;
        private Rigidbody2D body;
        private float desiredDistance = 3.25f;
        private float maximumSpeed = 8.5f;
        private float acceleration = 12f;
        private float currentSpeed;
        private float lastDirection = 1f;
        private float verticalVelocity;
        private bool forcedVisiblePosition;
        private float previousTargetX;
        private float smoothedTargetVelocityX;
        private bool hasPreviousTargetX;

        public void Configure(
            TinyWaveSurfer followTarget,
            float followDistance,
            float maxSpeed,
            float accelerationRate)
        {
            if (followTarget != null)
                target = followTarget;

            desiredDistance = Mathf.Max(0.5f, followDistance);
            maximumSpeed = Mathf.Max(0.5f, maxSpeed);
            acceleration = Mathf.Max(0.1f, accelerationRate);

            if (body == null)
                body = GetComponent<Rigidbody2D>();

            if (target != null)
            {
                previousTargetX = target.transform.position.x;
                hasPreviousTargetX = true;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void LateUpdate()
        {
            if (!RaceModeManager.RaceActive)
            {
                enabled = false;
                return;
            }

            if (target == null || target.IsDead)
            {
                target = FindObjectsByType<TinyWaveSurfer>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(surfer =>
                        surfer != null &&
                        !surfer.IsDead &&
                        surfer.IsPlayerControlled)
                    .FirstOrDefault();

                if (target == null)
                    return;
            }

            Vector3 targetPosition = target.transform.position;
            Vector3 position = transform.position;

            if (hasPreviousTargetX)
            {
                float rawTargetVelocityX =
                    (targetPosition.x - previousTargetX) /
                    Mathf.Max(0.0001f, Time.deltaTime);

                smoothedTargetVelocityX = Mathf.Lerp(
                    smoothedTargetVelocityX,
                    rawTargetVelocityX,
                    1f - Mathf.Exp(-9f * Time.deltaTime));
            }
            else
            {
                hasPreviousTargetX = true;
                smoothedTargetVelocityX = 0f;
            }

            previousTargetX = targetPosition.x;

            // Boss Start()/Initialise() may relocate itself to a story-mode
            // off-screen entry point. Pull it back beside the racer once, after
            // that initialization has occurred.
            if (!forcedVisiblePosition)
            {
                float side = position.x >= targetPosition.x ? 1f : -1f;
                position.x = targetPosition.x + side * desiredDistance;
                position.y = targetPosition.y;
                transform.position = position;

                if (body != null)
                    body.position = position;

                forcedVisiblePosition = true;
            }

            float delta = targetPosition.x - position.x;
            float absoluteDelta = Mathf.Abs(delta);

            if (absoluteDelta > 0.12f)
                lastDirection = Mathf.Sign(delta);

            float distanceError = Mathf.Max(
                0f,
                absoluteDelta - desiredDistance);

            float racerSpeed =
                Mathf.Abs(smoothedTargetVelocityX);

            float targetSpeed = distanceError <= 0f
                ? Mathf.Min(maximumSpeed, racerSpeed * 0.92f)
                : Mathf.Min(
                    maximumSpeed,
                    racerSpeed * 0.92f +
                    1.25f +
                    distanceError * 2.35f);

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                acceleration * Time.deltaTime);

            float movement =
                lastDirection *
                currentSpeed *
                Time.deltaTime;

            float allowedMovement = Mathf.Max(
                0f,
                absoluteDelta - desiredDistance);

            movement = Mathf.Sign(movement) *
                       Mathf.Min(Mathf.Abs(movement), allowedMovement);

            position.x += movement;

            // Keep the race boss in the same visible water band as the racer.
            // The boss's own attacks still run, but it cannot disappear vertically.
            position.y = Mathf.SmoothDamp(
                position.y,
                targetPosition.y,
                ref verticalVelocity,
                0.16f,
                maximumSpeed,
                Time.deltaTime);

            if (body != null && body.bodyType == RigidbodyType2D.Kinematic)
                body.position = position;

            transform.position = position;
        }
    }


    [DefaultExecutionOrder(26000)]
    [DisallowMultipleComponent]
    internal sealed class RaceBossChildRecycler : MonoBehaviour
    {
        private TinyWaveSurfer target;
        private float recycleDistance = 14f;
        private float nextScanTime;

        public void Configure(
            TinyWaveSurfer followTarget,
            float maximumDistance)
        {
            if (followTarget != null)
                target = followTarget;

            recycleDistance = Mathf.Max(3f, maximumDistance);
        }

        private void LateUpdate()
        {
            if (!RaceModeManager.RaceActive)
            {
                enabled = false;
                return;
            }

            if (Time.time < nextScanTime)
                return;

            nextScanTime = Time.time + 0.2f;

            if (target == null || target.IsDead)
            {
                target = FindObjectsByType<TinyWaveSurfer>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(surfer =>
                        surfer != null &&
                        !surfer.IsDead &&
                        surfer.IsPlayerControlled)
                    .FirstOrDefault();

                if (target == null)
                    return;
            }

            Vector3 targetPosition = target.transform.position;

            foreach (GodzillaSkullSwimmer skull in
                     FindObjectsByType<GodzillaSkullSwimmer>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                RecycleChild(skull != null ? skull.transform : null, targetPosition);
            }

            foreach (RubberDucklingSwimmer duckling in
                     FindObjectsByType<RubberDucklingSwimmer>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                RecycleChild(duckling != null ? duckling.transform : null, targetPosition);
            }
        }

        private void RecycleChild(
            Transform child,
            Vector3 targetPosition)
        {
            if (child == null)
                return;

            float horizontalDistance =
                Mathf.Abs(child.position.x - targetPosition.x);

            if (horizontalDistance <= recycleDistance)
                return;

            Vector3 recycled = child.position;
            float side = child.position.x >= targetPosition.x ? 1f : -1f;

            recycled.x =
                targetPosition.x +
                side *
                Mathf.Min(5f, recycleDistance * 0.35f);

            recycled.y = targetPosition.y +
                         UnityEngine.Random.Range(-0.6f, 0.6f);

            child.position = recycled;

            Rigidbody2D childBody = child.GetComponent<Rigidbody2D>();
            if (childBody != null)
                childBody.position = recycled;
        }
    }

}
