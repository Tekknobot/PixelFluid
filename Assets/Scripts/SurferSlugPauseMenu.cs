using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        private GameObject controlsPanel;
        private GameObject settingsPanel;
        private Button playButton;
        private Button continueButton;
        private Button controlsButton;
        private Button settingsButton;
        private Button quitButton;
        private Image logoImage;
        private Image startupBlackImage;
        private CanvasGroup startupBlackGroup;
        private Sprite[] logoFrames;
        private Coroutine motionRoutine;
        private Coroutine logoRoutine;
        private bool firstMenu = true;
        private bool menuVisible;
        private float inputReadyTime;

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
            if (continueButton != null) continueButton.gameObject.SetActive(isMainMenu);
            menuRoot.SetActive(true);
            controlsPanel.SetActive(false);
            settingsPanel.SetActive(false);
            logoPanel.gameObject.SetActive(true);
            buttonPanel.gameObject.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (motionRoutine != null)
                StopCoroutine(motionRoutine);
            motionRoutine = StartCoroutine(ShowAnimated());
            RefreshContinueButton();
            inputReadyTime = Time.unscaledTime + motionDuration + 0.12f;
        }

        private IEnumerator ShowAnimated()
        {
            Vector2 logoTarget = new(-390f, 0f);
            Vector2 buttonsTarget = new(560f, 0f);
            Vector2 logoStart = new(-1450f, 0f);
            Vector2 buttonsStart = new(1500f, 0f);

            logoPanel.anchoredPosition = logoStart;
            buttonPanel.anchoredPosition = buttonsStart;

            float elapsed = 0f;
            while (elapsed < motionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutBack(Mathf.Clamp01(elapsed / motionDuration));
                logoPanel.anchoredPosition = Vector2.LerpUnclamped(logoStart, logoTarget, t);
                buttonPanel.anchoredPosition = Vector2.LerpUnclamped(buttonsStart, buttonsTarget, t);
                yield return null;
            }

            logoPanel.anchoredPosition = logoTarget;
            buttonPanel.anchoredPosition = buttonsTarget;
            RefreshContinueButton();
            Select(continueButton != null && continueButton.gameObject.activeInHierarchy && continueButton.interactable ? continueButton : playButton);
        }

        private IEnumerator HideAnimated()
        {
            Vector2 logoStart = logoPanel.anchoredPosition;
            Vector2 buttonStart = buttonPanel.anchoredPosition;
            Vector2 logoEnd = new(-1450f, 0f);
            Vector2 buttonEnd = new(1500f, 0f);

            float elapsed = 0f;
            while (elapsed < motionDuration * 0.72f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseInCubic(Mathf.Clamp01(elapsed / (motionDuration * 0.72f)));
                logoPanel.anchoredPosition = Vector2.LerpUnclamped(logoStart, logoEnd, t);
                buttonPanel.anchoredPosition = Vector2.LerpUnclamped(buttonStart, buttonEnd, t);
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

            menuRoot = CreateUIObject(canvasObject.transform, "Menu Root");
            Stretch(menuRoot.GetComponent<RectTransform>());
            Image dim = menuRoot.AddComponent<Image>();
            dim.color = screenDim;

            BuildLogo(menuRoot.transform);
            BuildButtons(menuRoot.transform);
            BuildControls(menuRoot.transform);
            BuildSettings(menuRoot.transform);
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
            buttonPanel.sizeDelta = new Vector2(430f, 590f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 26f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            playButton = CreateSpriteButton(panel.transform, "play_button", PlayPressed);
            continueButton = CreateSpriteButton(panel.transform, "continue_button", ContinuePressed);
            controlsButton = CreateSpriteButton(panel.transform, "controls_button", ShowControls);
            settingsButton = CreateSpriteButton(panel.transform, "settings_button", ShowSettings);
            quitButton = CreateSpriteButton(panel.transform, "quit_button", QuitGame);

            settingsButton.gameObject.SetActive(false);
        }

        private Button CreateSpriteButton(Transform parent, string resourceName, UnityEngine.Events.UnityAction action)
        {
            GameObject go = CreateUIObject(parent, resourceName);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 112f;
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
            settingsPanel = CreateSubPanel(parent, "Settings Panel");
            AddText(settingsPanel.transform, "SETTINGS", 44, 90f);
            CreateVolumeRow(settingsPanel.transform, "MASTER VOLUME");
            CreateToggleRow(settingsPanel.transform, "FULLSCREEN", Screen.fullScreen,
                value => Screen.fullScreen = value);
            CreateToggleRow(settingsPanel.transform, "DIALOGUE BUBBLES", true, _ => { });
            Button back = CreateSpriteButton(settingsPanel.transform, "back_button", ShowMainLayout);
            settingsPanel.SetActive(false);
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
            controlsPanel.SetActive(true);
            Select(controlsPanel.GetComponentInChildren<Button>());
        }

        private void ShowSettings()
        {
            logoPanel.gameObject.SetActive(false);
            buttonPanel.gameObject.SetActive(false);
            controlsPanel.SetActive(false);
            settingsPanel.SetActive(true);
            Select(settingsPanel.GetComponentInChildren<Selectable>());
        }

        private void ShowMainLayout()
        {
            controlsPanel.SetActive(false);
            settingsPanel.SetActive(false);
            logoPanel.gameObject.SetActive(true);
            buttonPanel.gameObject.SetActive(true);
            RefreshContinueButton();
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
            float duration = motionDuration * 0.72f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseInCubic(Mathf.Clamp01(elapsed / duration));
                logoPanel.anchoredPosition = Vector2.LerpUnclamped(logoStart, logoEnd, t);
                buttonPanel.anchoredPosition = Vector2.LerpUnclamped(buttonStart, buttonEnd, t);
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
            startupBlackGroup.alpha = from;
            startupBlackGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                startupBlackGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            startupBlackGroup.alpha = to;
            startupBlackGroup.blocksRaycasts = to > 0.001f;
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
