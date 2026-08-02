using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

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
        }

        public static AirTrickScoreSystem Instance { get; private set; }

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
        [SerializeField] private Color baseTierColour = Color.white;
        [SerializeField] private Color cleanTierColour = new(0.25f, 0.95f, 1f, 1f);
        [SerializeField] private Color radicalTierColour = new(1f, 0.88f, 0.15f, 1f);
        [SerializeField] private Color legendaryTierColour = new(1.00f, 0.72f, 0.12f, 1f);

        private readonly List<FloatingScore> floatingScores = new();
        private GUIStyle floatingStyle;
        private GUIStyle recapTitleStyle;
        private GUIStyle recapValueStyle;
        private GUIStyle recapSmallStyle;

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
        public bool IsRecapVisible => recapVisible;
        public float Flow01 => Mathf.Clamp01(currentFlow / Mathf.Max(1f, maximumFlow));
        public float CurrentFlow => currentFlow;
        public float MaximumFlow => Mathf.Max(1f, maximumFlow);
        public bool IsOnFire => Time.unscaledTime < onFireUntil;
        public float OnFireTimeRemaining => Mathf.Max(0f, onFireUntil - Time.unscaledTime);
        public float OnFireAnimationMultiplier => IsOnFire ? 1.2f : 1f;
        public float OnFireJumpMultiplier => IsOnFire ? 1.12f : 1f;

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
            floatingScores.RemoveAll(score => now - score.CreatedAt >= score.Lifetime);

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
                float flowGain = chainLength >= 3
                    ? tripleChainFlow
                    : chainLength == 2 ? doubleChainFlow : singleTrickFlow;
                AddFlow(flowGain);
                if (chainLength >= 2 && comboCompleteClip != null && flowAudioSource != null)
                    flowAudioSource.PlayOneShot(comboCompleteClip, flowAudioVolume);
            }

            GetPopupTier(score, out string tierName, out Color tierColour);
            floatingScores.Add(new FloatingScore
            {
                WorldPosition = landingPosition + Vector3.up * 0.35f,
                Text = (scoredWhileOnFire ? "ON FIRE  " : string.Empty) +
                    tierName + "  " + chainLabel + "\n" +
                    trickName + "  +" + score + " STOKE",
                TextColour = tierColour,
                CreatedAt = Time.unscaledTime,
                Lifetime = floatingScoreLifetime
            });

            return score;
        }

        private void AddFlow(float amount)
        {
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
                if (onFireActivationClip != null && flowAudioSource != null)
                    flowAudioSource.PlayOneShot(onFireActivationClip, flowAudioVolume);
            }
        }

        public bool ConsumeFlowFinisher(Vector3 worldPosition)
        {
            if (!IsOnFire) return false;
            onFireUntil = 0f;
            currentFlow = 0f;
            onFireWasActive = false;
            lastFlowGainTime = Time.unscaledTime;
            int award = Mathf.Max(0, flowFinisherStoke);
            dayStoke += award;
            totalStoke += award;
            floatingScores.Add(new FloatingScore
            {
                WorldPosition = worldPosition + Vector3.up * 0.55f,
                Text = "FLOW FINISH!\n+" + award + " STOKE",
                TextColour = legendaryTierColour,
                CreatedAt = Time.unscaledTime,
                Lifetime = Mathf.Max(1.5f, floatingScoreLifetime)
            });
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
            if (floatingStyle != null) return;

            floatingStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(1f, 0.9f, 0.2f, 1f) }
            };
            recapTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 64,
                fontStyle = FontStyle.Normal,
                normal = { textColor = Color.white }
            };
            recapValueStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(1f, 0.9f, 0.2f, 1f) }
            };
            recapSmallStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Medium,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                normal = { textColor = Color.white }
            };
        }

        private void OnGUI()
        {
            EnsureStyles();

            Camera camera = Camera.main;
            if (camera != null)
            {
                float now = Time.unscaledTime;
                foreach (FloatingScore score in floatingScores)
                {
                    float age01 = Mathf.Clamp01((now - score.CreatedAt) / Mathf.Max(0.01f, score.Lifetime));
                    Vector3 screen = camera.WorldToScreenPoint(score.WorldPosition + Vector3.up * age01 * 0.55f);
                    if (screen.z <= 0f) continue;

                    Color old = GUI.color;
                    Color tierColour = score.TextColour;
                    GUI.color = new Color(tierColour.r, tierColour.g, tierColour.b,
                        tierColour.a * (1f - age01));
                    GUI.Label(
                        new Rect(
                            screen.x - 250f,
                            Screen.height - screen.y - 80f,
                            500f,
                            180f),
                        score.Text,
                        floatingStyle);
                    GUI.color = old;
                }
            }

            if (!recapVisible) return;

            float width = Mathf.Min(700f, Screen.width - 40f);
            float height = 500f;

            Rect panel = new(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height
            );

            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = oldColor;

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 25f, panel.width - 40f, 60f),
                "DAY " + recapDay + " RECAP",
                recapTitleStyle
            );

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 90f, panel.width - 40f, 50f),
                "+" + dayStoke.ToString("N0") + " STOKE",
                recapValueStyle
            );

            GUI.Label(
                new Rect(panel.x + 30f, panel.y + 155f, panel.width - 60f, 40f),
                "TRICK JUMPS  " + jumpsLanded,
                recapSmallStyle
            );

            GUI.Label(
                new Rect(panel.x + 30f, panel.y + 205f, panel.width - 60f, 40f),
                "BEST TRICK  " + bestTrick + "  +" + bestJumpScore,
                recapSmallStyle
            );

            GUI.Label(
                new Rect(panel.x + 30f, panel.y + 255f, panel.width - 60f, 40f),
                "HIGHEST AIR  " + highestAir.ToString("0.00") + "m",
                recapSmallStyle
            );

            GUI.Label(
                new Rect(panel.x + 30f, panel.y + 305f, panel.width - 60f, 50f),
                "HANDSTANDS " + handstands +
                "   ROTATIONS " + rotations +
                "   FLIPS " + flips,
                recapSmallStyle
            );

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 390f, panel.width - 40f, 55f),
                "TOTAL STOKE  " + totalStoke.ToString("N0"),
                recapValueStyle
            );
        }
    }
}
