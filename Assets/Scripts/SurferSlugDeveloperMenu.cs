using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    [DefaultExecutionOrder(-11850)]
    [DisallowMultipleComponent]
    public sealed class SurferSlugDeveloperMenu : MonoBehaviour
    {
        private const int MenuItemCount = 9;
        private const string DeveloperUnlockedKey = "SurferSlug.DeveloperUnlocked";

        private static readonly Color PanelColor = new(0f, 0.035f, 0.065f, 0.97f);
        private static readonly Color ButtonColor = new(0.045f, 0.105f, 0.14f, 0.96f);
        private static readonly Color SelectedColor = new(0.08f, 0.29f, 0.37f, 1f);
        private static readonly Color AccentColor = new(0.31f, 0.84f, 0.95f, 1f);
        private static readonly Color MutedColor = new(0.68f, 0.78f, 0.84f, 1f);
        private static readonly Color OnColor = new(0.42f, 0.95f, 0.58f, 1f);
        private static readonly Color OffColor = new(0.72f, 0.76f, 0.79f, 1f);

        private static SurferSlugDeveloperMenu instance;
        public static bool IsOpen => instance != null && instance.visible;
        public static bool IsUnlocked => PlayerPrefs.GetInt(DeveloperUnlockedKey, 0) == 1;

        public static void UnlockAndOpen()
        {
            PlayerPrefs.SetInt(DeveloperUnlockedKey, 1);
            PlayerPrefs.Save();

            if (instance == null)
                return;

            instance.enabled = true;
            instance.unlockNoticeUntil = Time.unscaledTime + 2.25f;
            instance.SetVisible(true);
        }

        public static void Close()
        {
            if (instance != null)
                instance.SetVisible(false);
        }

        private bool visible;
        private bool godMode;
        private bool infiniteLives;
        private int selectedIndex;
        private float nextNavigationTime;
        private float previousTimeScale = 1f;
        private float unlockNoticeUntil;

        private Canvas canvas;
        private GameObject panelRoot;
        private TMP_Text noticeText;
        private TMP_Text statusText;
        private TMP_Text[] buttonLabels;
        private Image[] buttonBackgrounds;
        private Button[] buttons;
        private TMP_FontAsset developerFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurferSlugDeveloperMenu>() != null)
                return;

            GameObject host = new("Surfer Slug Developer Menu");
            DontDestroyOnLoad(host);
            host.AddComponent<SurferSlugDeveloperMenu>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildInterface();
            SetCanvasVisible(false);
        }

        private void OnDestroy()
        {
            if (visible)
                Time.timeScale = previousTimeScale;

            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (IsUnlocked && TogglePressed())
                SetVisible(!visible);

            if (visible)
            {
                UpdateControllerNavigation();
                RefreshInterface();
            }

            if (infiniteLives && SurfRunLifeManager.Instance != null)
                SurfRunLifeManager.Instance.RestoreLives(SurfRunLifeManager.Instance.StartingLives);

            if (godMode)
            {
                foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
                {
                    if (surfer != null && surfer.IsPlayerControlled)
                        surfer.HealFromHeart(99);
                }
            }
        }

        private void SetVisible(bool shouldShow)
        {
            if (visible == shouldShow)
                return;

            visible = shouldShow;
            SetCanvasVisible(visible);

            if (visible)
            {
                selectedIndex = 0;
                nextNavigationTime = Time.unscaledTime + 0.15f;
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                SurferSlugPauseMenu.Instance?.SetDeveloperOverlayPresentation(true);
                RefreshInterface();
            }
            else
            {
                Time.timeScale = previousTimeScale;
                SurferSlugPauseMenu.Instance?.SetDeveloperOverlayPresentation(false);
            }
        }

        private void SetCanvasVisible(bool shouldShow)
        {
            if (canvas != null)
                canvas.enabled = shouldShow;
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F10);
#else
            return false;
#endif
        }

        private void UpdateControllerNavigation()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return;

            float vertical = gamepad.leftStick.y.ReadValue();
            bool upPressed = gamepad.dpad.up.wasPressedThisFrame;
            bool downPressed = gamepad.dpad.down.wasPressedThisFrame;

            if (Time.unscaledTime >= nextNavigationTime)
            {
                if (upPressed || vertical > 0.55f)
                {
                    selectedIndex = (selectedIndex - 1 + MenuItemCount) % MenuItemCount;
                    nextNavigationTime = Time.unscaledTime + 0.18f;
                }
                else if (downPressed || vertical < -0.55f)
                {
                    selectedIndex = (selectedIndex + 1) % MenuItemCount;
                    nextNavigationTime = Time.unscaledTime + 0.18f;
                }
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
                ActivateSelectedItem();

            if (gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)
                SetVisible(false);
#endif
        }

        private void ActivateSelectedItem()
        {
            switch (selectedIndex)
            {
                case 0:
                    godMode = !godMode;
                    break;
                case 1:
                    infiniteLives = !infiniteLives;
                    break;
                case 2:
                    SurfAbilityProgression.Instance?.DebugUnlockAll();
                    BoomboxSurferSpawner.UnlockSummoning();
                    SurferSlugMinimalHud.ShowNotice("ALL MECHANICS UNLOCKED\nMUSIC BOARD: LB / M TO TOGGLE", 4.5f);
                    unlockNoticeUntil = Time.unscaledTime + 2.25f;
                    break;
                case 3:
                    AirTrickScoreSystem.Instance?.DebugMaxFlow();
                    break;
                case 4:
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugNextChapter();
                    break;
                case 5:
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugSpawnBoss();
                    break;
                case 6:
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugNextDay();
                    break;
                case 7:
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugResetCurrentDay();
                    break;
            }

            RefreshInterface();
        }

        private void BuildInterface()
        {
            developerFont = PixelFontLibrary.TmpSemiBold;
            if (developerFont == null)
                developerFont = TMP_Settings.defaultFontAsset;

            GameObject canvasObject = new("Developer Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32700;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image dimmer = CreateImage("Dimmer", canvasObject.transform, new Color(0f, 0f, 0f, 0.46f));
            Stretch(dimmer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            panelRoot = new GameObject("Developer Panel", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 900f);
            panelRoot.GetComponent<Image>().color = PanelColor;

            VerticalLayoutGroup layout = panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 24, 24);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            CreateText("Title", panelRoot.transform, "DEVELOPER TOOLS", 34f, TextAlignmentOptions.Left, Color.white, 48f);
            CreateText("Instructions", panelRoot.transform,
                "F10 TO TOGGLE   •   D-PAD / STICK TO SELECT   •   A TO USE   •   B TO CLOSE",
                15f, TextAlignmentOptions.Left, MutedColor, 28f);

            CreateDivider(panelRoot.transform);
            CreateSection("PLAYER STATUS");
            CreateMenuButton(0);
            CreateMenuButton(1);

            CreateSection("ABILITIES");
            CreateMenuButton(2);
            CreateMenuButton(3);

            CreateSection("PROGRESSION");
            CreateMenuButton(4);
            CreateMenuButton(5);
            CreateMenuButton(6);
            CreateMenuButton(7);

            GameObject spacer = new("Flexible Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(panelRoot.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleHeight = 1f;

            CreateDivider(panelRoot.transform);
            statusText = CreateText("Session Status", panelRoot.transform, string.Empty, 17f,
                TextAlignmentOptions.Center, Color.white, 54f);

            noticeText = CreateText("Developer Notice", canvasObject.transform, "DEVELOPER MODE ENABLED", 24f,
                TextAlignmentOptions.Center, Color.white, 50f);
            RectTransform noticeRect = noticeText.rectTransform;
            noticeRect.anchorMin = new Vector2(0.5f, 0f);
            noticeRect.anchorMax = new Vector2(0.5f, 0f);
            noticeRect.pivot = new Vector2(0.5f, 0f);
            noticeRect.anchoredPosition = new Vector2(0f, 34f);
            noticeRect.sizeDelta = new Vector2(720f, 50f);
        }

        private void CreateSection(string label)
        {
            CreateText(label, panelRoot.transform, label, 16f, TextAlignmentOptions.Left, AccentColor, 28f);
        }

        private void CreateMenuButton(int index)
        {
            buttonLabels ??= new TMP_Text[MenuItemCount];
            buttonBackgrounds ??= new Image[MenuItemCount];
            buttons ??= new Button[MenuItemCount];

            GameObject buttonObject = new($"Developer Button {index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(panelRoot.transform, false);

            LayoutElement element = buttonObject.GetComponent<LayoutElement>();
            element.preferredHeight = 52f;
            element.minHeight = 52f;

            Image background = buttonObject.GetComponent<Image>();
            background.color = ButtonColor;
            buttonBackgrounds[index] = background;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            int capturedIndex = index;
            button.onClick.AddListener(() =>
            {
                selectedIndex = capturedIndex;
                ActivateSelectedItem();
            });
            buttons[index] = button;

            TMP_Text label = CreateText("Label", buttonObject.transform, string.Empty, 18f,
                TextAlignmentOptions.MidlineLeft, Color.white, 52f);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            buttonLabels[index] = label;
        }

        private void RefreshInterface()
        {
            if (buttonLabels == null)
                return;

            string[] labels =
            {
                $"GOD MODE                                      {(godMode ? "ON" : "OFF")}",
                $"INFINITE LIVES                                {(infiniteLives ? "ON" : "OFF")}",
                "UNLOCK ALL MECHANICS",
                "MAX FLOW / ON FIRE",
                "ADVANCE TO NEXT CHAPTER",
                "SPAWN CURRENT DAY BOSS",
                "ADVANCE TO NEXT DAY",
                "RESET CURRENT DAY"
            };

            for (int i = 0; i < MenuItemCount; i++)
            {
                if (buttonLabels[i] == null)
                    continue;

                buttonLabels[i].font = developerFont;
                buttonLabels[i].text = (selectedIndex == i ? "▶  " : "    ") + labels[i];
                buttonLabels[i].color = selectedIndex == i ? Color.white : new Color(0.9f, 0.94f, 0.96f, 1f);
                buttonBackgrounds[i].color = selectedIndex == i ? SelectedColor : ButtonColor;
            }

            if (buttonLabels[0] != null)
                buttonLabels[0].color = godMode ? OnColor : (selectedIndex == 0 ? Color.white : OffColor);
            if (buttonLabels[1] != null)
                buttonLabels[1].color = infiniteLives ? OnColor : (selectedIndex == 1 ? Color.white : OffColor);

            SurfDayProgressionDirector director = FindFirstObjectByType<SurfDayProgressionDirector>();
            if (statusText != null)
            {
                statusText.font = developerFont;
                statusText.text = director == null
                    ? "NO ACTIVE SURF SESSION"
                    : $"DAY {director.CurrentDay}   •   {director.CurrentChapter}\nTIME {director.RunTime:0.0}s   •   DISTANCE {director.DistanceTravelled:0}/{director.DayDistance:0}m";
            }

            if (noticeText != null)
            {
                noticeText.font = developerFont;
                noticeText.gameObject.SetActive(Time.unscaledTime < unlockNoticeUntil);
            }
        }

        private TMP_Text CreateText(string name, Transform parent, string value, float size,
            TextAlignmentOptions alignment, Color color, float preferredHeight)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = developerFont;
            text.fontSize = size;
            text.fontStyle = FontStyles.Normal;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;

            LayoutElement element = textObject.GetComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void CreateDivider(Transform parent)
        {
            Image divider = CreateImage("Divider", parent, new Color(0.16f, 0.34f, 0.41f, 1f));
            LayoutElement element = divider.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 2f;
            element.minHeight = 2f;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
