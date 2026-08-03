using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    /// <summary>
    /// Camera-following procedural rain made entirely at runtime. It supports
    /// several rain situations and uses a separate splash/mist particle layer.
    /// No imported rain textures or prefabs are required.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class ProceduralRainSystem : MonoBehaviour
    {
        public enum RainSituation
        {
            Clear,
            Drizzle,
            LightRain,
            SteadyRain,
            HeavyRain,
            WindDrivenRain,
            TropicalDownpour
        }

        [Header("Weather")]
        [SerializeField] private RainSituation startingSituation = RainSituation.Clear;
        [SerializeField] private bool randomWeatherChanges;
        [SerializeField, Min(2f)] private float minimumSituationDuration = 25f;
        [SerializeField, Min(2f)] private float maximumSituationDuration = 70f;
        [SerializeField, Range(0f, 1f)] private float chanceOfClearWeather = 0.30f;
        [SerializeField, Min(0.05f)] private float transitionSeconds = 2.5f;
        [Tooltip("Press R to cycle rain situations while testing in Play mode.")]
        [SerializeField] private bool allowDebugCycleKey = true;

        [Header("Rain Placement")]
        [SerializeField, Min(0f)] private float horizontalCameraMargin = 2.5f;
        [SerializeField, Min(0f)] private float emitterHeightAboveCamera = 1.25f;
        [SerializeField, Min(0f)] private float fallDistanceBelowCamera = 2f;
        [SerializeField] private int rainSortingOrder = 900;

        [Header("Appearance")]
        [SerializeField] private Color rainColour = new(0.68f, 0.82f, 1f, 0.70f);
        [SerializeField] private Color stormRainColour = new(0.78f, 0.86f, 0.96f, 0.82f);
        [SerializeField, Range(0.005f, 0.08f)] private float dropWidth = 0.018f;
        [SerializeField, Range(0.05f, 1.2f)] private float minimumDropLength = 0.18f;
        [SerializeField, Range(0.05f, 1.8f)] private float maximumDropLength = 0.58f;

        [Header("Splash / Mist Layer")]
        [SerializeField] private bool createSplashes = true;
        [Tooltip("Vertical offset from the camera centre for the broad ocean splash band.")]
        [SerializeField] private float splashBandCameraOffset = -0.55f;
        [SerializeField, Range(0.02f, 0.30f)] private float splashSize = 0.07f;
        [SerializeField, Min(0.02f)] private float splashLifetime = 0.22f;
        [SerializeField] private int splashSortingOrder = 901;

        [Header("Optional Atmosphere")]
        [SerializeField] private bool dimCameraDuringRain = true;
        [SerializeField, Range(0f, 0.35f)] private float maximumCameraDimming = 0.16f;

        private ParticleSystem rainParticles;
        private ParticleSystem splashParticles;
        private ParticleSystemRenderer rainRenderer;
        private ParticleSystemRenderer splashRenderer;
        private Material rainMaterial;
        private Material splashMaterial;
        private Texture2D rainTexture;
        private Texture2D splashTexture;

        private sealed class WaveSplashLayer
        {
            public PixelWaterGPU Water;
            public ParticleSystem Particles;
            public ParticleSystemRenderer Renderer;
            public Material Material;
        }

        private readonly List<WaveSplashLayer> waveSplashLayers = new();
        private float nextWaveRefreshAt;
        private float nextSurfaceSplashAt;
        private Camera gameplayCamera;
        private RainSituation currentSituation;
        private RainSituation targetSituation;
        private float transitionProgress = 1f;
        private float situationTimer;
        private float nextSituationDuration;
        private Color originalCameraColour;
        private bool storedCameraColour;

        private float currentRate;
        private float currentSpeed;
        private float currentWind;
        private float currentLength;
        private float currentOpacity;
        private float currentSplashRate;
        private float currentDimming;

        private struct RainPreset
        {
            public float Rate;
            public float Speed;
            public float Wind;
            public float Length;
            public float Opacity;
            public float SplashRate;
            public float Dimming;
        }

        public RainSituation CurrentSituation => targetSituation;
        public bool IsRaining => targetSituation != RainSituation.Clear;

        private void Awake()
        {
            gameplayCamera = Camera.main;
            BuildSystems();
            currentSituation = startingSituation;
            targetSituation = startingSituation;
            ApplyPresetImmediate(GetPreset(startingSituation));
            ScheduleNextSituation();
        }

        private void OnEnable()
        {
            if (rainParticles != null && !rainParticles.isPlaying)
                rainParticles.Play();
            if (splashParticles != null && !splashParticles.isPlaying)
                splashParticles.Play();
        }

        private void Update()
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            HandleDebugInput();
            UpdateRandomWeather();
            UpdateTransition(Time.deltaTime);
            FollowCamera();
            RefreshWaveSplashLayers();
            ApplyLiveSettings();
            EmitWaveSurfaceSplashes();
        }

        private void OnDestroy()
        {
            if (storedCameraColour && gameplayCamera != null)
                gameplayCamera.backgroundColor = originalCameraColour;

            if (rainMaterial != null) Destroy(rainMaterial);
            if (splashMaterial != null) Destroy(splashMaterial);
            foreach (WaveSplashLayer layer in waveSplashLayers)
            {
                if (layer == null) continue;
                if (layer.Material != null) Destroy(layer.Material);
                if (layer.Particles != null) Destroy(layer.Particles.gameObject);
            }
            waveSplashLayers.Clear();

            if (rainTexture != null) Destroy(rainTexture);
            if (splashTexture != null) Destroy(splashTexture);
        }

        public void SetSituation(RainSituation situation)
        {
            if (targetSituation == situation && transitionProgress >= 1f)
                return;

            currentSituation = targetSituation;
            targetSituation = situation;
            transitionProgress = 0f;
            situationTimer = 0f;
            ScheduleNextSituation();
        }

        public void ClearRain()
        {
            SetSituation(RainSituation.Clear);
        }

        public void CycleSituation()
        {
            int next = ((int)targetSituation + 1) % System.Enum.GetValues(typeof(RainSituation)).Length;
            SetSituation((RainSituation)next);
        }

        private void HandleDebugInput()
        {
            if (!allowDebugCycleKey)
                return;

            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.R);
#endif
            if (pressed)
                CycleSituation();
        }

        private void UpdateRandomWeather()
        {
            if (!randomWeatherChanges)
                return;

            situationTimer += Time.deltaTime;
            if (situationTimer < nextSituationDuration)
                return;

            situationTimer = 0f;
            RainSituation next;
            if (Random.value < chanceOfClearWeather)
                next = RainSituation.Clear;
            else
                next = (RainSituation)Random.Range(1, System.Enum.GetValues(typeof(RainSituation)).Length);

            SetSituation(next);
        }

        private void ScheduleNextSituation()
        {
            float min = Mathf.Min(minimumSituationDuration, maximumSituationDuration);
            float max = Mathf.Max(minimumSituationDuration, maximumSituationDuration);
            nextSituationDuration = Random.Range(min, max);
        }

        private void UpdateTransition(float dt)
        {
            RainPreset target = GetPreset(targetSituation);
            float response = transitionSeconds <= 0.05f ? 1f : 1f - Mathf.Exp(-dt * (5f / transitionSeconds));

            currentRate = Mathf.Lerp(currentRate, target.Rate, response);
            currentSpeed = Mathf.Lerp(currentSpeed, target.Speed, response);
            currentWind = Mathf.Lerp(currentWind, target.Wind, response);
            currentLength = Mathf.Lerp(currentLength, target.Length, response);
            currentOpacity = Mathf.Lerp(currentOpacity, target.Opacity, response);
            currentSplashRate = Mathf.Lerp(currentSplashRate, target.SplashRate, response);
            currentDimming = Mathf.Lerp(currentDimming, target.Dimming, response);

            transitionProgress = Mathf.MoveTowards(transitionProgress, 1f, dt / Mathf.Max(0.05f, transitionSeconds));
        }

        private void ApplyPresetImmediate(RainPreset preset)
        {
            currentRate = preset.Rate;
            currentSpeed = preset.Speed;
            currentWind = preset.Wind;
            currentLength = preset.Length;
            currentOpacity = preset.Opacity;
            currentSplashRate = preset.SplashRate;
            currentDimming = preset.Dimming;
        }

        private RainPreset GetPreset(RainSituation situation)
        {
            switch (situation)
            {
                case RainSituation.Drizzle:
                    return new RainPreset { Rate = 45f, Speed = 5.5f, Wind = 0.15f, Length = 0.16f, Opacity = 0.40f, SplashRate = 8f, Dimming = 0.025f };
                case RainSituation.LightRain:
                    return new RainPreset { Rate = 95f, Speed = 7.5f, Wind = 0.25f, Length = 0.23f, Opacity = 0.52f, SplashRate = 20f, Dimming = 0.05f };
                case RainSituation.SteadyRain:
                    return new RainPreset { Rate = 175f, Speed = 10f, Wind = 0.40f, Length = 0.31f, Opacity = 0.62f, SplashRate = 42f, Dimming = 0.08f };
                case RainSituation.HeavyRain:
                    return new RainPreset { Rate = 310f, Speed = 13f, Wind = 0.65f, Length = 0.43f, Opacity = 0.74f, SplashRate = 80f, Dimming = 0.12f };
                case RainSituation.WindDrivenRain:
                    return new RainPreset { Rate = 260f, Speed = 14f, Wind = 5.2f, Length = 0.48f, Opacity = 0.75f, SplashRate = 68f, Dimming = 0.14f };
                case RainSituation.TropicalDownpour:
                    return new RainPreset { Rate = 480f, Speed = 17f, Wind = 2.1f, Length = 0.62f, Opacity = 0.88f, SplashRate = 130f, Dimming = 0.18f };
                default:
                    return new RainPreset { Rate = 0f, Speed = 8f, Wind = 0f, Length = 0.20f, Opacity = 0f, SplashRate = 0f, Dimming = 0f };
            }
        }

        private void BuildSystems()
        {
            GameObject rainObject = new GameObject("Rain Drops");
            rainObject.transform.SetParent(transform, false);
            rainParticles = rainObject.AddComponent<ParticleSystem>();
            rainRenderer = rainObject.GetComponent<ParticleSystemRenderer>();

            ParticleSystem.MainModule main = rainParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2400;
            main.startLifetime = 1.5f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = dropWidth;
            main.startSizeY = minimumDropLength;
            main.startSizeZ = dropWidth;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = rainParticles.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = rainParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 0.05f, 0.05f);

            ParticleSystem.VelocityOverLifetimeModule velocity = rainParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 0f;
            velocity.y = -8f;

            rainTexture = CreateRainTexture();
            rainMaterial = CreateParticleMaterial("Procedural Rain Material", rainTexture);
            rainRenderer.sharedMaterial = rainMaterial;
            rainRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            rainRenderer.sortingOrder = rainSortingOrder;

            GameObject splashObject = new GameObject("Rain Surface Splashes");
            splashObject.transform.SetParent(transform, false);
            splashParticles = splashObject.AddComponent<ParticleSystem>();
            splashRenderer = splashObject.GetComponent<ParticleSystemRenderer>();

            ParticleSystem.MainModule splashMain = splashParticles.main;
            splashMain.loop = true;
            splashMain.playOnAwake = true;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.maxParticles = 700;
            splashMain.startLifetime = splashLifetime;
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
            splashMain.startSize = new ParticleSystem.MinMaxCurve(splashSize * 0.6f, splashSize * 1.35f);
            splashMain.gravityModifier = 0.35f;

            ParticleSystem.EmissionModule splashEmission = splashParticles.emission;
            splashEmission.rateOverTime = 0f;

            ParticleSystem.ShapeModule splashShape = splashParticles.shape;
            splashShape.enabled = true;
            splashShape.shapeType = ParticleSystemShapeType.Box;
            splashShape.scale = new Vector3(20f, 0.05f, 0.05f);
            splashShape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule colourOverLife = splashParticles.colorOverLifetime;
            colourOverLife.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.18f), new GradientAlphaKey(0f, 1f) });
            colourOverLife.color = fade;

            splashTexture = CreateSplashTexture();
            splashMaterial = CreateParticleMaterial("Procedural Rain Splash Material", splashTexture);
            splashRenderer.sharedMaterial = splashMaterial;
            splashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            splashRenderer.sortingOrder = splashSortingOrder;
            splashRenderer.enabled = false; // Replaced by per-wave surface splashes.

            rainParticles.Play();
            splashParticles.Play();
        }

        private void FollowCamera()
        {
            if (gameplayCamera == null)
                return;

            float halfHeight = gameplayCamera.orthographic
                ? gameplayCamera.orthographicSize
                : 5f;
            float halfWidth = halfHeight * gameplayCamera.aspect;

            Vector3 cameraPosition = gameplayCamera.transform.position;
            Vector3 rainPosition = cameraPosition;
            rainPosition.y += halfHeight + emitterHeightAboveCamera;
            rainPosition.z = 0f;
            rainParticles.transform.position = rainPosition;

            ParticleSystem.ShapeModule rainShape = rainParticles.shape;
            rainShape.scale = new Vector3((halfWidth + horizontalCameraMargin) * 2f, 0.05f, 0.05f);

            Vector3 splashPosition = cameraPosition;
            splashPosition.y += splashBandCameraOffset;
            splashPosition.z = 0f;
            splashParticles.transform.position = splashPosition;

            ParticleSystem.ShapeModule splashShape = splashParticles.shape;
            splashShape.scale = new Vector3((halfWidth + horizontalCameraMargin) * 2f, 0.06f, 0.05f);

            float lifetime = (halfHeight * 2f + emitterHeightAboveCamera + fallDistanceBelowCamera) /
                             Mathf.Max(0.1f, currentSpeed);
            ParticleSystem.MainModule main = rainParticles.main;
            main.startLifetime = Mathf.Clamp(lifetime, 0.35f, 4f);
        }

        private void ApplyLiveSettings()
        {
            if (rainParticles == null)
                return;

            ParticleSystem.EmissionModule emission = rainParticles.emission;
            emission.rateOverTime = currentRate;

            ParticleSystem.MainModule main = rainParticles.main;
            main.startSize3D = true;
            main.startSizeX = dropWidth;
            main.startSizeY = Mathf.Lerp(minimumDropLength, maximumDropLength,
                Mathf.InverseLerp(0.14f, 0.62f, currentLength));
            main.startSizeZ = dropWidth;

            Color baseColour = targetSituation == RainSituation.TropicalDownpour ||
                               targetSituation == RainSituation.WindDrivenRain
                ? stormRainColour
                : rainColour;
            baseColour.a *= currentOpacity;
            main.startColor = baseColour;

            ParticleSystem.VelocityOverLifetimeModule velocity = rainParticles.velocityOverLifetime;
            velocity.x = currentWind;
            velocity.y = -currentSpeed;

            if (splashParticles != null)
            {
                ParticleSystem.EmissionModule splashEmission = splashParticles.emission;
                splashEmission.rateOverTime = 0f;

                ParticleSystem.MainModule splashMain = splashParticles.main;
                Color splashColour = baseColour;
                splashColour.a = Mathf.Clamp01(baseColour.a * 0.8f);
                splashMain.startColor = splashColour;
            }

            ApplyCameraDimming();
        }

        private void RefreshWaveSplashLayers()
        {
            if (!createSplashes || Time.unscaledTime < nextWaveRefreshAt)
                return;

            nextWaveRefreshAt = Time.unscaledTime + 1f;
            PixelWaterGPU[] waters = FindObjectsByType<PixelWaterGPU>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = waveSplashLayers.Count - 1; i >= 0; i--)
            {
                WaveSplashLayer layer = waveSplashLayers[i];
                bool stillExists = layer != null && layer.Water != null &&
                                   System.Array.IndexOf(waters, layer.Water) >= 0;
                if (stillExists) continue;

                if (layer != null)
                {
                    if (layer.Material != null) Destroy(layer.Material);
                    if (layer.Particles != null) Destroy(layer.Particles.gameObject);
                }
                waveSplashLayers.RemoveAt(i);
            }

            foreach (PixelWaterGPU water in waters)
            {
                if (water == null || waveSplashLayers.Exists(layer => layer.Water == water))
                    continue;

                waveSplashLayers.Add(CreateWaveSplashLayer(water));
            }
        }

        private WaveSplashLayer CreateWaveSplashLayer(PixelWaterGPU water)
        {
            GameObject host = new GameObject($"Rain Splashes - Wave {water.IndependentLayerIndex}");
            host.transform.SetParent(transform, false);

            ParticleSystem particles = host.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                splashLifetime * 0.75f,
                splashLifetime * 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.34f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                splashSize * 0.55f,
                splashSize * 1.45f);
            main.gravityModifier = 0.28f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colourOverLife = particles.colorOverLifetime;
            colourOverLife.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.55f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            colourOverLife.color = fade;

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            Material material = CreateParticleMaterial(
                $"Rain Splash Wave {water.IndependentLayerIndex}",
                splashTexture);
            material.renderQueue = Mathf.Clamp(
                water.GetWaveLayerRenderQueue() + 2,
                2501,
                4999);
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = splashSortingOrder;

            return new WaveSplashLayer
            {
                Water = water,
                Particles = particles,
                Renderer = renderer,
                Material = material
            };
        }

        private void EmitWaveSurfaceSplashes()
        {
            if (!createSplashes || currentSplashRate <= 0.01f || gameplayCamera == null ||
                waveSplashLayers.Count == 0 || Time.unscaledTime < nextSurfaceSplashAt)
                return;

            const float interval = 0.075f;
            nextSurfaceSplashAt = Time.unscaledTime + interval;

            float halfHeight = gameplayCamera.orthographic
                ? gameplayCamera.orthographicSize
                : 5f;
            float halfWidth = halfHeight * gameplayCamera.aspect + horizontalCameraMargin;
            float left = gameplayCamera.transform.position.x - halfWidth;
            float right = gameplayCamera.transform.position.x + halfWidth;

            float expectedPerLayer = currentSplashRate * interval /
                                     Mathf.Max(1, waveSplashLayers.Count);

            foreach (WaveSplashLayer layer in waveSplashLayers)
            {
                if (layer == null || layer.Water == null || layer.Particles == null)
                    continue;

                int count = Mathf.FloorToInt(expectedPerLayer);
                if (Random.value < expectedPerLayer - count)
                    count++;

                count = Mathf.Clamp(count, 0, 5);
                for (int i = 0; i < count; i++)
                {
                    float x = Random.Range(left, right);
                    float y = layer.Water.GetGameplaySurfaceHeight(x) + 0.018f;
                    Vector2 waveVelocity = layer.Water.GetGameplayWaveVelocity(x);

                    Color colour = targetSituation == RainSituation.TropicalDownpour ||
                                   targetSituation == RainSituation.WindDrivenRain
                        ? stormRainColour
                        : rainColour;
                    colour.a = Mathf.Clamp01(currentOpacity * 0.82f);

                    ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                    {
                        position = new Vector3(x, y, 0f),
                        velocity = new Vector3(
                            waveVelocity.x * 0.05f + Random.Range(-0.10f, 0.10f),
                            Random.Range(0.10f, 0.32f),
                            0f),
                        startColor = colour,
                        startLifetime = Random.Range(splashLifetime * 0.75f, splashLifetime * 1.35f),
                        startSize = Random.Range(splashSize * 0.55f, splashSize * 1.45f)
                    };
                    layer.Particles.Emit(emit, 1);
                }
            }
        }

        private void ApplyCameraDimming()
        {
            if (!dimCameraDuringRain || gameplayCamera == null)
                return;

            if (!storedCameraColour)
            {
                originalCameraColour = gameplayCamera.backgroundColor;
                storedCameraColour = true;
            }

            float amount = Mathf.Clamp(currentDimming, 0f, maximumCameraDimming);
            gameplayCamera.backgroundColor = Color.Lerp(originalCameraColour,
                new Color(0.07f, 0.09f, 0.14f, 1f), amount / Mathf.Max(0.001f, maximumCameraDimming));
        }

        private static Material CreateParticleMaterial(string materialName, Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
            };

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            return material;
        }

        private static Texture2D CreateRainTexture()
        {
            const int width = 5;
            const int height = 24;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Procedural Rain Drop",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                alphaIsTransparency = true
            };

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float along = y / (float)(height - 1);
                float endFade = Mathf.SmoothStep(0f, 1f, Mathf.Min(along * 5f, (1f - along) * 4f));
                float centre = 2f + Mathf.Lerp(-0.55f, 0.55f, along);

                for (int x = 0; x < width; x++)
                {
                    float distance = Mathf.Abs(x - centre);
                    float core = Mathf.Clamp01(1f - distance / 1.35f);
                    float alpha = core * core * endFade;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateSplashTexture()
        {
            const int size = 9;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural Rain Splash",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                alphaIsTransparency = true
            };

            Color[] pixels = new Color[size * size];
            Vector2 centre = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x, y) - centre;
                    float radius = d.magnitude;
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(radius - 2.8f) / 0.7f);
                    float lowerHalf = y <= centre.y + 1f ? 1f : 0f;
                    float sideSpray = Mathf.Clamp01(1f - Mathf.Abs(d.x) / 3.8f) *
                                      Mathf.Clamp01((d.y + 1.5f) / 3.5f);
                    float alpha = Mathf.Max(ring * lowerHalf, sideSpray * 0.55f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRainSystem()
        {
            if (Object.FindFirstObjectByType<ProceduralRainSystem>() != null)
                return;

            new GameObject("Procedural Rain System").AddComponent<ProceduralRainSystem>();
        }
    }
}
