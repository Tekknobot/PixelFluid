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
        [Tooltip("Minimum world-space height where the soft top of the fog begins.")]
        [SerializeField] private float horizonY = 1.65f;
        [Tooltip("Extra width beyond both camera edges.")]
        [SerializeField, Min(0f)] private float horizontalPadding = 3f;
        [Tooltip("Extra depth below the camera so the mask always covers the ocean.")]
        [SerializeField, Min(0f)] private float lowerPadding = 3f;
        [Tooltip("Render above distant scenery but below the water simulations.")]
        [SerializeField] private int sortingOrder = 0;

        [Header("Wave Stack Coverage")]
        [Tooltip("How far the fully opaque fog overlaps upward into the visible bottom of the highest wave layer.")]
        [SerializeField, Range(0f, 1f)] private float opaqueWaveOverlap = 0.27f;
        [Tooltip("How often the active wave-layer list is refreshed. The layer positions themselves are still checked every frame.")]
        [SerializeField, Range(0.1f, 3f)] private float waveRefreshInterval = 0.75f;

        [Header("Pixel Fog")]
        [SerializeField, Range(32, 512)] private int textureWidth = 256;
        [SerializeField, Range(16, 256)] private int textureHeight = 96;
        [SerializeField, Range(2, 32)] private int pixelBlockSize = 2;
        [SerializeField] private int randomSeed = 8917;
        [SerializeField, Range(0.03f, 0.5f)] private float refreshInterval = 0.12f;
        [SerializeField, Range(0f, 2f)] private float driftSpeed = 0.20f;
        [SerializeField, Range(0.02f, 0.8f)] private float softBandFraction = 0.41f;
        [SerializeField, Range(0f, 0.5f)] private float edgeNoise = 0.16f;
        [SerializeField, Range(0f, 1f)] private float mistStrength = 0.72f;

        [Header("Colours")]
        // Top of the gradient (near the horizon)
        [SerializeField] private Color nightHorizonColour = new(0.003f, 0.030f, 0.040f, 1f);
        [SerializeField] private Color dayHorizonColour   = new(0.006f, 0.055f, 0.070f, 1f);

        // Bottom of the gradient (deep ocean)
        [SerializeField] private Color nightDeepWaterColour = new(0.012f, 0.095f, 0.120f, 1f);
        [SerializeField] private Color dayDeepWaterColour   = new(0.018f, 0.135f, 0.165f, 1f);

        [SerializeField, Range(0f, 1f)]
        private float colourFollowStrength = 0.8f;

        private SpriteRenderer spriteRenderer;
        private Texture2D texture;
        private Sprite sprite;
        private Color[] pixels;
        private Camera gameplayCamera;
        private ProceduralStarryNight dayNight;
        private PixelWaterGPU[] waveLayers;
        private float nextWaveRefreshTime;
        private float timer;
        private float phase;

        private void Awake()
        {
            gameplayCamera = Camera.main;
            dayNight = FindFirstObjectByType<ProceduralStarryNight>();
            RefreshWaveLayers();
            BuildResources();
            Redraw();
        }

        private void OnEnable()
        {
            RefreshWaveLayers();
            FitToCamera();
        }

        private void LateUpdate()
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (dayNight == null)
                dayNight = FindFirstObjectByType<ProceduralStarryNight>();

            if (Time.unscaledTime >= nextWaveRefreshTime)
                RefreshWaveLayers();

            FitToCamera();

            phase += Time.deltaTime * driftSpeed;
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = refreshInterval;
                Redraw();
            }
        }

        private void RefreshWaveLayers()
        {
            waveLayers = FindObjectsByType<PixelWaterGPU>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            nextWaveRefreshTime =
                Time.unscaledTime + Mathf.Max(0.1f, waveRefreshInterval);
        }

        private float GetHighestVisibleWaveBottom()
        {
            bool foundWave = false;
            float highestBottom = float.NegativeInfinity;

            if (waveLayers != null)
            {
                for (int i = 0; i < waveLayers.Length; i++)
                {
                    PixelWaterGPU wave = waveLayers[i];
                    if (wave == null || !wave.isActiveAndEnabled)
                        continue;

                    highestBottom = Mathf.Max(
                        highestBottom,
                        wave.VisibleWaveBottom);

                    foundWave = true;
                }
            }

            return foundWave
                ? highestBottom
                : float.NegativeInfinity;
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
            float bottomY =
                gameplayCamera.transform.position.y -
                gameplayCamera.orthographicSize -
                lowerPadding;

            /*
             * The texture becomes fully opaque after softBandFraction of its
             * world-space height has passed downward from the top.
             *
             * Solve for the top position required to place that opaque cutoff at
             * the highest visible bottom edge of the complete wave stack.
             */
            float topY = horizonY;
            float highestWaveBottom = GetHighestVisibleWaveBottom();

            if (!float.IsNegativeInfinity(highestWaveBottom))
            {
                float desiredOpaqueCutoff =
                    highestWaveBottom + opaqueWaveOverlap;

                float softFraction = Mathf.Clamp(
                    softBandFraction,
                    0.02f,
                    0.8f);

                float denominator = Mathf.Max(0.01f, 1f - softFraction);

                float requiredTopForCoverage =
                    (desiredOpaqueCutoff - softFraction * bottomY) /
                    denominator;

                topY = Mathf.Max(horizonY, requiredTopForCoverage);
            }

            float requiredHeight = Mathf.Max(0.5f, topY - bottomY);

            transform.position = new Vector3(
                gameplayCamera.transform.position.x,
                topY,
                0f);

            transform.localScale = new Vector3(
                cameraWidth + horizontalPadding * 2f,
                requiredHeight,
                1f);
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
                 * by = 0                  bottom
                 * by = textureHeight - 1  top
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
                     * At the top, fogDepth is around zero and transparent.
                     * Moving downward, fogDepth reaches one and becomes opaque.
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
