using System.Collections.Generic;
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
        [SerializeField, Min(0f)] private float heightPointsPerUnit = 120f;
        [SerializeField, Min(0)] private int handstandPoints = 100;
        [SerializeField, Min(0)] private int rotationPoints = 140;
        [SerializeField, Min(0)] private int flipPoints = 180;
        [SerializeField, Min(0)] private int extraTrickComboBonus = 75;
        [SerializeField, Min(0f)] private float floatingScoreLifetime = 1.35f;

        [Header("Popup Tiers")]
        [SerializeField, Min(1)] private int cleanTierMinimum = 240;
        [SerializeField, Min(1)] private int radicalTierMinimum = 260;
        [SerializeField, Min(1)] private int legendaryTierMinimum = 280;
        [SerializeField] private Color baseTierColour = Color.white;
        [SerializeField] private Color cleanTierColour = new(0.25f, 0.95f, 1f, 1f);
        [SerializeField] private Color radicalTierColour = new(1f, 0.88f, 0.15f, 1f);
        [SerializeField] private Color legendaryTierColour = new(1f, 0.30f, 0.82f, 1f);

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

        public int TotalStoke => totalStoke;
        public int DayStoke => dayStoke;
        public bool IsRecapVisible => recapVisible;

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
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            floatingScores.RemoveAll(score => now - score.CreatedAt >= score.Lifetime);

            if (recapVisible && now >= recapUntil)
                recapVisible = false;
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
        }

        public int AwardJump(Vector3 landingPosition, float height, bool didHandstand,
            bool didRotation, bool didFlip)
        {
            int trickCount = (didHandstand ? 1 : 0) + (didRotation ? 1 : 0) + (didFlip ? 1 : 0);
            if (trickCount <= 0)
                return 0;

            int score = Mathf.RoundToInt(Mathf.Max(0f, height) * heightPointsPerUnit);
            if (didHandstand) { score += handstandPoints; handstands++; }
            if (didRotation) { score += rotationPoints; rotations++; }
            if (didFlip) { score += flipPoints; flips++; }
            if (trickCount > 1) score += (trickCount - 1) * extraTrickComboBonus;

            score = Mathf.Max(1, score);
            dayStoke += score;
            totalStoke += score;
            jumpsLanded++;

            string trickName = BuildTrickName(didHandstand, didRotation, didFlip);
            if (score > bestJumpScore)
            {
                bestJumpScore = score;
                bestTrick = trickName;
            }
            highestAir = Mathf.Max(highestAir, height);

            GetPopupTier(score, out string tierName, out Color tierColour);
            floatingScores.Add(new FloatingScore
            {
                WorldPosition = landingPosition + Vector3.up * 0.35f,
                Text = tierName + "  " + trickName + "\n+" + score + " STOKE",
                TextColour = tierColour,
                CreatedAt = Time.unscaledTime,
                Lifetime = floatingScoreLifetime
            });

            return score;
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
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.2f, 1f) }
            };
            recapTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            recapValueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.2f, 1f) }
            };
            recapSmallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
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
                    GUI.Label(new Rect(screen.x - 140f, Screen.height - screen.y - 42f, 280f, 84f),
                        score.Text, floatingStyle);
                    GUI.color = old;
                }
            }

            if (!recapVisible) return;

            float width = Mathf.Min(520f, Screen.width - 40f);
            float height = 330f;
            Rect panel = new((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = oldColor;

            GUI.Label(new Rect(panel.x, panel.y + 22f, panel.width, 44f),
                "DAY " + recapDay + " RECAP", recapTitleStyle);
            GUI.Label(new Rect(panel.x, panel.y + 76f, panel.width, 40f),
                "+" + dayStoke.ToString("N0") + " STOKE", recapValueStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 130f, panel.width - 60f, 30f),
                "TRICK JUMPS  " + jumpsLanded, recapSmallStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 164f, panel.width - 60f, 30f),
                "BEST TRICK  " + bestTrick + "  +" + bestJumpScore, recapSmallStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 198f, panel.width - 60f, 30f),
                "HIGHEST AIR  " + highestAir.ToString("0.00") + "m", recapSmallStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 232f, panel.width - 60f, 30f),
                "HANDSTANDS " + handstands + "   ROTATIONS " + rotations + "   FLIPS " + flips,
                recapSmallStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 274f, panel.width - 60f, 30f),
                "TOTAL STOKE  " + totalStoke.ToString("N0"), recapValueStyle);
        }
    }
}
