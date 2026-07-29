using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Procedurally renders a complete camera-following day/night sky into one
    /// pixel-art texture. The original star field is retained and now fades
    /// naturally through dawn, day, sunset and night while a sun, moon and thin
    /// clouds travel across the sky.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralStarryNight : MonoBehaviour
    {
        [Header("Day / Night Cycle")]
        [SerializeField] private bool runCycle = true;
        [Tooltip("Real-world minutes required for one complete 24-hour cycle.")]
        [SerializeField, Min(0.25f)] private float fullDayLengthMinutes = 15f;
        [Tooltip("0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset.")]
        [SerializeField, Range(0f, 1f)] private float startingTimeOfDay = 0.82f;
        [SerializeField, Range(0.1f, 20f)] private float editorFastForwardMultiplier = 1f;
        [SerializeField] private bool useUnscaledTime;

        [Header("Generated Texture")]
        [SerializeField, Range(64, 1024)] private int textureWidth = 512;
        [SerializeField, Range(64, 1024)] private int textureHeight = 288;
        [SerializeField, Range(10, 900)] private int starCount = 180;
        [SerializeField] private int randomSeed = 7421;
        [SerializeField, Range(0.04f, 0.5f)] private float textureRefreshInterval = 0.10f;

        [Header("Night Sky Colours")]
        [SerializeField] private Color topSky = new(0.015f, 0.025f, 0.10f, 1f);
        [SerializeField] private Color horizonSky = new(0.07f, 0.10f, 0.24f, 1f);

        [Header("Day Sky Colours")]
        [SerializeField] private Color dayTopSky = new(0.12f, 0.50f, 0.88f, 1f);
        [SerializeField] private Color dayHorizonSky = new(0.58f, 0.82f, 0.96f, 1f);
        [SerializeField] private Color dawnTopSky = new(0.18f, 0.16f, 0.38f, 1f);
        [SerializeField] private Color dawnHorizonSky = new(1f, 0.46f, 0.25f, 1f);
        [SerializeField] private Color sunsetTopSky = new(0.12f, 0.08f, 0.30f, 1f);
        [SerializeField] private Color sunsetHorizonSky = new(1f, 0.28f, 0.16f, 1f);

        [Header("Stars")]
        [SerializeField] private Color dimStar = new(0.62f, 0.72f, 1f, 1f);
        [SerializeField] private Color brightStar = new(1f, 0.96f, 0.78f, 1f);
        [SerializeField, Range(0f, 1f)] private float largeStarChance = 0.09f;
        [SerializeField] private bool animateTwinkle = true;
        [SerializeField, Range(0.1f, 8f)] private float twinkleSpeed = 1.8f;
        [SerializeField, Range(0f, 0.8f)] private float twinkleStrength = 0.28f;

        [Header("Sun and Moon")]
        [SerializeField] private Color sunColour = new(1f, 0.86f, 0.38f, 1f);
        [SerializeField] private Color sunGlowColour = new(1f, 0.48f, 0.18f, 1f);
        [SerializeField] private Color moonColour = new(0.82f, 0.88f, 1f, 1f);
        [SerializeField, Range(2, 30)] private int sunRadiusPixels = 9;
        [SerializeField, Range(2, 30)] private int moonRadiusPixels = 7;
        [SerializeField, Range(0.25f, 0.9f)] private float celestialArcHeight = 0.72f;

        [Header("Procedural Clouds")]
        [SerializeField] private bool drawClouds = true;
        [SerializeField, Range(0, 24)] private int cloudCount = 7;
        [SerializeField, Range(0f, 1f)] private float cloudOpacity = 0.32f;
        [SerializeField, Range(0f, 0.2f)] private float cloudSpeed = 0.018f;
        [SerializeField] private Color dayCloudColour = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color nightCloudColour = new(0.18f, 0.22f, 0.38f, 1f);

        [Header("Placement")]
        [SerializeField] private Vector3 worldPosition = new(0f, 3.5f, 6f);
        [SerializeField] private Vector2 worldSize = new(18f, 10.125f);
        [SerializeField] private int sortingOrder = -500;
        [SerializeField] private bool updateCameraBackgroundColour = true;

        private SpriteRenderer spriteRenderer;
        private Texture2D generatedTexture;
        private Sprite generatedSprite;
        private Color[] pixels;
        private StarData[] stars;
        private CloudData[] clouds;
        private float updateTimer;
        private float timeOfDay;
        private Camera gameplayCamera;

        public float TimeOfDay => timeOfDay;
        public bool IsNight => timeOfDay < 0.225f || timeOfDay > 0.775f;
        public bool IsDay => timeOfDay > 0.30f && timeOfDay < 0.70f;

        private struct StarData
        {
            public int X;
            public int Y;
            public int Radius;
            public float Phase;
            public Color Colour;
        }

        private struct CloudData
        {
            public float X;
            public float Y;
            public int Width;
            public int Height;
            public float Speed;
            public float Phase;
        }

        private void Awake()
        {
            gameplayCamera = Camera.main;
            timeOfDay = Mathf.Repeat(startingTimeOfDay, 1f);
            BuildNightSky();
        }

        private void Update()
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (gameplayCamera != null)
            {
                Vector3 position = transform.position;
                position.x = gameplayCamera.transform.position.x;
                transform.position = position;
            }

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (runCycle)
            {
                float secondsPerDay = Mathf.Max(1f, fullDayLengthMinutes * 60f);
                timeOfDay = Mathf.Repeat(timeOfDay + dt * editorFastForwardMultiplier / secondsPerDay, 1f);
            }

            updateTimer += dt;
            if (updateTimer < textureRefreshInterval)
                return;

            updateTimer = 0f;
            RenderSky(Time.time);
        }

        [ContextMenu("Rebuild Procedural Day Night Sky")]
        public void BuildNightSky()
        {
            CleanupGeneratedAssets();
            transform.position = worldPosition;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            generatedTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                name = "Procedural Day Night Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            pixels = new Color[textureWidth * textureHeight];
            BuildProceduralObjects();
            RenderSky(0f);

            generatedSprite = Sprite.Create(
                generatedTexture,
                new Rect(0f, 0f, textureWidth, textureHeight),
                new Vector2(0.5f, 0.5f),
                textureWidth / Mathf.Max(0.01f, worldSize.x),
                0,
                SpriteMeshType.FullRect);

            generatedSprite.name = "Procedural Day Night Sprite";
            spriteRenderer.sprite = generatedSprite;
            spriteRenderer.sortingOrder = sortingOrder;

            transform.localScale = new Vector3(
                worldSize.x / (generatedSprite.rect.width / generatedSprite.pixelsPerUnit),
                worldSize.y / (generatedSprite.rect.height / generatedSprite.pixelsPerUnit),
                1f);
        }

        private void BuildProceduralObjects()
        {
            Random.State previousState = Random.state;
            Random.InitState(randomSeed);

            stars = new StarData[Mathf.Max(0, starCount)];
            for (int i = 0; i < stars.Length; i++)
            {
                float heightBias = Mathf.Pow(Random.value, 0.72f);
                bool large = Random.value < largeStarChance;
                stars[i] = new StarData
                {
                    X = Random.Range(2, Mathf.Max(3, textureWidth - 2)),
                    Y = Mathf.Clamp(Mathf.RoundToInt(heightBias * (textureHeight - 6)) + 3, 3, textureHeight - 3),
                    Radius = large ? Random.Range(2, 4) : 1,
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    Colour = Color.Lerp(dimStar, brightStar, Random.Range(0.2f, 1f))
                };
            }

            clouds = new CloudData[Mathf.Max(0, cloudCount)];
            for (int i = 0; i < clouds.Length; i++)
            {
                clouds[i] = new CloudData
                {
                    X = Random.value,
                    Y = Random.Range(0.40f, 0.84f),
                    Width = Random.Range(Mathf.Max(10, textureWidth / 24), Mathf.Max(18, textureWidth / 10)),
                    Height = Random.Range(Mathf.Max(3, textureHeight / 80), Mathf.Max(6, textureHeight / 35)),
                    Speed = cloudSpeed * Random.Range(0.65f, 1.35f),
                    Phase = Random.Range(0f, 100f)
                };
            }

            Random.state = previousState;
        }

        private void RenderSky(float animationTime)
        {
            if (generatedTexture == null || pixels == null)
                return;

            EvaluateSkyColours(timeOfDay, out Color top, out Color horizon);

            for (int y = 0; y < textureHeight; y++)
            {
                float vertical = y / Mathf.Max(1f, textureHeight - 1f);
                Color row = Color.Lerp(horizon, top, Mathf.SmoothStep(0f, 1f, vertical));
                int rowStart = y * textureWidth;
                for (int x = 0; x < textureWidth; x++)
                    pixels[rowStart + x] = row;
            }

            float starVisibility = GetStarVisibility(timeOfDay);
            if (stars != null && starVisibility > 0.001f)
            {
                foreach (StarData star in stars)
                {
                    float wave = animateTwinkle
                        ? 0.5f + 0.5f * Mathf.Sin(animationTime * twinkleSpeed + star.Phase)
                        : 1f;
                    float brightness = Mathf.Lerp(1f - twinkleStrength, 1f, wave) * starVisibility;
                    Color colour = Color.Lerp(pixels[star.Y * textureWidth + star.X], star.Colour, brightness);
                    DrawStar(star.X, star.Y, star.Radius, colour);
                }
            }

            DrawCelestialBody(timeOfDay, true);
            DrawCelestialBody(Mathf.Repeat(timeOfDay + 0.5f, 1f), false);

            if (drawClouds)
                DrawCloudLayer(animationTime, top, horizon);

            generatedTexture.SetPixels(pixels);
            generatedTexture.Apply(false, false);

            if (updateCameraBackgroundColour && gameplayCamera != null)
                gameplayCamera.backgroundColor = horizon;
        }

        private void EvaluateSkyColours(float t, out Color top, out Color horizon)
        {
            // Blend around a circular 24-hour clock using broad transition windows.
            if (t < 0.20f)
            {
                top = topSky;
                horizon = horizonSky;
            }
            else if (t < 0.28f)
            {
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, 0.28f, t));
                top = Color.Lerp(topSky, dawnTopSky, blend);
                horizon = Color.Lerp(horizonSky, dawnHorizonSky, blend);
            }
            else if (t < 0.38f)
            {
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.38f, t));
                top = Color.Lerp(dawnTopSky, dayTopSky, blend);
                horizon = Color.Lerp(dawnHorizonSky, dayHorizonSky, blend);
            }
            else if (t < 0.68f)
            {
                top = dayTopSky;
                horizon = dayHorizonSky;
            }
            else if (t < 0.77f)
            {
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.77f, t));
                top = Color.Lerp(dayTopSky, sunsetTopSky, blend);
                horizon = Color.Lerp(dayHorizonSky, sunsetHorizonSky, blend);
            }
            else if (t < 0.86f)
            {
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.77f, 0.86f, t));
                top = Color.Lerp(sunsetTopSky, topSky, blend);
                horizon = Color.Lerp(sunsetHorizonSky, horizonSky, blend);
            }
            else
            {
                top = topSky;
                horizon = horizonSky;
            }
        }

        private static float GetStarVisibility(float t)
        {
            if (t <= 0.19f || t >= 0.84f)
                return 1f;
            if (t < 0.31f)
                return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.19f, 0.31f, t));
            if (t > 0.72f)
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.84f, t));
            return 0f;
        }

        private void DrawCelestialBody(float bodyTime, bool sun)
        {
            // Sun is visible from sunrise to sunset. The moon uses the same arc
            // half a cycle later and is therefore visible during the night.
            float arc = Mathf.InverseLerp(0.25f, 0.75f, bodyTime);
            if (bodyTime < 0.25f || bodyTime > 0.75f)
                return;

            int x = Mathf.RoundToInt(Mathf.Lerp(-0.06f, 1.06f, arc) * textureWidth);
            float height = Mathf.Sin(arc * Mathf.PI);
            int y = Mathf.RoundToInt(Mathf.Lerp(0.10f, celestialArcHeight, height) * textureHeight);
            int radius = sun ? sunRadiusPixels : moonRadiusPixels;
            Color centre = sun ? sunColour : moonColour;

            if (sun)
            {
                DrawDisc(x, y, radius + Mathf.Max(2, radius / 2), sunGlowColour, 0.18f);
                DrawDisc(x, y, radius, centre, 1f);
            }
            else
            {
                DrawDisc(x, y, radius, centre, 0.92f);
                // A small offset cutout creates a readable crescent in pixel art.
                Color skyAtMoon = SampleSkyPixel(x + Mathf.Max(1, radius / 3), y + 1);
                DrawDisc(x + Mathf.Max(1, radius / 3), y + 1, Mathf.Max(1, radius - 2), skyAtMoon, 1f);
            }
        }

        private void DrawCloudLayer(float animationTime, Color top, Color horizon)
        {
            if (clouds == null)
                return;

            float daylight = 1f - GetStarVisibility(timeOfDay);
            Color cloud = Color.Lerp(nightCloudColour, dayCloudColour, daylight);
            float alpha = cloudOpacity * Mathf.Lerp(0.45f, 1f, daylight);

            foreach (CloudData data in clouds)
            {
                float normalizedX = Mathf.Repeat(data.X + animationTime * data.Speed + data.Phase, 1.18f) - 0.09f;
                int cx = Mathf.RoundToInt(normalizedX * textureWidth);
                int cy = Mathf.RoundToInt(data.Y * textureHeight);
                int w = data.Width;
                int h = data.Height;

                DrawEllipse(cx, cy, w, h, cloud, alpha * 0.82f);
                DrawEllipse(cx - w / 4, cy + h / 3, w / 2, h, cloud, alpha);
                DrawEllipse(cx + w / 5, cy + h / 3, w / 2, h + 1, cloud, alpha);
            }
        }

        private void DrawStar(int x, int y, int radius, Color colour)
        {
            SetPixel(x, y, colour);
            if (radius <= 1) return;

            Color arm = Color.Lerp(SampleSkyPixel(x, y), colour, 0.72f);
            SetPixel(x - 1, y, arm);
            SetPixel(x + 1, y, arm);
            SetPixel(x, y - 1, arm);
            SetPixel(x, y + 1, arm);

            if (radius >= 3)
            {
                Color outer = Color.Lerp(SampleSkyPixel(x, y), colour, 0.38f);
                SetPixel(x - 2, y, outer);
                SetPixel(x + 2, y, outer);
                SetPixel(x, y - 2, outer);
                SetPixel(x, y + 2, outer);
            }
        }

        private void DrawDisc(int cx, int cy, int radius, Color colour, float alpha)
        {
            int r2 = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y > r2) continue;
                    BlendPixel(cx + x, cy + y, colour, alpha);
                }
            }
        }

        private void DrawEllipse(int cx, int cy, int width, int height, Color colour, float alpha)
        {
            int rx = Mathf.Max(1, width / 2);
            int ry = Mathf.Max(1, height / 2);
            for (int y = -ry; y <= ry; y++)
            {
                float ny = y / (float)ry;
                for (int x = -rx; x <= rx; x++)
                {
                    float nx = x / (float)rx;
                    if (nx * nx + ny * ny <= 1f)
                        BlendPixel(cx + x, cy + y, colour, alpha);
                }
            }
        }

        private Color SampleSkyPixel(int x, int y)
        {
            x = Mathf.Clamp(x, 0, textureWidth - 1);
            y = Mathf.Clamp(y, 0, textureHeight - 1);
            return pixels[y * textureWidth + x];
        }

        private void BlendPixel(int x, int y, Color colour, float alpha)
        {
            if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight)
                return;
            int index = y * textureWidth + x;
            pixels[index] = Color.Lerp(pixels[index], colour, Mathf.Clamp01(alpha));
        }

        private void SetPixel(int x, int y, Color colour)
        {
            if (x < 0 || x >= textureWidth || y < 0 || y >= textureHeight)
                return;
            pixels[y * textureWidth + x] = colour;
        }

        public void SetTimeOfDay(float normalizedTime)
        {
            timeOfDay = Mathf.Repeat(normalizedTime, 1f);
            RenderSky(Time.time);
        }

        [ContextMenu("Set Time: Dawn")]
        private void SetDawn() => SetTimeOfDay(0.27f);

        [ContextMenu("Set Time: Noon")]
        private void SetNoon() => SetTimeOfDay(0.50f);

        [ContextMenu("Set Time: Sunset")]
        private void SetSunset() => SetTimeOfDay(0.75f);

        [ContextMenu("Set Time: Midnight")]
        private void SetMidnight() => SetTimeOfDay(0f);

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
            pixels = null;
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

            GameObject sky = new("Procedural Day Night Sky");
            sky.AddComponent<ProceduralStarryNight>();
        }
    }
}
