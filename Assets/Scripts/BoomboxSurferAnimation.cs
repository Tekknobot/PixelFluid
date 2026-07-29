using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BoomboxSurferAnimation : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float framesPerSecond = 10f;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float frameClock;
        private int frameIndex;

        public void SetFrames(Sprite[] animationFrames)
        {
            frames = animationFrames;
            frameIndex = 0;
            frameClock = 0f;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (frames != null && frames.Length > 0)
                spriteRenderer.sprite = frames[0];
        }

        private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (frames == null || frames.Length < 2)
                return;

            frameClock += Time.deltaTime * framesPerSecond;
            int nextFrame = Mathf.FloorToInt(frameClock) % frames.Length;
            if (nextFrame == frameIndex)
                return;

            frameIndex = nextFrame;
            spriteRenderer.sprite = frames[frameIndex];
        }
    }
}
