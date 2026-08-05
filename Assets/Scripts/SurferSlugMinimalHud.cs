using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelOcean
{
    /// <summary>
    /// Runtime-built pixel HUD: objective/lives, day clock, and throwable inventory.
    /// Everything is kept in one Canvas so no gameplay UI overlaps.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class SurferSlugMinimalHud : MonoBehaviour
    {
        public static SurferSlugMinimalHud Instance { get; private set; }

        private static string queuedNotice = string.Empty;
        private static float queuedNoticeDuration;

        public static void ShowNotice(string message, float duration = 4f)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (Instance != null)
            {
                Instance.noticeText = message;
                Instance.noticeUntil =
                    Time.unscaledTime + Mathf.Max(0.1f, duration);
                return;
            }

            queuedNotice = message;
            queuedNoticeDuration = Mathf.Max(0.1f, duration);
        }
        [Header("Layout")]
        [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
        [SerializeField] private Vector2 safeMargin = new(30f, 22f);
        [SerializeField, Min(28f)] private float inventoryIconSize = 46f;
        [SerializeField, Min(1f)] private float inventorySpacing = 10f;

        [Header("Pixel HUD Appearance")]
        [SerializeField] private Color panelColour = new(0.10f, 0.055f, 0.16f, 0.74f);
        [SerializeField] private Color insetColour = new(0.035f, 0.022f, 0.070f, 0.72f);
        [SerializeField] private Color borderColour = new(1f, 1f, 1f, 0.92f);
        [SerializeField, Range(1f, 4f)] private float borderThickness = 2f;
        [SerializeField] private Color trackColour = new(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color foregroundColour = new(1f, 1f, 1f, 0.98f);
        [SerializeField] private Color mutedColour = new(1f, 1f, 1f, 0.72f);

        private TinyWaveSurfer player;
        private ProceduralStarryNight dayNight;
        private SurfDayProgressionDirector progression;
        private SurfRunLifeManager lifeManager;

        private RectTransform inventoryRow;
        private RectTransform dayFill;
        private RectTransform flowFill;
        private Image flowFillImage;

        private TMP_Text dayPhaseLabel;
        private TMP_Text timeLabel;
        private TMP_Text objectiveLabel;
        private TMP_Text livesLabel;
        private TMP_Text stokeLabel;
        private TMP_Text flowLabel;
        private GameObject flowTrackObject;
        private TMP_Text chapterLabel;
        private TMP_Text inventoryOverflowLabel;
        private CanvasGroup chapterGroup;
        private TMP_FontAsset font;
        private CanvasGroup hudGroup;
        private string noticeText = string.Empty;
        private float noticeUntil;
        private string inventoryFingerprint = string.Empty;
        private readonly List<GameObject> inventorySlots = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            font = PixelFontLibrary.TmpMedium;

            if (font == null)
            {
                Debug.LogError(
                    "Surfer Slug HUD could not load Pixelify Sans Medium TMP font. " +
                    "Make sure the font asset is inside Assets/Resources/Fonts."
                );
            }

            BuildHud();
            SetHudVisible(false, true);

            if (!string.IsNullOrEmpty(queuedNotice))
            {
                noticeText = queuedNotice;
                noticeUntil =
                    Time.unscaledTime +
                    Mathf.Max(0.1f, queuedNoticeDuration);
                queuedNotice = string.Empty;
                queuedNoticeDuration = 0f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (GameModeSession.IsRace || !GameModeSession.HasChosenMode)
            {
                SetStoryHudActive(false);
                return;
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled) canvas.enabled = true;
            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null && !raycaster.enabled) raycaster.enabled = true;
            if (player == null || !player.IsPlayerControlled)
                player = FindPlayer();
            if (dayNight == null)
                dayNight = FindFirstObjectByType<ProceduralStarryNight>();
            if (progression == null)
                progression = FindFirstObjectByType<SurfDayProgressionDirector>();
            if (lifeManager == null)
                lifeManager = SurfRunLifeManager.Instance != null
                    ? SurfRunLifeManager.Instance
                    : FindFirstObjectByType<SurfRunLifeManager>();

            bool shouldShow = player != null
                && player.isActiveAndEnabled
                && player.gameObject.activeInHierarchy
                && !player.IsDead;

            SetHudVisible(shouldShow);
            if (!shouldShow)
                return;

            RefreshProgressionAndLives();
            RefreshStoke();
            RefreshFlow();
            RefreshDayDisplay();
            RefreshInventory();
        }

        public void SetStoryHudActive(bool active)
        {
            if (hudGroup == null)
                BuildHud();

            SetHudVisible(active && GameModeSession.IsStory, true);

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
                canvas.enabled = active && GameModeSession.IsStory;

            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = active && GameModeSession.IsStory;

            if (!active)
                player = null;
        }

        private void SetHudVisible(bool visible, bool immediate = false)
        {
            if (hudGroup == null)
                return;

            float target = visible ? 1f : 0f;
            hudGroup.alpha = immediate
                ? target
                : Mathf.MoveTowards(hudGroup.alpha, target, Time.unscaledDeltaTime * 10f);
            hudGroup.interactable = visible;
            hudGroup.blocksRaycasts = visible;

            if (!visible && inventorySlots.Count > 0)
            {
                foreach (GameObject slot in inventorySlots)
                    if (slot != null) Destroy(slot);
                inventorySlots.Clear();
                inventoryFingerprint = string.Empty;
                if (inventoryOverflowLabel != null) inventoryOverflowLabel.text = string.Empty;
            }
        }

        private TinyWaveSurfer FindPlayer()
        {
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
                if (surfer != null && surfer.IsPlayerControlled)
                    return surfer;
            return null;
        }

        private void BuildHud()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            canvas.pixelPerfect = true;

            hudGroup = GetComponent<CanvasGroup>();
            if (hudGroup == null)
                hudGroup = gameObject.AddComponent<CanvasGroup>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // Match width instead of blending width and height scales.
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;

            BuildTopPanels(transform);
            BuildChapterBanner(transform);
        }

        private void BuildTopPanels(Transform parent)
        {
            RectTransform root = CreateRect("HUD Panels", parent, Vector2.zero);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.offsetMin = new Vector2(safeMargin.x, -174f);
            root.offsetMax = new Vector2(-safeMargin.x, -safeMargin.y);

            HorizontalLayoutGroup row = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 16f;
            row.padding = new RectOffset(0, 0, 0, 0);
            row.childAlignment = TextAnchor.UpperCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            RectTransform objectivePanel = CreatePanel("Objective Panel", root, 470f, 1f);
            RectTransform dayPanel = CreatePanel("Day Panel", root, 0f, 1.8f);
            RectTransform inventoryPanel = CreatePanel("Items Panel", root, 430f, 1f);

            BuildObjectivePanel(objectivePanel);
            BuildDayPanel(dayPanel);
            BuildInventoryPanel(inventoryPanel);
        }

        private RectTransform CreatePanel(string name, Transform parent, float preferredWidth, float flexibleWidth)
        {
            RectTransform panel = CreateRect(name, parent, Vector2.zero);
            AddImage(panel.gameObject, panelColour);
            AddPixelBorder(panel, borderColour, borderThickness);
            LayoutElement layout = panel.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = flexibleWidth;
            layout.minWidth = preferredWidth > 0f ? preferredWidth * 0.72f : 520f;
            layout.preferredHeight = 152f;
            return panel;
        }

        private void BuildObjectivePanel(RectTransform panel)
        {
            TMP_Text heading = CreateText("OBJECTIVE + CURRENT LESSON", panel, 16, TextAnchor.UpperLeft, mutedColour, true);
            Stretch(heading.rectTransform, new Vector2(0f, 0.76f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -10f));

            objectiveLabel = CreateText("Surf. Stay alive.\nLEARN  •  Move Left/Right.", panel, 32, TextAnchor.MiddleLeft, foregroundColour);
            objectiveLabel.enableAutoSizing = true;
            objectiveLabel.fontSizeMin = 10f;
            objectiveLabel.fontSizeMax = 16f;
            objectiveLabel.enableWordWrapping = true;
            Stretch(objectiveLabel.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.78f), new Vector2(18f, 0f), new Vector2(-18f, 0f));

            RectTransform livesInset = CreateRect("Lives Inset", panel, Vector2.zero);
            livesInset.anchorMin = new Vector2(0f, 0.16f);
            livesInset.anchorMax = new Vector2(0.58f, 0.38f);
            livesInset.offsetMin = new Vector2(0f, 0f);
            livesInset.offsetMax = Vector2.zero;
            AddImage(livesInset.gameObject, insetColour);
            AddPixelBorder(livesInset, borderColour, borderThickness);

            livesLabel = CreateText("LIVES  3/3", livesInset, 32, TextAnchor.MiddleCenter, foregroundColour);
            Stretch(livesLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));

            RectTransform stokeInset = CreateRect("Stoke Inset", panel, Vector2.zero);
            stokeInset.anchorMin = new Vector2(0.60f, 0.16f);
            stokeInset.anchorMax = new Vector2(1f, 0.38f);
            stokeInset.offsetMin = Vector2.zero;
            stokeInset.offsetMax = Vector2.zero;
            AddImage(stokeInset.gameObject, insetColour);
            AddPixelBorder(stokeInset, borderColour, borderThickness);

            stokeLabel = CreateText("STOKE  0", stokeInset, 32, TextAnchor.MiddleCenter, foregroundColour);
            stokeLabel.enableAutoSizing = true;
            stokeLabel.fontSizeMin = 14f;
            stokeLabel.fontSizeMax = 22f;
            Stretch(stokeLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));

            RectTransform flowTrack = CreateRect("Flow Track", panel, Vector2.zero);
            flowTrackObject = flowTrack.gameObject;
            flowTrack.anchorMin = new Vector2(0f, 0f);
            flowTrack.anchorMax = new Vector2(1f, 0.14f);
            flowTrack.offsetMin = Vector2.zero;
            flowTrack.offsetMax = Vector2.zero;
            AddImage(flowTrack.gameObject, insetColour);
            AddPixelBorder(flowTrack, borderColour, borderThickness);

            flowFill = CreateRect("Flow Fill", flowTrack, Vector2.zero);
            flowFill.anchorMin = Vector2.zero;
            flowFill.anchorMax = new Vector2(0f, 1f);
            flowFill.pivot = new Vector2(0f, 0.5f);
            flowFill.offsetMin = new Vector2(3f, 3f);
            flowFill.offsetMax = new Vector2(-3f, -3f);
            flowFillImage = AddImage(flowFill.gameObject, new Color(1f, 0.55f, 0.08f, 0.95f));

            flowLabel = CreateText("FLOW  0%", flowTrack, 16, TextAnchor.MiddleCenter, foregroundColour);
            Stretch(flowLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        }

        private void BuildDayPanel(RectTransform panel)
        {
            dayPhaseLabel = CreateText("DAY 1  •  DAWN", panel, 32, TextAnchor.UpperCenter, foregroundColour);
            Stretch(dayPhaseLabel.rectTransform,
                new Vector2(0f, 0.58f),
                new Vector2(0.65f, 1f),
                new Vector2(18f, 0f),
                new Vector2(0f, -10f));
                
            timeLabel = CreateText("7:03 AM", panel, 16, TextAnchor.UpperCenter, foregroundColour);
            Stretch(timeLabel.rectTransform,
                new Vector2(0.65f, 0.58f),
                Vector2.one,
                Vector2.zero,
                new Vector2(-16f, -10f));

            RectTransform track = CreateRect("Day Track", panel, Vector2.zero);
            track.anchorMin = new Vector2(0f, 0.37f);
            track.anchorMax = new Vector2(1f, 0.46f);
            track.offsetMin = new Vector2(28f, 0f);
            track.offsetMax = new Vector2(-28f, 0f);
            AddImage(track.gameObject, trackColour);

            dayFill = CreateRect("Day Fill", track, Vector2.zero);
            dayFill.anchorMin = Vector2.zero;
            dayFill.anchorMax = new Vector2(0f, 1f);
            dayFill.pivot = new Vector2(0f, 0.5f);
            dayFill.offsetMin = Vector2.zero;
            dayFill.offsetMax = Vector2.zero;
            AddImage(dayFill.gameObject, foregroundColour);

            CreateRulerMark(panel, 0.00f, true);
            CreateRulerMark(panel, 0.125f, false);
            CreateRulerMark(panel, 0.25f, true);
            CreateRulerMark(panel, 0.375f, false);
            CreateRulerMark(panel, 0.50f, true);
            CreateRulerMark(panel, 0.625f, false);
            CreateRulerMark(panel, 0.75f, true);
            CreateRulerMark(panel, 0.875f, false);
            CreateRulerMark(panel, 1.00f, true);
        }

        private void CreateRulerMark(RectTransform panel, float x, bool major)
        {
            RectTransform mark = CreateRect(
                major ? "Major Mark" : "Minor Mark",
                panel,
                Vector2.zero);

            mark.anchorMin = mark.anchorMax = new Vector2(x, 0f);
            mark.pivot = new Vector2(0.5f, 0f);

            float height = major ? 12f : 6f;
            float width = 2f;

            mark.sizeDelta = new Vector2(width, height);
            mark.anchoredPosition = new Vector2(0f, 16f);

            AddImage(mark.gameObject, mutedColour);
        }

        private void CreateTimeMark(string value, RectTransform panel, float x, TextAnchor alignment)
        {
            TMP_Text mark = CreateText(value, panel, 16, alignment, mutedColour);
            RectTransform rect = mark.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(x, 0f);
            rect.pivot = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(58f, 32f);
            float edge = x <= 0f ? 26f : x >= 1f ? -26f : 0f;
            rect.anchoredPosition = new Vector2(edge, 8f);
        }

        private void BuildInventoryPanel(RectTransform panel)
        {
            TMP_Text heading = CreateText("ITEMS  •  NEXT TO THROW", panel, 16, TextAnchor.UpperLeft, mutedColour, true);
            Stretch(heading.rectTransform, new Vector2(0f, 0.68f), Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, -12f));

            inventoryRow = CreateRect("Item Row", panel, Vector2.zero);
            inventoryRow.anchorMin = new Vector2(0f, 0f);
            inventoryRow.anchorMax = new Vector2(1f, 0.70f);
            inventoryRow.offsetMin = new Vector2(14f, 10f);
            inventoryRow.offsetMax = new Vector2(-44f, 0f);

            HorizontalLayoutGroup layout = inventoryRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = inventorySpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            inventoryOverflowLabel = CreateText(string.Empty, panel, 16, TextAnchor.MiddleRight, mutedColour);
            Stretch(inventoryOverflowLabel.rectTransform, new Vector2(0.84f, 0f), new Vector2(1f, 0.70f), Vector2.zero, new Vector2(-12f, 0f));
        }

        private void BuildChapterBanner(Transform parent)
        {
            RectTransform banner = CreateRect("Chapter Banner", parent, new Vector2(650f, 74f));
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
            banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2(0f, -(safeMargin.y + 170f));
            AddImage(banner.gameObject, panelColour);
            AddPixelBorder(banner, borderColour, borderThickness);
            chapterGroup = banner.gameObject.AddComponent<CanvasGroup>();
            chapterGroup.alpha = 0f;

            chapterLabel = CreateText(string.Empty, banner, 32, TextAnchor.MiddleCenter, foregroundColour);
            chapterLabel.enableAutoSizing = true;
            chapterLabel.fontSizeMin = 12f;
            chapterLabel.fontSizeMax = 20f;
            Stretch(chapterLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 8f), new Vector2(-18f, -8f));
        }

        private void RefreshProgressionAndLives()
        {
            if (objectiveLabel != null)
                objectiveLabel.text = progression != null && !string.IsNullOrEmpty(progression.CurrentObjective)
                    ? progression.CurrentObjective
                    : "Surf. Stay alive.";

            if (livesLabel != null)
            {
                int remaining = lifeManager != null ? lifeManager.LivesRemaining : 3;
                int maximum = lifeManager != null ? lifeManager.StartingLives : 3;
                livesLabel.text = $"LIVES  {remaining}/{maximum}";
            }

            bool showNotice =
                Time.unscaledTime < noticeUntil &&
                !string.IsNullOrEmpty(noticeText);

            bool showProgressionBanner =
                progression != null &&
                progression.IsBannerVisible;

            bool showBanner =
                showNotice ||
                showProgressionBanner;

            if (chapterGroup != null)
            {
                chapterGroup.alpha = Mathf.MoveTowards(
                    chapterGroup.alpha,
                    showBanner ? 1f : 0f,
                    Time.unscaledDeltaTime * 5f);
            }

            if (chapterLabel != null && showBanner)
            {
                chapterLabel.text = showNotice
                    ? noticeText
                    : progression.CurrentBanner;
            }

            if (!showNotice &&
                Time.unscaledTime >= noticeUntil &&
                !string.IsNullOrEmpty(noticeText))
            {
                noticeText = string.Empty;
            }
        }

        private void RefreshStoke()
        {
            if (stokeLabel == null)
                return;

            int stoke = AirTrickScoreSystem.Instance != null
                ? AirTrickScoreSystem.Instance.TotalStoke
                : 0;
            stokeLabel.text = "STOKE  " + stoke.ToString("N0");
        }

        private void RefreshFlow()
        {
            bool unlocked = SurfAbilityProgression.Instance == null || SurfAbilityProgression.Instance.Has(SurfAbility.Flow);
            if (flowTrackObject != null) flowTrackObject.SetActive(unlocked);
            if (!unlocked) return;
            AirTrickScoreSystem scoring = AirTrickScoreSystem.Instance;
            float flow01 = scoring != null ? scoring.Flow01 : 0f;
            bool onFire = scoring != null && scoring.IsOnFire;

            if (flowFill != null)
                flowFill.anchorMax = new Vector2(onFire ? 1f : flow01, 1f);

            if (flowFillImage != null)
            {
                float green = onFire
                    ? 0.72f + Mathf.Sin(Time.unscaledTime * 12f) * 0.18f
                    : 0.55f;
                flowFillImage.color = new Color(1f, green, 0.08f, onFire ? 1f : 0.95f);
            }

            if (flowLabel != null)
            {
                flowLabel.text = onFire
                    ? "ON FIRE  " + Mathf.CeilToInt(scoring.OnFireTimeRemaining)
                    : "FLOW  " + Mathf.RoundToInt(flow01 * 100f) + "%";
                flowLabel.fontSize = onFire ? 18f : 16f;
            }
        }

        private void RefreshDayDisplay()
        {
            float visualTime = dayNight != null ? Mathf.Repeat(dayNight.TimeOfDay, 1f) : 0f;
            float progress = progression != null ? progression.NormalizedDayProgress : visualTime;
            if (dayFill != null) dayFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

            int totalMinutes = Mathf.FloorToInt(visualTime * 24f * 60f) % (24 * 60);
            int hour24 = totalMinutes / 60;
            int minute = totalMinutes % 60;
            int hour12 = hour24 % 12;
            if (hour12 == 0) hour12 = 12;

            string phase = GetDayPhase(visualTime);
            int currentDay = progression != null ? Mathf.Max(1, progression.CurrentDay) : 1;
            if (dayPhaseLabel != null) dayPhaseLabel.text = $"DAY {currentDay}  •  {phase}";
            if (timeLabel != null)
            {
                string clock = $"{hour12}:{minute:00} {(hour24 < 12 ? "AM" : "PM")}";
                timeLabel.text = progression != null
                    ? $"{clock}  •  {Mathf.RoundToInt(progression.DistanceTravelled)} / {Mathf.RoundToInt(progression.DayDistance)} m"
                    : clock;
            }
        }

        private static string GetDayPhase(float time)
        {
            if (time < 0.20f || time >= 0.82f) return "NIGHT";
            if (time < 0.31f) return "DAWN";
            if (time < 0.68f) return "DAY";
            return time < 0.82f ? "DUSK" : "NIGHT";
        }

        private void RefreshInventory()
        {
            Sprite[] sprites = player != null ? player.GetThrowableInventorySnapshot() : System.Array.Empty<Sprite>();
            string fingerprint = BuildFingerprint(sprites);
            if (fingerprint == inventoryFingerprint) return;
            inventoryFingerprint = fingerprint;

            foreach (GameObject slot in inventorySlots)
                if (slot != null) Destroy(slot);
            inventorySlots.Clear();

            // The gameplay queue throws the oldest pickup first. Show at most the
            // next four distinct item types, preserving that exact throw order.
            Dictionary<Sprite, int> counts = new();
            List<Sprite> order = new();
            foreach (Sprite sprite in sprites)
            {
                if (sprite == null) continue;
                if (!counts.ContainsKey(sprite))
                {
                    counts.Add(sprite, 0);
                    order.Add(sprite);
                }
                counts[sprite]++;
            }

            int visibleTypes = Mathf.Min(5, order.Count);
            for (int i = 0; i < visibleTypes; i++)
                CreateInventorySlot(order[i], counts[order[i]], i == 0);

            if (inventoryOverflowLabel != null)
            {
                int hiddenTypes = Mathf.Max(0, order.Count - visibleTypes);
                inventoryOverflowLabel.text = hiddenTypes > 0 ? $"+{hiddenTypes}" : string.Empty;
            }
        }

        private static string BuildFingerprint(Sprite[] sprites)
        {
            StringBuilder builder = new();
            foreach (Sprite sprite in sprites)
                builder.Append(sprite != null ? sprite.name : "null").Append('|');
            return builder.ToString();
        }

        private void CreateInventorySlot(Sprite sprite, int count, bool isNext)
        {
            GameObject slot = new($"{sprite.name} x{count}", typeof(RectTransform));
            slot.transform.SetParent(inventoryRow, false);
            RectTransform slotRect = (RectTransform)slot.transform;
            slotRect.sizeDelta = new Vector2(82f, 66f);
            AddImage(slot, insetColour);
            AddPixelBorder(slotRect, isNext ? foregroundColour : borderColour, isNext ? borderThickness + 1f : borderThickness);

            Image icon = new GameObject("Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(slot.transform, false);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(inventoryIconSize, inventoryIconSize);
            iconRect.anchoredPosition = new Vector2(8f, 0f);

            TMP_Text countText = CreateText($"×{count}", slot.transform, 20, TextAnchor.MiddleRight, foregroundColour);
            Stretch(countText.rectTransform, Vector2.zero, Vector2.one, new Vector2(45f, 0f), new Vector2(-6f, 0f));

            if (isNext)
            {
                TMP_Text nextLabel = CreateText("NEXT", slot.transform, 10, TextAnchor.UpperLeft, foregroundColour);
                Stretch(nextLabel.rectTransform, new Vector2(0f, 0.64f), new Vector2(1f, 1f), new Vector2(5f, 0f), new Vector2(-5f, -3f));
            }

            inventorySlots.Add(slot);
        }

        private RectTransform CreateRect(string objectName, Transform parent, Vector2 size)
        {
            GameObject go = new(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private Image AddImage(GameObject go, Color colour)
        {
            Image image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private void AddPixelBorder(RectTransform target, Color colour, float thickness)
        {
            CreateBorderEdge("Border Top", target, colour,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -thickness), Vector2.zero);
            CreateBorderEdge("Border Bottom", target, colour,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, thickness));
            CreateBorderEdge("Border Left", target, colour,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(thickness, 0f));
            CreateBorderEdge("Border Right", target, colour,
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-thickness, 0f), Vector2.zero);
        }

        private void CreateBorderEdge(
            string edgeName,
            RectTransform parent,
            Color colour,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform edge = CreateRect(edgeName, parent, Vector2.zero);
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
            edge.SetAsLastSibling();
            AddImage(edge.gameObject, colour);
        }

        private TMP_Text CreateText(
            string value,
            Transform parent,
            int fontSize,
            TextAnchor alignment,
            Color colour,
            bool uppercase = false)
        {
            GameObject go = new(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );

            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();

            text.text = value;
            text.font = font != null ? font : PixelFontLibrary.TmpMedium;
            text.fontSize = fontSize;
            text.fontStyle = uppercase
                ? FontStyles.UpperCase
                : FontStyles.Normal;
            text.alignment = ConvertAlignment(alignment);
            text.color = colour;
            text.raycastTarget = false;

            text.enableWordWrapping = false;
            text.extraPadding = false;

            return text;
        }

        private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
        {
            return alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,

                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,

                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,

                _ => TextAlignmentOptions.Center
            };
        }
    }

    public static class SurferSlugMinimalHudBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateHud()
        {
            if (Object.FindFirstObjectByType<SurferSlugMinimalHud>() != null)
                return;

            new GameObject(
                "Surfer Slug Gameplay HUD",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(SurferSlugMinimalHud));
        }
    }
}
