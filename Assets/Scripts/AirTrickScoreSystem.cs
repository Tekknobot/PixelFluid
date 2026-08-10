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
        [SerializeField, Min(1)] private int cleanTierMinimum = 500;
        [SerializeField, Min(1)] private int radicalTierMinimum = 1500;
        [SerializeField, Min(1)] private int legendaryTierMinimum = 4500;
        [SerializeField] private Color baseTierColour       = Color.white;
        [SerializeField] private Color cleanTierColour      = new Color32(0,255,255,255);     // Aqua
        [SerializeField] private Color radicalTierColour    = new Color32(255,70,180,255);    // Hot Pink
        [SerializeField] private Color legendaryTierColour  = new Color32(255,205,0,255);     // Gold

        private readonly List<FloatingScore> floatingScores = new();
        private Canvas floatingStokeCanvas;
        private RectTransform floatingStokeCanvasRect;
        private Canvas recapCanvas;
        private RectTransform recapContentRoot;
        private readonly List<EndDayPanelPresentation.MotionElement> recapMotionElements = new();
        private TextMeshProUGUI recapDayLabel;
        private TextMeshProUGUI recapDayStokeValue;
        private TextMeshProUGUI recapJumpsValue;
        private TextMeshProUGUI recapBestValue;
        private TextMeshProUGUI recapHighestAirValue;
        private TextMeshProUGUI recapTrickMixValue;
        private TextMeshProUGUI recapTotalValue;

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
        private float recapShownAt;
        private int recapDay;
        private int lastClosedRecapDay = -1;
        private float lastRecapClosedAt = -100f;
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
            // This developer action is intended to create a complete, immediately
            // testable ON FIRE state. Unlocking only the meter could leave the
            // right bumper with no valid Flow Finisher action.
            if (SurfAbilityProgression.Instance != null)
                SurfAbilityProgression.Instance.Unlock(
                    SurfAbility.Flow | SurfAbility.FlowFinisher);

            bool wasOnFire = IsOnFire;
            currentFlow = MaximumFlow;
            onFireUntil = Time.unscaledTime + Mathf.Max(10f, onFireDuration);
            onFireWasActive = true;
            lastFlowGainTime = Time.unscaledTime;

            if (!wasOnFire)
            {
                OnFireActivated?.Invoke();
                if (onFireActivationClip != null && flowAudioSource != null)
                    flowAudioSource.PlayOneShot(onFireActivationClip, flowAudioVolume);
            }
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

            if (recapVisible)
            {
                EndDayPanelPresentation.Animate(recapMotionElements, recapShownAt);
                if (now >= recapUntil)
                    HideRecap();
            }

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
            if (recapCanvas != null)
            {
                recapCanvas.gameObject.SetActive(false);
                EndDayUiFocusController.End(recapCanvas);
            }
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
            if (recapVisible)
                return;

            if (SurfDayUpgradeScreen.Instance != null &&
                SurfDayUpgradeScreen.Instance.IsVisible)
                return;

            if (day == lastClosedRecapDay &&
                Time.unscaledTime - lastRecapClosedAt < 1f)
                return;

            EnsureRecapUi();
            recapDay = day;
            recapVisible = true;
            recapShownAt = Time.unscaledTime;
            recapUntil = recapShownAt + Mathf.Max(1f, duration);
            UpdateRecapText();
            EndDayPanelPresentation.ResetMotion(recapMotionElements);
            recapCanvas.gameObject.SetActive(true);
            EndDayUiFocusController.Begin(recapCanvas);
        }

        public void DismissRecapImmediate()
        {
            if (!recapVisible &&
                (recapCanvas == null || !recapCanvas.gameObject.activeSelf))
                return;

            HideRecap();
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

        private void HideRecap()
        {
            if (!recapVisible &&
                (recapCanvas == null || !recapCanvas.gameObject.activeSelf))
                return;

            recapVisible = false;
            lastClosedRecapDay = recapDay;
            lastRecapClosedAt = Time.unscaledTime;
            if (recapCanvas != null)
            {
                recapCanvas.gameObject.SetActive(false);
                EndDayUiFocusController.End(recapCanvas);
            }
        }

        private void UpdateRecapText()
        {
            if (recapDayLabel == null)
                return;

            recapDayLabel.text = "DAY " + recapDay + "  /  COMPLETE";
            recapDayStokeValue.text = "+" + dayStoke.ToString("N0");
            recapJumpsValue.text = jumpsLanded.ToString("N0");
            recapBestValue.text = bestTrick + "  /  +" + bestJumpScore.ToString("N0");
            recapHighestAirValue.text = highestAir.ToString("0.00") + " m";
            recapTrickMixValue.text =
                "H " + handstands + "  /  R " + rotations + "  /  F " + flips;
            recapTotalValue.text = "TOTAL STOKE  " + totalStoke.ToString("N0");
        }

        private void EnsureRecapUi()
        {
            if (recapCanvas != null)
                return;

            recapCanvas = EndDayPanelPresentation.CreateCanvas(
                transform, "End Day Recap TMP Canvas", 32550);
            GraphicRaycaster raycaster = recapCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;

            GameObject content = new("Left Middle Recap Stack", typeof(RectTransform));
            content.transform.SetParent(recapCanvas.transform, false);
            recapContentRoot = content.GetComponent<RectTransform>();
            recapContentRoot.anchorMin = recapContentRoot.anchorMax = new Vector2(0f, 0.5f);
            recapContentRoot.pivot = new Vector2(0f, 0.5f);
            recapContentRoot.anchoredPosition = new Vector2(106f, 0f);
            recapContentRoot.sizeDelta = new Vector2(720f, 610f);

            recapDayLabel = CreateRecapMotionText("Day Complete", string.Empty,
                new Vector2(0f, 0f), new Vector2(720f, 28f),
                PixelFontLibrary.TmpMedium, 18f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.Cyan, 0);
            CreateRecapMotionText("Recap Heading", "SESSION RECAP",
                new Vector2(0f, 29f), new Vector2(720f, 70f),
                PixelFontLibrary.TmpBold, 58f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.White, 1);
            CreateRecapMotionText("Stoke Caption", "TODAY'S STOKE",
                new Vector2(0f, 111f), new Vector2(720f, 26f),
                PixelFontLibrary.TmpMedium, 18f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.Cyan, 2);
            recapDayStokeValue = CreateRecapMotionText("Day Stoke", string.Empty,
                new Vector2(0f, 131f), new Vector2(720f, 82f),
                PixelFontLibrary.TmpBold, 68f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.Gold, 3);

            CreateRecapMotionRule("Stats Divider", new Vector2(0f, 216f),
                new Vector2(540f, 2f), new Color(0.42f, 0.94f, 1f, 0.68f), 4);

            const float rowsTop = 237f;
            const float rowHeight = 52f;
            recapJumpsValue = CreateRecapStatRow(
                "TRICK JUMPS", rowsTop, rowHeight, 5);
            recapBestValue = CreateRecapStatRow(
                "BEST TRICK", rowsTop + rowHeight, rowHeight, 6);
            recapHighestAirValue = CreateRecapStatRow(
                "HIGHEST AIR", rowsTop + rowHeight * 2f, rowHeight, 7);
            recapTrickMixValue = CreateRecapStatRow(
                "TRICK MIX", rowsTop + rowHeight * 3f, rowHeight, 8);

            float footerY = rowsTop + rowHeight * 4f + 20f;
            CreateRecapMotionRule("Total Divider", new Vector2(0f, footerY),
                new Vector2(540f, 4f), EndDayPanelPresentation.Gold, 9);
            recapTotalValue = CreateRecapMotionText("Total Stoke", string.Empty,
                new Vector2(0f, footerY + 13f), new Vector2(720f, 49f),
                PixelFontLibrary.TmpSemiBold, 31f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.White, 10);

            recapCanvas.gameObject.SetActive(false);
        }

        private TextMeshProUGUI CreateRecapMotionText(string name, string text,
            Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize,
            TextAlignmentOptions alignment, Color colour, int order)
        {
            RectTransform group = EndDayPanelPresentation.CreateTopLeftRect(
                recapContentRoot, name + " Motion", position, size);
            TextMeshProUGUI label = EndDayPanelPresentation.CreateText(
                group, name, text, font, fontSize, alignment, colour);
            EndDayPanelPresentation.Stretch(label.rectTransform);
            EndDayPanelPresentation.AddMotion(group, order, recapMotionElements);
            return label;
        }

        private void CreateRecapMotionRule(string name, Vector2 position,
            Vector2 size, Color colour, int order)
        {
            RectTransform group = EndDayPanelPresentation.CreateTopLeftRect(
                recapContentRoot, name + " Motion", position, size);
            Image rule = EndDayPanelPresentation.CreateRule(group, name, colour);
            EndDayPanelPresentation.Stretch(rule.rectTransform);
            EndDayPanelPresentation.AddMotion(group, order, recapMotionElements);
        }

        private TextMeshProUGUI CreateRecapStatRow(string labelText,
            float y, float height, int order)
        {
            RectTransform row = EndDayPanelPresentation.CreateTopLeftRect(
                recapContentRoot, labelText + " Motion",
                new Vector2(0f, y), new Vector2(720f, height));
            EndDayPanelPresentation.AddMotion(row, order, recapMotionElements);

            RectTransform railHolder = EndDayPanelPresentation.CreateTopLeftRect(
                row, "Rail Holder", new Vector2(0f, 7f), new Vector2(3f, height - 14f));
            Image rail = EndDayPanelPresentation.CreateRule(
                railHolder, "Rail", new Color(0.42f, 0.94f, 1f, 0.58f));
            EndDayPanelPresentation.Stretch(rail.rectTransform);

            RectTransform labelHolder = EndDayPanelPresentation.CreateTopLeftRect(
                row, "Label Holder", new Vector2(17f, 0f), new Vector2(205f, height));
            TextMeshProUGUI label = EndDayPanelPresentation.CreateText(
                labelHolder, "Label", labelText, PixelFontLibrary.TmpMedium,
                18f, TextAlignmentOptions.MidlineLeft, EndDayPanelPresentation.Cyan);
            EndDayPanelPresentation.Stretch(label.rectTransform);

            RectTransform valueHolder = EndDayPanelPresentation.CreateTopLeftRect(
                row, "Value Holder", new Vector2(225f, 0f), new Vector2(495f, height));
            TextMeshProUGUI value = EndDayPanelPresentation.CreateText(
                valueHolder, "Value", string.Empty, PixelFontLibrary.TmpRegular,
                25f, TextAlignmentOptions.MidlineLeft, EndDayPanelPresentation.White);
            EndDayPanelPresentation.Stretch(value.rectTransform);
            return value;
        }

    }
}
