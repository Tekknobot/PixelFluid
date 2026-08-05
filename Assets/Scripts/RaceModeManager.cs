using System;
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
        private const float PrototypeRaceSeconds = 75f;
        private AudioSource musicSource;
        private SurferSlugPauseMenu selectionMenu;
        private GameObject ecosystemRoot;
        private float nextEcosystemSpawnTime;

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
            windowRect.sizeDelta = new Vector2(1060f, 430f);
            window.GetComponent<Image>().color = new Color(0.015f, 0.075f, 0.105f, 0.98f);

            TextMeshProUGUI title = CreateText(
                window.transform,
                "SELECT SURFER",
                32,
                TextAlignmentOptions.Center);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.15f, 0.82f);
            titleRect.anchorMax = new Vector2(0.85f, 0.97f);
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

            GameObject row = new GameObject(
                "Roster",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            row.transform.SetParent(window.transform, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.04f, 0.22f);
            rowRect.anchorMax = new Vector2(0.96f, 0.80f);
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

                        menu.SetRaceSelectionPresentation(false);
                        menu.BeginRaceMode(capturedRacer);
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
            helpRect.anchorMin = new Vector2(0.06f, 0.04f);
            helpRect.anchorMax = new Vector2(0.94f, 0.18f);
            helpRect.offsetMin = helpRect.offsetMax = Vector2.zero;
            help.color = new Color(0.72f, 0.88f, 0.92f, 1f);

            EventSystem.current?.SetSelectedGameObject(
                buttons.Count > 0 ? buttons[0].gameObject : null);
        }

        public void BeginRace(string selectedSurfer)
        {
            GameModeSession.SelectRaceMode();
            ExitRaceMode(false);
            RaceActive = true;
            raceTimeRemaining = PrototypeRaceSeconds;
            SurfAbilityProgression.Instance?.DebugUnlockAll();
            DisableStoryAndSpawners();
            DestroyExistingSurfers();
            SpawnRoster(selectedSurfer);
            SetupRaceEcosystem();
            BuildRaceHud();
            StartMusic();
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
                racer.Distance += Mathf.Abs(x - racer.LastX);
                racer.LastX = x;
            }
            RefreshHud();
            UpdateRaceEcosystem();
            if (raceTimeRemaining <= 0f) FinishRace();
        }


        private static bool CancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
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
                racers.Add(new Racer { Name = name, Surfer = surfer, LastX = startX, Player = player });
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
            if (selectionRoot != null) { Destroy(selectionRoot); selectionRoot = null; }
            if (raceHud != null) { Destroy(raceHud); raceHud = null; }
            if (ecosystemRoot != null) { Destroy(ecosystemRoot); ecosystemRoot = null; }
            if (musicSource != null) musicSource.Stop();

            if (destroyRacers)
                DestroyExistingSurfers();

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour == this) continue;
                string n = behaviour.GetType().Name;
                if (n.Contains("Spawner", StringComparison.OrdinalIgnoreCase) || n.Contains("ProgressionDirector", StringComparison.OrdinalIgnoreCase))
                    behaviour.enabled = GameModeSession.IsStory;
            }
        }

        private void FinishRace()
        {
            RaceActive = false;
            racers.Sort((a, b) => b.Distance.CompareTo(a.Distance));
            if (timerLabel != null) timerLabel.text = "RACE COMPLETE";
            if (standingsLabel != null)
                standingsLabel.text = string.Join("\n", racers.Select((r, i) => $"{i + 1}. {r.Name.ToUpperInvariant()}  {r.Distance:0.0}m"));
            if (musicSource != null) musicSource.Stop();
            if (ecosystemRoot != null)
            {
                Destroy(ecosystemRoot);
                ecosystemRoot = null;
            }
        }

        private void SetupRaceEcosystem()
        {
            if (ecosystemRoot != null)
                Destroy(ecosystemRoot);

            ecosystemRoot = new GameObject("Race Mode Random Ecosystem");
            DontDestroyOnLoad(ecosystemRoot);
            nextEcosystemSpawnTime = Time.time + 1.5f;

            // Seed the water immediately with a varied, boss-free group.
            SpawnRandomWaterEnemy();
            SpawnRandomWaterEnemy();
            SpawnRandomWaterEnemy();
        }

        private void UpdateRaceEcosystem()
        {
            if (ecosystemRoot == null || Time.time < nextEcosystemSpawnTime)
                return;

            int activeEnemies =
                FindObjectsByType<SharkLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<GiantSquidLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<WhaleLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<JellyfishSchoolController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

            if (activeEnemies < 7)
                SpawnRandomWaterEnemy();

            nextEcosystemSpawnTime = Time.time + UnityEngine.Random.Range(7f, 13f);
        }

        private void SpawnRandomWaterEnemy()
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

            switch (UnityEngine.Random.Range(0, 4))
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
                default:
                    holder.gameObject.AddComponent<JellyfishSchoolSpawner>().SpawnSchool();
                    break;
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

        private void DestroyExistingSurfers()
        {
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Destroy(surfer.gameObject);
            foreach (TinyWaveSurferSpawnListener listener in FindObjectsByType<TinyWaveSurferSpawnListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Destroy(listener.gameObject);
            racers.Clear();
        }

        private void StartMusic()
        {
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = Resources.Load<AudioClip>("Audio/Music/Death Surfer");
            musicSource.loop = true; musicSource.playOnAwake = false; musicSource.spatialBlend = 0f; musicSource.volume = 0.8f;
            if (musicSource.clip != null) musicSource.Play();
        }

        private void BuildRaceHud()
        {
            EnsureCanvas();
            if (raceHud != null) Destroy(raceHud);
            raceHud = new GameObject("Race HUD", typeof(RectTransform), typeof(CanvasGroup));
            raceHud.transform.SetParent(canvas.transform, false);
            RectTransform root = raceHud.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            TextMeshProUGUI modeLabel = CreateText(raceHud.transform, "RACE MODE", 24, TextAlignmentOptions.Top);
            modeLabel.rectTransform.anchorMin = new Vector2(0.4f, 0.94f); modeLabel.rectTransform.anchorMax = new Vector2(0.6f, 0.99f); modeLabel.rectTransform.offsetMin = modeLabel.rectTransform.offsetMax = Vector2.zero;
            timerLabel = CreateText(raceHud.transform, "1:15", 40, TextAlignmentOptions.Top);
            timerLabel.rectTransform.anchorMin = new Vector2(0.35f, 0.86f); timerLabel.rectTransform.anchorMax = new Vector2(0.65f, 0.98f); timerLabel.rectTransform.offsetMin = timerLabel.rectTransform.offsetMax = Vector2.zero;
            standingsLabel = CreateText(raceHud.transform, string.Empty, 23, TextAlignmentOptions.TopLeft);
            standingsLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.68f); standingsLabel.rectTransform.anchorMax = new Vector2(0.28f, 0.94f); standingsLabel.rectTransform.offsetMin = standingsLabel.rectTransform.offsetMax = Vector2.zero;
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
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32100;
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
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size; label.alignment = alignment; label.color = Color.white; label.enableWordWrapping = false;
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
            GameObject go = new GameObject(racer, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)); go.transform.SetParent(parent, false);
            LayoutElement le = go.GetComponent<LayoutElement>(); le.preferredWidth = 220f; le.preferredHeight = 245f;
            Image image = go.GetComponent<Image>(); image.color = new Color(0.025f, 0.14f, 0.18f, 1f);
            Button button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            ColorBlock cb = button.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, .82f, .25f, 1f); cb.selectedColor = new Color(1f, .65f, .12f, 1f); button.colors = cb;
            if (portrait != null)
            {
                GameObject portraitObject = new GameObject("Single Frame Portrait", typeof(RectTransform), typeof(Image));
                portraitObject.transform.SetParent(go.transform, false);
                Image portraitImage = portraitObject.GetComponent<Image>();
                portraitImage.sprite = portrait;
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;
                RectTransform pr = portraitImage.rectTransform;
                pr.anchorMin = new Vector2(0.16f, 0.25f); pr.anchorMax = new Vector2(0.84f, 0.88f); pr.offsetMin = pr.offsetMax = Vector2.zero;
            }
            TextMeshProUGUI label = CreateText(go.transform, racer.ToUpperInvariant(), 22, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = new Vector2(0f, 0.04f); label.rectTransform.anchorMax = new Vector2(1f, 0.24f); label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }
    }
}
