using System;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HeartSpriteAnimation : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float framesPerSecond = 9f;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float frameTimer;
        private int frameIndex;

        public static Sprite LoadFirstFrame()
        {
            Sprite[] loaded = LoadFrames();
            return loaded.Length > 0 ? loaded[0] : null;
        }

        private static Sprite[] LoadFrames()
        {
            return Resources.LoadAll<Sprite>("Items/health_heart_float")
                .Where(sprite => sprite != null)
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            frames = LoadFrames();

            if (frames.Length == 0)
            {
                Debug.LogError("HeartSpriteAnimation could not load Resources/Items/health_heart_float.", this);
                enabled = false;
                return;
            }

            spriteRenderer.sprite = frames[0];
        }

        private void Update()
        {
            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);

            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex = (frameIndex + 1) % frames.Length;
                spriteRenderer.sprite = frames[frameIndex];
            }
        }
    }
}
