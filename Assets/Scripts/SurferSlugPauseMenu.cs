using System;
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
    /// <summary>
    /// Minimal in-scene pause panel for Surfer Slug.
    /// Start/Escape pauses gameplay scripts without changing Time.timeScale,
    /// allowing waves, particles, weather, lighting, audio, and other visual
    /// simulations to keep running behind the menu.
    /// </summary>
    [DefaultExecutionOrder(-30000)]
    [DisallowMultipleComponent]
    public sealed class SurferSlugPauseMenu : MonoBehaviour
    {
        public static SurferSlugPauseMenu Instance { get; private set; }
        public static bool GameplayPaused { get; private set; }

        [Header("Input")]
        [SerializeField] private KeyCode keyboardPauseKey = KeyCode.Escape;
        [SerializeField, Min(0.05f)] private float reopenDelay = 0.18f;

        [Header("Visuals")]
        [SerializeField] private string title = "PAUSED";
        [SerializeField] private string resumeLabel = "RESUME";
        [SerializeField] private string controlsLabel = "CONTROLS";
        [SerializeField] private string quitLabel = "QUIT";
        [SerializeField] private Color screenDim = new(0f, 0.015f, 0.03f, 0.34f);
        [SerializeField] private Color panelColor = new(0.018f, 0.065f, 0.09f, 0.94f);
        [SerializeField] private Color normalButtonColor = new(0.035f, 0.14f, 0.18f, 1f);
        [SerializeField] private Color selectedButtonColor = new(0.18f, 0.68f, 0.70f, 1f);
        [SerializeField] private Color textColor = new(0.94f, 0.98f, 0.95f, 1f);

        private readonly List<MonoBehaviour> disabledGameplayBehaviours = new();
        private Canvas canvas;
        private GameObject pauseRoot;
        private GameObject mainPanel;
        private GameObject controlsPanel;
        private Button resumeButton;
        private Button controlsButton;
        private Button controlsBackButton;
        private float inputReadyTime;

        private static readonly HashSet<string> SimulationTypeNames = new(StringComparer.Ordinal)
        {
            nameof(PixelWaterGPU),
            nameof(PixelWaterSimulation),
            nameof(PixelWaterRenderer),
            nameof(EndlessWaveSections),
            nameof(ProceduralWaveAudio),
            nameof(ProceduralStarryNight),
            "ProceduralDayNightSystem",
            nameof(ProceduralRainSystem),
            nameof(ProceduralHorizonFog),
            nameof(TropicalSeabed),
            nameof(SceneFadeIn),
            "InterWaveLaneSystem",
            "InterWaveLane",
            "InterWaveRenderItem",
            "InterWaveWorldItem"
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
            SetMenuVisible(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < inputReadyTime)
                return;

            if (!PausePressed())
                return;

            if (GameplayPaused)
                ResumeGame();
            else
                PauseGame();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            RestoreGameplayBehaviours();
            GameplayPaused = false;
            Instance = null;
        }

        public void PauseGame()
        {
            if (GameplayPaused)
                return;

            GameplayPaused = true;
            DisableGameplayBehaviours();
            ShowMainPanel();
            SetMenuVisible(true);
            inputReadyTime = Time.unscaledTime + reopenDelay;
            Select(resumeButton);
        }

        public void ResumeGame()
        {
            if (!GameplayPaused)
                return;

            RestoreGameplayBehaviours();
            GameplayPaused = false;
            SetMenuVisible(false);
            inputReadyTime = Time.unscaledTime + reopenDelay;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void DisableGameplayBehaviours()
        {
            disabledGameplayBehaviours.Clear();

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.enabled)
                    continue;

                if (behaviour == this || behaviour.transform.IsChildOf(transform))
                    continue;

                Type type = behaviour.GetType();
                if (type.Namespace != typeof(SurferSlugPauseMenu).Namespace)
                    continue;

                if (SimulationTypeNames.Contains(type.Name))
                    continue;

                behaviour.enabled = false;
                disabledGameplayBehaviours.Add(behaviour);
            }
        }

        private void RestoreGameplayBehaviours()
        {
            foreach (MonoBehaviour behaviour in disabledGameplayBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            disabledGameplayBehaviours.Clear();
        }

        private bool PausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboard = Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame;
            bool gamepad = Gamepad.current != null &&
                Gamepad.current.startButton.wasPressedThisFrame;
            return keyboard || gamepad;
#else
            return Input.GetKeyDown(keyboardPauseKey) ||
                   Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
        }

        private void BuildMenu()
        {
            EnsureEventSystem();

            GameObject canvasObject = new("Pause Menu Canvas");
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

            pauseRoot = CreateObject(canvasObject.transform, "Pause Root");
            Stretch(pauseRoot.GetComponent<RectTransform>());
            Image dim = pauseRoot.AddComponent<Image>();
            dim.color = screenDim;

            mainPanel = CreatePanel(pauseRoot.transform, "Main Panel");
            SetRect(mainPanel.GetComponent<RectTransform>(),
                new Vector2(0.38f, 0.22f), new Vector2(0.62f, 0.78f));
            AddVerticalLayout(mainPanel, 16f, new RectOffset(38, 38, 36, 36));

            Text heading = CreateText(mainPanel.transform, title, 44, TextAnchor.MiddleCenter);
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

            resumeButton = CreateButton(mainPanel.transform, resumeLabel, ResumeGame);
            controlsButton = CreateButton(mainPanel.transform, controlsLabel, ShowControlsPanel);
            CreateButton(mainPanel.transform, quitLabel, QuitGame);

            Text hint = CreateText(mainPanel.transform,
                "START / ESC TO RESUME", 18, TextAnchor.MiddleCenter);
            hint.color = new Color(textColor.r, textColor.g, textColor.b, 0.62f);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;

            controlsPanel = CreatePanel(pauseRoot.transform, "Controls Panel");
            SetRect(controlsPanel.GetComponent<RectTransform>(),
                new Vector2(0.27f, 0.15f), new Vector2(0.73f, 0.85f));
            AddVerticalLayout(controlsPanel, 12f, new RectOffset(46, 46, 36, 36));

            Text controlsHeading = CreateText(
                controlsPanel.transform, "CONTROLS", 40, TextAnchor.MiddleCenter);
            controlsHeading.gameObject.AddComponent<LayoutElement>().preferredHeight = 82f;

            Text controls = CreateText(controlsPanel.transform,
                "MOVE                 A / D  •  LEFT STICK\n\n" +
                "CHANGE WAVE          UP / DOWN  •  D-PAD\n\n" +
                "JUMP                 SPACE  •  A\n\n" +
                "ACTION / TRICK       F / X  •  X\n\n" +
                "CAMERA               Z  •  Y\n\n" +
                "PAUSE                ESC  •  START",
                25,
                TextAnchor.MiddleLeft);
            controls.gameObject.AddComponent<LayoutElement>().preferredHeight = 430f;

            controlsBackButton = CreateButton(
                controlsPanel.transform, "BACK", ShowMainPanel);

            ShowMainPanel();
        }

        private void ShowMainPanel()
        {
            mainPanel.SetActive(true);
            controlsPanel.SetActive(false);
            Select(resumeButton);
        }

        private void ShowControlsPanel()
        {
            mainPanel.SetActive(false);
            controlsPanel.SetActive(true);
            Select(controlsBackButton);
        }

        private void SetMenuVisible(bool visible)
        {
            pauseRoot.SetActive(visible);
            Cursor.visible = visible;
            if (visible)
                Cursor.lockState = CursorLockMode.None;
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

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystemObject = new("Pause Menu EventSystem");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private GameObject CreatePanel(Transform parent, string name)
        {
            GameObject panel = CreateObject(parent, name);
            Image image = panel.AddComponent<Image>();
            image.color = panelColor;
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(selectedButtonColor.r,
                selectedButtonColor.g, selectedButtonColor.b, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);
            return panel;
        }

        private GameObject CreateObject(Transform parent, string name)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private Text CreateText(Transform parent, string value, int size, TextAnchor anchor)
        {
            GameObject textObject = CreateObject(parent, value + " Text");
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = textColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateObject(parent, label + " Button");
            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 68f;

            Image image = buttonObject.AddComponent<Image>();
            image.color = normalButtonColor;

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = normalButtonColor;
            colors.highlightedColor = selectedButtonColor;
            colors.selectedColor = selectedButtonColor;
            colors.pressedColor = new Color(0.08f, 0.36f, 0.40f, 1f);
            button.colors = colors;
            button.onClick.AddListener(action);

            Text text = CreateText(buttonObject.transform, label, 27, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        private static void AddVerticalLayout(GameObject target, float spacing, RectOffset padding)
        {
            VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void Select(Selectable selectable)
        {
            if (selectable == null || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private static void Stretch(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.one);
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Automatically creates the pause menu once per scene.</summary>
    internal static class SurferSlugPauseMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateMenu()
        {
            if (UnityEngine.Object.FindFirstObjectByType<SurferSlugPauseMenu>() != null)
                return;

            GameObject menuObject = new("Surfer Slug Pause Menu");
            menuObject.AddComponent<SurferSlugPauseMenu>();
        }
    }
}
