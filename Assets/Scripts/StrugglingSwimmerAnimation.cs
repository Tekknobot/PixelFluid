using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StrugglingSwimmerAnimation : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float framesPerSecond = 10f;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float frameTimer;
        private int frameIndex;

        public static Sprite[] LoadFrames()
        {
            return Resources.LoadAll<Sprite>("Items/struggling_swimmer")
                .Where(sprite => sprite != null)
                .OrderBy(sprite => ParseFrameNumber(sprite.name))
                .ToArray();
        }

        public static Sprite LoadFirstFrame()
        {
            Sprite[] loaded = LoadFrames();
            return loaded.Length > 0 ? loaded[0] : null;
        }

        private static int ParseFrameNumber(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(spriteName[(underscore + 1)..], out int value)
                ? value
                : 0;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            frames = LoadFrames();
            if (frames.Length > 0)
                spriteRenderer.sprite = frames[0];
        }

        private void Update()
        {
            if (frames == null || frames.Length < 2)
                return;

            frameTimer += Time.deltaTime * framesPerSecond;
            int nextFrame = Mathf.FloorToInt(frameTimer) % frames.Length;
            if (nextFrame == frameIndex)
                return;

            frameIndex = nextFrame;
            spriteRenderer.sprite = frames[frameIndex];
        }
    }
}
