using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PixelOcean
{
    /// <summary>
    /// Runtime-installed trick score, Stoke currency, floating score feedback,
    /// and compact end-of-day recap. No scene setup is required.
    /// </summary>
    [DefaultExecutionOrder(-11900)]
    [DisallowMultipleComponent]
    public sealed class AirTrickScoreSystem : MonoBehaviour
    {
        private sealed class FloatingScore
        {
            public Vector3 WorldPosition;
            public string Text;
            public Color TextColour;
            public float CreatedAt;
            public float Lifetime;
            public TextMeshProUGUI Label;
        }

        public static AirTrickScoreSystem Instance { get; private set; }
        public static event Action<int> CleanChainLanded;
        public static event Action OnFireActivated;

        [Header("Scoring")]
        [SerializeField, Min(0.1f)]
        private float maximumScoringHeight = 2.0f;

        [SerializeField, Min(1)]
        private int maximumHeightPoints = 600;

        [SerializeField, Min(0)] private int handstandPoints = 100;
        [SerializeField, Min(0)] private int rotationPoints = 140;
        [SerializeField, Min(0)] private int flipPoints = 180;
        [SerializeField, Min(0)] private int extraTrickComboBonus = 75;

        [Header("Combo Chain Scoring")]
        [Tooltip("Extra Stoke for completing the second distinct trick in one aerial chain.")]
        [SerializeField, Min(0)] private int doubleChainBonus = 120;
        [Tooltip("Additional Stoke for completing all three distinct tricks in one aerial chain.")]
        [SerializeField, Min(0)] private int tripleChainBonus = 260;
        [Tooltip("Stoke awarded per second spent airborne, capped by Maximum Scoring Airtime.")]
        [SerializeField, Min(0)] private int airtimePointsPerSecond = 90;
        [SerializeField, Min(0.1f)] private float maximumScoringAirtime = 4f;
        [Tooltip("Stoke awarded per world unit travelled during the complete jump.")]
        [SerializeField, Min(0)] private int distancePointsPerUnit = 35;
        [Tooltip("Maximum horizontal distance that contributes to Stoke.")]
        [SerializeField, Min(0.1f)] private float maximumScoringDistance = 8f;
        [Tooltip("Multiplier applied after all base, height, airtime and distance points are combined.")]
        [SerializeField, Range(1f, 3f)] private float doubleChainMultiplier = 1.15f;
        [SerializeField, Range(1f, 4f)] private float tripleChainMultiplier = 1.35f;
        [Tooltip("Small reward for completing the whole chain and returning to the water.")]
        [SerializeField, Min(0)] private int cleanLandingBonus = 50;

        [SerializeField, Min(0f)] private float floatingScoreLifetime = 1.35f;

        [Header("Floating Stoke TMP Font")]
        [Tooltip("TMP font used only for the floating Stoke number.")]
        [SerializeField] private TMP_FontAsset floatingStokeFont;
        [Tooltip("Optional Resources path used when Floating Stoke Font is not assigned.")]
        [SerializeField] private string floatingStokeFontResource = "Fonts/PixeloidSans-Bold SDF";

        [Header("Flow Meter / On Fire")]
        [SerializeField, Min(1f)] private float maximumFlow = 100f;
        [SerializeField, Min(0f)] private float singleTrickFlow = 30f;
        [SerializeField, Min(0f)] private float doubleChainFlow = 48f;
        [SerializeField, Min(0f)] private float tripleChainFlow = 68f;
        [SerializeField, Min(0f)] private float flowDecayDelay = 2.5f;
        [SerializeField, Min(0f)] private float flowDecayPerSecond = 8f;
        [SerializeField, Min(1f)] private float onFireDuration = 20f;
        [SerializeField, Range(1f, 3f)] private float onFireStokeMultiplier = 1.5f;
        [SerializeField, Min(0)] private int flowFinisherStoke = 750;
        [SerializeField] private AudioClip onFireActivationClip;
        [SerializeField] private AudioClip comboCompleteClip;
        [SerializeField, Range(0f, 1f)] private float flowAudioVolume = 0.9f;
        private AudioSource flowAudioSource;

        [Header("Popup Tiers")]
        [SerializeField, Min(1)] private int cleanTierMinimum = 350;
        [SerializeField, Min(1)] private int radicalTierMinimum = 450;
        [SerializeField, Min(1)] private int legendaryTierMinimum = 650;
        [SerializeField] private Color baseTierColour       = Color.white;
        [SerializeField] private Color cleanTierColour      = new Color32(0,255,255,255);     // Aqua
        [SerializeField] private Color radicalTierColour    = new Color32(255,70,180,255);    // Hot Pink
        [SerializeField] private Color legendaryTierColour  = new Color32(255,205,0,255);     // Gold

        private readonly List<FloatingScore> floatingScores = new();
        private Canvas floatingStokeCanvas;
        private RectTransform floatingStokeCanvasRect;
        private GUIStyle floatingStyle;
        private GUIStyle recapTitleStyle;
        private GUIStyle recapValueStyle;
        private GUIStyle recapLabelStyle;
        private GUIStyle recapSmallStyle;
        private GUIStyle recapFooterStyle;

        private int totalStoke;
        private int dayStoke;
        private int jumpsLanded;
        private int handstands;
        private int rotations;
        private int flips;
        private int bestJumpScore;
        private float highestAir;
        private string bestTrick = "NONE";

        private bool recapVisible;
        private float recapUntil;
        private int recapDay;
        private float currentFlow;
        private float lastFlowGainTime;
        private float onFireUntil;
        private bool onFireWasActive;

        public int TotalStoke => totalStoke;
        public int DayStoke => dayStoke;

        public void RestorePersistentStoke(int savedTotalStoke, int savedDayStoke)
        {
            totalStoke = Mathf.Max(0, savedTotalStoke);
            dayStoke = Mathf.Clamp(savedDayStoke, 0, totalStoke);
        }
        public bool IsRecapVisible => recapVisible;
        public float Flow01 => Mathf.Clamp01(currentFlow / Mathf.Max(1f, maximumFlow));
        public float CurrentFlow => currentFlow;
        public float MaximumFlow => Mathf.Max(1f, maximumFlow);
        public bool IsOnFire => (SurfAbilityProgression.Instance == null || SurfAbilityProgression.Instance.Has(SurfAbility.Flow)) && Time.unscaledTime < onFireUntil;
        public float OnFireTimeRemaining => Mathf.Max(0f, onFireUntil - Time.unscaledTime);
        public float OnFireAnimationMultiplier => IsOnFire ? 1.2f : 1f;
        public float OnFireJumpMultiplier => IsOnFire ? 1.12f : 1f;

        public void DebugMaxFlow()
        {
            currentFlow = MaximumFlow;
            onFireUntil = Time.unscaledTime + Mathf.Max(10f, onFireDuration);
            onFireWasActive = true;
            lastFlowGainTime = Time.unscaledTime;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<AirTrickScoreSystem>() != null)
                return;

            GameObject host = new("Air Trick Score System");
            DontDestroyOnLoad(host);
            host.AddComponent<AirTrickScoreSystem>();
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
            flowAudioSource = GetComponent<AudioSource>();
            if (flowAudioSource == null) flowAudioSource = gameObject.AddComponent<AudioSource>();
            flowAudioSource.playOnAwake = false;
            if (onFireActivationClip == null) onFireActivationClip = Resources.Load<AudioClip>("Audio/SFX/on_fire_activate");
            if (comboCompleteClip == null) comboCompleteClip = Resources.Load<AudioClip>("Audio/SFX/ching");
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            UpdateFloatingStokeLabels(now);

            if (recapVisible && now >= recapUntil)
                recapVisible = false;

            bool onFireNow = IsOnFire;
            if (onFireWasActive && !onFireNow)
            {
                currentFlow = 0f;
                lastFlowGainTime = now;
            }
            onFireWasActive = onFireNow;

            if (!onFireNow && currentFlow > 0f &&
                now - lastFlowGainTime >= flowDecayDelay)
            {
                currentFlow = Mathf.Max(0f,
                    currentFlow - flowDecayPerSecond * Time.unscaledDeltaTime);
            }
        }

        private void EnsureFloatingStokeCanvas()
        {
            if (floatingStokeCanvas != null)
                return;

            GameObject canvasObject = new("Floating Stoke Canvas");
            canvasObject.transform.SetParent(transform, false);

            floatingStokeCanvas = canvasObject.AddComponent<Canvas>();
            floatingStokeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            floatingStokeCanvas.sortingOrder = 32000;

            // No CanvasScaler: the old IMGUI popup used literal screen pixels.
            canvasObject.AddComponent<GraphicRaycaster>().enabled = false;
            floatingStokeCanvasRect = floatingStokeCanvas.transform as RectTransform;

            if (floatingStokeFont == null &&
                !string.IsNullOrWhiteSpace(floatingStokeFontResource))
            {
                floatingStokeFont =
                    Resources.Load<TMP_FontAsset>(floatingStokeFontResource);
            }

            if (floatingStokeFont == null)
                floatingStokeFont = TMP_Settings.defaultFontAsset;
        }

        private void AddFloatingStoke(
            Vector3 worldPosition,
            string text,
            Color colour,
            float lifetime)
        {
            EnsureFloatingStokeCanvas();
            if (floatingStokeCanvasRect == null)
                return;

            GameObject labelObject = new("Floating Stoke");
            labelObject.transform.SetParent(floatingStokeCanvasRect, false);

            TextMeshProUGUI label =
                labelObject.AddComponent<TextMeshProUGUI>();

            label.raycastTarget = false;
            label.text = text;
            label.font = floatingStokeFont;
            label.fontSize = 64f;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = colour;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(500f, 180f);
            rect.localScale = Vector3.one;

            floatingScores.Add(new FloatingScore
            {
                WorldPosition = worldPosition,
                Text = text,
                TextColour = colour,
                CreatedAt = Time.unscaledTime,
                Lifetime = Mathf.Max(0.01f, lifetime),
                Label = label
            });
        }

        private void UpdateFloatingStokeLabels(float now)
        {
            Camera camera = Camera.main;

            for (int i = floatingScores.Count - 1; i >= 0; i--)
            {
                FloatingScore score = floatingScores[i];
                float age = now - score.CreatedAt;

                if (score.Label == null || age >= score.Lifetime)
                {
                    if (score.Label != null)
                        Destroy(score.Label.gameObject);

                    floatingScores.RemoveAt(i);
                    continue;
                }

                if (camera == null || floatingStokeCanvasRect == null)
                {
                    score.Label.enabled = false;
                    continue;
                }

                float age01 = Mathf.Clamp01(
                    age / Mathf.Max(0.01f, score.Lifetime));

                // Exact original rise: 0.55 world units over the popup lifetime.
                Vector3 screen = camera.WorldToScreenPoint(
                    score.WorldPosition + Vector3.up * age01 * 0.55f);

                if (screen.z <= 0f)
                {
                    score.Label.enabled = false;
                    continue;
                }

                score.Label.enabled = true;

                // Convert the old IMGUI screen position to centered overlay space.
                score.Label.rectTransform.anchoredPosition = new Vector2(
                    screen.x - Screen.width * 0.5f,
                    screen.y - Screen.height * 0.5f);

                Color faded = score.TextColour;
                faded.a *= 1f - age01;
                score.Label.color = faded;
            }
        }

        public void BeginDay(int day)
        {
            dayStoke = 0;
            jumpsLanded = 0;
            handstands = 0;
            rotations = 0;
            flips = 0;
            bestJumpScore = 0;
            highestAir = 0f;
            bestTrick = "NONE";
            recapVisible = false;
            currentFlow = 0f;
            onFireUntil = 0f;
            onFireWasActive = false;
            lastFlowGainTime = Time.unscaledTime;
        }

        // Compatibility overload for any older callers. New controller code uses
        // the full combo-aware overload below.
        public int AwardJump(Vector3 landingPosition, float height, bool didHandstand,
            bool didRotation, bool didFlip)
        {
            int inferredChain = (didHandstand ? 1 : 0) +
                (didRotation ? 1 : 0) +
                (didFlip ? 1 : 0);

            return AwardJump(
                landingPosition,
                height,
                didHandstand,
                didRotation,
                didFlip,
                inferredChain,
                0f,
                0f,
                true);
        }

        /// <summary>
        /// Awards Stoke for the complete landed aerial sequence. Chain depth is
        /// reported explicitly by the surfer controller, while airtime and travel
        /// reward the new double/triple-jump motion without counting height twice.
        /// </summary>
        public int AwardJump(Vector3 landingPosition, float height, bool didHandstand,
            bool didRotation, bool didFlip, int completedChainLength,
            float airtimeSeconds, float horizontalDistance, bool cleanLanding)
        {
            int uniqueTrickCount = (didHandstand ? 1 : 0) +
                (didRotation ? 1 : 0) +
                (didFlip ? 1 : 0);
            if (uniqueTrickCount <= 0)
                return 0;

            int chainLength = Mathf.Clamp(
                Mathf.Min(completedChainLength, uniqueTrickCount),
                1,
                3);

            float safeHeight = Mathf.Max(0f, height);
            float normalizedHeight = Mathf.Clamp01(
                safeHeight / Mathf.Max(0.1f, maximumScoringHeight));
            int heightScore = Mathf.RoundToInt(
                normalizedHeight * maximumHeightPoints);

            float scoredAirtime = Mathf.Clamp(
                airtimeSeconds,
                0f,
                Mathf.Max(0.1f, maximumScoringAirtime));
            int airtimeScore = Mathf.RoundToInt(
                scoredAirtime * airtimePointsPerSecond);

            float scoredDistance = Mathf.Clamp(
                Mathf.Abs(horizontalDistance),
                0f,
                Mathf.Max(0.1f, maximumScoringDistance));
            int distanceScore = Mathf.RoundToInt(
                scoredDistance * distancePointsPerUnit);

            int score = heightScore + airtimeScore + distanceScore;
            if (didHandstand) { score += handstandPoints; handstands++; }
            if (didRotation) { score += rotationPoints; rotations++; }
            if (didFlip) { score += flipPoints; flips++; }

            // Retain the original per-extra-trick reward, then layer the new
            // sequence-completion bonuses on top. A triple chain receives both
            // the double milestone and the triple milestone.
            if (chainLength > 1)
                score += (chainLength - 1) * extraTrickComboBonus;
            if (chainLength >= 2)
                score += doubleChainBonus;
            if (chainLength >= 3)
                score += tripleChainBonus;
            if (cleanLanding)
                score += cleanLandingBonus;

            float chainMultiplier = chainLength >= 3
                ? tripleChainMultiplier
                : chainLength == 2
                    ? doubleChainMultiplier
                    : 1f;
            score = Mathf.RoundToInt(score * chainMultiplier);
            bool scoredWhileOnFire = IsOnFire;
            if (scoredWhileOnFire)
                score = Mathf.RoundToInt(score * onFireStokeMultiplier);
            score = Mathf.Max(1, score);

            dayStoke += score;
            totalStoke += score;
            jumpsLanded++;

            string trickName = BuildTrickName(didHandstand, didRotation, didFlip);
            string chainLabel = chainLength >= 3
                ? "TRIPLE CHAIN"
                : chainLength == 2
                    ? "DOUBLE CHAIN"
                    : "SINGLE TRICK";

            if (score > bestJumpScore)
            {
                bestJumpScore = score;
                bestTrick = chainLabel + "  " + trickName;
            }
            highestAir = Mathf.Max(highestAir, safeHeight);

            if (cleanLanding)
            {
                // The progression director listens before AddFlow so the first
                // successful multi-trick landing can unlock Flow and immediately
                // receive the Flow earned by that same landing.
                if (chainLength >= 2)
                    CleanChainLanded?.Invoke(chainLength);

                float flowGain = chainLength >= 3
                    ? tripleChainFlow
                    : chainLength == 2 ? doubleChainFlow : singleTrickFlow;
                AddFlow(flowGain);
                if (chainLength >= 2 && comboCompleteClip != null && flowAudioSource != null)
                    flowAudioSource.PlayOneShot(comboCompleteClip, flowAudioVolume);
            }

            GetPopupTier(score, out string tierName, out Color tierColour);
            AddFloatingStoke(
                landingPosition + Vector3.up * 0.35f,
                "+" + score,
                tierColour,
                floatingScoreLifetime);

            return score;
        }

        private void AddFlow(float amount)
        {
            if (SurfAbilityProgression.Instance != null && !SurfAbilityProgression.Instance.Has(SurfAbility.Flow)) return;
            if (amount <= 0f)
                return;

            lastFlowGainTime = Time.unscaledTime;
            if (IsOnFire)
            {
                onFireUntil = Mathf.Min(
                    Time.unscaledTime + onFireDuration * 1.5f,
                    onFireUntil + amount / Mathf.Max(1f, maximumFlow) * 2f);
                currentFlow = maximumFlow;
                return;
            }

            currentFlow = Mathf.Clamp(currentFlow + amount, 0f, maximumFlow);
            if (currentFlow >= maximumFlow)
            {
                currentFlow = maximumFlow;
                onFireUntil = Time.unscaledTime + onFireDuration;
                onFireWasActive = true;
                OnFireActivated?.Invoke();
                if (onFireActivationClip != null && flowAudioSource != null)
                    flowAudioSource.PlayOneShot(onFireActivationClip, flowAudioVolume);
            }
        }

        public bool ConsumeFlowFinisher(Vector3 worldPosition)
        {
            if (!IsOnFire || (SurfAbilityProgression.Instance != null && !SurfAbilityProgression.Instance.Has(SurfAbility.FlowFinisher))) return false;
            onFireUntil = 0f;
            currentFlow = 0f;
            onFireWasActive = false;
            lastFlowGainTime = Time.unscaledTime;
            int award = Mathf.Max(0, flowFinisherStoke);
            dayStoke += award;
            totalStoke += award;
            AddFloatingStoke(
                worldPosition + Vector3.up * 0.55f,
                "FLOW FINISH!\n+" + award + " STOKE",
                legendaryTierColour,
                Mathf.Max(1.5f, floatingScoreLifetime));
            return true;
        }

        public void ShowDayRecap(int day, float duration)
        {
            recapDay = day;
            recapVisible = true;
            recapUntil = Time.unscaledTime + Mathf.Max(1f, duration);
        }


        private void GetPopupTier(int score, out string tierName, out Color tierColour)
        {
            if (score >= legendaryTierMinimum)
            {
                tierName = "LEGENDARY";
                tierColour = legendaryTierColour;
                return;
            }

            if (score >= radicalTierMinimum)
            {
                tierName = "RADICAL";
                tierColour = radicalTierColour;
                return;
            }

            if (score >= cleanTierMinimum)
            {
                tierName = "CLEAN";
                tierColour = cleanTierColour;
                return;
            }

            tierName = "AIR";
            tierColour = baseTierColour;
        }

        private static string BuildTrickName(bool handstand, bool rotation, bool flip)
        {
            if (handstand && rotation && flip) return "TRIPLE THREAT";
            if (handstand && rotation) return "HANDSTAND SPIN";
            if (handstand && flip) return "HANDSTAND FLIP";
            if (rotation && flip) return "SPIN FLIP";
            if (handstand) return "HANDSTAND";
            if (rotation) return "ROTATION";
            return "FLIP";
        }

        private void EnsureStyles()
        {
            if (recapTitleStyle != null) return;

            recapTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 86,
                fontStyle = FontStyle.Normal,
                wordWrap = false,
                normal = { textColor = new Color(0.49f, 0.94f, 1f, 1f) }
            };

            recapValueStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 62,
                fontStyle = FontStyle.Normal,
                wordWrap = false,
                normal = { textColor = new Color(1f, 0.82f, 0.28f, 1f) }
            };

            recapLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.SemiBold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Normal,
                wordWrap = false,
                normal = { textColor = new Color(0.55f, 0.92f, 1f, 1f) }
            };

            recapSmallStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Medium,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 38,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            recapFooterStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 50,
                fontStyle = FontStyle.Normal,
                wordWrap = false,
                normal = { textColor = new Color(1f, 0.82f, 0.28f, 1f) }
            };
        }

        private static void DrawSolidRect(Rect rect, Color colour)
        {
            Color previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawPixelFrame(Rect panel)
        {
            // Chunky, square pixel frame: shadow, white outer rail, cyan inner rail.
            DrawSolidRect(new Rect(panel.x + 10f, panel.y + 12f, panel.width, panel.height),
                new Color(0f, 0f, 0f, 0.42f));
            DrawSolidRect(panel, new Color(0.96f, 0.99f, 1f, 1f));
            DrawSolidRect(new Rect(panel.x + 5f, panel.y + 5f, panel.width - 10f, panel.height - 10f),
                new Color(0.18f, 0.76f, 0.91f, 1f));
            DrawSolidRect(new Rect(panel.x + 10f, panel.y + 10f, panel.width - 20f, panel.height - 20f),
                new Color(0.015f, 0.09f, 0.15f, 0.98f));
        }

        private static void DrawWaveRail(Rect rect, float phase)
        {
            DrawSolidRect(rect, new Color(0.05f, 0.30f, 0.43f, 1f));

            const float blockWidth = 28f;
            const float crestWidth = 14f;
            float offset = Mathf.Repeat(phase, blockWidth);
            for (float x = rect.x - blockWidth + offset; x < rect.xMax; x += blockWidth)
            {
                DrawSolidRect(new Rect(x, rect.y, crestWidth, rect.height * 0.5f),
                    new Color(0.38f, 0.91f, 1f, 1f));
                DrawSolidRect(new Rect(x + crestWidth, rect.y + rect.height * 0.5f,
                        blockWidth - crestWidth, rect.height * 0.5f),
                    new Color(0.20f, 0.66f, 0.82f, 1f));
            }
        }

        private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style,
            float shadowOffset = 3f)
        {
            Color original = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.72f);
            GUI.Label(new Rect(rect.x + shadowOffset, rect.y + shadowOffset, rect.width, rect.height),
                text, style);
            style.normal.textColor = original;
            GUI.Label(rect, text, style);
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!recapVisible) return;

            float width = Mathf.Min(980f, Screen.width - 36f);
            float height = Mathf.Min(700f, Screen.height - 36f);
            float scale = Mathf.Clamp(Mathf.Min(width / 980f, height / 700f), 0.62f, 1f);

            recapTitleStyle.fontSize = Mathf.RoundToInt(86f * scale);
            recapValueStyle.fontSize = Mathf.RoundToInt(62f * scale);
            recapLabelStyle.fontSize = Mathf.RoundToInt(30f * scale);
            recapSmallStyle.fontSize = Mathf.RoundToInt(38f * scale);
            recapFooterStyle.fontSize = Mathf.RoundToInt(50f * scale);

            Rect panel = new(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height
            );

            DrawPixelFrame(panel);

            Rect interior = new(panel.x + 18f, panel.y + 18f,
                panel.width - 36f, panel.height - 36f);
            float wavePhase = Time.unscaledTime * 18f;
            DrawWaveRail(new Rect(interior.x, interior.y, interior.width, 14f * scale), wavePhase);
            DrawWaveRail(new Rect(interior.x, interior.yMax - 14f * scale,
                interior.width, 14f * scale), -wavePhase);

            float top = interior.y + 22f * scale;
            DrawShadowedLabel(
                new Rect(interior.x + 20f, top, interior.width - 40f, 82f * scale),
                "DAY " + recapDay + " RECAP",
                recapTitleStyle,
                4f * scale);

            float stokeLabelY = top + 82f * scale;
            GUI.Label(
                new Rect(interior.x + 30f, stokeLabelY, interior.width - 60f, 36f * scale),
                "TODAY'S STOKE",
                recapLabelStyle);

            DrawShadowedLabel(
                new Rect(interior.x + 30f, stokeLabelY + 27f * scale,
                    interior.width - 60f, 72f * scale),
                "+" + dayStoke.ToString("N0"),
                recapValueStyle,
                3f * scale);

            float dividerY = stokeLabelY + 105f * scale;
            DrawSolidRect(new Rect(interior.x + 52f * scale, dividerY,
                    interior.width - 104f * scale, 3f * scale),
                new Color(0.20f, 0.67f, 0.82f, 0.95f));

            float statsTop = dividerY + 16f * scale;
            float rowHeight = 68f * scale;
            float statsWidth = interior.width - 84f * scale;
            float statsX = interior.x + 42f * scale;

            DrawSolidRect(new Rect(statsX, statsTop, statsWidth, rowHeight),
                new Color(0.02f, 0.17f, 0.24f, 0.92f));
            GUI.Label(new Rect(statsX + 12f * scale, statsTop, statsWidth - 24f * scale, rowHeight),
                "TRICK JUMPS    " + jumpsLanded,
                recapSmallStyle);

            DrawSolidRect(new Rect(statsX, statsTop + rowHeight + 7f * scale, statsWidth, rowHeight * 1.18f),
                new Color(0.025f, 0.21f, 0.29f, 0.92f));
            GUI.Label(new Rect(statsX + 12f * scale, statsTop + rowHeight + 7f * scale,
                    statsWidth - 24f * scale, rowHeight * 1.18f),
                "BEST TRICK\n" + bestTrick + "    +" + bestJumpScore,
                recapSmallStyle);

            float lowerRowY = statsTop + rowHeight * 2.18f + 14f * scale;
            float halfGap = 7f * scale;
            float halfWidth = (statsWidth - halfGap) * 0.5f;

            DrawSolidRect(new Rect(statsX, lowerRowY, halfWidth, rowHeight),
                new Color(0.02f, 0.17f, 0.24f, 0.92f));
            GUI.Label(new Rect(statsX + 8f * scale, lowerRowY, halfWidth - 16f * scale, rowHeight),
                "HIGHEST AIR\n" + highestAir.ToString("0.00") + " m",
                recapSmallStyle);

            DrawSolidRect(new Rect(statsX + halfWidth + halfGap, lowerRowY, halfWidth, rowHeight),
                new Color(0.02f, 0.17f, 0.24f, 0.92f));
            GUI.Label(new Rect(statsX + halfWidth + halfGap + 8f * scale, lowerRowY,
                    halfWidth - 16f * scale, rowHeight),
                "H " + handstands + "    R " + rotations + "    F " + flips,
                recapSmallStyle);

            float footerY = interior.yMax - 105f * scale;
            DrawSolidRect(new Rect(interior.x + 34f * scale, footerY,
                    interior.width - 68f * scale, 78f * scale),
                new Color(0.035f, 0.25f, 0.34f, 0.96f));
            DrawSolidRect(new Rect(interior.x + 34f * scale, footerY,
                    interior.width - 68f * scale, 3f * scale),
                new Color(0.45f, 0.94f, 1f, 1f));

            DrawShadowedLabel(
                new Rect(interior.x + 45f * scale, footerY + 2f * scale,
                    interior.width - 90f * scale, 72f * scale),
                "TOTAL STOKE    " + totalStoke.ToString("N0"),
                recapFooterStyle,
                3f * scale);
        }

    }
}
