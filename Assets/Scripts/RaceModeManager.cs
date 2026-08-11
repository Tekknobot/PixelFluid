using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
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
            public int PlayerSlot;
        }

        private sealed class StandingRow
        {
            public string RacerName;
            public RectTransform Root;
            public Image Background;
            public Image Portrait;
            public Material PortraitMaterial;
            public TextMeshProUGUI Rank;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Distance;
        }

        public static RaceModeManager Instance { get; private set; }
        public static bool RaceActive { get; private set; }
        public static bool IsTwoPlayerRace => Instance != null && Instance.twoPlayerRace;
        public static bool IsSelectingSurfer => Instance != null && Instance.selectionRoot != null;
        private const int RaceHudCanvasOrder = 32750;
        private const int RaceSelectionCanvasOrder = 32767;

        // Every selectable surfer also enters the race, so the player always has
        // a complete eight-surfer field to race against.
        private static readonly string[] Roster =
        {
            "Chuck", "Fred", "Josh", "Jason",
            "Angie", "Ginger", "Summer", "Jane"
        };
        private readonly List<Racer> racers = new();
        private bool twoPlayerRace;
        private string secondPlayerSurfer;
        private Racer cameraLeader;
        private TextMeshProUGUI controllerStatusLabel;
        private readonly List<Button> rosterButtons = new();
        private string playerOneChoice;
        private string playerTwoChoice;
        private bool playerTwoJoined;
        private bool playerOneLocked;
        private bool playerTwoLocked;
        private int playerOneIndex;
        private int playerTwoIndex;
        private float selectionInputReadyAt;
#if ENABLE_INPUT_SYSTEM
        private Vector2Int playerOneStickDirection;
        private Vector2Int playerTwoStickDirection;
        private float playerOneStickRepeatAt;
        private float playerTwoStickRepeatAt;
#endif
        private Canvas canvas;
        private GameObject selectionRoot;
        private GameObject raceHud;
        private bool raceHudRequestedVisible;
        private TextMeshProUGUI timerLabel;
        private readonly List<StandingRow> standingRows = new();
        private readonly Dictionary<string, Sprite> racePortraitCache = new();
        private float raceTimeRemaining;
        private const float PrototypeRaceSeconds = 180f;

        [Header("Pole Position HUD Layout")]
        [SerializeField] private Vector2 polePanelAnchorMin = new(0.025f, 0.75f);
        [SerializeField] private Vector2 polePanelAnchorMax = new(0.975f, 1.0f);
        [SerializeField, Range(0f, 1f)] private float poleFirstSlotCenter = 0.1715f;
        [SerializeField, Range(0.02f, 0.2f)] private float poleSlotSpacing = 0.109f;
        [SerializeField, Range(0f, 1f)] private float poleRowVerticalAnchor = 0.36f;
        [SerializeField, Min(64f)] private float polePortraitSize = 64f;
        [SerializeField, Min(48f)] private float poleInfoCellWidth = 140f;
        [SerializeField, Min(24f)] private float poleInfoCellHeight = 60f;
        [SerializeField, Min(0f)] private float polePortraitInfoGap = 5f;
        [SerializeField] private Vector2 poleTextPadding = new(6f, 2f);
        [SerializeField, Range(8, 40)] private int poleNameFontSize = 16;
        [SerializeField, Range(8, 32)] private int poleDetailFontSize = 13;
        [SerializeField, Min(0.1f)] private float polePositionSlideSpeed = 10f;
        [SerializeField, Range(0f, 1f)] private float polePortraitFadeBottom = 0.25f;
        [SerializeField, Range(0f, 1f)] private float polePortraitFadeTop = 0.45f;
        [SerializeField, Range(1, 8)] private int polePortraitFadeSteps = 4;

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
        private float nextRaceBossSweepTime;

        private bool raceTeardownInProgress;

        private enum RaceHostileKind
        {
            Shark,
            GiantSquid,
            JellyfishSchool,
            AlienUfo,
            BloodShark,
            TransparentSquid,
            Stingray,
            BloodfishSchool,
            Helicopter,
            Searchlight,
            SurveillanceBuoy,
            Drone,
            Fishbowl,
            Starfish,
            MustacheShark,
            Toaster,
            MushroomSquid,
            Resort,
            Toilet,
            ClawUfo,
            RacerUfo,
            RetroUfo
        }

        [Header("Race Ecosystem Difficulty")]
        [SerializeField, Range(1, 12)] private int openingEnemyCap = 3;
        [SerializeField, Range(1, 16)] private int earlyEnemyCap = 6;
        [SerializeField, Range(1, 20)] private int midEnemyCap = 9;
        [SerializeField, Range(1, 24)] private int finalEnemyCap = 12;
        [SerializeField, Min(0.5f)] private float openingSpawnInterval = 6f;
        [SerializeField, Min(0.5f)] private float earlySpawnInterval = 4.8f;
        [SerializeField, Min(0.5f)] private float midSpawnInterval = 3.6f;
        [SerializeField, Min(0.5f)] private float finalSpawnInterval = 2.5f;
        [SerializeField, Range(0f, 1f)] private float openingPhaseEnd = 0.18f;
        [SerializeField, Range(0f, 1f)] private float earlyPhaseEnd = 0.42f;
        [SerializeField, Range(0f, 1f)] private float midPhaseEnd = 0.70f;
        [SerializeField, Range(1, 6)] private int maximumSpawnsPerPulse = 1;
        [SerializeField, Min(0.05f)] private float raceCreatureFadeInDuration = 0.85f;
        private int nextRaceWaterEntrySide = -1;
        private int nextRaceSkyEntrySide = -1;
        private static readonly RaceHostileKind[] OpeningHostiles =
        {
            RaceHostileKind.Shark,
            RaceHostileKind.GiantSquid,
            RaceHostileKind.JellyfishSchool,
            RaceHostileKind.AlienUfo
        };
        private static readonly RaceHostileKind[] EarlyHostiles =
        {
            RaceHostileKind.Shark,
            RaceHostileKind.GiantSquid,
            RaceHostileKind.JellyfishSchool,
            RaceHostileKind.AlienUfo,
            RaceHostileKind.BloodShark,
            RaceHostileKind.TransparentSquid,
            RaceHostileKind.Stingray,
            RaceHostileKind.BloodfishSchool,
            RaceHostileKind.Helicopter
        };
        private static readonly RaceHostileKind[] MidHostiles =
        {
            RaceHostileKind.Shark,
            RaceHostileKind.GiantSquid,
            RaceHostileKind.JellyfishSchool,
            RaceHostileKind.AlienUfo,
            RaceHostileKind.BloodShark,
            RaceHostileKind.TransparentSquid,
            RaceHostileKind.Stingray,
            RaceHostileKind.BloodfishSchool,
            RaceHostileKind.Helicopter,
            RaceHostileKind.Searchlight,
            RaceHostileKind.SurveillanceBuoy,
            RaceHostileKind.Drone
        };
        private static readonly RaceHostileKind[] FinalHostiles =
        {
            RaceHostileKind.Shark,
            RaceHostileKind.GiantSquid,
            RaceHostileKind.JellyfishSchool,
            RaceHostileKind.BloodShark,
            RaceHostileKind.TransparentSquid,
            RaceHostileKind.Stingray,
            RaceHostileKind.BloodfishSchool,
            RaceHostileKind.Searchlight,
            RaceHostileKind.SurveillanceBuoy,
            RaceHostileKind.Drone,
            RaceHostileKind.Fishbowl,
            RaceHostileKind.Starfish,
            RaceHostileKind.MustacheShark,
            RaceHostileKind.Toaster,
            RaceHostileKind.MushroomSquid,
            RaceHostileKind.Resort,
            RaceHostileKind.Toilet,
            RaceHostileKind.ClawUfo,
            RaceHostileKind.RacerUfo,
            RaceHostileKind.RetroUfo
        };
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
            enabled = true;
            selectionMenu = menu;

            if (selectionMenu != null)
                selectionMenu.SetRaceSelectionPresentation(true);

            BuildSelection(menu);
        }

        private void BuildSelection(SurferSlugPauseMenu menu)
        {
            if (selectionRoot != null) Destroy(selectionRoot);
            EnsureCanvas();
            canvas.sortingOrder = RaceSelectionCanvasOrder;

            // Keep the current racers selected when this screen is reopened.
            // The choices remain movable (not locked), but both controller
            // cursors immediately return to the surfers they were controlling.
            string currentPlayerOne = racers.FirstOrDefault(r => r.PlayerSlot == 1)?.Name;
            string currentPlayerTwo = racers.FirstOrDefault(r => r.PlayerSlot == 2)?.Name;
            if (Array.IndexOf(Roster, currentPlayerOne) < 0)
                currentPlayerOne = Array.IndexOf(Roster, playerOneChoice) >= 0
                    ? playerOneChoice
                    : Roster[0];
            if (Array.IndexOf(Roster, currentPlayerTwo) < 0)
                currentPlayerTwo = Array.IndexOf(Roster, secondPlayerSurfer) >= 0
                    ? secondPlayerSurfer
                    : playerTwoChoice;

            bool restorePlayerTwo = (twoPlayerRace || playerTwoJoined) &&
                                    Array.IndexOf(Roster, currentPlayerTwo) >= 0;

            rosterButtons.Clear();
            playerOneLocked = false;
            playerTwoLocked = false;
            playerOneIndex = Mathf.Max(0, Array.IndexOf(Roster, currentPlayerOne));
            playerTwoIndex = restorePlayerTwo
                ? Mathf.Max(0, Array.IndexOf(Roster, currentPlayerTwo))
                : 0;
            playerOneChoice = Roster[playerOneIndex];
            playerTwoJoined = restorePlayerTwo;
            playerTwoChoice = restorePlayerTwo ? Roster[playerTwoIndex] : null;
            selectionInputReadyAt = Time.unscaledTime + 0.25f;
#if ENABLE_INPUT_SYSTEM
            playerOneStickDirection = Vector2Int.zero;
            playerTwoStickDirection = Vector2Int.zero;
            playerOneStickRepeatAt = 0f;
            playerTwoStickRepeatAt = 0f;
#endif

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
            windowRect.sizeDelta = new Vector2(1080f, 620f);

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

            controllerStatusLabel = CreateText(selectionRoot.transform, BuildControllerStatus(), 20, TextAlignmentOptions.Center);
            controllerStatusLabel.rectTransform.anchorMin = new Vector2(0.08f, 0.92f);
            controllerStatusLabel.rectTransform.anchorMax = new Vector2(0.92f, 0.98f);
            controllerStatusLabel.rectTransform.offsetMin = controllerStatusLabel.rectTransform.offsetMax = Vector2.zero;
            controllerStatusLabel.color = new Color(0.1f, 0.95f, 1f, 1f);

            GameObject row = new GameObject(
                "Roster",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            row.transform.SetParent(window.transform, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.08f, 0.20f);
            rowRect.anchorMax = new Vector2(0.92f, 0.76f);
            rowRect.offsetMin = rowRect.offsetMax = Vector2.zero;

            GridLayoutGroup layout = row.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(200f, 160f);
            layout.spacing = new Vector2(16f, 16f);
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;

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
                        if (playerTwoLocked && capturedRacer == playerTwoChoice)
                            return;

                        playerOneChoice = capturedRacer;
                        playerOneLocked = true;
                        RefreshPlayerFrames();

                        // In single-player the first confirmation starts the race.
                        // Once P2 has joined, both confirmations are mandatory.
                        if (!playerTwoJoined || playerTwoLocked)
                            StartPickerRace();
                    });
                buttons.Add(button);
                rosterButtons.Add(button);
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                // Race selection reads each paired controller directly. Leaving
                // Unity UI navigation enabled makes both gamepads drive the same
                // EventSystem cursor.
                Navigation nav = new Navigation { mode = Navigation.Mode.None };
                buttons[i].navigation = nav;
            }

            TextMeshProUGUI help = CreateText(
                selectionRoot.transform,
                "P1 SELECTS  •  P2 JOINS AUTOMATICALLY WITH CONTROLLER 2  •  ESC / B TO BACK",
                18,
                TextAlignmentOptions.Center);
            RectTransform helpRect = help.rectTransform;
            helpRect.anchorMin = new Vector2(0.08f, 0.03f);
            helpRect.anchorMax = new Vector2(0.92f, 0.09f);
            helpRect.offsetMin = helpRect.offsetMax = Vector2.zero;
            help.color = new Color(0.72f, 0.88f, 0.92f, 1f);

            EventSystem.current?.SetSelectedGameObject(null);
            RefreshPlayerFrames();
        }

        public void BeginRace(string selectedSurfer, bool showHudImmediately = true)
        {
            EndDayUiFocusController.ReleaseForModeTransition();
            if (canvas != null)
                canvas.sortingOrder = RaceHudCanvasOrder;
            twoPlayerRace = twoPlayerRace && !string.IsNullOrEmpty(secondPlayerSurfer);
            secondPlayerSurfer = twoPlayerRace ? secondPlayerSurfer : null;
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
            raceHudRequestedVisible = visible;

            if (!visible)
            {
                if (raceHud != null)
                    raceHud.SetActive(false);
                return;
            }

            EnsureRaceHudVisible();
        }

        private void EnsureRaceHudVisible()
        {
            EnsureCanvas();
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = RaceHudCanvasOrder;

            // Rebuild if another UI transition destroyed the old hierarchy. The
            // rebuilt HUD includes the timer and complete pole-position panel.
            if (raceHud == null && RaceActive)
                BuildRaceHud();

            if (raceHud == null)
                return;

            raceHud.SetActive(true);
            RefreshHud();
        }

        private void Update()
        {
            // While the picker is visible it exclusively owns controller input.
            // Do not advance the existing race, its camera, or its racers behind
            // the selection screen.
            if (selectionRoot != null)
            {
                UpdateTwoPlayerPicker();
                UpdateControllerStatus();
                if (CancelPressed())
                    CloseSelection();
                return;
            }

            if (SurferSlugPauseMenu.GameplayPaused)
                return;

            if (!RaceActive) return;

            // End-day focus may have disabled this canvas on a previous frame.
            // Once race gameplay owns the screen, repair the HUD automatically.
            if (raceHudRequestedVisible &&
                !EndDayUiFocusController.IsActive &&
                (canvas == null || !canvas.enabled || raceHud == null || !raceHud.activeSelf))
            {
                EnsureRaceHudVisible();
            }

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
            RemoveBossesFromRace();
            UpdateRaceWeatherPattern();
            UpdateSharedRaceCamera();
            UpdateControllerStatus();
            if (raceTimeRemaining <= 0f) FinishRace();
        }

        private void UpdateTwoPlayerPicker()
        {
            if (selectionRoot == null) return;
            if (Time.unscaledTime < selectionInputReadyAt)
            {
                RefreshPlayerFrames();
                return;
            }
#if ENABLE_INPUT_SYSTEM
            Gamepad p1 = Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
            Keyboard keyboard = Keyboard.current;
            Vector2Int p1StickMove = ReadSelectorMove(
                p1,
                ref playerOneStickDirection,
                ref playerOneStickRepeatAt);

            if (!playerOneLocked)
            {
                bool left = (p1 != null && p1.dpad.left.wasPressedThisFrame) ||
                            p1StickMove.x < 0 ||
                            (keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame);
                bool right = (p1 != null && p1.dpad.right.wasPressedThisFrame) ||
                             p1StickMove.x > 0 ||
                             (keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame);
                bool up = (p1 != null && p1.dpad.up.wasPressedThisFrame) ||
                          p1StickMove.y > 0 ||
                          (keyboard != null && keyboard.upArrowKey.wasPressedThisFrame);
                bool down = (p1 != null && p1.dpad.down.wasPressedThisFrame) ||
                            p1StickMove.y < 0 ||
                            (keyboard != null && keyboard.downArrowKey.wasPressedThisFrame);

                if (left) playerOneIndex = (playerOneIndex + 7) % 8;
                if (right) playerOneIndex = (playerOneIndex + 1) % 8;
                if (up || down) playerOneIndex = (playerOneIndex + 4) % 8;
                playerOneChoice = Roster[playerOneIndex];

                bool confirm = (p1 != null && p1.buttonSouth.wasPressedThisFrame) ||
                               (keyboard != null &&
                                (keyboard.enterKey.wasPressedThisFrame ||
                                 keyboard.numpadEnterKey.wasPressedThisFrame));
                if (confirm && (!playerTwoLocked || playerOneChoice != playerTwoChoice))
                {
                    playerOneLocked = true;
                    if (!playerTwoJoined || playerTwoLocked)
                        StartPickerRace();
                }
            }

            if (Gamepad.all.Count > 1)
            {
                Gamepad p2 = Gamepad.all[1];
                Vector2Int p2StickMove = ReadSelectorMove(
                    p2,
                    ref playerTwoStickDirection,
                    ref playerTwoStickRepeatAt);
                bool joinedThisFrame = false;
                if (!playerTwoJoined &&
                    (p2.startButton.wasPressedThisFrame || p2.buttonSouth.wasPressedThisFrame))
                {
                    playerTwoJoined = true;
                    joinedThisFrame = true;
                    playerTwoChoice = Roster[playerTwoIndex];
                }
                if (playerTwoJoined)
                {
                    if (!playerTwoLocked &&
                        (p2.dpad.left.wasPressedThisFrame || p2StickMove.x < 0))
                        playerTwoIndex = (playerTwoIndex + 7) % 8;
                    if (!playerTwoLocked &&
                        (p2.dpad.right.wasPressedThisFrame || p2StickMove.x > 0))
                        playerTwoIndex = (playerTwoIndex + 1) % 8;
                    if (!playerTwoLocked &&
                        (p2.dpad.up.wasPressedThisFrame ||
                         p2.dpad.down.wasPressedThisFrame ||
                         p2StickMove.y != 0))
                        playerTwoIndex = (playerTwoIndex + 4) % 8;
                    playerTwoChoice = Roster[playerTwoIndex];

                    if (!joinedThisFrame && !playerTwoLocked &&
                        p2.buttonSouth.wasPressedThisFrame &&
                        playerOneChoice != playerTwoChoice)
                    {
                        playerTwoLocked = true;
                        if (playerOneLocked)
                            StartPickerRace();
                    }
                }
            }
            else if (playerTwoJoined)
            {
                // A disconnected second controller cleanly returns the menu to
                // one-player selection instead of leaving an impossible lock.
                playerTwoJoined = false;
                playerTwoLocked = false;
                playerTwoChoice = null;
            }
#endif
            RefreshPlayerFrames();
        }

#if ENABLE_INPUT_SYSTEM
        private static Vector2Int ReadSelectorMove(
            Gamepad gamepad,
            ref Vector2Int heldDirection,
            ref float repeatAt)
        {
            Vector2Int direction = Vector2Int.zero;
            if (gamepad != null)
            {
                // Read the D-pad as a held vector so it cannot be missed when
                // another race component observes the same input frame. Fall
                // back to the left stick when the D-pad is neutral.
                Vector2 input = gamepad.dpad.ReadValue();
                float threshold = 0.5f;
                if (input.sqrMagnitude < 0.25f)
                {
                    input = gamepad.leftStick.ReadValue();
                    threshold = 0.55f;
                }

                if (Mathf.Abs(input.x) >= threshold || Mathf.Abs(input.y) >= threshold)
                {
                    direction = Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                        ? new Vector2Int(input.x < 0f ? -1 : 1, 0)
                        : new Vector2Int(0, input.y < 0f ? -1 : 1);
                }
            }

            if (direction == Vector2Int.zero)
            {
                heldDirection = Vector2Int.zero;
                repeatAt = 0f;
                return Vector2Int.zero;
            }

            float now = Time.unscaledTime;
            if (direction != heldDirection)
            {
                heldDirection = direction;
                repeatAt = now + 0.32f;
                return direction;
            }

            if (now < repeatAt)
                return Vector2Int.zero;

            repeatAt = now + 0.12f;
            return direction;
        }
#endif

        private void StartPickerRace()
        {
            if (!playerOneLocked) return;
            if (playerTwoJoined && (!playerTwoLocked || playerOneChoice == playerTwoChoice)) return;
            twoPlayerRace = playerTwoJoined;
            secondPlayerSurfer = playerTwoChoice;

            // Keep the title presentation suppressed and hand the confirmed
            // choices back to the pause menu. Its existing transition builds
            // the race behind an opaque cover, animates the title away, fades
            // into gameplay, and only then enables the race HUD.
            Destroy(selectionRoot);
            selectionRoot = null;
            if (canvas != null)
                canvas.sortingOrder = RaceHudCanvasOrder;

            if (selectionMenu != null)
                selectionMenu.BeginRaceMode(playerOneChoice);
            else
                BeginRace(playerOneChoice);
        }

        private void RefreshPlayerFrames()
        {
            for (int i = 0; i < rosterButtons.Count; i++)
            {
                Outline frame = rosterButtons[i].GetComponent<Outline>();
                Image panel = rosterButtons[i].GetComponent<Image>();
                bool p1 = Roster[i] == playerOneChoice;
                bool p2 = playerTwoJoined && Roster[i] == playerTwoChoice;

                frame.enabled = p1 || p2;
                frame.effectColor = p2
                    ? new Color(1f, 0.05f, 0.2f, 1f)
                    : new Color(0.05f, 0.95f, 1f, 1f);
                frame.effectDistance = new Vector2(5f, -5f);

                if (panel != null)
                {
                    bool locked = (playerOneLocked && Roster[i] == playerOneChoice) ||
                                  (playerTwoLocked && Roster[i] == playerTwoChoice);
                    panel.color = locked
                        ? (p2 ? new Color(0.22f, 0.015f, 0.045f, 0.94f)
                              : new Color(0.01f, 0.19f, 0.22f, 0.94f))
                        : new Color(0.015f, 0.055f, 0.075f, 0.82f);
                }
            }
        }


        private void LateUpdate()
        {
            // The pause menu and EventSystem can process B/Escape before this
            // manager's normal Update. Check again at the end of the frame so
            // character select always honours the Back command.
            if (selectionRoot == null || !CancelPressed())
                return;

            CloseSelection();
        }

        public void CloseSelection()
        {
            if (selectionRoot != null)
            {
                Destroy(selectionRoot);
                selectionRoot = null;
            }

            if (canvas != null)
                canvas.sortingOrder = RaceHudCanvasOrder;

            selectionMenu?.SetRaceSelectionPresentation(false);
            selectionMenu = null;
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

            Gamepad gamepad = Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
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
                bool player = string.Equals(name, selected, StringComparison.OrdinalIgnoreCase) ||
                              (twoPlayerRace && string.Equals(name, secondPlayerSurfer, StringComparison.OrdinalIgnoreCase));
                int playerIndex = string.Equals(name, selected, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                int randomWave = i < shuffledWaves.Count
                    ? shuffledWaves[i]
                    : UnityEngine.Random.Range(0, waveCount);

                GameObject go = new GameObject(player ? "Race Player - " + name : "Race AI - " + name);
                TinyWaveSurfer surfer = go.AddComponent<TinyWaveSurfer>();
                surfer.ConfigureGeneratedSurfer(randomWave, true, 0.95f, Color.white, Color.white, 100 + i, 0.2f + i * 0.1f, i * 0.08f);
                surfer.ConfigureRaceSurfer(!player, speed * (player ? 1f : UnityEngine.Random.Range(0.93f, 1.07f)), boost);
                if (player)
                    surfer.ConfigureRaceInput(playerIndex, playerIndex == 0 && ConnectedGamepadCount() == 0);
                surfer.ConfigureRaceReactionAudio(IsWomanRacer(name));
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
                    Player = player,
                    PlayerSlot = player ? playerIndex + 1 : 0
                });
            }
        }

        private static bool IsWomanRacer(string racer)
        {
            return string.Equals(racer, "Angie", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(racer, "Ginger", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(racer, "Summer", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(racer, "Jane", StringComparison.OrdinalIgnoreCase);
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

        private void UpdateSharedRaceCamera()
        {
            Racer leader = racers.Where(r => r.Player && r.Surfer != null && !r.Surfer.IsDead)
                .OrderByDescending(r => r.Distance).FirstOrDefault();
            if (leader == null) return;
            if (cameraLeader == null || leader.Distance > cameraLeader.Distance + 0.75f)
            {
                cameraLeader = leader;
                Camera.main?.GetComponent<TinySurferCinematicCamera>()?.SetFollowTarget(leader.Surfer, false);
            }
            foreach (Racer racer in racers.Where(r => r.Player && r != leader && r.Surfer != null && !r.Surfer.IsDead))
            {
                if (leader.Surfer.transform.position.x - racer.Surfer.transform.position.x > 8f)
                {
                    racer.Surfer.CatchUpToRaceLeader(leader.Surfer.transform.position.x - 4.5f, racer.Surfer.CurrentWave != null ? racer.Surfer.CurrentWave.IndependentLayerIndex : 0);
                }
            }
        }

        private string BuildControllerStatus()
        {
            int count = ConnectedGamepadCount();
            return count >= 2 ? "2 CONTROLLERS CONNECTED  •  TWO PLAYER READY" :
                count == 1 ? "1 CONTROLLER CONNECTED  •  CONNECT A SECOND FOR P2" :
                "NO CONTROLLERS  •  CONNECT 1 FOR P1, 2 FOR TWO PLAYER";
        }

        private void UpdateControllerStatus()
        {
            if (controllerStatusLabel != null)
                controllerStatusLabel.text = BuildControllerStatus();
        }

        private static int ConnectedGamepadCount()
        {
#if ENABLE_INPUT_SYSTEM
            return Gamepad.all.Count;
#else
            return 0;
#endif
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
            raceHudRequestedVisible = false;
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
            RefreshStandingRows(racers, true);
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
            nextEcosystemSpawnTime = Time.time + 2f;
            nextRaceWaterEntrySide = -1;
            nextRaceSkyEntrySide = -1;
            RemoveBossesFromRace();

            // Begin with one readable baseline threat. The normal spawn pulses
            // introduce the rest of the roster as race progress unlocks each tier.
            SpawnRaceHostile(RaceHostileKind.Shark);
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
                SpawnRandomRaceHostile();

            float jitter = UnityEngine.Random.Range(0.82f, 1.18f);
            nextEcosystemSpawnTime = Time.time + spawnInterval * jitter;
        }

        private void GetRaceEcosystemDifficulty(out int enemyCap, out float spawnInterval)
        {
            float progress = GetRaceProgress();

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
                FindObjectsByType<JellyfishSchoolController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<BloodSharkLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<TransparentSquidLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<StingrayLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<BloodfishSchoolController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<AlienUfoController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<DayTwoHelicopterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<DayFiveCombatant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Count(combatant => combatant != null && !combatant.IsBoss) +
                FindObjectsByType<DaySixCreature>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                FindObjectsByType<DaySixSkyHostile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        }

        private void SpawnRandomRaceHostile()
        {
            float progress = GetRaceProgress();
            float openingEnd = Mathf.Clamp01(openingPhaseEnd);
            float earlyEnd = Mathf.Max(openingEnd, Mathf.Clamp01(earlyPhaseEnd));
            float midEnd = Mathf.Max(earlyEnd, Mathf.Clamp01(midPhaseEnd));

            RaceHostileKind[] available = progress < openingEnd
                ? OpeningHostiles
                : progress < earlyEnd
                    ? EarlyHostiles
                    : progress < midEnd
                        ? MidHostiles
                        : FinalHostiles;

            int start = UnityEngine.Random.Range(0, available.Length);
            for (int i = 0; i < available.Length; i++)
            {
                RaceHostileKind candidate = available[(start + i) % available.Length];
                if (!CanSpawnRaceHostile(candidate))
                    continue;

                SpawnRaceHostile(candidate);
                return;
            }
        }

        private static bool CanSpawnRaceHostile(RaceHostileKind kind)
        {
            if (kind == RaceHostileKind.AlienUfo)
                return FindFirstObjectByType<AlienUfoController>() == null;
            if (kind == RaceHostileKind.Helicopter)
                return FindFirstObjectByType<DayTwoHelicopterController>() == null;
            if (kind == RaceHostileKind.ClawUfo ||
                kind == RaceHostileKind.RacerUfo ||
                kind == RaceHostileKind.RetroUfo)
            {
                return FindObjectsByType<DaySixSkyHostile>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length < 2;
            }

            return true;
        }

        private float GetRaceProgress()
        {
            return Mathf.Clamp01(
                1f - raceTimeRemaining / Mathf.Max(1f, PrototypeRaceSeconds));
        }

        private void SpawnRaceHostile(RaceHostileKind kind)
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

            // This catalog contains every normal hostile from Days 1-6. Neutral
            // wildlife, helpers, bosses and boss-only minions are intentionally absent.
            switch (kind)
            {
                case RaceHostileKind.Shark:
                    holder.gameObject.AddComponent<SharkLaneSpawner>().SpawnShark(true);
                    break;
                case RaceHostileKind.GiantSquid:
                    holder.gameObject.AddComponent<GiantSquidLaneSpawner>().SpawnSquid(true);
                    break;
                case RaceHostileKind.JellyfishSchool:
                    holder.gameObject.AddComponent<JellyfishSchoolSpawner>().SpawnSchool();
                    break;
                case RaceHostileKind.AlienUfo:
                    holder.gameObject.name = "Race Alien UFO";
                    holder.gameObject.AddComponent<SpriteRenderer>();
                    holder.gameObject.AddComponent<AlienUfoController>();
                    break;
                case RaceHostileKind.BloodShark:
                    holder.gameObject.AddComponent<BloodSharkLaneSpawner>().SpawnBloodShark(true);
                    break;
                case RaceHostileKind.TransparentSquid:
                    holder.gameObject.AddComponent<TransparentSquidLaneSpawner>().SpawnTransparentSquid(true);
                    break;
                case RaceHostileKind.Stingray:
                    holder.gameObject.AddComponent<StingrayLaneSpawner>().SpawnStingray(true);
                    break;
                case RaceHostileKind.BloodfishSchool:
                    holder.gameObject.AddComponent<BloodfishSchoolSpawner>().SpawnSchool();
                    break;
                case RaceHostileKind.Helicopter:
                    holder.gameObject.name = "Race Missile Helicopter";
                    holder.gameObject.AddComponent<SpriteRenderer>();
                    holder.gameObject.AddComponent<BoxCollider2D>();
                    holder.gameObject.AddComponent<Rigidbody2D>();
                    holder.gameObject.AddComponent<DayTwoHelicopterController>();
                    break;
                case RaceHostileKind.Searchlight:
                    SpawnRaceDayFiveHostile(holder, DayFiveEnemyKind.Searchlight);
                    break;
                case RaceHostileKind.SurveillanceBuoy:
                    SpawnRaceDayFiveHostile(holder, DayFiveEnemyKind.SurveillanceBuoy);
                    break;
                case RaceHostileKind.Drone:
                    SpawnRaceDayFiveHostile(holder, DayFiveEnemyKind.Drone);
                    break;
                case RaceHostileKind.Fishbowl:
                    SpawnRaceDaySixCreature(holder, DaySixCreatureKind.Fishbowl);
                    break;
                case RaceHostileKind.Starfish:
                    SpawnRaceDaySixCreature(holder, DaySixCreatureKind.Starfish);
                    break;
                case RaceHostileKind.MustacheShark:
                    SpawnRaceDaySixCreature(holder, DaySixCreatureKind.MustacheShark);
                    break;
                case RaceHostileKind.Toaster:
                    SpawnRaceDaySixCreature(holder, DaySixCreatureKind.Toaster);
                    break;
                case RaceHostileKind.MushroomSquid:
                    SpawnRaceDaySixCreature(holder, DaySixCreatureKind.MushroomSquid);
                    break;
                case RaceHostileKind.Resort:
                    SpawnRaceDaySixCreature(holder, DaySixCreatureKind.Resort);
                    break;
                case RaceHostileKind.Toilet:
                    SpawnRaceDaySixCreature(holder, DaySixCreatureKind.Toilet);
                    break;
                case RaceHostileKind.ClawUfo:
                    SpawnRaceDaySixSkyHostile(holder, DaySixSkyHostileKind.ClawUfo);
                    break;
                case RaceHostileKind.RacerUfo:
                    SpawnRaceDaySixSkyHostile(holder, DaySixSkyHostileKind.RacerUfo);
                    break;
                case RaceHostileKind.RetroUfo:
                    SpawnRaceDaySixSkyHostile(holder, DaySixSkyHostileKind.RetroUfo);
                    break;
            }

            EnsureRaceCreatureFade(holder.gameObject);
        }

        private void SpawnRaceDayFiveHostile(Transform holder, DayFiveEnemyKind kind)
        {
            TinyWaveSurfer player = FindRacePlayer();
            float sampleX = player != null ? player.transform.position.x : holder.position.x;
            List<PixelWaterGPU> waterLayers = EndlessWaveSections.LayersNearest(sampleX);
            waterLayers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            int lane = UnityEngine.Random.Range(0, laneCount);
            PixelWaterGPU sortingSource = waterLayers.Count > 0
                ? waterLayers[Mathf.Clamp(lane, 0, waterLayers.Count - 1)]
                : null;

            holder.gameObject.name = $"Race Day 5 {kind}";
            holder.gameObject.AddComponent<SpriteRenderer>();
            holder.gameObject.AddComponent<BoxCollider2D>();
            holder.gameObject.AddComponent<Rigidbody2D>();
            DayFiveCombatant combatant = holder.gameObject.AddComponent<DayFiveCombatant>();
            combatant.Initialise(kind, nextRaceWaterEntrySide, null, sortingSource, lane);
            nextRaceWaterEntrySide *= -1;
        }

        private void SpawnRaceDaySixCreature(Transform holder, DaySixCreatureKind kind)
        {
            TinyWaveSurfer player = FindRacePlayer();
            float sampleX = player != null ? player.transform.position.x : holder.position.x;
            List<PixelWaterGPU> waterLayers = EndlessWaveSections.LayersNearest(sampleX);
            waterLayers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            int lane = UnityEngine.Random.Range(0, Mathf.Max(1, waterLayers.Count - 1));

            holder.gameObject.name = $"Race Day 6 {kind}";
            holder.position = new Vector3(sampleX, player != null ? player.transform.position.y : 0f, 0f);
            holder.gameObject.AddComponent<SpriteRenderer>();
            holder.gameObject.AddComponent<BoxCollider2D>();
            holder.gameObject.AddComponent<Rigidbody2D>();
            holder.gameObject.AddComponent<InterWaveRenderItem>();
            DaySixCreature creature = holder.gameObject.AddComponent<DaySixCreature>();
            creature.Initialise(kind, lane, nextRaceWaterEntrySide, null);
            nextRaceWaterEntrySide *= -1;
        }

        private void SpawnRaceDaySixSkyHostile(Transform holder, DaySixSkyHostileKind kind)
        {
            TinyWaveSurfer player = FindRacePlayer();
            float sampleX = player != null ? player.transform.position.x : holder.position.x;

            holder.gameObject.name = $"Race Day 6 {kind}";
            holder.position = new Vector3(
                sampleX,
                player != null ? player.transform.position.y + 2f : 2f,
                0f);
            holder.gameObject.AddComponent<SpriteRenderer>();
            holder.gameObject.AddComponent<BoxCollider2D>();
            holder.gameObject.AddComponent<Rigidbody2D>();
            holder.gameObject.AddComponent<InterWaveRenderItem>();
            DaySixSkyHostile hostile = holder.gameObject.AddComponent<DaySixSkyHostile>();
            hostile.Initialise(kind, nextRaceSkyEntrySide, null);
            nextRaceSkyEntrySide *= -1;
        }

        private static TinyWaveSurfer FindRacePlayer()
        {
            foreach (TinyWaveSurfer surfer in GameplayTargetCache.Surfers)
                if (surfer != null && surfer.IsPlayerControlled && !surfer.IsDead)
                    return surfer;

            return null;
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

        private void RemoveBossesFromRace()
        {
            if (!RaceActive && !GameModeSession.IsRace)
                return;

            // BossSpawnAuthority blocks normal creation. This slower safety sweep
            // catches scene/developer objects that bypassed the central spawners.
            if (Time.unscaledTime < nextRaceBossSweepTime)
                return;
            nextRaceBossSweepTime = Time.unscaledTime + 0.5f;

            DestroyRaceBosses<GodzillaLaneSwimmer>();
            DestroyRaceBosses<RubberDuckBossSwimmer>();
            DestroyRaceBosses<RubberDucklingSwimmer>();
            DestroyRaceBosses<AionFinalBoss>();
            DestroyRaceBosses<AionLaneLaser>();
            DestroyRaceBosses<GodzillaSkullSwimmer>();
            foreach (DayFiveCombatant combatant in FindObjectsByType<DayFiveCombatant>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (combatant != null && combatant.IsBoss)
                    Destroy(combatant.gameObject);
            }
            DestroyRaceBosses<DayFiveEncounter>();
            DestroyRaceBosses<BossArenaPrison>();
        }

        private static void DestroyRaceBosses<TBoss>() where TBoss : Component
        {
            foreach (TBoss boss in FindObjectsByType<TBoss>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (boss != null)
                    Destroy(boss.gameObject);
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

            RemoveBossesFromRace();
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
            {
                ReleaseStandingRowMaterials();
                Destroy(raceHud);
            }

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

            BuildPolePositionsPanel();
        }

        private void RefreshHud()
        {
            if (timerLabel != null)
            {
                int seconds = Mathf.CeilToInt(raceTimeRemaining);
                timerLabel.text = $"{seconds / 60}:{seconds % 60:00}";
            }
            RefreshStandingRows(racers.OrderByDescending(r => r.Distance).ToArray());
        }

        private void BuildPolePositionsPanel()
        {
            ReleaseStandingRowMaterials();
            standingRows.Clear();

            GameObject panelObject = new GameObject(
                "Pole Positions Panel",
                typeof(RectTransform),
                typeof(Image));
            panelObject.transform.SetParent(raceHud.transform, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = polePanelAnchorMin;
            panel.anchorMax = polePanelAnchorMax;
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;

            TextMeshProUGUI heading = CreateText(
                panelObject.transform,
                "POLE POSITIONS",
                20,
                TextAlignmentOptions.Center);
            heading.rectTransform.anchorMin = new Vector2(0.12f, 0.80f);
            heading.rectTransform.anchorMax = new Vector2(0.99f, 0.94f);
            heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = Vector2.zero;
            heading.color = new Color(0.72f, 0.9f, 0.94f, 1f);

            GameObject timerCard = new GameObject(
                "Race Timer Card",
                typeof(RectTransform),
                typeof(Image));
            timerCard.transform.SetParent(panelObject.transform, false);
            RectTransform timerCardRect = timerCard.GetComponent<RectTransform>();
            timerCardRect.anchorMin = new Vector2(0.012f, 0.10f);
            timerCardRect.anchorMax = new Vector2(0.112f, 0.65f);
            timerCardRect.offsetMin = timerCardRect.offsetMax = Vector2.zero;
            Image timerCardImage = timerCard.GetComponent<Image>();
            timerCardImage.color = Color.clear;
            timerCardImage.raycastTarget = false;

            TextMeshProUGUI timerCaption = CreateText(
                timerCard.transform,
                "TIME",
                15,
                TextAlignmentOptions.Center);
            timerCaption.rectTransform.anchorMin = new Vector2(0f, 0.62f);
            timerCaption.rectTransform.anchorMax = new Vector2(1f, 0.96f);
            timerCaption.rectTransform.offsetMin = timerCaption.rectTransform.offsetMax = Vector2.zero;
            timerCaption.color = new Color(0.62f, 0.86f, 0.91f, 1f);

            timerLabel = CreateText(
                timerCard.transform,
                "3:00",
                34,
                TextAlignmentOptions.Center);
            timerLabel.rectTransform.anchorMin = new Vector2(0f, 0.04f);
            timerLabel.rectTransform.anchorMax = new Vector2(1f, 0.68f);
            timerLabel.rectTransform.offsetMin = timerLabel.rectTransform.offsetMax = Vector2.zero;

            for (int i = 0; i < Roster.Length; i++)
                standingRows.Add(CreateStandingRow(panelObject.transform, i));
        }

        private StandingRow CreateStandingRow(Transform parent, int index)
        {
            GameObject rowObject = new GameObject(
                $"Position {index + 1}",
                typeof(RectTransform),
                typeof(Image));
            rowObject.transform.SetParent(parent, false);

            RectTransform row = rowObject.GetComponent<RectTransform>();
            float centerX = poleFirstSlotCenter + index * poleSlotSpacing;
            float rowHeight = polePortraitSize + polePortraitInfoGap + poleInfoCellHeight;
            row.anchorMin = row.anchorMax = new Vector2(centerX, poleRowVerticalAnchor);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(
                Mathf.Max(polePortraitSize, poleInfoCellWidth),
                rowHeight);
            row.localScale = Vector3.one;

            Image background = rowObject.GetComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = false;

            GameObject portraitObject = new GameObject(
                "Portrait Cell 64x64",
                typeof(RectTransform),
                typeof(Image));
            portraitObject.transform.SetParent(rowObject.transform, false);
            Image portrait = portraitObject.GetComponent<Image>();
            portrait.type = Image.Type.Simple;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(
                0f,
                (poleInfoCellHeight + polePortraitInfoGap) * 0.5f);
            portraitRect.sizeDelta = new Vector2(polePortraitSize, polePortraitSize);
            portraitRect.localScale = Vector3.one;

            Shader fadeShader =
                Resources.Load<Shader>("Shaders/PixelPortraitBottomFade");

            if (fadeShader == null)
                fadeShader = Shader.Find("UI/Pixel Portrait Bottom Fade");

            Material portraitMaterial = null;
            if (fadeShader != null)
            {
                // Each portrait needs its own material because atlas UVs can be
                // different for every surfer portrait.
                portraitMaterial = new Material(fadeShader)
                {
                    name = $"{Roster[index]} Portrait Bottom Fade"
                };

                portrait.material = portraitMaterial;
            }
            else
            {
                Debug.LogWarning(
                    "Could not load Resources/Shaders/PixelPortraitBottomFade.shader.",
                    this);
            }

            // This is a separate, clipped cell below the portrait. Its contents
            // cannot render into the 64x64 portrait rectangle above it.
            GameObject infoObject = new GameObject(
                "Info Cell Below Portrait",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D));
            infoObject.transform.SetParent(rowObject.transform, false);
            RectTransform infoRect = infoObject.GetComponent<RectTransform>();
            infoRect.anchorMin = infoRect.anchorMax = new Vector2(0.5f, 0.5f);
            infoRect.pivot = new Vector2(0.5f, 0.5f);
            infoRect.anchoredPosition = new Vector2(
                0f,
                -(polePortraitSize + polePortraitInfoGap) * 0.5f);
            infoRect.sizeDelta = new Vector2(poleInfoCellWidth, poleInfoCellHeight);

            Image infoCellImage = infoObject.GetComponent<Image>();
            infoCellImage.color = Color.clear;
            infoCellImage.raycastTarget = false;

            float horizontalPadding = Mathf.Max(0f, poleTextPadding.x);
            float verticalPadding = Mathf.Max(0f, poleTextPadding.y);

            TextMeshProUGUI rank = CreateText(infoObject.transform, string.Empty, poleDetailFontSize, TextAlignmentOptions.Center);
            rank.rectTransform.anchorMin = new Vector2(0.02f, 0f);
            rank.rectTransform.anchorMax = new Vector2(0.48f, 0.52f);
            rank.rectTransform.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rank.rectTransform.offsetMax = new Vector2(-2f, -verticalPadding);
            rank.overflowMode = TextOverflowModes.Truncate;
            ApplyPortraitTextOutline(rank);

            TextMeshProUGUI name = CreateText(infoObject.transform, string.Empty, poleNameFontSize, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = new Vector2(0.02f, 0.52f);
            name.rectTransform.anchorMax = new Vector2(0.98f, 1f);
            name.rectTransform.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            name.rectTransform.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            name.overflowMode = TextOverflowModes.Truncate;
            ApplyPortraitTextOutline(name);

            TextMeshProUGUI distance = CreateText(infoObject.transform, string.Empty, poleDetailFontSize, TextAlignmentOptions.Center);
            distance.rectTransform.anchorMin = new Vector2(0.48f, 0f);
            distance.rectTransform.anchorMax = new Vector2(0.98f, 0.52f);
            distance.rectTransform.offsetMin = new Vector2(2f, verticalPadding);
            distance.rectTransform.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            distance.overflowMode = TextOverflowModes.Truncate;
            distance.color = new Color(0.72f, 0.88f, 0.92f, 1f);
            ApplyPortraitTextOutline(distance);

            return new StandingRow
            {
                RacerName = Roster[index],
                Root = row,
                Background = background,
                Portrait = portrait,
                PortraitMaterial = portraitMaterial,
                Rank = rank,
                Name = name,
                Distance = distance
            };
        }

        private void RefreshStandingRows(
            IReadOnlyList<Racer> ordered,
            bool snap = false)
        {
            for (int i = 0; i < standingRows.Count; i++)
            {
                StandingRow row = standingRows[i];
                Racer racer = null;
                int rankIndex = -1;

                for (int rank = 0; rank < ordered.Count; rank++)
                {
                    if (ordered[rank].Name != row.RacerName)
                        continue;

                    racer = ordered[rank];
                    rankIndex = rank;
                    break;
                }

                if (racer == null)
                {
                    row.Background.gameObject.SetActive(false);
                    continue;
                }

                row.Background.gameObject.SetActive(true);

                Vector2 targetAnchor = new Vector2(
                    poleFirstSlotCenter + rankIndex * poleSlotSpacing,
                    poleRowVerticalAnchor);

                float blend = snap
                    ? 1f
                    : 1f - Mathf.Exp(
                        -polePositionSlideSpeed * Time.unscaledDeltaTime);

                Vector2 smoothAnchor = Vector2.Lerp(
                    row.Root.anchorMin,
                    targetAnchor,
                    blend);

                row.Root.anchorMin = row.Root.anchorMax = smoothAnchor;

                Sprite portraitSprite = GetRacePortrait(racer.Name);
                row.Portrait.sprite = portraitSprite;
                row.Portrait.enabled = portraitSprite != null;

                if (portraitSprite != null)
                {
                    // Render using the sprite's native imported dimensions.
                    // Do not overwrite sizeDelta after SetNativeSize().
                    row.Portrait.type = Image.Type.Simple;
                    row.Portrait.preserveAspect = true;
                    row.Portrait.SetNativeSize();
                    row.Portrait.rectTransform.localScale = Vector3.one;

                    if (row.PortraitMaterial != null)
                    {
                        Vector4 spriteUv =
                            DataUtility.GetOuterUV(portraitSprite);

                        row.PortraitMaterial.SetVector(
                            "_SpriteUVRect",
                            spriteUv);
                        row.PortraitMaterial.SetFloat(
                            "_FadeBottom",
                            polePortraitFadeBottom);
                        row.PortraitMaterial.SetFloat(
                            "_FadeTop",
                            Mathf.Max(
                                polePortraitFadeBottom + 0.001f,
                                polePortraitFadeTop));
                        row.PortraitMaterial.SetFloat(
                            "_FadeSteps",
                            polePortraitFadeSteps);
                    }
                }

                row.Rank.text = $"POS {rankIndex + 1}";
                row.Name.text = racer.Name.ToUpperInvariant();
                row.Distance.text = $"{racer.Distance:0.0}m";

                if (racer.PlayerSlot == 1)
                {
                    row.Background.color = Color.clear;
                    row.Rank.color = row.Name.color =
                        new Color(0.1f, 0.95f, 1f, 1f);
                }
                else if (racer.PlayerSlot == 2)
                {
                    row.Background.color = Color.clear;
                    row.Rank.color = row.Name.color =
                        new Color(1f, 0.08f, 0.25f, 1f);
                }
                else
                {
                    row.Background.color = Color.clear;
                    row.Rank.color = row.Name.color = Color.white;
                }
            }
        }

        private void ReleaseStandingRowMaterials()
        {
            foreach (StandingRow row in standingRows)
            {
                if (row != null && row.PortraitMaterial != null)
                {
                    Destroy(row.PortraitMaterial);
                    row.PortraitMaterial = null;
                }
            }
        }

        private static void ApplyPortraitTextOutline(TextMeshProUGUI label)
        {
            label.outlineColor = Color.black;
            label.outlineWidth = 0.24f;
        }

        private Sprite GetRacePortrait(string racerName)
        {
            if (racePortraitCache.TryGetValue(racerName, out Sprite cached))
                return cached;

            string path = "RaceSurfers/Portraits/" + racerName.ToLowerInvariant();
            Sprite[] portraits = Resources.LoadAll<Sprite>(path);
            Sprite portrait = portraits != null && portraits.Length > 0
                ? portraits[0]
                : Resources.Load<Sprite>(path);
            racePortraitCache[racerName] = portrait;
            return portrait;
        }

        private void EnsureCanvas()
        {
            if (canvas != null) return;
            GameObject go = new GameObject("Race Mode Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = RaceHudCanvasOrder;
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
            le.preferredWidth = 200f;
            le.preferredHeight = 160f;

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
                pr.anchorMin = new Vector2(0.18f, 0.31f);
                pr.anchorMax = new Vector2(0.82f, 0.88f);
                pr.offsetMin = pr.offsetMax = Vector2.zero;
            }

            TextMeshProUGUI label = CreateText(
                go.transform,
                racer.ToUpperInvariant(),
                20,
                TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = new Vector2(0f, 0.06f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.28f);
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
