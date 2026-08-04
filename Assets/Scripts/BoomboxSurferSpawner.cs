using System.Collections;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class BoomboxSurferSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 1;
        [SerializeField, Min(0.05f)] private float scale = 1f;

        [Header("Player Music Summon")]
        [Tooltip("The story unlocks the music board at Strange Tide. Enable this to allow it immediately for testing.")]
        [SerializeField] private bool unlockedOnStart;
        [Tooltip("Horizontal distance from Chuck where the board appears.")]
        [SerializeField, Range(0.5f, 4f)] private float summonDistance = 1.65f;
        [Tooltip("Prevents one input press from being read twice.")]
        [SerializeField, Range(0.05f, 0.5f)] private float summonInputCooldown = 0.18f;

        private static BoomboxSurferSpawner instance;
        private static bool summoningUnlocked;
        private BoomboxSurferSwimmer activeBoard;
        private float nextInputTime;

        public static bool IsSummoningUnlocked => summoningUnlocked;
        public static bool IsBoardActive =>
            instance != null &&
            instance.activeBoard != null &&
            !instance.activeBoard.IsReleasing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
            summoningUnlocked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<BoomboxSurferSpawner>() != null)
                return;

            new GameObject("Boombox Surfer Summon Controller")
                .AddComponent<BoomboxSurferSpawner>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                // Story progression may create another spawner host. Keep one
                // authoritative input controller and discard duplicate hosts.
                Destroy(gameObject);
                return;
            }

            instance = this;

            if (unlockedOnStart)
                summoningUnlocked = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (!summoningUnlocked ||
                SurferSlugPauseMenu.GameplayPaused ||
                Time.unscaledTime < nextInputTime ||
                !SummonTogglePressed())
            {
                return;
            }

            nextInputTime =
                Time.unscaledTime + summonInputCooldown;

            ToggleMusicBoard();
        }

        /// <summary>
        /// Called by Strange Tide progression. It grants access without forcing
        /// the board to appear; the player decides when the music enters.
        /// </summary>
        public static void UnlockSummoning()
        {
            summoningUnlocked = true;

            if (instance == null)
            {
                GameObject host =
                    new("Boombox Surfer Summon Controller");
                instance = host.AddComponent<BoomboxSurferSpawner>();
            }
        }

        public static void LockAndRelease()
        {
            summoningUnlocked = false;
            instance?.ReleaseBoard();
        }


        /// <summary>
        /// Synchronizes music-board availability to the current story stage.
        /// Strange Tide and every later chapter allow LB / M summoning.
        /// Earlier chapters lock the mechanic and release an active board.
        /// </summary>
        public static bool RestoreForStage(
            int currentDay,
            SurfDayProgressionDirector.Chapter chapter,
            bool showHudNotice)
        {
            // Music is unlocked during Day 1's Strange Tide chapter and remains
            // permanently available on every later day. Day 2 restarts at Dawn,
            // so comparing only the chapter enum would incorrectly lock it again
            // when loading or beginning a later day.
            bool shouldBeUnlocked =
                currentDay >= 2 ||
                chapter >= SurfDayProgressionDirector.Chapter.StrangeTide;

            if (!shouldBeUnlocked)
            {
                LockAndRelease();
                return false;
            }

            bool newlyUnlocked = !summoningUnlocked;
            UnlockSummoning();

            if (showHudNotice)
            {
                SurferSlugMinimalHud.ShowNotice(
                    newlyUnlocked
                        ? "MUSIC BOARD UNLOCKED\nLB / M TO TOGGLE SUMMON"
                        : "MUSIC BOARD AVAILABLE\nLB / M TO TOGGLE SUMMON",
                    4.5f);
            }

            return true;
        }

        /// <summary>
        /// Developer-menu entry point. Ensures the summon controller exists,
        /// unlocks the mechanic, then summons or releases the music board.
        /// </summary>
        public static void DebugToggleBoard()
        {
            UnlockSummoning();

            if (instance != null)
                instance.ToggleMusicBoard();
        }

        [ContextMenu("Toggle Music Board")]
        public void ToggleMusicBoard()
        {
            if (activeBoard != null)
            {
                ReleaseBoard();
                return;
            }

            SpawnForPlayer();
        }

        // Kept for compatibility with older progression/developer calls.
        [ContextMenu("Spawn Boombox Surfer Once")]
        public void SpawnOnce()
        {
            summoningUnlocked = true;
            if (activeBoard == null)
                SpawnForPlayer();
        }

        private void SpawnForPlayer()
        {
            TinyWaveSurfer player =
                FindObjectsByType<TinyWaveSurfer>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(candidate =>
                        candidate != null &&
                        !candidate.IsDead)
                    .OrderByDescending(candidate =>
                        candidate.IsPlayerControlled)
                    .FirstOrDefault();

            if (player == null)
                return;

            Sprite[] frames =
                Resources.LoadAll<Sprite>("Boombox/boombox")
                    .OrderBy(sprite =>
                        FrameNumber(sprite.name))
                    .ToArray();

            AudioClip music =
                Resources.Load<AudioClip>(
                    "Audio/Music/Death Surfer");

            if (frames.Length == 0 || music == null)
            {
                Debug.LogError(
                    "Boombox surfer could not load its sprite frames or Death Surfer music.",
                    this);
                return;
            }

            GameObject swimmer =
                new("Summoned Boombox Surfboard - Death Surfer");

            swimmer.transform.SetParent(null, true);
            swimmer.transform.localScale =
                Vector3.one * scale;

            SpriteRenderer renderer =
                swimmer.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];

            swimmer.AddComponent<Rigidbody2D>();
            swimmer.AddComponent<InterWaveRenderItem>();
            swimmer.AddComponent<AudioSource>();

            CircleCollider2D interactionTrigger = swimmer.AddComponent<CircleCollider2D>();
            interactionTrigger.isTrigger = true;
            interactionTrigger.radius = 0.48f;

            swimmer.AddComponent<BoomboxSurferAnimation>()
                .SetFrames(frames);

            activeBoard =
                swimmer.AddComponent<BoomboxSurferSwimmer>();

            int lane = Mathf.Clamp(
                player.CurrentWaveIndex,
                0,
                Mathf.Max(0, player.WaveCount - 2));

            float side =
                player.TravelDirection >= 0f ? -1f : 1f;

            Vector3 summonPosition =
                player.transform.position +
                Vector3.right * side * summonDistance;

            swimmer.transform.position = summonPosition;

            activeBoard.InitialiseSummoned(
                lane,
                music,
                player.transform,
                summonPosition);

            activeBoard.Released += HandleBoardReleased;
        }

        private void ReleaseBoard()
        {
            if (activeBoard == null)
                return;

            activeBoard.BeginRelease();
        }

        private void HandleBoardReleased(
            BoomboxSurferSwimmer board)
        {
            if (board != null)
                board.Released -= HandleBoardReleased;

            if (activeBoard == board)
                activeBoard = null;
        }

        private static bool SummonTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboard =
                Keyboard.current != null &&
                Keyboard.current.mKey.wasPressedThisFrame;

            bool gamepad =
                Gamepad.current != null &&
                Gamepad.current.leftShoulder
                    .wasPressedThisFrame;

            return keyboard || gamepad;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return
                Input.GetKeyDown(KeyCode.M) ||
                Input.GetKeyDown(
                    KeyCode.JoystickButton4);
#else
            return false;
#endif
        }

        private static int FrameNumber(string name)
        {
            int separator = name.LastIndexOf('_');
            return separator >= 0 &&
                   int.TryParse(
                       name[(separator + 1)..],
                       out int number)
                ? number
                : int.MaxValue;
        }
    }
}
