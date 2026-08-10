using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelOcean
{
    /// <summary>
    /// Runtime-built pixel HUD: objective/lives, day clock, and throwable inventory.
    /// Everything is kept in one Canvas so no gameplay UI overlaps ever.
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
        [SerializeField] private Vector2 safeMargin = new(36f, 32f);
        [SerializeField, Min(16f)] private float inventoryIconSize = 32f;
        [SerializeField, Min(1f)] private float inventorySpacing = 8f;

        [Header("Pixel HUD Appearance")]
        [SerializeField] private Color panelColour = new(0.035f, 0.035f, 0.045f, 0f);
        [SerializeField] private Color insetColour = new(0.018f, 0.018f, 0.024f, 0f);
        [SerializeField] private Color borderColour = new(0.92f, 0.58f, 0.08f, 0f);
        [SerializeField, Range(1f, 4f)] private float borderThickness = 2f;
        [SerializeField] private Color trackColour = new(0.16f, 0.13f, 0.16f, 0.33f);
        [SerializeField] private Color foregroundColour = new(1f, 0.89f, 0.62f, 1f);
        [SerializeField] private Color mutedColour = new(0.84f, 0.73f, 0.53f, 1f);

        private TinyWaveSurfer player;
        private ProceduralStarryNight dayNight;
        private SurfDayProgressionDirector progression;
        private SurfRunLifeManager lifeManager;

        private RectTransform inventoryRow;
        private RectTransform dayFill;
        private RectTransform flowFill;
        private Image flowFillImage;
        private Image phaseIcon;
        private Image livesIcon;
        private Image stokeIcon;
        private Image flowIcon;
        private Image fireIcon;

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
        private bool presentationSuppressed;
        private float displayedFlow01;
        private RectTransform chapterBannerRect;
        private string heldChapterText = string.Empty;
        private float chapterHoldUntil;
        private float chapterTypewriterStartedAt;
        private int chapterTypewriterCharacterCount;

        [Header("Chapter Banner Typewriter")]
        [SerializeField, Min(1f)]
        private float chapterCharactersPerSecond = 32f;
        [SerializeField, Min(0f)]
        private float chapterTypewriterDelay = 0.08f;

        private const float HudIconReferenceSize = 48f;
        private const float InventoryReferenceSize = 48f;
        private const float ChapterHoldSeconds = 8f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            font = PixelFontLibrary.TmpBold;

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
            if (presentationSuppressed)
            {
                SetHudVisible(false, true);
                return;
            }

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

        public void SuppressPresentation(bool suppress)
        {
            presentationSuppressed = suppress;
        }

        public void SetPresentationSuppressed(bool suppressed)
        {
            bool wasSuppressed = presentationSuppressed;
            presentationSuppressed = suppressed;

            if (suppressed)
            {
                SetHudVisible(false, true);

                Canvas canvas = GetComponent<Canvas>();
                if (canvas != null)
                    canvas.enabled = false;

                GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                    raycaster.enabled = false;

                return;
            }

            // When the menu transition releases the HUD, reset its alpha before
            // re-enabling the canvas. This prevents one stale fully-visible frame.
            if (wasSuppressed && hudGroup != null)
            {
                hudGroup.alpha = 0f;
                hudGroup.interactable = false;
                hudGroup.blocksRaycasts = false;
            }

            Canvas releasedCanvas = GetComponent<Canvas>();
            if (releasedCanvas != null)
                releasedCanvas.enabled = GameModeSession.IsStory;

            GraphicRaycaster releasedRaycaster = GetComponent<GraphicRaycaster>();
            if (releasedRaycaster != null)
                releasedRaycaster.enabled = GameModeSession.IsStory;
        }

        public void SetStoryHudActive(bool active)
        {
            if (hudGroup == null)
                BuildHud();

            bool shouldBeActive =
                active &&
                GameModeSession.IsStory &&
                !presentationSuppressed;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
                canvas.enabled = shouldBeActive;

            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = shouldBeActive;

            if (shouldBeActive)
            {
                // Never force the HUD directly to alpha 1. Begin at zero and let
                // Update/SetHudVisible perform the smooth fade-in.
                hudGroup.alpha = 0f;
                hudGroup.interactable = false;
                hudGroup.blocksRaycasts = false;
            }
            else
            {
                SetHudVisible(false, true);
                player = null;
            }
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
            // The gameplay HUD is intentionally frameless so the sky and ocean remain open.
            // Force these alpha values at runtime as well, so older serialized component values
            // cannot bring the previous dark panels or gold borders back.
            panelColour.a = 0f;
            insetColour.a = 0f;
            borderColour.a = 0f;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
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
            RectTransform root = CreateRect("Minimal HUD", parent, Vector2.zero);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);

            const float hudHeight = 148f;
            float topInset = Mathf.Max(40f, safeMargin.y);
            root.offsetMin = new Vector2(safeMargin.x, -(topInset + hudHeight));
            root.offsetMax = new Vector2(-safeMargin.x, -topInset);

            RectTransform topBar = CreateRect("Unified Status Bar", root, Vector2.zero);
            topBar.anchorMin = new Vector2(0f, 0.54f);
            topBar.anchorMax = Vector2.one;
            topBar.offsetMin = Vector2.zero;
            topBar.offsetMax = Vector2.zero;
            AddImage(topBar.gameObject, panelColour);
            AddPixelBorder(topBar, borderColour, borderThickness);

            BuildCompactVitals(topBar);
            BuildCompactDay(topBar);
            BuildCompactInventory(topBar);

            RectTransform objectiveStrip = CreateRect("Objective Strip", root, Vector2.zero);
            objectiveStrip.anchorMin = new Vector2(0f, 0f);
            objectiveStrip.anchorMax = new Vector2(1f, 0.23f);
            objectiveStrip.offsetMin = Vector2.zero;
            objectiveStrip.offsetMax = Vector2.zero;
            AddImage(objectiveStrip.gameObject, insetColour);
            AddPixelBorder(objectiveStrip, borderColour, borderThickness);

            objectiveLabel = CreateText("Surf. Stay alive.", objectiveStrip, 27, TextAnchor.MiddleLeft, foregroundColour);
            objectiveLabel.enableAutoSizing = true;
            objectiveLabel.fontSizeMin = 20f;
            objectiveLabel.fontSizeMax = 27f;
            objectiveLabel.enableWordWrapping = false;
            objectiveLabel.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(objectiveLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 1f), new Vector2(-16f, -1f));

            RectTransform flowTrack = CreateRect("Flow Track", root, Vector2.zero);
            flowTrackObject = flowTrack.gameObject;
            flowTrack.anchorMin = new Vector2(0f, 0.27f);
            flowTrack.anchorMax = new Vector2(1f, 0.50f);
            flowTrack.offsetMin = Vector2.zero;
            flowTrack.offsetMax = Vector2.zero;
            AddImage(flowTrack.gameObject, insetColour);
            AddPixelBorder(flowTrack, borderColour, borderThickness);

            fireIcon = CreateHudIcon("Flow Fire Icon", flowTrack, "SurferSlugUI/fire_icon", true);
            fireIcon.rectTransform.anchorMin = fireIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fireIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            fireIcon.rectTransform.anchoredPosition = new Vector2(14f, 0f);

            RectTransform meter = CreateRect("Flow Meter Background", flowTrack, Vector2.zero);
            // Keep the flow meter compact so it does not span the full screen.
            meter.anchorMin = new Vector2(0f, 0f);
            meter.anchorMax = new Vector2(0.52f, 1f);
            meter.offsetMin = new Vector2(76f, 9f);
            meter.offsetMax = new Vector2(-18f, -9f);
            AddImage(meter.gameObject, trackColour);

            flowFill = CreateRect("Flow Fill", meter, Vector2.zero);
            flowFill.anchorMin = Vector2.zero;
            flowFill.anchorMax = new Vector2(0f, 1f);
            flowFill.pivot = new Vector2(0f, 0.5f);
            flowFill.offsetMin = new Vector2(2f, 2f);
            flowFill.offsetMax = new Vector2(-2f, -2f);
            flowFillImage = AddImage(flowFill.gameObject, new Color(0.18f, 0.74f, 1f, 0.95f));

            flowLabel = CreateText("0%", flowTrack, 27, TextAnchor.MiddleLeft, foregroundColour);
            Stretch(flowLabel.rectTransform, new Vector2(0.53f, 0f), new Vector2(0.64f, 1f), new Vector2(10f, 0f), Vector2.zero);
        }

        private void BuildCompactVitals(RectTransform bar)
        {
            RectTransform vitals = CreateRect("Vitals", bar, Vector2.zero);
            vitals.anchorMin = new Vector2(0f, 0f);
            vitals.anchorMax = new Vector2(0.23f, 1f);
            vitals.offsetMin = new Vector2(16f, 8f);
            vitals.offsetMax = new Vector2(-10f, -8f);

            livesIcon = CreateHudIcon("Lives Icon", vitals, "SurferSlugUI/lives_icon", true);
            livesIcon.rectTransform.anchorMin = livesIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            livesIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            livesIcon.rectTransform.anchoredPosition = Vector2.zero;

            livesLabel = CreateText("3/3", vitals, 30, TextAnchor.MiddleLeft, foregroundColour);
            Stretch(livesLabel.rectTransform, Vector2.zero, new Vector2(0.45f, 1f), new Vector2(48f, 0f), Vector2.zero);

            stokeIcon = CreateHudIcon("Stoke Icon", vitals, "SurferSlugUI/stoke_icon", true);
            stokeIcon.rectTransform.anchorMin = stokeIcon.rectTransform.anchorMax = new Vector2(0.52f, 0.5f);
            stokeIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            stokeIcon.rectTransform.anchoredPosition = Vector2.zero;

            stokeLabel = CreateText("0", vitals, 28, TextAnchor.MiddleLeft, foregroundColour);
            stokeLabel.enableAutoSizing = true;
            stokeLabel.fontSizeMin = 20f;
            stokeLabel.fontSizeMax = 28f;
            Stretch(stokeLabel.rectTransform, new Vector2(0.52f, 0f), Vector2.one, new Vector2(46f, 0f), new Vector2(-2f, 0f));
        }

        private void BuildCompactDay(RectTransform bar)
        {
            RectTransform day = CreateRect("Day Status", bar, Vector2.zero);
            day.anchorMin = new Vector2(0.23f, 0f);
            day.anchorMax = new Vector2(0.69f, 1f);
            day.offsetMin = new Vector2(12f, 8f);
            day.offsetMax = new Vector2(-12f, -8f);

            phaseIcon = CreateHudIcon("Phase Icon", day, "SurferSlugUI/dawn_icon", true);
            phaseIcon.rectTransform.anchorMin = phaseIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            phaseIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            phaseIcon.rectTransform.anchoredPosition = Vector2.zero;

            dayPhaseLabel = CreateText("DAY 1", day, 42, TextAnchor.MiddleLeft, foregroundColour);
            Stretch(dayPhaseLabel.rectTransform, Vector2.zero, new Vector2(0.31f, 1f), new Vector2(52f, 0f), Vector2.zero);

            RectTransform track = CreateRect("Day Track", day, Vector2.zero);
            track.anchorMin = new Vector2(0.33f, 0.36f);
            track.anchorMax = new Vector2(0.58f, 0.64f);
            track.offsetMin = Vector2.zero;
            track.offsetMax = Vector2.zero;
            AddImage(track.gameObject, trackColour);
            AddPixelBorder(track, borderColour, 2f);

            dayFill = CreateRect("Day Fill", track, Vector2.zero);
            dayFill.anchorMin = Vector2.zero;
            dayFill.anchorMax = new Vector2(0f, 1f);
            dayFill.pivot = new Vector2(0f, 0.5f);
            dayFill.offsetMin = new Vector2(3f, 3f);
            dayFill.offsetMax = new Vector2(-3f, -3f);
            AddImage(dayFill.gameObject, new Color(0.95f, 0.42f, 0.14f, 1f));

            timeLabel = CreateText("7:03 AM  •  0 / 0 m", day, 28, TextAnchor.MiddleLeft, foregroundColour);
            timeLabel.enableAutoSizing = true;
            timeLabel.fontSizeMin = 22f;
            timeLabel.fontSizeMax = 28f;
            Stretch(timeLabel.rectTransform, new Vector2(0.60f, 0f), Vector2.one, new Vector2(10f, 0f), Vector2.zero);
        }

        private void BuildCompactInventory(RectTransform bar)
        {
            RectTransform inventory = CreateRect("Items", bar, Vector2.zero);
            inventory.anchorMin = new Vector2(0.69f, 0f);
            inventory.anchorMax = Vector2.one;
            inventory.offsetMin = new Vector2(10f, 6f);
            inventory.offsetMax = new Vector2(-10f, -6f);

            inventoryRow = CreateRect("Item Row", inventory, Vector2.zero);
            Stretch(inventoryRow, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-54f, 0f));

            HorizontalLayoutGroup layout = inventoryRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            inventoryOverflowLabel = CreateText(string.Empty, inventory, 26, TextAnchor.MiddleRight, foregroundColour);
            Stretch(inventoryOverflowLabel.rectTransform, new Vector2(0.88f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void BuildChapterBanner(Transform parent)
        {
            chapterBannerRect = CreateRect("Chapter Banner", parent, new Vector2(780f, 120f));
            chapterBannerRect.anchorMin = chapterBannerRect.anchorMax = new Vector2(0f, 1f);
            chapterBannerRect.pivot = new Vector2(0f, 1f);
            chapterBannerRect.anchoredPosition = new Vector2(
                Mathf.Max(32f, safeMargin.x),
                -(Mathf.Max(32f, safeMargin.y) + 218f));
            AddImage(chapterBannerRect.gameObject, panelColour);
            AddPixelBorder(chapterBannerRect, borderColour, borderThickness);
            chapterGroup = chapterBannerRect.gameObject.AddComponent<CanvasGroup>();
            chapterGroup.alpha = 0f;

            chapterLabel = CreateText(string.Empty, chapterBannerRect, 16, TextAnchor.MiddleLeft, foregroundColour);
            chapterLabel.alignment = TextAlignmentOptions.MidlineLeft;
            chapterLabel.horizontalAlignment = HorizontalAlignmentOptions.Left;
            chapterLabel.verticalAlignment = VerticalAlignmentOptions.Middle;
            chapterLabel.enableAutoSizing = true;
            chapterLabel.fontSizeMin = 16f;
            chapterLabel.fontSizeMax = 32f;
            chapterLabel.enableWordWrapping = true;
            chapterLabel.overflowMode = TextOverflowModes.Overflow;
            Stretch(chapterLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 18f), new Vector2(-28f, -18f));
        }

        private void ResizeChapterBannerToText()
        {
            if (chapterBannerRect == null || chapterLabel == null)
                return;

            chapterLabel.ForceMeshUpdate();
            float preferred = chapterLabel.GetPreferredValues(
                chapterLabel.text,
                Mathf.Max(320f, chapterBannerRect.sizeDelta.x - 56f),
                0f).y;
            float height = Mathf.Clamp(preferred + 44f, 104f, 240f);
            chapterBannerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(height / 2f) * 2f);
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
                livesLabel.text = $"{remaining}/{maximum}";
            }

            bool showNotice =
                Time.unscaledTime < noticeUntil &&
                !string.IsNullOrEmpty(noticeText);

            bool progressionBannerActive =
                progression != null &&
                progression.IsBannerVisible &&
                !string.IsNullOrWhiteSpace(progression.CurrentBanner);

            if (showNotice)
            {
                heldChapterText = noticeText;
                chapterHoldUntil = Mathf.Max(chapterHoldUntil, noticeUntil);
            }
            else if (progressionBannerActive)
            {
                heldChapterText = progression.CurrentBanner;
                chapterHoldUntil = Time.unscaledTime + ChapterHoldSeconds;
            }

            bool showBanner =
                !string.IsNullOrWhiteSpace(heldChapterText) &&
                Time.unscaledTime < chapterHoldUntil;

            if (chapterLabel != null && showBanner && chapterLabel.text != heldChapterText)
            {
                chapterLabel.text = heldChapterText;
                chapterLabel.maxVisibleCharacters = 0;
                chapterTypewriterStartedAt = Time.unscaledTime + chapterTypewriterDelay;
                ResizeChapterBannerToText();
                chapterLabel.ForceMeshUpdate();
                chapterTypewriterCharacterCount = chapterLabel.textInfo.characterCount;
            }

            if (chapterLabel != null && showBanner)
            {
                float typingTime = Mathf.Max(0f,
                    Time.unscaledTime - chapterTypewriterStartedAt);
                int visibleCharacters = Mathf.FloorToInt(
                    typingTime * Mathf.Max(1f, chapterCharactersPerSecond));
                chapterLabel.maxVisibleCharacters = Mathf.Clamp(
                    visibleCharacters, 0, chapterTypewriterCharacterCount);
            }

            if (chapterGroup != null)
            {
                chapterGroup.alpha = Mathf.MoveTowards(
                    chapterGroup.alpha,
                    showBanner ? 1f : 0f,
                    Time.unscaledDeltaTime * (showBanner ? 4f : 1.4f));
            }

            if (!showBanner && Time.unscaledTime >= chapterHoldUntil)
                heldChapterText = string.Empty;

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
            stokeLabel.text = stoke.ToString("N0");
        }

        private void RefreshFlow()
        {
            bool unlocked = SurfAbilityProgression.Instance == null || SurfAbilityProgression.Instance.Has(SurfAbility.Flow);
            if (flowTrackObject != null) flowTrackObject.SetActive(unlocked);
            if (!unlocked)
            {
                displayedFlow01 = 0f;
                return;
            }

            AirTrickScoreSystem scoring = AirTrickScoreSystem.Instance;
            float targetFlow01 = scoring != null ? scoring.Flow01 : 0f;
            bool onFire = scoring != null && scoring.IsOnFire;
            if (onFire)
                targetFlow01 = 1f;

            // Ease both filling and draining so the meter never jumps upward.
            displayedFlow01 = Mathf.MoveTowards(
                displayedFlow01,
                Mathf.Clamp01(targetFlow01),
                Time.unscaledDeltaTime * 0.32f);

            if (flowFill != null)
                flowFill.anchorMax = new Vector2(displayedFlow01, 1f);

            if (flowFillImage != null)
            {
                float green = onFire
                    ? 0.62f + Mathf.Sin(Time.unscaledTime * 12f) * 0.18f
                    : 0.74f;
                flowFillImage.color = onFire
                    ? new Color(1f, green, 0.08f, 1f)
                    : new Color(0.18f, 0.74f, 1f, 0.95f);
            }

            if (fireIcon != null)
                fireIcon.gameObject.SetActive(fireIcon.sprite != null);

            if (flowIcon != null)
                flowIcon.gameObject.SetActive(false);

            if (flowLabel != null)
            {
                flowLabel.text = onFire && scoring != null
                    ? Mathf.CeilToInt(scoring.OnFireTimeRemaining).ToString()
                    : Mathf.RoundToInt(displayedFlow01 * 100f) + "%";
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
            if (dayPhaseLabel != null) dayPhaseLabel.text = $"DAY {currentDay}";
            if (phaseIcon != null)
            {
                phaseIcon.sprite = LoadHudSprite("SurferSlugUI/" + phase.ToLowerInvariant() + "_icon");
                ApplyHudIconReferenceSize(phaseIcon);
            }
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
            slotRect.sizeDelta = new Vector2(82f, 76f);
            AddImage(slot, insetColour);
            AddPixelBorder(slotRect, isNext ? foregroundColour : borderColour, isNext ? borderThickness + 1f : borderThickness);

            Image icon = new GameObject("Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(slot.transform, false);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            FitInventoryIcon(icon);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.56f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;

            TMP_Text countText = CreateText($"×{count}", slot.transform, 22, TextAnchor.LowerRight, foregroundColour);
            Stretch(countText.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-5f, -3f));

            inventorySlots.Add(slot);
        }

        private static void FitInventoryIcon(Image icon)
        {
            if (icon == null || icon.sprite == null)
                return;

            icon.SetNativeSize();

            RectTransform rect = icon.rectTransform;
            Vector2 native = rect.sizeDelta;

            float largest = Mathf.Max(1f, native.x, native.y);
            float scale = InventoryReferenceSize / largest;

            rect.sizeDelta = new Vector2(
                Mathf.Round(native.x * scale),
                Mathf.Round(native.y * scale));
        }

        private Image CreateHudIcon(string name, Transform parent, string resourcePath, bool matchInventoryCanSize = false)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = LoadHudSprite(resourcePath);
            image.preserveAspect = true;
            image.raycastTarget = false;

            if (image.sprite != null)
            {
                image.SetNativeSize();
                if (matchInventoryCanSize)
                {
                    RectTransform rect = image.rectTransform;
                    Vector2 native = rect.sizeDelta;
                    float largest = Mathf.Max(1f, native.x, native.y);
                    float scale = HudIconReferenceSize / largest;
                    rect.sizeDelta = new Vector2(
                        Mathf.Max(1f, Mathf.Round(native.x * scale)),
                        Mathf.Max(1f, Mathf.Round(native.y * scale)));
                }
            }
            else
            {
                // A missing resource must not render Unity's default white box.
                image.enabled = false;
                image.rectTransform.sizeDelta = Vector2.zero;
            }

            return image;
        }

        private static void ApplyHudIconReferenceSize(Image image)
        {
            if (image == null || image.sprite == null)
                return;

            image.enabled = true;
            image.SetNativeSize();
            RectTransform rect = image.rectTransform;
            Vector2 native = rect.sizeDelta;
            float largest = Mathf.Max(1f, native.x, native.y);
            float scale = HudIconReferenceSize / largest;
            rect.sizeDelta = new Vector2(
                Mathf.Max(1f, Mathf.Round(native.x * scale)),
                Mathf.Max(1f, Mathf.Round(native.y * scale)));
        }

        private static Sprite LoadHudSprite(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
                return null;

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
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
            // Transparent HUD style: do not create invisible border graphics.
            if (colour.a <= 0.001f || thickness <= 0f)
                return;

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

            // A crisp offset shadow keeps all HUD copy readable over bright sky, clouds,
            // waves, and nighttime backgrounds without needing opaque panels.
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
            shadow.effectDistance = new Vector2(3f, -3f);
            shadow.useGraphicAlpha = true;

            text.text = value;
            text.font = font != null ? font : PixelFontLibrary.TmpBold;
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
