using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace PixelOcean
{
    [DefaultExecutionOrder(-30000)]
    [DisallowMultipleComponent]
    public sealed class SurferSlugPauseMenu : MonoBehaviour
    {
        public static SurferSlugPauseMenu Instance { get; private set; }
        public static bool GameplayPaused { get; private set; }

        [Header("Startup")]
        [SerializeField] private bool showAsMainMenuOnStart = true;
        [SerializeField, Min(0.1f)] private float motionDuration = 0.42f;
        [SerializeField, Min(0.05f)] private float buttonStagger = 0.055f;
        [SerializeField] private Color screenDim = new(0f, 0f, 0f, 0.16f);

        private readonly List<MonoBehaviour> disabledGameplayBehaviours = new();
        private Canvas canvas;
        private GameObject menuRoot;
        private RectTransform logoPanel;
        private RectTransform buttonPanel;
        private CanvasGroup buttonPanelInputGroup;
        private RectTransform titleCreditPanel;
        private CanvasGroup titleCreditGroup;
        private GameObject controlsPanel;
        private GameObject settingsPanel;
        private Button playButton;
        private Button continueButton;
        private Button controlsButton;
        private Button settingsButton;
        private Button developerButton;
        private Button quitButton;
        private Image logoImage;
        private Image startupBlackImage;
        private CanvasGroup startupBlackGroup;
        private readonly List<RectTransform> fadeWaveBands = new();
        private readonly List<CanvasGroup> fadeWaveGroups = new();
        private readonly List<Vector2> fadeWaveHomePositions = new();
        private Sprite[] logoFrames;
        private Coroutine motionRoutine;
        private Coroutine logoRoutine;
        private Coroutine developerOverlayMotionRoutine;
        private bool firstMenu = true;
        private bool menuVisible;
        private bool showingTitleMenu;
        private float inputReadyTime;

        // Secret controller code: UP, UP, LEFT, RIGHT, LEFT, RIGHT, LB, RB.
        private const float DeveloperCheatStepTimeout = 1.0f;
        private const string MasterVolumePref = "SurferSlug.MasterVolume";
        private const string FullscreenPref = "SurferSlug.Fullscreen";
        private Slider masterVolumeSlider;
        private Toggle fullscreenToggle;
        private Button resetOptionsButton;
        private Button optionsBackButton;
        private int developerCheatStep;
        private float developerCheatDeadline;

        private static readonly HashSet<string> SimulationTypeNames = new(StringComparer.Ordinal)
        {
            nameof(PixelWaterGPU), nameof(PixelWaterSimulation), nameof(PixelWaterRenderer),
            nameof(EndlessWaveSections), nameof(ProceduralWaveAudio), nameof(ProceduralStarryNight),
            "ProceduralDayNightSystem", nameof(ProceduralRainSystem), nameof(ProceduralHorizonFog),
            nameof(TropicalSeabed), nameof(SceneFadeIn), "InterWaveLaneSystem", "InterWaveLane",
            "InterWaveRenderItem", "InterWaveWorldItem"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ApplySavedOptions();
            BuildMenu();
        }

        private void Start()
        {
            if (showAsMainMenuOnStart)
            {
                GameplayPaused = true;
                DisableGameplayBehaviours();
                ShowMenu(true);
            }
            else
            {
                menuRoot.SetActive(false);
                firstMenu = false;
            }
        }

        private void Update()
        {
            // The developer overlay owns controller navigation while it is open.
            // Do not let the hidden title menu process the same button presses.
            if (SurferSlugDeveloperMenu.IsOpen)
                return;

            if (menuVisible &&
                ((controlsPanel != null && controlsPanel.activeSelf) ||
                 (settingsPanel != null && settingsPanel.activeSelf)) &&
                SubPanelBackPressed())
            {
                ShowMainLayout();
                inputReadyTime = Time.unscaledTime + 0.14f;
                return;
            }

            if (menuVisible)
                UpdateDeveloperCheat();

            if (Time.unscaledTime < inputReadyTime)
                return;

            if (!PausePressed())
                return;

            if (menuVisible)
            {
                if (!firstMenu)
                    ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }


        private enum DeveloperCheatInput
        {
            None,
            Up,
            Left,
            Right,
            LeftBumper,
            RightBumper,
            Other
        }

        private void UpdateDeveloperCheat()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
            {
                ResetDeveloperCheat();
                return;
            }

            if (developerCheatStep > 0 && Time.unscaledTime > developerCheatDeadline)
                ResetDeveloperCheat();

            DeveloperCheatInput input = ReadDeveloperCheatInput(gamepad);
            if (input == DeveloperCheatInput.None)
                return;

            DeveloperCheatInput expected = developerCheatStep switch
            {
                0 => DeveloperCheatInput.Up,
                1 => DeveloperCheatInput.Up,
                2 => DeveloperCheatInput.Left,
                3 => DeveloperCheatInput.Right,
                4 => DeveloperCheatInput.Left,
                5 => DeveloperCheatInput.Right,
                6 => DeveloperCheatInput.LeftBumper,
                7 => DeveloperCheatInput.RightBumper,
                _ => DeveloperCheatInput.None
            };

            if (input == expected)
            {
                developerCheatStep++;
                developerCheatDeadline = Time.unscaledTime + DeveloperCheatStepTimeout;

                if (developerCheatStep >= 8)
                {
                    ResetDeveloperCheat();
                    RefreshDeveloperButton();
                    SurferSlugDeveloperMenu.UnlockAndOpen();
                }
                return;
            }

            // A fresh UP can immediately begin another attempt.
            developerCheatStep = input == DeveloperCheatInput.Up ? 1 : 0;
            developerCheatDeadline = developerCheatStep > 0
                ? Time.unscaledTime + DeveloperCheatStepTimeout
                : 0f;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static DeveloperCheatInput ReadDeveloperCheatInput(Gamepad gamepad)
        {
            if (gamepad.dpad.up.wasPressedThisFrame) return DeveloperCheatInput.Up;
            if (gamepad.dpad.left.wasPressedThisFrame) return DeveloperCheatInput.Left;
            if (gamepad.dpad.right.wasPressedThisFrame) return DeveloperCheatInput.Right;
            if (gamepad.leftShoulder.wasPressedThisFrame) return DeveloperCheatInput.LeftBumper;
            if (gamepad.rightShoulder.wasPressedThisFrame) return DeveloperCheatInput.RightBumper;

            // Inputs outside the code intentionally cancel a partial sequence.
            if (gamepad.dpad.down.wasPressedThisFrame ||
                gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.buttonNorth.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.buttonWest.wasPressedThisFrame ||
                gamepad.leftTrigger.wasPressedThisFrame ||
                gamepad.rightTrigger.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame ||
                gamepad.selectButton.wasPressedThisFrame)
                return DeveloperCheatInput.Other;

            return DeveloperCheatInput.None;
        }
#endif

        private void ResetDeveloperCheat()
        {
            developerCheatStep = 0;
            developerCheatDeadline = 0f;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            RestoreGameplayBehaviours();
            GameplayPaused = false;
            Instance = null;
        }

        public void ShowGameOver()
        {
            GameplayPaused = true;
            DisableGameplayBehaviours();
            ShowMenu(true);
            RefreshContinueButton();
        }

        public void PauseGame()
        {
            if (menuVisible)
                return;

            GameplayPaused = true;
            DisableGameplayBehaviours();
            ShowMenu(false);
        }

        public void ResumeGame()
        {
            if (!menuVisible)
                return;

            firstMenu = false;
            if (motionRoutine != null)
                StopCoroutine(motionRoutine);
            motionRoutine = StartCoroutine(HideAnimated());
        }

        private void ShowMenu(bool isMainMenu)
        {
            firstMenu = isMainMenu;
            menuVisible = true;
            showingTitleMenu = isMainMenu;
            if (continueButton != null) continueButton.gameObject.SetActive(isMainMenu);
            menuRoot.SetActive(true);
            controlsPanel.SetActive(false);
            settingsPanel.SetActive(false);
            logoPanel.gameObject.SetActive(true);
            buttonPanel.gameObject.SetActive(true);
            if (titleCreditPanel != null) titleCreditPanel.gameObject.SetActive(isMainMenu);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (motionRoutine != null)
                StopCoroutine(motionRoutine);
            motionRoutine = StartCoroutine(ShowAnimated());
            RefreshContinueButton();
            RefreshDeveloperButton();
            inputReadyTime = Time.unscaledTime + motionDuration + 0.12f;
        }

        private IEnumerator ShowAnimated()
        {
            Vector2 logoTarget = new(-390f, 0f);
            Vector2 buttonsTarget = new(560f, 0f);
            Vector2 logoStart = new(-1450f, 0f);
            Vector2 buttonsStart = new(1500f, 0f);
            Vector2 creditsTarget = new(0f, 34f);
            Vector2 creditsStart = new(0f, -28f);

            logoPanel.anchoredPosition = logoStart;
            buttonPanel.anchoredPosition = buttonsStart;
            if (titleCreditPanel != null) titleCreditPanel.anchoredPosition = creditsStart;
            if (titleCreditGroup != null) titleCreditGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < motionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutBack(Mathf.Clamp01(elapsed / motionDuration));
                logoPanel.anchoredPosition = Vector2.LerpUnclamped(logoStart, logoTarget, t);
                buttonPanel.anchoredPosition = Vector2.LerpUnclamped(buttonsStart, buttonsTarget, t);
                if (showingTitleMenu && titleCreditPanel != null)
                {
                    float creditT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed / motionDuration - 0.34f) / 0.66f));
                    titleCreditPanel.anchoredPosition = Vector2.LerpUnclamped(creditsStart, creditsTarget, creditT);
                    if (titleCreditGroup != null) titleCreditGroup.alpha = creditT;
                }
                yield return null;
            }

            logoPanel.anchoredPosition = logoTarget;
            buttonPanel.anchoredPosition = buttonsTarget;
            if (showingTitleMenu && titleCreditPanel != null) titleCreditPanel.anchoredPosition = creditsTarget;
            if (titleCreditGroup != null) titleCreditGroup.alpha = showingTitleMenu ? 1f : 0f;
            RefreshContinueButton();
            Select(continueButton != null && continueButton.gameObject.activeInHierarchy && continueButton.interactable ? continueButton : playButton);
        }

        private IEnumerator HideAnimated()
        {
            Vector2 logoStart = logoPanel.anchoredPosition;
            Vector2 buttonStart = buttonPanel.anchoredPosition;
            Vector2 logoEnd = new(-1450f, 0f);
            Vector2 buttonEnd = new(1500f, 0f);
            Vector2 creditsStart = titleCreditPanel != null ? titleCreditPanel.anchoredPosition : Vector2.zero;
            Vector2 creditsEnd = new(0f, -28f);

            float elapsed = 0f;
            while (elapsed < motionDuration * 0.72f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseInCubic(Mathf.Clamp01(elapsed / (motionDuration * 0.72f)));
                logoPanel.anchoredPosition = Vector2.LerpUnclamped(logoStart, logoEnd, t);
                buttonPanel.anchoredPosition = Vector2.LerpUnclamped(buttonStart, buttonEnd, t);
                if (titleCreditPanel != null) titleCreditPanel.anchoredPosition = Vector2.LerpUnclamped(creditsStart, creditsEnd, t);
                if (titleCreditGroup != null) titleCreditGroup.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }

            menuRoot.SetActive(false);
            menuVisible = false;
            GameplayPaused = false;
            RestoreGameplayBehaviours();
            EventSystem.current?.SetSelectedGameObject(null);
            inputReadyTime = Time.unscaledTime + 0.16f;
        }

        private void BuildMenu()
        {
            EnsureEventSystem();

            GameObject canvasObject = new("Surfer Slug Front End Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject blackObject = CreateUIObject(canvasObject.transform, "Opening Black Hold");
            Stretch(blackObject.GetComponent<RectTransform>());
            startupBlackImage = blackObject.AddComponent<Image>();
            startupBlackImage.color = Color.black;
            startupBlackImage.raycastTarget = true;
            startupBlackGroup = blackObject.AddComponent<CanvasGroup>();
            startupBlackGroup.alpha = 1f;
            startupBlackGroup.interactable = false;
            startupBlackGroup.blocksRaycasts = true;

            BuildRigidAbyssBands(blackObject.transform);

            menuRoot = CreateUIObject(canvasObject.transform, "Menu Root");
            Stretch(menuRoot.GetComponent<RectTransform>());
            Image dim = menuRoot.AddComponent<Image>();
            dim.color = screenDim;

            BuildLogo(menuRoot.transform);
            BuildButtons(menuRoot.transform);
            BuildTitleCredits(menuRoot.transform);
            BuildControls(menuRoot.transform);
            BuildSettings(menuRoot.transform);
        }


        private void BuildRigidAbyssBands(Transform parent)
        {
            fadeWaveBands.Clear();
            fadeWaveGroups.Clear();
            fadeWaveHomePositions.Clear();

            // Twelve narrow, near-black layers create a deliberate stepped gradient.
            Color[] colors =
            {
                new Color(0.002f, 0.004f, 0.009f, 1f),
                new Color(0.004f, 0.009f, 0.016f, 1f),
                new Color(0.006f, 0.015f, 0.024f, 1f),
                new Color(0.008f, 0.021f, 0.032f, 1f),
                new Color(0.010f, 0.027f, 0.041f, 1f),
                new Color(0.012f, 0.033f, 0.050f, 1f),
                new Color(0.014f, 0.038f, 0.058f, 1f),
                new Color(0.012f, 0.031f, 0.049f, 1f),
                new Color(0.009f, 0.024f, 0.039f, 1f),
                new Color(0.006f, 0.017f, 0.029f, 1f),
                new Color(0.004f, 0.010f, 0.019f, 1f),
                new Color(0.002f, 0.005f, 0.011f, 1f)
            };

            const float screenHeight = 1080f;
            float bandHeight = screenHeight / colors.Length + 4f;

            for (int i = 0; i < colors.Length; i++)
            {
                GameObject bandObject = CreateUIObject(parent, $"Rigid Abyss Layer {i + 1:00}");
                RectTransform rect = bandObject.GetComponent<RectTransform>();

                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(180f, bandHeight);

                float y = -screenHeight * 0.5f + bandHeight * 0.5f + i * (screenHeight / colors.Length);
                Vector2 home = new Vector2(0f, y);
                rect.anchoredPosition = home;

                Image image = bandObject.AddComponent<Image>();
                image.color = colors[i];
                image.raycastTarget = false;

                // A one-pixel-feeling separator makes each division intentional.
                Outline divider = bandObject.AddComponent<Outline>();
                divider.effectColor = new Color(0.09f, 0.15f, 0.19f, i % 3 == 0 ? 0.18f : 0.08f);
                divider.effectDistance = new Vector2(0f, -2f);
                divider.useGraphicAlpha = true;

                CanvasGroup group = bandObject.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                group.interactable = false;
                group.blocksRaycasts = false;

                fadeWaveBands.Add(rect);
                fadeWaveGroups.Add(group);
                fadeWaveHomePositions.Add(home);
            }
        }

        private void BuildLogo(Transform parent)
        {
            GameObject logoObject = CreateUIObject(parent, "Animated Logotype");
            logoPanel = logoObject.GetComponent<RectTransform>();
            logoPanel.anchorMin = logoPanel.anchorMax = new Vector2(0.5f, 0.55f);
            logoPanel.pivot = new Vector2(0.5f, 0.5f);
            logoPanel.sizeDelta = Vector2.zero;

            logoImage = logoObject.AddComponent<Image>();
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;

            logoFrames = Resources.LoadAll<Sprite>("SurferSlugUI/surfer_slug_logotype-sheet");
            Array.Sort(logoFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (logoFrames.Length > 0)
            {
                logoImage.sprite = logoFrames[0];
                logoImage.SetNativeSize();
                logoRoutine = StartCoroutine(AnimateLogo());
            }
        }

        private IEnumerator AnimateLogo()
        {
            int frame = 0;
            while (true)
            {
                if (logoFrames.Length > 0 && logoImage != null)
                {
                    logoImage.sprite = logoFrames[frame % logoFrames.Length];
                    frame++;
                }
                yield return new WaitForSecondsRealtime(0.10f);
            }
        }

        private void BuildButtons(Transform parent)
        {
            GameObject panel = CreateUIObject(parent, "Button Column");
            buttonPanel = panel.GetComponent<RectTransform>();
            buttonPanel.anchorMin = buttonPanel.anchorMax = new Vector2(0.5f, 0.5f);
            buttonPanel.pivot = new Vector2(0.5f, 0.5f);
            buttonPanel.sizeDelta = new Vector2(430f, 700f);

            buttonPanelInputGroup = panel.AddComponent<CanvasGroup>();
            buttonPanelInputGroup.interactable = true;
            buttonPanelInputGroup.blocksRaycasts = true;

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            playButton = CreateSpriteButton(panel.transform, "play_button", PlayPressed);
            continueButton = CreateSpriteButton(panel.transform, "continue_button", ContinuePressed);
            controlsButton = CreateSpriteButton(panel.transform, "controls_button", ShowControls);
            settingsButton = CreateSpriteButton(panel.transform, "options_button", ShowSettings);
            developerButton = CreateSpriteButton(panel.transform, "developer_button", OpenDeveloperMenu);
            quitButton = CreateSpriteButton(panel.transform, "quit_button", QuitGame);

            RefreshDeveloperButton();
        }


        private void RefreshDeveloperButton()
        {
            if (developerButton == null)
                return;

            developerButton.gameObject.SetActive(SurferSlugDeveloperMenu.IsUnlocked);
        }

        private void OpenDeveloperMenu()
        {
            SurferSlugDeveloperMenu.UnlockAndOpen();
        }

        public void SetDeveloperOverlayPresentation(bool overlayVisible)
        {
            if (!menuVisible || logoPanel == null || buttonPanel == null)
                return;

            // Immediately hand all UI input to the developer overlay before its
            // opening button press can also activate a title-screen selection.
            if (buttonPanelInputGroup != null)
            {
                buttonPanelInputGroup.interactable = !overlayVisible;
                buttonPanelInputGroup.blocksRaycasts = !overlayVisible;
            }

            if (overlayVisible)
                EventSystem.current?.SetSelectedGameObject(null);

            if (developerOverlayMotionRoutine != null)
                StopCoroutine(developerOverlayMotionRoutine);

            developerOverlayMotionRoutine = StartCoroutine(AnimateDeveloperOverlayPresentation(overlayVisible));
        }

        private IEnumerator AnimateDeveloperOverlayPresentation(bool overlayVisible)
        {
            Vector2 logoFrom = logoPanel.anchoredPosition;
            Vector2 buttonsFrom = buttonPanel.anchoredPosition;
            Vector2 creditsFrom = titleCreditPanel != null ? titleCreditPanel.anchoredPosition : Vector2.zero;
            float creditsAlphaFrom = titleCreditGroup != null ? titleCreditGroup.alpha : 0f;

            Vector2 logoTo = overlayVisible ? new Vector2(-1450f, 0f) : new Vector2(-390f, 0f);
            Vector2 buttonsTo = overlayVisible ? new Vector2(1500f, 0f) : new Vector2(560f, 0f);
            Vector2 creditsTo = overlayVisible ? new Vector2(0f, -28f) : new Vector2(0f, 34f);
            float creditsAlphaTo = overlayVisible || !showingTitleMenu ? 0f : 1f;

            float duration = overlayVisible ? motionDuration * 0.72f : motionDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                float t = overlayVisible ? EaseInCubic(normalized) : EaseOutBack(normalized);

                logoPanel.anchoredPosition = Vector2.LerpUnclamped(logoFrom, logoTo, t);
                buttonPanel.anchoredPosition = Vector2.LerpUnclamped(buttonsFrom, buttonsTo, t);

                if (titleCreditPanel != null)
                    titleCreditPanel.anchoredPosition = Vector2.LerpUnclamped(creditsFrom, creditsTo, t);

                if (titleCreditGroup != null)
                    titleCreditGroup.alpha = Mathf.Lerp(creditsAlphaFrom, creditsAlphaTo, Mathf.Clamp01(normalized));

                yield return null;
            }

            logoPanel.anchoredPosition = logoTo;
            buttonPanel.anchoredPosition = buttonsTo;
            if (titleCreditPanel != null) titleCreditPanel.anchoredPosition = creditsTo;
            if (titleCreditGroup != null) titleCreditGroup.alpha = creditsAlphaTo;

            if (!overlayVisible)
            {
                if (buttonPanelInputGroup != null)
                {
                    buttonPanelInputGroup.interactable = true;
                    buttonPanelInputGroup.blocksRaycasts = true;
                }

                RefreshContinueButton();
                RefreshDeveloperButton();
                Select(continueButton != null &&
                       continueButton.gameObject.activeInHierarchy &&
                       continueButton.interactable
                    ? continueButton
                    : playButton);
            }

            developerOverlayMotionRoutine = null;
        }

        private Button CreateSpriteButton(Transform parent, string resourceName, UnityEngine.Events.UnityAction action)
        {
            GameObject go = CreateUIObject(parent, resourceName);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 96f;
            le.preferredWidth = 400f;

            Image image = go.AddComponent<Image>();
            Sprite[] sprites = Resources.LoadAll<Sprite>("SurferSlugUI/Buttons/" + resourceName);
            image.sprite = sprites.Length > 0 ? sprites[0] : null;
            image.preserveAspect = true;

            Button button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.86f, 0.50f, 1f);
            colors.selectedColor = new Color(1f, 0.86f, 0.50f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 1f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }


        private void BuildTitleCredits(Transform parent)
        {
            GameObject panel = CreateUIObject(parent, "Title Credit Lines");
            titleCreditPanel = panel.GetComponent<RectTransform>();
            titleCreditPanel.anchorMin = new Vector2(0f, 0f);
            titleCreditPanel.anchorMax = new Vector2(1f, 0f);
            titleCreditPanel.pivot = new Vector2(0.5f, 0f);
            titleCreditPanel.sizeDelta = new Vector2(-96f, 24f);
            titleCreditPanel.anchoredPosition = new Vector2(0f, 34f);

            titleCreditGroup = panel.AddComponent<CanvasGroup>();
            titleCreditGroup.alpha = 0f;
            titleCreditGroup.interactable = false;
            titleCreditGroup.blocksRaycasts = false;

            CreateTitleCreditLabel(panel.transform, "A ZILLATRONICS PRODUCTION", TextAnchor.MiddleLeft);
            CreateTitleCreditLabel(panel.transform, "SURFER SLUG  //  © 2026  //  ALL RIGHTS RESERVED", TextAnchor.MiddleRight);
        }

        private void CreateTitleCreditLabel(
            Transform parent,
            string copy,
            TextAnchor alignment)
        {
            GameObject go = CreateUIObject(parent, "Credit Label");

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = alignment == TextAnchor.MiddleLeft
                ? new Vector2(0f, 0f)
                : new Vector2(0.5f, 0f);

            rect.anchorMax = alignment == TextAnchor.MiddleLeft
                ? new Vector2(0.5f, 1f)
                : new Vector2(1f, 1f);

            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.font = PixelFontLibrary.TmpRegular;
            label.text = copy;
            label.fontSize = 16f;
            label.fontStyle = FontStyles.Normal;
            label.alignment = alignment == TextAnchor.MiddleLeft
                ? TextAlignmentOptions.MidlineLeft
                : TextAlignmentOptions.MidlineRight;

            label.color = new Color(0.86f, 0.88f, 0.90f, 0.72f);
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        private void BuildControls(Transform parent)
        {
            controlsPanel = CreateSubPanel(parent, "Controls Panel");

            GameObject imageObject = CreateUIObject(controlsPanel.transform, "Controls Diagram");

            LayoutElement layout = imageObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 420f;
            layout.preferredHeight = 420f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Image image = imageObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.sprite = Resources.Load<Sprite>("SurferSlugUI/controls_diagram");

            CreateSpriteButton(controlsPanel.transform, "back_button", ShowMainLayout);

            controlsPanel.SetActive(false);
        }

        private void BuildSettings(Transform parent)
        {
            settingsPanel = CreateSubPanel(parent, "Options Panel");

            AddTmpHeading(settingsPanel.transform, "OPTIONS", 42f, 82f);
            AddTmpCaption(settingsPanel.transform,
                "USE LEFT / RIGHT TO ADJUST   •   A / ENTER TO SELECT   •   B / ESC TO GO BACK",
                15f, 46f);

            masterVolumeSlider = CreateOptionSliderRow(
                settingsPanel.transform,
                "MASTER VOLUME",
                PlayerPrefs.GetFloat(MasterVolumePref, 1f),
                SetMasterVolume);

            fullscreenToggle = CreateOptionToggleRow(
                settingsPanel.transform,
                "FULLSCREEN",
                PlayerPrefs.GetInt(FullscreenPref, Screen.fullScreen ? 1 : 0) != 0,
                SetFullscreen);

            resetOptionsButton = CreateOptionTextButton(
                settingsPanel.transform,
                "RESET DEFAULTS",
                ResetOptionsToDefaults);

            optionsBackButton = CreateSpriteButton(settingsPanel.transform, "back_button", ShowMainLayout);

            ConfigureExplicitOptionNavigation();
            settingsPanel.SetActive(false);
        }

        private void ApplySavedOptions()
        {
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePref, 1f));
            AudioListener.volume = volume;

            bool fullscreen = PlayerPrefs.GetInt(
                FullscreenPref,
                Screen.fullScreen ? 1 : 0) != 0;

            if (Screen.fullScreen != fullscreen)
                Screen.fullScreen = fullscreen;
        }

        private void SetMasterVolume(float value)
        {
            value = Mathf.Clamp01(value);
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumePref, value);
            PlayerPrefs.Save();
        }

        private void SetFullscreen(bool value)
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenPref, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ResetOptionsToDefaults()
        {
            if (masterVolumeSlider != null)
                masterVolumeSlider.value = 1f;

            if (fullscreenToggle != null)
                fullscreenToggle.isOn = true;

            SetMasterVolume(1f);
            SetFullscreen(true);
            Select(masterVolumeSlider);
        }

        private Slider CreateOptionSliderRow(
            Transform parent,
            string labelText,
            float initialValue,
            UnityEngine.Events.UnityAction<float> callback)
        {
            GameObject row = CreateUIObject(parent, labelText + " Option Row");
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 108f;

            Image rowBackground = row.AddComponent<Image>();
            rowBackground.color = new Color(0.075f, 0.08f, 0.105f, 0.96f);

            Outline rowOutline = row.AddComponent<Outline>();
            rowOutline.effectColor = new Color(0.43f, 0.47f, 0.58f, 0.82f);
            rowOutline.effectDistance = new Vector2(2f, -2f);

            HorizontalLayoutGroup horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(28, 28, 18, 18);
            horizontal.spacing = 24f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            TextMeshProUGUI label = CreateTmpText(row.transform, labelText, 22f, TextAlignmentOptions.MidlineLeft);
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 265f;
            labelLayout.flexibleWidth = 0f;

            GameObject sliderObject = CreateUIObject(row.transform, labelText + " Slider");
            LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
            sliderLayout.preferredWidth = 290f;
            sliderLayout.preferredHeight = 48f;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            GameObject backgroundObject = CreateUIObject(sliderObject.transform, "Background");
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(0f, 16f);
            Image background = backgroundObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.025f, 0.04f, 1f);

            GameObject fillAreaObject = CreateUIObject(sliderObject.transform, "Fill Area");
            RectTransform fillArea = fillAreaObject.GetComponent<RectTransform>();
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(4f, -7f);
            fillArea.offsetMax = new Vector2(-4f, 7f);

            GameObject fillObject = CreateUIObject(fillAreaObject.transform, "Fill");
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fill = fillObject.AddComponent<Image>();
            fill.color = new Color(0.28f, 0.68f, 0.84f, 1f);

            GameObject handleAreaObject = CreateUIObject(sliderObject.transform, "Handle Slide Area");
            Stretch(handleAreaObject.GetComponent<RectTransform>());

            GameObject handleObject = CreateUIObject(handleAreaObject.transform, "Handle");
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(28f, 38f);
            Image handle = handleObject.AddComponent<Image>();
            handle.color = new Color(0.93f, 0.87f, 0.63f, 1f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.transition = Selectable.Transition.ColorTint;
            ColorBlock sliderColors = slider.colors;
            sliderColors.normalColor = Color.white;
            sliderColors.highlightedColor = new Color(1f, 0.93f, 0.67f, 1f);
            sliderColors.selectedColor = new Color(1f, 0.93f, 0.67f, 1f);
            sliderColors.pressedColor = new Color(0.62f, 0.84f, 1f, 1f);
            slider.colors = sliderColors;

            TextMeshProUGUI valueLabel = CreateTmpText(row.transform, "100%", 20f, TextAlignmentOptions.MidlineRight);
            LayoutElement valueLayout = valueLabel.gameObject.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 78f;
            valueLayout.flexibleWidth = 0f;

            slider.SetValueWithoutNotify(Mathf.Clamp01(initialValue));
            valueLabel.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            slider.onValueChanged.AddListener(value =>
            {
                valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
                callback?.Invoke(value);
            });

            return slider;
        }

        private Toggle CreateOptionToggleRow(
            Transform parent,
            string labelText,
            bool initialValue,
            UnityEngine.Events.UnityAction<bool> callback)
        {
            GameObject row = CreateUIObject(parent, labelText + " Option Row");
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 92f;

            Image rowBackground = row.AddComponent<Image>();
            rowBackground.color = new Color(0.075f, 0.08f, 0.105f, 0.96f);

            Outline rowOutline = row.AddComponent<Outline>();
            rowOutline.effectColor = new Color(0.43f, 0.47f, 0.58f, 0.82f);
            rowOutline.effectDistance = new Vector2(2f, -2f);

            Toggle toggle = row.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.ColorTint;

            TextMeshProUGUI label = CreateTmpText(row.transform, labelText, 22f, TextAlignmentOptions.MidlineLeft);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(28f, 0f);
            labelRect.offsetMax = new Vector2(-180f, 0f);

            GameObject stateObject = CreateUIObject(row.transform, "Toggle State");
            RectTransform stateRect = stateObject.GetComponent<RectTransform>();
            stateRect.anchorMin = stateRect.anchorMax = new Vector2(1f, 0.5f);
            stateRect.pivot = new Vector2(1f, 0.5f);
            stateRect.sizeDelta = new Vector2(132f, 50f);
            stateRect.anchoredPosition = new Vector2(-28f, 0f);
            Image stateBackground = stateObject.AddComponent<Image>();
            stateBackground.color = new Color(0.025f, 0.03f, 0.045f, 1f);

            TextMeshProUGUI stateLabel = CreateTmpText(stateObject.transform, initialValue ? "ON" : "OFF", 22f, TextAlignmentOptions.Center);
            Stretch(stateLabel.rectTransform);
            stateLabel.color = initialValue
                ? new Color(0.48f, 0.86f, 0.88f, 1f)
                : new Color(0.62f, 0.64f, 0.69f, 1f);

            toggle.targetGraphic = rowBackground;
            toggle.graphic = null;
            toggle.SetIsOnWithoutNotify(initialValue);
            toggle.onValueChanged.AddListener(value =>
            {
                stateLabel.text = value ? "ON" : "OFF";
                stateLabel.color = value
                    ? new Color(0.48f, 0.86f, 0.88f, 1f)
                    : new Color(0.62f, 0.64f, 0.69f, 1f);
                callback?.Invoke(value);
            });

            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.89f, 0.57f, 1f);
            colors.selectedColor = new Color(1f, 0.89f, 0.57f, 1f);
            colors.pressedColor = new Color(0.66f, 0.84f, 1f, 1f);
            toggle.colors = colors;

            return toggle;
        }

        private Button CreateOptionTextButton(
            Transform parent,
            string text,
            UnityEngine.Events.UnityAction action)
        {
            GameObject go = CreateUIObject(parent, text + " Button");
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 72f;
            layout.preferredWidth = 390f;

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.10f, 0.105f, 0.135f, 1f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.48f, 0.51f, 0.62f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.87f, 0.54f, 1f);
            colors.selectedColor = new Color(1f, 0.87f, 0.54f, 1f);
            colors.pressedColor = new Color(0.68f, 0.84f, 1f, 1f);
            button.colors = colors;

            TextMeshProUGUI label = CreateTmpText(go.transform, text, 21f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        private TextMeshProUGUI CreateTmpText(
            Transform parent,
            string text,
            float size,
            TextAlignmentOptions alignment)
        {
            GameObject go = CreateUIObject(parent, text + " TMP");
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.font = PixelFontLibrary.TmpMedium;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Normal;
            label.alignment = alignment;
            label.color = new Color(0.91f, 0.91f, 0.92f, 1f);
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        private void AddTmpHeading(Transform parent, string text, float size, float height)
        {
            TextMeshProUGUI label = CreateTmpText(parent, text, size, TextAlignmentOptions.Center);
            label.font = PixelFontLibrary.TmpBold;
            label.color = new Color(0.93f, 0.81f, 0.57f, 1f);
            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }

        private void AddTmpCaption(Transform parent, string text, float size, float height)
        {
            TextMeshProUGUI label = CreateTmpText(parent, text, size, TextAlignmentOptions.Center);
            label.font = PixelFontLibrary.TmpRegular;
            label.color = new Color(0.66f, 0.69f, 0.75f, 1f);
            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }

        private void ConfigureExplicitOptionNavigation()
        {
            if (masterVolumeSlider == null ||
                fullscreenToggle == null ||
                resetOptionsButton == null ||
                optionsBackButton == null)
                return;

            SetSliderNavigation(masterVolumeSlider, optionsBackButton, fullscreenToggle);
            SetVerticalNavigation(fullscreenToggle, masterVolumeSlider, resetOptionsButton);
            SetVerticalNavigation(resetOptionsButton, fullscreenToggle, optionsBackButton);
            SetVerticalNavigation(optionsBackButton, resetOptionsButton, masterVolumeSlider);
        }

        private static void SetSliderNavigation(
            Slider slider,
            Selectable up,
            Selectable down)
        {
            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;

            // Do not assign left/right. Slider.OnMove handles those.
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;

            slider.navigation = navigation;
        }

        private static void SetVerticalNavigation(Selectable selectable, Selectable up, Selectable down)
        {
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = selectable is Slider ? selectable : null;
            navigation.selectOnRight = selectable is Slider ? selectable : null;
            selectable.navigation = navigation;
        }

        private GameObject CreateSubPanel(Transform parent, string name)
        {
            GameObject panel = CreateUIObject(parent, name);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 700f);
            rect.anchoredPosition = Vector2.zero;
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.04f, 0.04f, 0.055f, 0.96f);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.53f, 0.57f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(55, 55, 45, 45);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        private void ShowControls()
        {
            logoPanel.gameObject.SetActive(false);
            buttonPanel.gameObject.SetActive(false);
            settingsPanel.SetActive(false);
            if (titleCreditPanel != null) titleCreditPanel.gameObject.SetActive(false);
            controlsPanel.SetActive(true);
            Select(controlsPanel.GetComponentInChildren<Button>());
        }

        private void ShowSettings()
        {
            logoPanel.gameObject.SetActive(false);
            buttonPanel.gameObject.SetActive(false);
            controlsPanel.SetActive(false);
            if (titleCreditPanel != null) titleCreditPanel.gameObject.SetActive(false);
            settingsPanel.SetActive(true);
            Select(masterVolumeSlider != null ? masterVolumeSlider : settingsPanel.GetComponentInChildren<Selectable>());
        }

        private void ShowMainLayout()
        {
            controlsPanel.SetActive(false);
            settingsPanel.SetActive(false);
            logoPanel.gameObject.SetActive(true);
            buttonPanel.gameObject.SetActive(true);
            if (titleCreditPanel != null) titleCreditPanel.gameObject.SetActive(showingTitleMenu);
            RefreshContinueButton();
            RefreshDeveloperButton();
            Select(continueButton != null && continueButton.interactable ? continueButton : playButton);
        }

        private void RefreshContinueButton()
        {
            if (continueButton == null) return;
            bool hasSave = SurfStageSaveSystem.HasSave;
            continueButton.interactable = hasSave;
            Image image = continueButton.GetComponent<Image>();
            if (image != null) image.color = hasSave ? Color.white : new Color(0.42f, 0.42f, 0.42f, 0.75f);
        }

        private void PlayPressed()
        {
            if (!firstMenu)
            {
                ResumeGame();
                return;
            }
            StartCoroutine(StartNewAndResume());
        }

        private IEnumerator StartNewAndResume()
        {
            // Keep the dedicated black layer visible while the front-end UI exits.
            yield return HideMenuForOpeningTransition();

            SurfDayProgressionDirector director = FindFirstObjectByType<SurfDayProgressionDirector>();
            if (director != null)
                yield return director.StartNewRunFromMenu();

            SurfRunLifeManager.Instance?.ResetLivesForNewRun();
            TinyWaveSurfer surfer = FindFirstObjectByType<TinyWaveSurfer>();
            surfer?.RespawnForManagedRun();

            // The opening boards now belong to the Play flow, not the day director.
            // They appear over pure black before the ocean is ever revealed.
            yield return StoryboardCutsceneSystem.PlayDayOneOpening();

            yield return FadeStartupBlack(1f, 0f, 0.85f);
            FinishOpeningTransition();
        }

        private void ContinuePressed()
        {
            if (!SurfStageSaveSystem.TryLoad(out SurfStageSaveSystem.SaveData data))
            {
                RefreshContinueButton();
                return;
            }
            StartCoroutine(LoadAndResume(data));
        }

        private IEnumerator LoadAndResume(SurfStageSaveSystem.SaveData data)
        {
            // Continue skips the opening boards but still prevents a one-frame view
            // of the ocean before the saved state is ready.
            yield return HideMenuForOpeningTransition();

            SurfDayProgressionDirector director = FindFirstObjectByType<SurfDayProgressionDirector>();
            if (director != null)
                yield return director.LoadSavedRun(data);

            SurfRunLifeManager.Instance?.RestoreLives(data.lives);
            TinyWaveSurfer surfer = FindFirstObjectByType<TinyWaveSurfer>();
            surfer?.RespawnForManagedRun();
            surfer?.RestorePersistentState(data);

            yield return FadeStartupBlack(1f, 0f, 0.85f);
            FinishOpeningTransition();
        }

        private IEnumerator HideMenuForOpeningTransition()
        {
            if (startupBlackGroup != null)
            {
                startupBlackGroup.gameObject.SetActive(true);
                startupBlackGroup.alpha = 1f;
                startupBlackGroup.blocksRaycasts = true;
            }

            if (motionRoutine != null)
                StopCoroutine(motionRoutine);

            Vector2 logoStart = logoPanel.anchoredPosition;
            Vector2 buttonStart = buttonPanel.anchoredPosition;
            Vector2 logoEnd = new(-1450f, 0f);
            Vector2 buttonEnd = new(1500f, 0f);
            Vector2 creditsStart = titleCreditPanel != null ? titleCreditPanel.anchoredPosition : Vector2.zero;
            Vector2 creditsEnd = new(0f, -28f);
            float duration = motionDuration * 0.72f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseInCubic(Mathf.Clamp01(elapsed / duration));
                logoPanel.anchoredPosition = Vector2.LerpUnclamped(logoStart, logoEnd, t);
                buttonPanel.anchoredPosition = Vector2.LerpUnclamped(buttonStart, buttonEnd, t);
                if (titleCreditPanel != null) titleCreditPanel.anchoredPosition = Vector2.LerpUnclamped(creditsStart, creditsEnd, t);
                if (titleCreditGroup != null) titleCreditGroup.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }

            menuRoot.SetActive(false);
            menuVisible = false;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private IEnumerator FadeStartupBlack(float from, float to, float duration)
        {
            if (startupBlackGroup == null)
                yield break;

            startupBlackGroup.gameObject.SetActive(true);
            startupBlackGroup.alpha = 1f;
            startupBlackGroup.blocksRaycasts = true;

            bool revealing = to < from;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                float eased = Mathf.SmoothStep(0f, 1f, normalized);

                if (startupBlackImage != null)
                {
                    Color black = startupBlackImage.color;
                    black.a = revealing
                        ? Mathf.Lerp(1f, 0f, Mathf.Clamp01((normalized - 0.12f) / 0.88f))
                        : Mathf.Lerp(0f, 1f, eased);
                    startupBlackImage.color = black;
                }

                for (int i = 0; i < fadeWaveBands.Count; i++)
                {
                    RectTransform band = fadeWaveBands[i];
                    if (band == null)
                        continue;

                    float stagger = i * 0.025f;
                    float layerT = Mathf.Clamp01((normalized - stagger) / Mathf.Max(0.01f, 1f - stagger));
                    layerT = Mathf.SmoothStep(0f, 1f, layerT);

                    Vector2 home = i < fadeWaveHomePositions.Count
                        ? fadeWaveHomePositions[i]
                        : Vector2.zero;

                    // Keep the layers rigid. Only stagger their vertical exit/arrival.
                    float directionBias = (i % 2 == 0) ? -1f : 1f;
                    float horizontalStep = directionBias * (10f + (i % 4) * 5f);
                    float verticalTravel = 1180f;

                    float verticalOffset = revealing
                        ? -verticalTravel * layerT
                        : -verticalTravel * (1f - layerT);

                    band.anchoredPosition = home + new Vector2(horizontalStep, verticalOffset);

                    if (i < fadeWaveGroups.Count && fadeWaveGroups[i] != null)
                    {
                        fadeWaveGroups[i].alpha = revealing
                            ? Mathf.Lerp(1f, 0f, layerT)
                            : Mathf.Lerp(0f, 1f, layerT);
                    }
                }

                yield return null;
            }

            if (startupBlackImage != null)
            {
                Color black = startupBlackImage.color;
                black.a = to;
                startupBlackImage.color = black;
            }

            startupBlackGroup.alpha = 1f;
            startupBlackGroup.blocksRaycasts = to > 0.001f;

            for (int i = 0; i < fadeWaveBands.Count; i++)
            {
                if (fadeWaveBands[i] != null && i < fadeWaveHomePositions.Count)
                    fadeWaveBands[i].anchoredPosition = fadeWaveHomePositions[i];

                if (i < fadeWaveGroups.Count && fadeWaveGroups[i] != null)
                    fadeWaveGroups[i].alpha = to;
            }

            if (to <= 0.001f)
                startupBlackGroup.gameObject.SetActive(false);
        }

        private void FinishOpeningTransition()
        {
            firstMenu = false;
            GameplayPaused = false;
            RestoreGameplayBehaviours();
            Cursor.visible = false;
            EventSystem.current?.SetSelectedGameObject(null);
            inputReadyTime = Time.unscaledTime + 0.16f;
        }

        private void DisableGameplayBehaviours()
        {
            disabledGameplayBehaviours.Clear();
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.enabled || behaviour == this || behaviour.transform.IsChildOf(transform))
                    continue;
                Type type = behaviour.GetType();
                if (type.Namespace != typeof(SurferSlugPauseMenu).Namespace || SimulationTypeNames.Contains(type.Name))
                    continue;
                behaviour.enabled = false;
                disabledGameplayBehaviours.Add(behaviour);
            }
        }

        private void RestoreGameplayBehaviours()
        {
            foreach (MonoBehaviour behaviour in disabledGameplayBehaviours)
                if (behaviour != null) behaviour.enabled = true;
            disabledGameplayBehaviours.Clear();
        }

        private bool SubPanelBackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                   (Gamepad.current != null && gamepadBackPressed(Gamepad.current));
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool gamepadBackPressed(Gamepad gamepad)
        {
            return gamepad.buttonEast.wasPressedThisFrame;
        }
#endif

        private bool PausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                   (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            GameObject go = new("Menu EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private Text AddText(Transform parent, string text, int size, float height)
        {
            GameObject go = CreateUIObject(parent, text + " Text");
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            Text label = go.AddComponent<Text>();
            label.font = size >= 30 ? PixelFontLibrary.Bold : PixelFontLibrary.SemiBold;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.92f, 0.81f, 0.57f, 1f);
            label.raycastTarget = false;
            return label;
        }

        private Button CreatePlainButton(Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            GameObject go = CreateUIObject(parent, text + " Button");
            LayoutElement le = go.AddComponent<LayoutElement>(); le.preferredHeight = 72f;
            Image img = go.AddComponent<Image>(); img.color = new Color(0.16f, 0.15f, 0.18f, 1f);
            Button button = go.AddComponent<Button>(); button.onClick.AddListener(action);
            Text label = AddText(go.transform, text, 25, 0f); Stretch(label.rectTransform);
            return button;
        }

        private void CreateVolumeRow(Transform parent, string label)
        {
            AddText(parent, label, 24, 55f);
            GameObject go = CreateUIObject(parent, label + " Slider");
            LayoutElement le = go.AddComponent<LayoutElement>(); le.preferredHeight = 48f;
            Slider slider = go.AddComponent<Slider>();
            Image bg = go.AddComponent<Image>(); bg.color = new Color(0.16f, 0.15f, 0.18f, 1f);
            slider.targetGraphic = bg; slider.minValue = 0f; slider.maxValue = 1f; slider.value = AudioListener.volume;
            slider.onValueChanged.AddListener(value => AudioListener.volume = value);
        }

        private void CreateToggleRow(Transform parent, string label, bool value, UnityEngine.Events.UnityAction<bool> callback)
        {
            GameObject go = CreateUIObject(parent, label + " Toggle");
            LayoutElement le = go.AddComponent<LayoutElement>(); le.preferredHeight = 58f;
            Toggle toggle = go.AddComponent<Toggle>(); toggle.isOn = value; toggle.onValueChanged.AddListener(callback);
            Image bg = go.AddComponent<Image>(); bg.color = new Color(0.16f, 0.15f, 0.18f, 1f); toggle.targetGraphic = bg;
            Text text = AddText(go.transform, label + "     " + (value ? "ON" : "OFF"), 23, 0f); Stretch(text.rectTransform);
            toggle.onValueChanged.AddListener(v => text.text = label + "     " + (v ? "ON" : "OFF"));
        }

        private void QuitGame()
        {
            RestoreGameplayBehaviours();
            GameplayPaused = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f; const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float EaseInCubic(float t) => t * t * t;

        private static GameObject CreateUIObject(Transform parent, string name)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Select(Selectable selectable)
        {
            if (selectable == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
    }

    internal static class SurferSlugPauseMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateMenu()
        {
            if (UnityEngine.Object.FindFirstObjectByType<SurferSlugPauseMenu>() != null) return;
            new GameObject("Surfer Slug Front End").AddComponent<SurferSlugPauseMenu>();
        }
    }
}
