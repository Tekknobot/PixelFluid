using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DayTwoPredatorAnimation : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Sprite[] swimFrames;
        private Sprite[] attackFrames;
        private float swimFps;
        private float attackFps;
        private float attackSpeedMultiplier;
        private int frameIndex;
        private float frameTimer;
        private float nextAllowedAttackTime;
        private bool attacking;

        public bool IsAttacking => attacking;
        public bool IsInHitWindow => attacking && attackFrames != null && attackFrames.Length > 0 && frameIndex >= Mathf.Max(1, attackFrames.Length / 2);
        public float MovementSpeedMultiplier => attacking ? attackSpeedMultiplier : 1f;

        public void Configure(string swimPath, string attackPath, float swimFramesPerSecond, float attackFramesPerSecond, float movementMultiplier)
        {
            swimFrames = Resources.LoadAll<Sprite>(swimPath);
            attackFrames = Resources.LoadAll<Sprite>(attackPath);
            System.Array.Sort(swimFrames, CompareFrames);
            System.Array.Sort(attackFrames, CompareFrames);
            swimFps = Mathf.Max(1f, swimFramesPerSecond);
            attackFps = Mathf.Max(1f, attackFramesPerSecond);
            attackSpeedMultiplier = Mathf.Max(1f, movementMultiplier);
            frameIndex = 0;
            frameTimer = 0f;
            attacking = false;
            ShowFrame(swimFrames, 0);
        }

        private static int CompareFrames(Sprite a, Sprite b) => ExtractIndex(a != null ? a.name : "").CompareTo(ExtractIndex(b != null ? b.name : ""));
        private static int ExtractIndex(string value)
        {
            int split = value.LastIndexOf('_');
            return split >= 0 && int.TryParse(value.Substring(split + 1), out int index) ? index : 0;
        }

        private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        private void Update()
        {
            Sprite[] active = attacking ? attackFrames : swimFrames;
            if (active == null || active.Length == 0) return;
            frameTimer += Time.deltaTime;
            float duration = 1f / (attacking ? attackFps : swimFps);
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                frameIndex++;
                if (frameIndex >= active.Length)
                {
                    if (attacking)
                    {
                        attacking = false;
                        nextAllowedAttackTime = Time.time + 0.75f;
                        active = swimFrames;
                    }
                    frameIndex = 0;
                }
                ShowFrame(active, frameIndex);
            }
        }

        public bool Attack()
        {
            if (attacking || Time.time < nextAllowedAttackTime || attackFrames == null || attackFrames.Length == 0) return false;
            attacking = true;
            frameIndex = 0;
            frameTimer = 0f;
            ShowFrame(attackFrames, 0);
            return true;
        }

        private void ShowFrame(Sprite[] frames, int index)
        {
            if (spriteRenderer != null && frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }
    }
}
