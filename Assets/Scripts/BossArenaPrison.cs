using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Seals a boss encounter inside a bounded arena. Escape is earned by completing
    /// the boss-specific objective instead of waiting out a timer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossArenaPrison : MonoBehaviour
    {
        public static BossArenaPrison Active { get; private set; }
        public static bool IsActive => Active != null && !Active.encounterFinished;

        public float LeftBoundary => leftX + edgePadding;
        public float RightBoundary => rightX - edgePadding;
        public float CameraLeftBoundary => leftX;
        public float CameraRightBoundary => rightX;
        public float CentreX => centreX;

        public enum ArenaTheme { Reaper, RubberDuck }

        [Header("Arena")]
        [SerializeField] private ArenaTheme theme = ArenaTheme.Reaper;
        [SerializeField, Min(6f)] private float arenaWidth = 16f;
        [SerializeField, Min(0.25f)] private float edgePadding = 0.8f;
        [SerializeField, Min(0.25f)] private float escapeDistance = 1.25f;
        [SerializeField, Min(0f)] private float introLockDuration = 1.25f;

        [Header("Reaper Objective")]
        [SerializeField, Range(2, 8)] private int reaperHitsToOpenEscape = 4;

        [Header("Rubber Duck Objective")]
        [SerializeField, Range(1, 12)] private int ducklingsPerOpening = 4;
        [SerializeField, Range(1, 8)] private int duckDamagePhasesToOpenEscape = 3;
        [SerializeField, Min(1f)] private float duckVulnerabilityDuration = 6f;

        [Header("Boss UI")]
        [SerializeField, Min(0f)] private float panelY = 325f;

        private MonoBehaviour boss;
        private GodzillaLaneSwimmer reaperBoss;
        private RubberDuckBossSwimmer duckBoss;
        private TinyWaveSurfer player;
        private Camera gameplayCamera;
        private float centreX;
        private float leftX;
        private float rightX;
        private float startedAt;
        private bool gateOpen;
        private bool gateOnRight;
        private bool encounterFinished;
        private int reaperHits;
        private int ducklingsDestroyed;
        private int duckDamagePhases;
        private bool duckWindowOpen;
        private GUIStyle titleStyle;
        private GUIStyle smallStyle;

        public void Configure(MonoBehaviour bossBehaviour, ArenaTheme arenaTheme)
        {
            UnsubscribeBossEvents();
            boss = bossBehaviour;
            theme = arenaTheme;
            SubscribeBossEvents();
            CaptureArena();
        }

        private void Awake()
        {
            Active = this;
        }

        private void Start()
        {
            SubscribeBossEvents();
            CaptureArena();
        }

        private void OnDestroy()
        {
            UnsubscribeBossEvents();
            if (Active == this)
                Active = null;
        }

        private void SubscribeBossEvents()
        {
            reaperBoss = boss as GodzillaLaneSwimmer;
            duckBoss = boss as RubberDuckBossSwimmer;

            if (reaperBoss != null)
                reaperBoss.ArenaHitAccepted += OnReaperHitAccepted;

            if (duckBoss != null)
            {
                duckBoss.ArenaHitAccepted += OnDuckHitAccepted;
                duckBoss.ConfigureArenaArmour(true);
                RubberDucklingSwimmer.DestroyedByProjectile += OnDucklingDestroyed;
            }
        }

        private void UnsubscribeBossEvents()
        {
            if (reaperBoss != null)
                reaperBoss.ArenaHitAccepted -= OnReaperHitAccepted;
            if (duckBoss != null)
                duckBoss.ArenaHitAccepted -= OnDuckHitAccepted;
            RubberDucklingSwimmer.DestroyedByProjectile -= OnDucklingDestroyed;
            reaperBoss = null;
            duckBoss = null;
        }

        private void OnReaperHitAccepted(GodzillaLaneSwimmer sender)
        {
            if (encounterFinished || gateOpen || sender != reaperBoss)
                return;

            reaperHits++;
            if (reaperHits >= Mathf.Max(1, reaperHitsToOpenEscape))
                OpenEscapeGate();
        }

        private void OnDucklingDestroyed(RubberDucklingSwimmer duckling)
        {
            if (encounterFinished || gateOpen || duckBoss == null || duckWindowOpen)
                return;

            ducklingsDestroyed++;
            if (ducklingsDestroyed >= Mathf.Max(1, ducklingsPerOpening))
            {
                ducklingsDestroyed = 0;
                duckWindowOpen = true;
                duckBoss.OpenArenaVulnerability(duckVulnerabilityDuration);
            }
        }

        private void OnDuckHitAccepted(RubberDuckBossSwimmer sender)
        {
            if (encounterFinished || gateOpen || sender != duckBoss || !duckWindowOpen)
                return;

            duckWindowOpen = false;
            duckDamagePhases++;
            duckBoss.CloseArenaVulnerability();

            if (duckDamagePhases >= Mathf.Max(1, duckDamagePhasesToOpenEscape))
                OpenEscapeGate();
        }

        private void OpenEscapeGate()
        {
            gateOpen = true;
            gateOnRight = boss == null || boss.transform.position.x <= centreX;
        }

        public float ClampPlayerX(float desiredX, out bool hitClosedBoundary)
        {
            hitClosedBoundary = false;
            float min = LeftBoundary;
            float max = RightBoundary;

            if (!gateOpen || Time.time - startedAt < introLockDuration)
            {
                float clamped = Mathf.Clamp(desiredX, min, max);
                hitClosedBoundary = !Mathf.Approximately(clamped, desiredX);
                return clamped;
            }

            if (gateOnRight)
            {
                if (desiredX < min)
                {
                    hitClosedBoundary = true;
                    return min;
                }
                return desiredX;
            }

            if (desiredX > max)
            {
                hitClosedBoundary = true;
                return max;
            }
            return desiredX;
        }

        public float ClampCameraX(float desiredCameraX, float viewportHalfWidth)
        {
            float min = CameraLeftBoundary + viewportHalfWidth;
            float max = CameraRightBoundary - viewportHalfWidth;
            return min <= max ? Mathf.Clamp(desiredCameraX, min, max) : CentreX;
        }

        private void CaptureArena()
        {
            gameplayCamera = Camera.main;
            player = FindFirstObjectByType<TinyWaveSurfer>();
            centreX = player != null ? player.transform.position.x : transform.position.x;

            if (gameplayCamera != null && gameplayCamera.orthographic)
            {
                float cameraWidth = gameplayCamera.orthographicSize * 2f * gameplayCamera.aspect;
                arenaWidth = Mathf.Max(6f, cameraWidth - edgePadding * 0.5f);
            }

            leftX = centreX - arenaWidth * 0.5f;
            rightX = centreX + arenaWidth * 0.5f;
            startedAt = Time.time;
            gateOnRight = boss == null || boss.transform.position.x <= centreX;
        }

        private void LateUpdate()
        {
            if (encounterFinished)
                return;

            if (boss == null)
            {
                FinishArena(false);
                return;
            }

            if (duckWindowOpen && duckBoss != null && !duckBoss.IsVulnerable)
            {
                duckWindowOpen = false;
                duckBoss.CloseArenaVulnerability();
            }

            if (player == null || player.IsDead)
                player = FindFirstObjectByType<TinyWaveSurfer>();
            if (player == null)
                return;

            Vector3 p = player.transform.position;
            float paddedLeft = leftX + edgePadding;
            float paddedRight = rightX - edgePadding;

            if (!gateOpen || Time.time - startedAt < introLockDuration)
            {
                float originalX = p.x;
                p.x = Mathf.Clamp(p.x, paddedLeft, paddedRight);
                if (!Mathf.Approximately(originalX, p.x))
                    player.StopArenaHorizontalMomentum();
                SetPlayerPosition(p);
                return;
            }

            if (gateOnRight)
            {
                float originalX = p.x;
                p.x = Mathf.Max(paddedLeft, p.x);
                if (!Mathf.Approximately(originalX, p.x)) player.StopArenaHorizontalMomentum();
                SetPlayerPosition(p);
                if (p.x >= rightX + escapeDistance)
                    FinishArena(true);
            }
            else
            {
                float originalX = p.x;
                p.x = Mathf.Min(paddedRight, p.x);
                if (!Mathf.Approximately(originalX, p.x)) player.StopArenaHorizontalMomentum();
                SetPlayerPosition(p);
                if (p.x <= leftX - escapeDistance)
                    FinishArena(true);
            }
        }

        private void SetPlayerPosition(Vector3 position)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = position;
            else player.transform.position = position;
        }

        private void FinishArena(bool escaped)
        {
            if (encounterFinished)
                return;
            encounterFinished = true;

            if (escaped && boss != null)
            {
                SurfDayProgressionDirector progression = FindFirstObjectByType<SurfDayProgressionDirector>();
                progression?.OnFinalBossDefeated();
                Destroy(boss.gameObject);
            }

            Destroy(gameObject, 0.1f);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                fontSize = 25,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Medium,
                fontSize = 17,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };
        }

        private string BuildStatus()
        {
            if (gateOpen)
                return gateOnRight ? "ESCAPE ROUTE OPEN  >>>" : "<<<  ESCAPE ROUTE OPEN";

            if (theme == ArenaTheme.Reaper)
            {
                int remaining = Mathf.Max(0, reaperHitsToOpenEscape - reaperHits);
                return remaining > 0 ? "BREAK THE TIDE  " + remaining + " HITS" : "THE TIDE IS SHIFTING...";
            }

            if (duckWindowOpen)
                return "DUCK EXPOSED — HIT IT NOW!";

            int ducklingsRemaining = Mathf.Max(0, ducklingsPerOpening - ducklingsDestroyed);
            int phasesRemaining = Mathf.Max(0, duckDamagePhasesToOpenEscape - duckDamagePhases);
            return "POP " + ducklingsRemaining + " DUCKLINGS  •  " + phasesRemaining + " WAVES LEFT";
        }

        private void OnGUI()
        {
            if (encounterFinished || boss == null)
                return;

            EnsureStyles();
            string bossName = theme == ArenaTheme.Reaper ? "REAPER TIDE" : "DUCK STORM";
            string status = BuildStatus();

            Color old = GUI.color;
            GUI.color = new Color(0f, 0.05f, 0.09f, 0.82f);
            GUI.Box(new Rect(Screen.width * 0.5f - 210f, panelY, 420f, 72f), GUIContent.none);
            GUI.color = old;
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, panelY + 4f, 400f, 34f), bossName, titleStyle);
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, panelY + 36f, 400f, 28f), status, smallStyle);

            DrawEdge(12f, !gateOpen || gateOnRight);
            DrawEdge(Screen.width - 24f, !gateOpen || !gateOnRight);
        }

        private static void DrawEdge(float x, bool sealedEdge)
        {
            Color old = GUI.color;
            GUI.color = sealedEdge
                ? new Color(0.35f, 0.9f, 1f, 0.85f)
                : new Color(1f, 0.82f, 0.18f, 0.45f);
            for (int y = 112; y < Screen.height - 54; y += 38)
                GUI.Box(new Rect(x, y, 12f, 25f), GUIContent.none);
            GUI.color = old;
        }
    }
}
