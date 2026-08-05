using System;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class RaceSurferSkin : MonoBehaviour
    {
        [SerializeField] private string surferName = "Chuck";
        [SerializeField, Min(1f)] private float framesPerSecond = 12f;

        private TinyWaveSurfer surfer;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private Sprite[] idle;
        private Sprite[] move;
        private Sprite[] jump;
        private Sprite[] surfJump;
        private Sprite[] handstand;
        private Sprite[] flip;
        private Sprite[] rotation;
        private Sprite[] death;
        private Vector3 lastPosition;
        private float frameTimer;
        private int frame;
        private Sprite[] active;

        public void Configure(string name)
        {
            surferName = string.IsNullOrWhiteSpace(name) ? "Chuck" : name;
            LoadSheets();
        }

        private void Awake()
        {
            surfer = GetComponent<TinyWaveSurfer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            lastPosition = transform.position;
        }

        private void Start()
        {
            LoadSheets();
        }

        private void LoadSheets()
        {
            if (string.Equals(surferName, "Chuck", StringComparison.OrdinalIgnoreCase))
                return;

            string folder = "RaceSurfers/" + Capitalize(surferName) + "/" + surferName.ToLowerInvariant() + "_";
            idle = Load(folder + "idle");
            move = Load(folder + "move");
            jump = Load(folder + "jump");
            surfJump = Load(folder + "surf_jump");
            handstand = Load(folder + "handstand");
            flip = Load(folder + "flip");
            rotation = Load(folder + (string.Equals(surferName, "Josh", StringComparison.OrdinalIgnoreCase) ? "roation" : "rotation"));
            death = Load(folder + "death");

            if (animator != null)
                animator.enabled = false;
            active = idle;
            frame = 0;
            ApplyFrame();
        }

        private void Update()
        {
            if (string.Equals(surferName, "Chuck", StringComparison.OrdinalIgnoreCase) || spriteRenderer == null)
                return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            Sprite[] desired = ChooseAnimation(delta);
            if (desired == null || desired.Length == 0)
                desired = idle;
            if (desired == null || desired.Length == 0)
                return;

            if (!ReferenceEquals(active, desired))
            {
                active = desired;
                frame = 0;
                frameTimer = 0f;
            }

            frameTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(1f, framesPerSecond);
            while (frameTimer >= interval)
            {
                frameTimer -= interval;
                frame = (frame + 1) % active.Length;
            }
            ApplyFrame();
        }

        private Sprite[] ChooseAnimation(Vector3 delta)
        {
            if (surfer != null)
            {
                if (surfer.IsVisualAirTrickActive)
                {
                    float z = Mathf.Repeat(Mathf.Abs(surfer.VisualRotation.eulerAngles.z), 360f);
                    if (z > 45f && z < 315f) return rotation;
                    return flip != null && flip.Length > 0 ? flip : handstand;
                }
                if (surfer.IsVisualObstacleJumpActive) return surfJump;
                if (surfer.IsVisualSpecialSkidding) return move;
            }
            return Mathf.Abs(delta.x) > 0.002f ? move : idle;
        }

        private void ApplyFrame()
        {
            if (active != null && active.Length > 0 && spriteRenderer != null)
                spriteRenderer.sprite = active[Mathf.Clamp(frame, 0, active.Length - 1)];
        }

        private static Sprite[] Load(string path)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(path);
            Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
        }
    }
}
