using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Lightweight one-shot runtime sprite-sheet explosion.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ExplosionBasicEffect : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float framesPerSecond = 22f;
        [SerializeField, Min(0.1f)] private float scale = 1.15f;
        [SerializeField] private int sortingOrder = 12030;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float frameClock;

        public static void Spawn(Vector3 worldPosition)
        {
            GameObject effect = new("Helicopter Missile Explosion");
            effect.transform.position = worldPosition;
            effect.AddComponent<SpriteRenderer>();
            effect.AddComponent<ExplosionBasicEffect>();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
            transform.localScale = Vector3.one * scale;

            frames = LoadFrames();
            if (frames.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            spriteRenderer.sprite = frames[0];
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
                return;

            frameClock += Time.deltaTime * framesPerSecond;
            int frameIndex = Mathf.FloorToInt(frameClock);
            if (frameIndex >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }

            spriteRenderer.sprite = frames[frameIndex];
        }

        private static Sprite[] LoadFrames()
        {
            Texture2D sheet = Resources.Load<Texture2D>("VFX/explosion_basic");
            if (sheet == null)
            {
                Debug.LogWarning("ExplosionBasicEffect could not load Resources/VFX/explosion_basic.");
                return System.Array.Empty<Sprite>();
            }

            const int frameSize = 64;
            int columns = Mathf.Max(1, sheet.width / frameSize);
            int rows = Mathf.Max(1, sheet.height / frameSize);
            Sprite[] result = new Sprite[columns * rows];
            int index = 0;

            // Read left-to-right, then top-to-bottom for multi-row compatibility.
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < columns; column++)
                {
                    result[index] = Sprite.Create(
                        sheet,
                        new Rect(column * frameSize, row * frameSize, frameSize, frameSize),
                        new Vector2(0.5f, 0.5f),
                        64f,
                        0,
                        SpriteMeshType.FullRect);
                    result[index].name = $"explosion_basic_{index:00}";
                    index++;
                }
            }

            return result;
        }
    }
}
