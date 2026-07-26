using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class ProceduralStarryNight : MonoBehaviour
    {
        [Header("Generated Texture")]
        [SerializeField, Range(64, 1024)] private int textureWidth = 512;
        [SerializeField, Range(64, 1024)] private int textureHeight = 288;
        [SerializeField, Range(10, 600)] private int starCount = 180;
        [SerializeField] private int randomSeed = 7421;

        [Header("Sky Colours")]
        [SerializeField] private Color topSky = new(0.015f, 0.025f, 0.10f, 1f);
        [SerializeField] private Color horizonSky = new(0.07f, 0.10f, 0.24f, 1f);

        [Header("Stars")]
        [SerializeField] private Color dimStar = new(0.62f, 0.72f, 1f, 1f);
        [SerializeField] private Color brightStar = new(1f, 0.96f, 0.78f, 1f);
        [SerializeField, Range(0f, 1f)] private float largeStarChance = 0.09f;

        [Header("Placement")]
        [SerializeField] private Vector3 worldPosition = new(0f, 3.5f, 6f);
        [SerializeField] private Vector2 worldSize = new(18f, 10.125f);
        [SerializeField] private int sortingOrder = -500;

        [Header("Twinkle")]
        [SerializeField] private bool animateTwinkle = true;
        [SerializeField, Range(0.1f, 8f)] private float twinkleSpeed = 1.8f;
        [SerializeField, Range(0f, 0.8f)] private float twinkleStrength = 0.28f;

        private SpriteRenderer spriteRenderer;
        private Texture2D generatedTexture;
        private Sprite generatedSprite;
        private Color[] basePixels;
        private StarData[] stars;
        private float updateTimer;

        private struct StarData
        {
            public int X;
            public int Y;
            public int Radius;
            public float Phase;
            public Color Colour;
        }

        private void Awake() => BuildNightSky();

        private void Update()
        {
            if (!animateTwinkle || generatedTexture == null || stars == null)
                return;

            updateTimer += Time.deltaTime;
            if (updateTimer < 0.08f)
                return;

            updateTimer = 0f;
            RenderStars(Time.time);
        }

        [ContextMenu("Rebuild Starry Night")]
        public void BuildNightSky()
        {
            CleanupGeneratedAssets();
            transform.position = worldPosition;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            generatedTexture = new Texture2D(
                textureWidth,
                textureHeight,
                TextureFormat.RGBA32,
                false)
            {
                name = "Procedural Starry Night Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            basePixels = new Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                float t = y / Mathf.Max(1f, textureHeight - 1f);
                Color rowColour = Color.Lerp(horizonSky, topSky, t);

                for (int x = 0; x < textureWidth; x++)
                    basePixels[y * textureWidth + x] = rowColour;
            }

            Random.State previousState = Random.state;
            Random.InitState(randomSeed);
            stars = new StarData[starCount];

            for (int i = 0; i < starCount; i++)
            {
                float heightBias = Mathf.Pow(Random.value, 0.72f);
                int x = Random.Range(2, textureWidth - 2);
                int y = Mathf.Clamp(
                    Mathf.RoundToInt(heightBias * (textureHeight - 6)) + 3,
                    3,
                    textureHeight - 3);

                bool large = Random.value < largeStarChance;
                Color colour = Color.Lerp(dimStar, brightStar, Random.Range(0.2f, 1f));

                stars[i] = new StarData
                {
                    X = x,
                    Y = y,
                    Radius = large ? Random.Range(2, 4) : 1,
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    Colour = colour
                };
            }

            Random.state = previousState;
            RenderStars(0f);

            generatedSprite = Sprite.Create(
                generatedTexture,
                new Rect(0f, 0f, textureWidth, textureHeight),
                new Vector2(0.5f, 0.5f),
                textureWidth / worldSize.x,
                0,
                SpriteMeshType.FullRect);

            generatedSprite.name = "Procedural Starry Night Sprite";
            spriteRenderer.sprite = generatedSprite;
            spriteRenderer.sortingOrder = sortingOrder;

            transform.localScale = new Vector3(
                worldSize.x / (generatedSprite.rect.width / generatedSprite.pixelsPerUnit),
                worldSize.y / (generatedSprite.rect.height / generatedSprite.pixelsPerUnit),
                1f);
        }

        private void RenderStars(float time)
        {
            Color[] pixels = new Color[basePixels.Length];
            System.Array.Copy(basePixels, pixels, basePixels.Length);

            foreach (StarData star in stars)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(time * twinkleSpeed + star.Phase);
                float brightness = Mathf.Lerp(1f - twinkleStrength, 1f, wave);
                Color colour = star.Colour * brightness;
                colour.a = 1f;
                DrawStar(pixels, star.X, star.Y, star.Radius, colour);
            }

            generatedTexture.SetPixels(pixels);
            generatedTexture.Apply(false, false);
        }

        private void DrawStar(Color[] pixels, int x, int y, int radius, Color colour)
        {
            SetPixel(pixels, x, y, colour);
            if (radius <= 1) return;

            Color arm = Color.Lerp(basePixels[y * textureWidth + x], colour, 0.72f);
            SetPixel(pixels, x - 1, y, arm);
            SetPixel(pixels, x + 1, y, arm);
            SetPixel(pixels, x, y - 1, arm);
            SetPixel(pixels, x, y + 1, arm);

            if (radius >= 3)
            {
                Color outer = Color.Lerp(basePixels[y * textureWidth + x], colour, 0.38f);
                SetPixel(pixels, x - 2, y, outer);
                SetPixel(pixels, x + 2, y, outer);
                SetPixel(pixels, x, y - 2, outer);
                SetPixel(pixels, x, y + 2, outer);
            }
        }

        private void SetPixel(Color[] pixels, int x, int y, Color colour)
        {
            if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight)
                return;

            pixels[y * textureWidth + x] = colour;
        }

        private void CleanupGeneratedAssets()
        {
            if (generatedSprite != null)
            {
                if (Application.isPlaying) Destroy(generatedSprite);
                else DestroyImmediate(generatedSprite);
            }

            if (generatedTexture != null)
            {
                if (Application.isPlaying) Destroy(generatedTexture);
                else DestroyImmediate(generatedTexture);
            }

            generatedSprite = null;
            generatedTexture = null;
        }

        private void OnDestroy() => CleanupGeneratedAssets();
    }

    public static class ProceduralStarryNightBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateStarryNight()
        {
            if (Object.FindFirstObjectByType<ProceduralStarryNight>() != null)
                return;

            GameObject night = new("Procedural Starry Night");
            night.AddComponent<ProceduralStarryNight>();
        }
    }
}