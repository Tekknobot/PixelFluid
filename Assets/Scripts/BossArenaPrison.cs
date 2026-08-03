using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Turns a boss encounter into a camera-sized arena. The player is held inside
    /// the current screen while the boss runs its normal lane AI. After the survival
    /// timer expires, one edge becomes an escape gate; reaching it ends the encounter.
    /// Defeating the boss normally also releases the arena automatically.
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
        [SerializeField, Min(5f)] private float escapeGateDelay = 42f;
        [SerializeField, Min(0.25f)] private float escapeDistance = 1.25f;
        [SerializeField, Min(0f)] private float introLockDuration = 1.25f;

        private MonoBehaviour boss;
        private TinyWaveSurfer player;
        private Camera gameplayCamera;
        private float centreX;
        private float leftX;
        private float rightX;
        private float startedAt;
        private bool gateOpen;
        private bool gateOnRight;
        private bool encounterFinished;
        private GUIStyle titleStyle;
        private GUIStyle smallStyle;

        public void Configure(MonoBehaviour bossBehaviour, ArenaTheme arenaTheme)
        {
            boss = bossBehaviour;
            theme = arenaTheme;
            CaptureArena();
        }

        private void Awake()
        {
            Active = this;
        }

        private void Start()
        {
            CaptureArena();
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
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

            // The open side remains traversable so the player can escape. The
            // opposite wall continues to stop skids, jumps and combo momentum.
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
            return min <= max
                ? Mathf.Clamp(desiredCameraX, min, max)
                : CentreX;
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

            if (player == null || player.IsDead)
                player = FindFirstObjectByType<TinyWaveSurfer>();
            if (player == null)
                return;

            gateOpen = Time.time - startedAt >= escapeGateDelay;

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
            if (body != null)
                body.position = position;
            else
                player.transform.position = position;
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

        private void OnGUI()
        {
            if (encounterFinished || boss == null)
                return;

            EnsureStyles();
            float elapsed = Time.time - startedAt;
            string bossName = theme == ArenaTheme.Reaper ? "REAPER TIDE" : "DUCK STORM";
            string status = gateOpen
                ? (gateOnRight ? "ESCAPE ROUTE OPEN  >>>" : "<<<  ESCAPE ROUTE OPEN")
                : "ARENA SEALED  " + Mathf.CeilToInt(Mathf.Max(0f, escapeGateDelay - elapsed));

            Color old = GUI.color;
            GUI.color = new Color(0f, 0.05f, 0.09f, 0.82f);
            const float panelY = 320f;
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
