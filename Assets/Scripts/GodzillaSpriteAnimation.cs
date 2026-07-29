using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GodzillaSpriteAnimation : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float swimFramesPerSecond = 7f;
        [SerializeField, Min(1f)] private float attackFramesPerSecond = 14f;

        private Sprite[] swimFrames;
        private Sprite[] attackFrames;
        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float frameTimer;
        private bool attacking;

        public bool IsAttacking => attacking;
        public bool IsInHitWindow => attacking && attackFrames != null && attackFrames.Length > 0 &&
                                     frameIndex >= Mathf.Max(2, attackFrames.Length / 2);
        public float AttackProgress => !attacking || attackFrames == null || attackFrames.Length <= 1
            ? 0f
            : frameIndex / (float)(attackFrames.Length - 1);

        public void SetFrames(Sprite[] movement, Sprite[] attack)
        {
            swimFrames = movement;
            attackFrames = attack;
            frameIndex = 0;
            frameTimer = 0f;
            ShowCurrentFrame();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            Animator animator = GetComponent<Animator>();
            if (animator != null)
                animator.enabled = false;
        }

        private void OnEnable()
        {
            attacking = false;
            frameIndex = 0;
            frameTimer = 0f;
            ShowCurrentFrame();
        }

        private void Update()
        {
            Sprite[] active = attacking ? attackFrames : swimFrames;
            if (active == null || active.Length == 0)
                return;

            float fps = attacking ? attackFramesPerSecond : swimFramesPerSecond;
            frameTimer += Time.deltaTime;
            float duration = 1f / Mathf.Max(1f, fps);

            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                frameIndex++;

                if (frameIndex >= active.Length)
                {
                    if (attacking)
                    {
                        attacking = false;
                        frameIndex = 0;
                        active = swimFrames;
                    }
                    else
                    {
                        frameIndex = 0;
                    }
                }

                ShowFrame(active, frameIndex);
            }
        }

        public bool Attack()
        {
            if (attacking || attackFrames == null || attackFrames.Length == 0)
                return false;

            attacking = true;
            frameIndex = 0;
            frameTimer = 0f;
            ShowFrame(attackFrames, 0);
            return true;
        }

        private void ShowCurrentFrame()
        {
            ShowFrame(attacking ? attackFrames : swimFrames, frameIndex);
        }

        private void ShowFrame(Sprite[] frames, int index)
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || frames == null || frames.Length == 0)
                return;

            spriteRenderer.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }
    }
}
