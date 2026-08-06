using System.Collections;
using System.Collections.Generic;
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
        public static bool IsActive => Active != null && Active.encounterStarted && !Active.encounterFinished;

        public float LeftBoundary => leftX + edgePadding;
        public float RightBoundary => rightX - edgePadding;
        public float CameraLeftBoundary => leftX;
        public float CameraRightBoundary => rightX;
        public float CentreX => centreX;

        /// <summary>Returns true only when this arena is controlling the supplied boss.</summary>
        public bool ControlsBoss(MonoBehaviour candidate) =>
            candidate != null && boss == candidate && !encounterFinished;

        public enum ArenaTheme { Reaper, RubberDuck }

        [Header("Arena")]
        [SerializeField] private ArenaTheme theme = ArenaTheme.Reaper;
        [SerializeField, Min(6f)] private float arenaWidth = 18f;
        [Tooltip("Arena width measured in visible camera widths. Values above 1 allow meaningful camera travel.")]
        [SerializeField, Range(1.15f, 3f)] private float arenaWidthInCameraWidths = 1.85f;
        [SerializeField, Min(0.25f)] private float edgePadding = 0.8f;
        [SerializeField, Min(0.25f)] private float escapeDistance = 1.25f;
        [SerializeField, Min(0f)] private float introLockDuration = 1.25f;

        [Header("Boss Entrance")]
        [SerializeField, Min(0.5f)] private float reaperEntranceSpeed = 8.5f;
        [SerializeField, Min(0.5f)] private float rubberDuckEntranceSpeed = 10f;
        [SerializeField, Range(0.12f, 0.42f)] private float entranceArrivalFromCentre = 0.28f;
        [Tooltip("Places the boss visibly inside the arena edge before its entrance begins.")]
        [SerializeField, Range(0.25f, 2.5f)] private float arenaSpawnInset = 0.9f;
        [Tooltip("Time used to reveal the boss after it has been placed in the arena.")]
        [SerializeField, Range(0.1f, 3f)] private float bossFadeInDuration = 1.0f;

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
        private bool encounterStarted;
        private bool entranceStarted;
        private bool eventsSubscribed;
        private int reaperHits;
        private int ducklingsDestroyed;
        private int duckDamagePhases;
        private bool duckWindowOpen;
        private GUIStyle titleStyle;
        private GUIStyle smallStyle;

        public void Configure(MonoBehaviour bossBehaviour, ArenaTheme arenaTheme)
        {
            if (bossBehaviour == null)
                return;

            // The spawner and progression director can both discover the same arena.
            // Do not restart an entrance that is already correctly configured.
            if (ControlsBoss(bossBehaviour) && entranceStarted)
                return;

            UnsubscribeBossEvents();

            boss = bossBehaviour;
            theme = arenaTheme;

            // A stale arena from a previous/rebuilt encounter must be reusable.
            reaperBoss = null;
            duckBoss = null;
            gateOpen = false;
            encounterFinished = false;
            encounterStarted = false;
            entranceStarted = false;
            eventsSubscribed = false;
            reaperHits = 0;
            ducklingsDestroyed = 0;
            duckDamagePhases = 0;
            duckWindowOpen = false;

            CaptureArena();
            EnsureBossHealthBar();
            BeginBossEntrance();
        }

        private void Awake()
        {
            Active = this;
        }

        private void Start()
        {
            if (boss != null && !entranceStarted)
            {
                CaptureArena();
                BeginBossEntrance();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeBossEvents();
            if (Active == this)
                Active = null;
        }

        private void SubscribeBossEvents()
        {
            if (eventsSubscribed)
                return;

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

            eventsSubscribed = true;
        }

        private void UnsubscribeBossEvents()
        {
            if (!eventsSubscribed)
                return;

            if (reaperBoss != null)
                reaperBoss.ArenaHitAccepted -= OnReaperHitAccepted;
            if (duckBoss != null)
                duckBoss.ArenaHitAccepted -= OnDuckHitAccepted;
            RubberDucklingSwimmer.DestroyedByProjectile -= OnDucklingDestroyed;
            reaperBoss = null;
            duckBoss = null;
            eventsSubscribed = false;
        }

        private void BeginBossEntrance()
        {
            if (boss == null || entranceStarted)
                return;

            entranceStarted = true;
            encounterStarted = false;

            /*
             * Boss spawners normally create their swimmers beyond the camera so
             * ordinary encounters can enter naturally. An arena encounter is
             * different: the arena can lock the camera before that swimmer ever
             * reaches it. Place the boss just inside the captured arena first,
             * then let its normal entrance movement carry it farther inward.
             */
            float side = boss.transform.position.x < centreX ? -1f : 1f;
            float inset = Mathf.Clamp(
                arenaSpawnInset,
                0.25f,
                Mathf.Max(0.25f, arenaWidth * 0.2f));

            float startX = side < 0f
                ? LeftBoundary + inset
                : RightBoundary - inset;

            float arrivalX =
                centreX + side * arenaWidth * entranceArrivalFromCentre;

            PlaceBossAtArenaX(startX);
            StartCoroutine(FadeBossIntoArena());

            reaperBoss = boss as GodzillaLaneSwimmer;
            duckBoss = boss as RubberDuckBossSwimmer;

            if (reaperBoss != null)
            {
                reaperBoss.BeginArenaEntrance(arrivalX, reaperEntranceSpeed);
                return;
            }

            if (duckBoss != null)
            {
                duckBoss.BeginArenaEntrance(arrivalX, rubberDuckEntranceSpeed);
                return;
            }

            BeginEncounter();
        }

        private void PlaceBossAtArenaX(float worldX)
        {
            if (boss == null)
                return;

            Vector3 position = boss.transform.position;
            position.x = Mathf.Clamp(worldX, LeftBoundary, RightBoundary);

            Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = new Vector2(position.x, position.y);
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            else
            {
                boss.transform.position = position;
            }
        }

        private IEnumerator FadeBossIntoArena()
        {
            if (boss == null)
                yield break;

            SpriteRenderer[] renderers =
                boss.GetComponentsInChildren<SpriteRenderer>(true);

            if (renderers == null || renderers.Length == 0)
                yield break;

            List<Color> targetColours =
                new List<Color>(renderers.Length);

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                Color target = renderer != null
                    ? renderer.color
                    : Color.white;

                targetColours.Add(target);

                if (renderer != null)
                {
                    // Boss spawners may keep the renderer disabled while the boss
                    // is still at its ordinary off-screen entry point. It becomes
                    // visible only after PlaceBossAtArenaX has put it inside.
                    renderer.enabled = true;
                    renderer.color = new Color(
                        target.r,
                        target.g,
                        target.b,
                        0f);
                }
            }

            float duration = Mathf.Max(0.1f, bossFadeInDuration);
            float elapsed = 0f;

            while (elapsed < duration && boss != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));

                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    Color target = targetColours[i];
                    renderer.color = new Color(
                        target.r,
                        target.g,
                        target.b,
                        target.a * t);
                }

                yield return null;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = targetColours[i];
            }
        }

        private bool IsBossStillEntering()
        {
            if (reaperBoss != null)
                return reaperBoss.IsArenaEntranceActive;
            if (duckBoss != null)
                return duckBoss.IsArenaEntranceActive;
            return false;
        }

        private void BeginEncounter()
        {
            if (encounterStarted || encounterFinished)
                return;

            encounterStarted = true;
            startedAt = Time.time;
            SubscribeBossEvents();
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


        private void EnsureBossHealthBar()
        {
            if (boss == null)
                return;

            BossHealthBar healthBar = boss.GetComponent<BossHealthBar>();
            if (healthBar == null)
                healthBar = boss.gameObject.AddComponent<BossHealthBar>();

            healthBar.Bind(boss);
        }

        private void CaptureArena()
        {
            gameplayCamera = Camera.main;
            player = FindPlayerControlledSurfer();
            centreX = player != null ? player.transform.position.x : transform.position.x;

            if (gameplayCamera != null && gameplayCamera.orthographic)
            {
                float cameraWidth = gameplayCamera.orthographicSize * 2f * gameplayCamera.aspect;
                float desiredWidth = cameraWidth * Mathf.Max(1.85f, arenaWidthInCameraWidths);
                arenaWidth = Mathf.Max(6f, arenaWidth, desiredWidth);
            }

            leftX = centreX - arenaWidth * 0.5f;
            rightX = centreX + arenaWidth * 0.5f;
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

            if (!encounterStarted)
            {
                if (!entranceStarted)
                    BeginBossEntrance();
                if (!IsBossStillEntering())
                    BeginEncounter();
                return;
            }

            if (duckWindowOpen && duckBoss != null && !duckBoss.IsVulnerable)
            {
                duckWindowOpen = false;
                duckBoss.CloseArenaVulnerability();
            }

            if (player == null || player.IsDead || !player.IsPlayerControlled)
                player = FindPlayerControlledSurfer();
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

        private static TinyWaveSurfer FindPlayerControlledSurfer()
        {
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (surfer != null && surfer.IsPlayerControlled)
                    return surfer;
            }

            return null;
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
                fontSize = 64,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Medium,
                fontSize = 32,
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
            if (!encounterStarted || encounterFinished || boss == null)
                return;

            // Keep only the arena boundary markers. The old IMGUI boss panel
            // ("REAPER TIDE" / "DUCK STORM" and status copy) is intentionally
            // disabled for both boss themes so it cannot display the legacy font.
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
