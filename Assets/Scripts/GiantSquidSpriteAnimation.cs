using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GiantSquidSpriteAnimation : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float swimFramesPerSecond = 7f;
        [SerializeField, Min(1f)] private float attackFramesPerSecond = 18f;
        [SerializeField, Range(2, 5)] private int comboCycles = 1;
        [SerializeField, Range(1f, 3f)] private float attackSpeedMultiplier = 1.7f;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int frameIndex;
        private int completedAttackCycles;
        private float frameTimer;
        private bool attacking;

        public bool IsAttacking => attacking;
        public float MovementSpeedMultiplier => attacking ? attackSpeedMultiplier : 1f;

        // Three broad strike windows per animation cycle. The swimmer still
        // applies damage only once for the entire combo.
        public bool IsInStrikeWindow
        {
            get
            {
                if (!attacking || frames == null || frames.Length == 0)
                    return false;

                float phase = (float)frameIndex / frames.Length;
                return (phase >= 0.18f && phase <= 0.32f) ||
                       (phase >= 0.48f && phase <= 0.62f) ||
                       (phase >= 0.76f && phase <= 0.92f);
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            frames = Resources.LoadAll<Sprite>("Squid/giant_squid_move")
                .OrderBy(GetFrameNumber)
                .ToArray();

            if (frames.Length == 0)
                Debug.LogError("Could not load Resources/Squid/giant_squid_move sprite frames.", this);
        }

        private void OnEnable()
        {
            attacking = false;
            frameIndex = 0;
            completedAttackCycles = 0;
            frameTimer = 0f;
            ShowFrame();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
                return;

            float fps = attacking ? attackFramesPerSecond : swimFramesPerSecond;
            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, fps);

            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;

                if (frameIndex >= frames.Length)
                {
                    frameIndex = 0;
                    if (attacking)
                    {
                        completedAttackCycles++;
                        if (completedAttackCycles >= comboCycles)
                            FinishAttack();
                    }
                }

                ShowFrame();
            }
        }

        public bool Attack()
        {
            if (attacking || frames == null || frames.Length == 0)
                return false;

            attacking = true;
            frameIndex = 0;
            completedAttackCycles = 0;
            frameTimer = 0f;
            ShowFrame();
            return true;
        }

        private void FinishAttack()
        {
            attacking = false;
            completedAttackCycles = 0;
            frameIndex = 0;
            frameTimer = 0f;
        }

        private void ShowFrame()
        {
            if (spriteRenderer != null && frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        }

        private static int GetFrameNumber(Sprite sprite)
        {
            if (sprite == null || string.IsNullOrEmpty(sprite.name))
                return 0;

            int underscore = sprite.name.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(sprite.name.Substring(underscore + 1), out int value)
                ? value
                : 0;
        }
    }
}
