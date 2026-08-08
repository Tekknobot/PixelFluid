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
        private const int MenuItemCount = 14;
        // Canvas sorting order is clamped to a signed 16-bit range by Unity.
        // Keep Developer Mode at the highest possible UI order.
        private const int DeveloperCanvasOrder = 32767;
        private const string DeveloperUnlockedKey = "SurferSlug.DeveloperUnlocked";

        private static readonly Color PanelColor = new(0.015f, 0.11f, 0.15f, 0.985f);
        private static readonly Color ButtonColor = new(0.025f, 0.20f, 0.25f, 0.98f);
        private static readonly Color SelectedColor = new(0.04f, 0.40f, 0.46f, 1f);
        private static readonly Color AccentColor = new(0.20f, 0.92f, 0.92f, 1f);
        private static readonly Color MutedColor = new(0.70f, 0.88f, 0.88f, 1f);
        private static readonly Color OnColor = new(0.30f, 1f, 0.66f, 1f);
        private static readonly Color OffColor = new(1f, 0.64f, 0.34f, 1f);
        private static readonly Color SandColor = new(1f, 0.88f, 0.58f, 1f);

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
        private GameObject[] selectionFrames;
        private TMP_Text[] stateBadges;
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
            if (canvas == null)
                return;

            if (shouldShow)
            {
                // Reassert this whenever the menu opens. Runtime title, pause,
                // cutscene, and race canvases may have been created afterward.
                canvas.overrideSorting = true;
                canvas.sortingOrder = DeveloperCanvasOrder;
                canvas.transform.SetAsLastSibling();
            }

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
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                {
                    int selectedDay = selectedIndex - 5;
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugSelectDay(selectedDay);
                    break;
                }
                case 13:
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
            canvas.overrideSorting = true;
            canvas.sortingOrder = DeveloperCanvasOrder;

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
            panelRect.sizeDelta = new Vector2(680f, 1010f);
            panelRoot.GetComponent<Image>().color = PanelColor;

            VerticalLayoutGroup layout = panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 22);
            layout.spacing = 7f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            CreateText("Title", panelRoot.transform, "SURF LAB  //  DEVELOPER TOOLS", 28f, TextAlignmentOptions.Left, SandColor, 42f);
            CreateText("Instructions", panelRoot.transform,
                "F10 TO TOGGLE   •   D-PAD / STICK TO SELECT   •   A TO USE   •   B TO CLOSE",
                13f, TextAlignmentOptions.Left, MutedColor, 24f);

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

            CreateSection("SELECT DAY");
            CreateMenuButton(6);
            CreateMenuButton(7);
            CreateMenuButton(8);
            CreateMenuButton(9);
            CreateMenuButton(10);
            CreateMenuButton(11);
            CreateMenuButton(12);

            CreateSection("CURRENT DAY");
            CreateMenuButton(13);

            GameObject spacer = new("Flexible Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(panelRoot.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleHeight = 1f;

            CreateDivider(panelRoot.transform);
            statusText = CreateText("Session Status", panelRoot.transform, string.Empty, 15f,
                TextAlignmentOptions.Center, MutedColor, 48f);

            noticeText = CreateText("Developer Notice", canvasObject.transform, "SURF LAB ENABLED", 20f,
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
            CreateText(label, panelRoot.transform, label, 14f, TextAlignmentOptions.Left, AccentColor, 24f);
        }

        private void CreateMenuButton(int index)
        {
            buttonLabels ??= new TMP_Text[MenuItemCount];
            buttonBackgrounds ??= new Image[MenuItemCount];
            buttons ??= new Button[MenuItemCount];
            selectionFrames ??= new GameObject[MenuItemCount];
            stateBadges ??= new TMP_Text[MenuItemCount];

            GameObject buttonObject = new($"Developer Button {index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(panelRoot.transform, false);

            LayoutElement element = buttonObject.GetComponent<LayoutElement>();
            element.preferredHeight = 40f;
            element.minHeight = 40f;

            Image background = buttonObject.GetComponent<Image>();
            background.color = ButtonColor;
            buttonBackgrounds[index] = background;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock buttonColors = button.colors;
            buttonColors.normalColor = Color.white;
            buttonColors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            buttonColors.selectedColor = new Color(1f, 1f, 1f, 1f);
            buttonColors.pressedColor = new Color(0.78f, 0.92f, 0.92f, 1f);
            buttonColors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            buttonColors.colorMultiplier = 1f;
            buttonColors.fadeDuration = 0.06f;
            button.colors = buttonColors;
            int capturedIndex = index;
            button.onClick.AddListener(() =>
            {
                selectedIndex = capturedIndex;
                ActivateSelectedItem();
            });
            buttons[index] = button;

            TMP_Text label = CreateText("Label", buttonObject.transform, string.Empty, 15f,
                TextAlignmentOptions.MidlineLeft, Color.white, 40f);
            Stretch(label.rectTransform, Vector2.zero, new Vector2(index < 2 ? 0.76f : 1f, 1f), new Vector2(16f, 0f), new Vector2(index < 2 ? -6f : -16f, 0f));
            buttonLabels[index] = label;

            if (index < 2)
            {
                TMP_Text badge = CreateText("State Badge", buttonObject.transform, "OFF", 13f,
                    TextAlignmentOptions.Center, OffColor, 40f);
                Stretch(badge.rectTransform, new Vector2(0.76f, 0f), Vector2.one,
                    new Vector2(4f, 7f), new Vector2(-12f, -7f));
                badge.fontStyle = FontStyles.Bold;
                stateBadges[index] = badge;
            }

            selectionFrames[index] = CreateSelectionFrame(buttonObject.transform);
        }

        private void RefreshInterface()
        {
            if (buttonLabels == null)
                return;

            string[] labels =
            {
                "GOD MODE",
                "INFINITE LIVES",
                "UNLOCK ALL MECHANICS",
                "MAX FLOW / ON FIRE",
                "ADVANCE TO NEXT CHAPTER",
                "SPAWN CURRENT DAY BOSS",
                "LOAD DAY 1",
                "LOAD DAY 2",
                "LOAD DAY 3",
                "LOAD DAY 4",
                "LOAD DAY 5",
                "LOAD DAY 6",
                "LOAD DAY 7",
                "RESET CURRENT DAY"
            };

            SurfDayProgressionDirector director = FindFirstObjectByType<SurfDayProgressionDirector>();

            for (int i = 0; i < MenuItemCount; i++)
            {
                if (buttonLabels[i] == null)
                    continue;

                bool selected = selectedIndex == i;
                buttonLabels[i].font = developerFont;
                string label = labels[i];
                if (i >= 6 && i <= 12 && director != null && director.CurrentDay == i - 5)
                    label += "  [CURRENT]";
                buttonLabels[i].text = (selected ? "▶  " : "   ") + label;
                buttonLabels[i].color = selected ? Color.white : new Color(0.88f, 0.97f, 0.97f, 1f);
                buttonBackgrounds[i].color = selected ? SelectedColor : ButtonColor;

                if (selectionFrames != null && selectionFrames[i] != null)
                    selectionFrames[i].SetActive(selected);
            }

            RefreshStateBadge(0, godMode);
            RefreshStateBadge(1, infiniteLives);

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

        private void RefreshStateBadge(int index, bool enabledState)
        {
            if (stateBadges == null || index < 0 || index >= stateBadges.Length || stateBadges[index] == null)
                return;

            TMP_Text badge = stateBadges[index];
            badge.font = developerFont;
            badge.text = enabledState ? "●  ON" : "○  OFF";
            badge.color = enabledState ? OnColor : OffColor;
        }

        private static GameObject CreateSelectionFrame(Transform parent)
        {
            GameObject frame = new("Selected White Border", typeof(RectTransform));
            frame.transform.SetParent(parent, false);
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            Stretch(frameRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateFrameEdge("Top", frame.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), Vector2.zero);
            CreateFrameEdge("Bottom", frame.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f));
            CreateFrameEdge("Left", frame.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
            CreateFrameEdge("Right", frame.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-3f, 0f), Vector2.zero);
            frame.SetActive(false);
            return frame;
        }

        private static void CreateFrameEdge(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            Image edge = CreateImage(name, parent, Color.white);
            edge.raycastTarget = false;
            RectTransform rect = edge.rectTransform;
            Stretch(rect, anchorMin, anchorMax, offsetMin, offsetMax);
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
