using System;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class RaceSurferSkin : MonoBehaviour
    {
        [SerializeField] private string surferName = "Chuck";
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;

        private static readonly int IdleStateHash = Animator.StringToHash("Idle");
        private static readonly int MoveStateHash = Animator.StringToHash("chuck_move");
        private static readonly int JumpStateHash = Animator.StringToHash("chuck_jump");
        private static readonly int WaveSwitchStateHash = Animator.StringToHash("chuck_wave_switch");
        private static readonly int SurfJumpStateHash = Animator.StringToHash("chuck_surf_jump");
        private static readonly int HandstandStateHash = Animator.StringToHash("chuck_handstand");
        private static readonly int FlipStateHash = Animator.StringToHash("chuck_flip");
        private static readonly int RotationStateHash = Animator.StringToHash("chuck_rotation");
        private static readonly int DeathStateHash = Animator.StringToHash("chuck_death");
        private static readonly int ProneStateHash = Animator.StringToHash("chuck_prone");

        private TinyWaveSurfer surfer;
        private SpriteRenderer spriteRenderer;
        private Sprite[] idle;
        private Sprite[] move;
        private Sprite[] jump;
        private Sprite[] waveSwitch;
        private Sprite[] surfJump;
        private Sprite[] handstand;
        private Sprite[] flip;
        private Sprite[] rotation;
        private Sprite[] death;
        private Sprite[] active;
        private float frameTimer;
        private int frame;
        private int lastStateHash;

        public void Configure(string name)
        {
            surferName = string.IsNullOrWhiteSpace(name) ? "Chuck" : name;
            LoadSheets();
        }

        private void Awake()
        {
            surfer = GetComponent<TinyWaveSurfer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            LoadSheets();
        }

        private void LoadSheets()
        {
            if (string.Equals(surferName, "Chuck", StringComparison.OrdinalIgnoreCase))
                return;

            string lower = surferName.ToLowerInvariant();
            string folder = "RaceSurfers/" + Capitalize(surferName) + "/" + lower + "_";

            idle = Load(folder + "idle");
            move = Load(folder + "move");
            jump = Load(folder + "jump");
            waveSwitch = Load(folder + "wave_switch");
            surfJump = Load(folder + "surf_jump");
            handstand = Load(folder + "handstand");
            flip = Load(folder + "flip");
            rotation = Load(folder +
                (string.Equals(surferName, "Josh", StringComparison.OrdinalIgnoreCase)
                    ? "roation"
                    : "rotation"));
            death = Load(folder + "death");

            active = FirstAvailable(idle, move);
            frame = 0;
            frameTimer = 0f;
            ApplyFrame();
        }

        // Run after TinyWaveSurfer and Animator have updated. Chuck's Animator remains
        // enabled as the authoritative player/AI animation state machine; this component
        // only replaces the final rendered sprite with the selected surfer's equivalent.
        private void LateUpdate()
        {
            if (string.Equals(surferName, "Chuck", StringComparison.OrdinalIgnoreCase) ||
                spriteRenderer == null ||
                surfer == null)
            {
                return;
            }

            surfer.TryGetVisualAnimationSnapshot(
                out int stateHash,
                out float normalizedTime,
                out float playbackSpeed,
                out bool flipX);

            Sprite[] desired = ChooseAnimation(stateHash);
            if (desired == null || desired.Length == 0)
                desired = FirstAvailable(idle, move);

            if (desired == null || desired.Length == 0)
                return;

            if (!ReferenceEquals(active, desired) || lastStateHash != stateHash)
            {
                active = desired;
                lastStateHash = stateHash;
                frame = 0;
                frameTimer = 0f;
            }

            // When the Animator can provide timing, mirror Chuck's exact clip progress.
            // Otherwise keep the replacement sheet moving at its configured FPS.
            if (normalizedTime > 0f && active.Length > 0)
            {
                float looped = Mathf.Repeat(normalizedTime, 1f);
                frame = Mathf.Clamp(
                    Mathf.FloorToInt(looped * active.Length),
                    0,
                    active.Length - 1);
            }
            else
            {
                frameTimer += Time.deltaTime * Mathf.Max(0.01f, playbackSpeed);
                float interval = 1f / Mathf.Max(1f, framesPerSecond);
                while (frameTimer >= interval)
                {
                    frameTimer -= interval;
                    frame = (frame + 1) % active.Length;
                }
            }

            spriteRenderer.flipX = flipX;
            ApplyFrame();
        }

        private Sprite[] ChooseAnimation(int stateHash)
        {
            if (stateHash == HandstandStateHash)
                return FirstAvailable(handstand, rotation, flip, jump);
            if (stateHash == FlipStateHash)
                return FirstAvailable(flip, rotation, handstand, jump);
            if (stateHash == RotationStateHash)
                return FirstAvailable(rotation, flip, handstand, jump);
            if (stateHash == SurfJumpStateHash)
                return FirstAvailable(surfJump, jump, move);
            if (stateHash == JumpStateHash)
                return FirstAvailable(jump, surfJump, move);
            if (stateHash == WaveSwitchStateHash)
                return FirstAvailable(waveSwitch, jump, move);
            if (stateHash == DeathStateHash)
                return FirstAvailable(death, idle);
            if (stateHash == MoveStateHash)
                return FirstAvailable(move, idle);
            if (stateHash == ProneStateHash || stateHash == IdleStateHash)
                return FirstAvailable(idle, move);

            return FirstAvailable(idle, move);
        }

        private void ApplyFrame()
        {
            if (active != null && active.Length > 0 && spriteRenderer != null)
                spriteRenderer.sprite = active[Mathf.Clamp(frame, 0, active.Length - 1)];
        }

        private static Sprite[] FirstAvailable(params Sprite[][] choices)
        {
            if (choices == null)
                return null;

            foreach (Sprite[] choice in choices)
            {
                if (choice != null && choice.Length > 0)
                    return choice;
            }

            return null;
        }

        private static Sprite[] Load(string path)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(path);
            Array.Sort(sprites, (a, b) => ExtractFrameNumber(a.name)
                .CompareTo(ExtractFrameNumber(b.name)));
            return sprites;
        }

        private static int ExtractFrameNumber(string name)
        {
            if (string.IsNullOrEmpty(name))
                return 0;

            int split = name.LastIndexOf('_');
            return split >= 0 &&
                   int.TryParse(name.Substring(split + 1), out int value)
                ? value
                : 0;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return char.ToUpperInvariant(value[0]) +
                   value.Substring(1).ToLowerInvariant();
        }
    }
}
