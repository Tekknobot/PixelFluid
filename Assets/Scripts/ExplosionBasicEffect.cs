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
        private AudioSource explosionAudioSource;
        private Sprite[] frames;
        private float frameClock;
        private bool visualFinished;

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
            PlayExplosionSound();
        }

        private void Update()
        {
            if (!visualFinished && frames != null && frames.Length > 0)
            {
                frameClock += Time.deltaTime * framesPerSecond;
                int frameIndex = Mathf.FloorToInt(frameClock);
                if (frameIndex >= frames.Length)
                {
                    visualFinished = true;
                    spriteRenderer.enabled = false;
                }
                else
                {
                    spriteRenderer.sprite = frames[frameIndex];
                }
            }

            if (visualFinished && (explosionAudioSource == null || !explosionAudioSource.isPlaying))
                Destroy(gameObject);
        }

        private void PlayExplosionSound()
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/SFX/explosion_8bit");
            if (clip == null)
            {
                Debug.LogWarning("ExplosionBasicEffect could not load Resources/Audio/SFX/explosion_8bit.");
                return;
            }

            explosionAudioSource = gameObject.AddComponent<AudioSource>();
            explosionAudioSource.playOnAwake = false;
            explosionAudioSource.loop = false;
            explosionAudioSource.clip = clip;
            explosionAudioSource.spatialBlend = 1f;
            explosionAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            explosionAudioSource.minDistance = 2.5f;
            explosionAudioSource.maxDistance = 32f;
            explosionAudioSource.dopplerLevel = 0f;
            explosionAudioSource.volume = 1f;
            explosionAudioSource.Play();
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
