using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WhaleSpriteAnimation : MonoBehaviour
    {
        [SerializeField] private Sprite[] swimFrames;
        [SerializeField, Min(1f)] private float framesPerSecond = 8f;

        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float frameTimer;

        public void SetFrames(Sprite[] frames)
        {
            swimFrames = frames;
            frameIndex = 0;
            frameTimer = 0f;
            ShowFrame();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            frameIndex = 0;
            frameTimer = 0f;
            ShowFrame();
        }

        private void Update()
        {
            if (swimFrames == null || swimFrames.Length == 0)
                return;

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex = (frameIndex + 1) % swimFrames.Length;
                ShowFrame();
            }
        }

        private void ShowFrame()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || swimFrames == null || swimFrames.Length == 0)
                return;

            spriteRenderer.sprite = swimFrames[Mathf.Clamp(frameIndex, 0, swimFrames.Length - 1)];
        }
    }
}
