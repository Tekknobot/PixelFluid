using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Camera-following pixel horizon mask. The lower portion is fully opaque so
    /// distant scenery can never show through the ocean, while the upper edge is
    /// broken into animated mist bands to soften the horizon.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralHorizonFog : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("World-space height where the soft top of the fog begins.")]
        [SerializeField] private float horizonY = 0.55f;
        [Tooltip("Extra width beyond both camera edges.")]
        [SerializeField, Min(0f)] private float horizontalPadding = 3f;
        [Tooltip("Extra depth below the camera so the mask always covers the ocean.")]
        [SerializeField, Min(0f)] private float lowerPadding = 3f;
        [Tooltip("Render above distant scenery but below the water simulations.")]
        [SerializeField] private int sortingOrder = 0;

        [Header("Pixel Fog")]
        [SerializeField, Range(32, 512)] private int textureWidth = 256;
        [SerializeField, Range(16, 256)] private int textureHeight = 96;
        [SerializeField, Range(2, 32)] private int pixelBlockSize = 4;
        [SerializeField] private int randomSeed = 8917;
        [SerializeField, Range(0.03f, 0.5f)] private float refreshInterval = 0.12f;
        [SerializeField, Range(0f, 2f)] private float driftSpeed = 0.20f;
        [SerializeField, Range(0.05f, 0.8f)] private float softBandFraction = 0.34f;
        [SerializeField, Range(0f, 0.5f)] private float edgeNoise = 0.16f;
        [SerializeField, Range(0f, 1f)] private float mistStrength = 0.72f;

        [Header("Colours")]
        [SerializeField] private Color nightHorizonColour = new(0.025f, 0.11f, 0.15f, 1f);
        [SerializeField] private Color dayHorizonColour = new(0.17f, 0.48f, 0.58f, 1f);
        [SerializeField] private Color nightDeepWaterColour = new(0.015f, 0.12f, 0.16f, 1f);
        [SerializeField] private Color dayDeepWaterColour = new(0.03f, 0.34f, 0.43f, 1f);
        [SerializeField, Range(0f, 1f)] private float colourFollowStrength = 0.8f;

        private SpriteRenderer spriteRenderer;
        private Texture2D texture;
        private Sprite sprite;
        private Color[] pixels;
        private Camera gameplayCamera;
        private ProceduralStarryNight dayNight;
        private float timer;
        private float phase;

        private void Awake()
        {
            gameplayCamera = Camera.main;
            dayNight = FindFirstObjectByType<ProceduralStarryNight>();
            BuildResources();
            Redraw();
        }

        private void OnEnable()
        {
            FitToCamera();
        }

        private void LateUpdate()
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (dayNight == null)
                dayNight = FindFirstObjectByType<ProceduralStarryNight>();

            FitToCamera();

            phase += Time.deltaTime * driftSpeed;
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = refreshInterval;
                Redraw();
            }
        }

        private void BuildResources()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                name = "Procedural Horizon Fog Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            pixels = new Color[textureWidth * textureHeight];
            sprite = Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight),
                new Vector2(0.5f, 1f), textureHeight, 0, SpriteMeshType.FullRect);
            sprite.name = "Procedural Horizon Fog Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;

            spriteRenderer.sprite = sprite;

            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
        }

        private void FitToCamera()
        {
            if (gameplayCamera == null || spriteRenderer == null)
                return;

            float cameraHeight = gameplayCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * gameplayCamera.aspect;
            float bottomY = gameplayCamera.transform.position.y - gameplayCamera.orthographicSize - lowerPadding;
            float requiredHeight = Mathf.Max(0.5f, horizonY - bottomY);

            transform.position = new Vector3(gameplayCamera.transform.position.x, horizonY, 0f);
            transform.localScale = new Vector3(cameraWidth + horizontalPadding * 2f, requiredHeight, 1f);
        }

        private void Redraw()
        {
            if (texture == null || pixels == null)
                return;

            float daylight = 0f;

            if (dayNight != null)
            {
                daylight = Mathf.Clamp01(
                    (Mathf.Cos((dayNight.TimeOfDay - 0.5f) * Mathf.PI * 2f) + 1f) *
                    0.5f
                );
            }

            daylight *= colourFollowStrength;

            Color horizon = Color.Lerp(
                nightHorizonColour,
                dayHorizonColour,
                daylight
            );

            Color deep = Color.Lerp(
                nightDeepWaterColour,
                dayDeepWaterColour,
                daylight
            );

            int block = Mathf.Max(1, pixelBlockSize);
            int softRows = Mathf.Max(
                2,
                Mathf.RoundToInt(textureHeight * softBandFraction)
            );

            System.Random random = new System.Random(randomSeed);
            float seedA = (float)random.NextDouble() * 10f;
            float seedB = (float)random.NextDouble() * 10f;

            for (int by = 0; by < textureHeight; by += block)
            {
                /*
                * Unity texture coordinates start at the bottom:
                *
                * by = 0                 bottom
                * by = textureHeight - 1 top
                *
                * Convert that into a distance measured downward from the top.
                */
                float yFromTop = (textureHeight - 1) - by;

                for (int bx = 0; bx < textureWidth; bx += block)
                {
                    float x01 = bx / (float)Mathf.Max(1, textureWidth - 1);

                    float n1 = Mathf.PerlinNoise(
                        x01 * 7f + seedA + phase,
                        phase * 0.23f
                    );

                    float n2 = Mathf.PerlinNoise(
                        x01 * 17f + seedB - phase * 0.45f,
                        phase * 0.11f
                    );

                    float brokenEdge =
                        (n1 * 0.7f + n2 * 0.3f - 0.5f) *
                        edgeNoise *
                        softRows;

                    /*
                    * At the top:
                    * fogDepth is around zero and therefore transparent.
                    *
                    * Moving downward:
                    * fogDepth reaches one and becomes fully opaque.
                    */
                    float fogDepth = (yFromTop - brokenEdge) / softRows;

                    float alpha;

                    if (fogDepth >= 1f)
                    {
                        alpha = 1f;
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(
                            Mathf.SmoothStep(0f, 1f, fogDepth) *
                            mistStrength
                        );
                    }

                    float verticalFromTop =
                        yFromTop /
                        (float)Mathf.Max(1, textureHeight - 1);

                    Color colour = Color.Lerp(
                        horizon,
                        deep,
                        Mathf.SmoothStep(0f, 1f, verticalFromTop)
                    );

                    colour.a = alpha;

                    int maxY = Mathf.Min(textureHeight, by + block);
                    int maxX = Mathf.Min(textureWidth, bx + block);

                    for (int y = by; y < maxY; y++)
                    {
                        for (int x = bx; x < maxX; x++)
                        {
                            pixels[y * textureWidth + x] = colour;
                        }
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (sprite != null) Destroy(sprite);
                if (texture != null) Destroy(texture);
            }
        }
    }

    public static class ProceduralHorizonFogBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateFog()
        {
            if (Object.FindFirstObjectByType<ProceduralHorizonFog>() != null)
                return;

            GameObject fog = new GameObject("Procedural Horizon Fog");
            fog.AddComponent<ProceduralHorizonFog>();
        }
    }
}
