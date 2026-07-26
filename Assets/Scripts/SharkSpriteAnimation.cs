using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Lightweight frame animation and attack state for the inter-wave shark.
    /// This deliberately does not rely on an Animator Controller, so the prefab
    /// remains functional even when controller state assets are missing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SharkSpriteAnimation : MonoBehaviour
    {
        [Header("Frames")]
        [SerializeField] private Sprite[] swimFrames;
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField, Min(1f)] private float swimFramesPerSecond = 9f;
        [SerializeField, Min(1f)] private float attackFramesPerSecond = 12f;

        [Header("Attack Behaviour")]
        [SerializeField] private Vector2 randomAttackDelayRange = new(3.5f, 7.5f);
        [SerializeField, Min(0f)] private float attackCooldown = 1.25f;
        [SerializeField, Range(1f, 3f)] private float attackSpeedMultiplier = 1.55f;
        [SerializeField] private bool attackOnTriggerContact = true;
        [SerializeField] private bool attackWhenClicked = true;

        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float frameTimer;
        private float nextRandomAttackTime;
        private float nextAllowedAttackTime;
        private bool attacking;

        public bool IsAttacking => attacking;
        public float MovementSpeedMultiplier => attacking ? attackSpeedMultiplier : 1f;

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
            ShowFrame(swimFrames, 0);
            ScheduleRandomAttack();
        }

        private void Update()
        {
            if (!attacking && Time.time >= nextRandomAttackTime)
                Attack();

            Sprite[] activeFrames = attacking ? attackFrames : swimFrames;
            if (activeFrames == null || activeFrames.Length == 0)
                return;

            float fps = attacking ? attackFramesPerSecond : swimFramesPerSecond;
            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, fps);

            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;

                if (frameIndex >= activeFrames.Length)
                {
                    if (attacking)
                    {
                        FinishAttack();
                        activeFrames = swimFrames;
                    }
                    else
                    {
                        frameIndex = 0;
                    }
                }

                ShowFrame(activeFrames, frameIndex);
            }
        }

        public bool Attack()
        {
            if (attacking || Time.time < nextAllowedAttackTime ||
                attackFrames == null || attackFrames.Length == 0)
                return false;

            attacking = true;
            frameIndex = 0;
            frameTimer = 0f;
            ShowFrame(attackFrames, 0);
            return true;
        }

        private void FinishAttack()
        {
            attacking = false;
            frameIndex = 0;
            frameTimer = 0f;
            nextAllowedAttackTime = Time.time + attackCooldown;
            ShowFrame(swimFrames, 0);
            ScheduleRandomAttack();
        }

        private void ScheduleRandomAttack()
        {
            float minimum = Mathf.Max(0.1f, Mathf.Min(randomAttackDelayRange.x, randomAttackDelayRange.y));
            float maximum = Mathf.Max(minimum, Mathf.Max(randomAttackDelayRange.x, randomAttackDelayRange.y));
            nextRandomAttackTime = Time.time + Random.Range(minimum, maximum);
        }

        private void ShowFrame(Sprite[] frames, int index)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
                return;

            spriteRenderer.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (attackOnTriggerContact && other != null && !other.isTrigger)
                Attack();
        }

        private void OnMouseDown()
        {
            if (attackWhenClicked)
                Attack();
        }
    }
}
