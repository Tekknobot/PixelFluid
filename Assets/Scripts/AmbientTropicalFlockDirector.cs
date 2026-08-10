using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Keeps several harmless animated bird formations crossing the distant sky.
    /// Frames are sliced from the eight 256x256 canvases at runtime because the
    /// source importer intentionally exposes the individual birds instead.
    /// </summary>
    [DefaultExecutionOrder(10030)]
    [DisallowMultipleComponent]
    public sealed class AmbientTropicalFlockDirector : MonoBehaviour
    {
        private sealed class ActiveFlock
        {
            public GameObject Object;
            public SpriteRenderer Renderer;
            public float Direction;
            public float Speed;
            public float BaseLocalY;
            public float BobHeight;
            public float BobFrequency;
            public float BobPhase;
            public float FrameClock;
            public int FrameOffset;
        }

        [Header("Flock Sprite Sheet")]
        [SerializeField] private string resourcePath = "Flock/flock_of_birds";
        [SerializeField, Min(1)] private int frameSize = 32;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 32f;
        [SerializeField, Min(0.1f)] private float animationFramesPerSecond = 10f;

        [Header("Continuous Population")]
        [SerializeField, Min(1)] private int minimumActiveFlocks = 6;
        [SerializeField, Min(1)] private int maximumActiveFlocks = 9;
        [SerializeField] private Vector2 spawnIntervalRange = new(7f, 14f);
        [SerializeField, Range(0f, 1f)] private float doubleSpawnChance = 0.32f;
        [SerializeField, Min(0.2f)] private float offscreenPadding = 2.8f;

        [Header("Flight Variation")]
        [SerializeField] private Vector2 speedRange = new(0.62f, 1.28f);
        [SerializeField] private Vector2 scaleRange = new(0.58f, 0.96f);
        [SerializeField] private Vector2 viewportHeightRange = new(0.57f, 0.91f);
        [SerializeField] private Vector2 bobHeightRange = new(0.04f, 0.16f);
        [SerializeField] private Vector2 bobFrequencyRange = new(0.45f, 1.05f);
        [SerializeField] private Vector2 opacityRange = new(0.76f, 0.98f);
        [SerializeField] private Vector2Int sortingOrderRange = new(-24, -8);

        [Header("Tropical Palette Shader")]
        [SerializeField] private string shaderResourcePath =
            "Shaders/TropicalBirdFlock";
        [SerializeField, Range(0f, 1f)] private float minimumPaletteStrength = 0.62f;
        [SerializeField, Range(0f, 1f)] private float maximumPaletteStrength = 0.92f;

        private static readonly Color[] TropicalColours =
        {
            new(0.08f, 0.90f, 0.78f, 1f), // lagoon turquoise
            new(1.00f, 0.34f, 0.22f, 1f), // coral
            new(1.00f, 0.78f, 0.12f, 1f), // mango
            new(0.32f, 0.86f, 0.22f, 1f), // palm green
            new(0.95f, 0.20f, 0.62f, 1f), // hibiscus
            new(0.24f, 0.58f, 1.00f, 1f)  // clear-sky blue
        };

        private static readonly int TropicalAId = Shader.PropertyToID("_TropicalA");
        private static readonly int TropicalBId = Shader.PropertyToID("_TropicalB");
        private static readonly int PaletteStrengthId = Shader.PropertyToID("_PaletteStrength");

        private readonly List<ActiveFlock> activeFlocks = new();
        private Camera worldCamera;
        private ProceduralStarryNight starryNight;
        private Sprite[] frames = System.Array.Empty<Sprite>();
        private Material sharedFlockMaterial;
        private bool usingTropicalShader;
        private float nextSpawnAt;

        private void Awake()
        {
            worldCamera = Camera.main;
            starryNight = FindFirstObjectByType<ProceduralStarryNight>();
            AttachToSky();
            LoadFrames();
            CreateSharedMaterial();
            ScheduleNextSpawn(0.5f);
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            if (frames.Length == 0 || worldCamera == null)
                yield break;

            int initialCount = Mathf.Max(1, minimumActiveFlocks);
            for (int i = 0; i < initialCount; i++)
                SpawnFlock();
        }

        private void Update()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (starryNight == null)
            {
                starryNight = FindFirstObjectByType<ProceduralStarryNight>();
                AttachToSky();
            }

            UpdateFlocks(Time.deltaTime);
            int maximum = Mathf.Max(minimumActiveFlocks, maximumActiveFlocks);
            if (Time.time < nextSpawnAt || activeFlocks.Count >= maximum)
                return;

            int spawnCount = activeFlocks.Count < minimumActiveFlocks
                ? minimumActiveFlocks - activeFlocks.Count
                : 1;
            if (spawnCount == 1 && activeFlocks.Count + 1 < maximum &&
                Random.value < doubleSpawnChance)
            {
                spawnCount = 2;
            }

            for (int i = 0; i < spawnCount && activeFlocks.Count < maximum; i++)
                SpawnFlock();
            ScheduleNextSpawn();
        }

        private void LateUpdate()
        {
            if (starryNight != null || worldCamera == null)
                return;

            Vector3 position = transform.position;
            position.x = worldCamera.transform.position.x;
            transform.position = position;
        }

        private void SpawnFlock()
        {
            if (frames.Length == 0 || worldCamera == null)
                return;

            float direction = Random.value < 0.5f ? -1f : 1f;
            float halfWidth = ViewHalfWidth();
            float cameraLocalX = transform.InverseTransformPoint(
                worldCamera.transform.position).x;
            float spawnX = cameraLocalX - direction * (halfWidth + offscreenPadding);
            float viewportY = Random.Range(
                Mathf.Min(viewportHeightRange.x, viewportHeightRange.y),
                Mathf.Max(viewportHeightRange.x, viewportHeightRange.y));
            float localY = ViewportLocalY(viewportY);

            GameObject flockObject = new($"Ambient Tropical Flock {activeFlocks.Count + 1}");
            flockObject.transform.SetParent(transform, false);
            flockObject.transform.localPosition = new Vector3(spawnX, localY, 0f);
            float scale = Random.Range(
                Mathf.Min(scaleRange.x, scaleRange.y),
                Mathf.Max(scaleRange.x, scaleRange.y));
            flockObject.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = flockObject.AddComponent<SpriteRenderer>();
            int frameOffset = Random.Range(0, frames.Length);
            renderer.sprite = frames[frameOffset];
            renderer.flipX = direction < 0f;
            renderer.sortingOrder = Random.Range(
                Mathf.Min(sortingOrderRange.x, sortingOrderRange.y),
                Mathf.Max(sortingOrderRange.x, sortingOrderRange.y) + 1);
            renderer.sharedMaterial = sharedFlockMaterial;
            float opacity = Random.Range(
                Mathf.Min(opacityRange.x, opacityRange.y),
                Mathf.Max(opacityRange.x, opacityRange.y));
            renderer.color = new Color(1f, 1f, 1f, opacity);
            ApplyRandomPalette(renderer);

            activeFlocks.Add(new ActiveFlock
            {
                Object = flockObject,
                Renderer = renderer,
                Direction = direction,
                Speed = Random.Range(
                    Mathf.Min(speedRange.x, speedRange.y),
                    Mathf.Max(speedRange.x, speedRange.y)),
                BaseLocalY = localY,
                BobHeight = Random.Range(
                    Mathf.Min(bobHeightRange.x, bobHeightRange.y),
                    Mathf.Max(bobHeightRange.x, bobHeightRange.y)),
                BobFrequency = Random.Range(
                    Mathf.Min(bobFrequencyRange.x, bobFrequencyRange.y),
                    Mathf.Max(bobFrequencyRange.x, bobFrequencyRange.y)),
                BobPhase = Random.Range(0f, Mathf.PI * 2f),
                FrameClock = Random.Range(0f, frames.Length),
                FrameOffset = frameOffset
            });
        }

        private void UpdateFlocks(float dt)
        {
            if (worldCamera == null)
                return;

            float cameraLocalX = transform.InverseTransformPoint(
                worldCamera.transform.position).x;
            float exitDistance = ViewHalfWidth() + offscreenPadding + 1.2f;
            for (int i = activeFlocks.Count - 1; i >= 0; i--)
            {
                ActiveFlock flock = activeFlocks[i];
                if (flock == null || flock.Object == null || flock.Renderer == null)
                {
                    activeFlocks.RemoveAt(i);
                    continue;
                }

                Transform flockTransform = flock.Object.transform;
                Vector3 localPosition = flockTransform.localPosition;
                localPosition.x += flock.Direction * flock.Speed * dt;
                localPosition.y = flock.BaseLocalY + Mathf.Sin(
                    Time.time * flock.BobFrequency + flock.BobPhase) * flock.BobHeight;
                flockTransform.localPosition = localPosition;

                flock.FrameClock += dt * animationFramesPerSecond;
                int frame = (Mathf.FloorToInt(flock.FrameClock) + flock.FrameOffset) % frames.Length;
                flock.Renderer.sprite = frames[frame];

                bool exited = flock.Direction > 0f
                    ? localPosition.x > cameraLocalX + exitDistance
                    : localPosition.x < cameraLocalX - exitDistance;
                if (!exited)
                    continue;

                Destroy(flock.Object);
                activeFlocks.RemoveAt(i);
            }

            if (activeFlocks.Count < minimumActiveFlocks)
                nextSpawnAt = Mathf.Min(nextSpawnAt, Time.time + 0.35f);
        }

        private void ApplyRandomPalette(SpriteRenderer renderer)
        {
            int first = Random.Range(0, TropicalColours.Length);
            int second = Random.Range(0, TropicalColours.Length - 1);
            if (second >= first)
                second++;

            if (!usingTropicalShader)
            {
                Color fallback = Color.Lerp(
                    TropicalColours[first],
                    TropicalColours[second],
                    0.5f);
                fallback.a = renderer.color.a;
                renderer.color = Color.Lerp(renderer.color, fallback, 0.48f);
                return;
            }

            MaterialPropertyBlock properties = new();
            renderer.GetPropertyBlock(properties);
            properties.SetColor(TropicalAId, TropicalColours[first]);
            properties.SetColor(TropicalBId, TropicalColours[second]);
            properties.SetFloat(PaletteStrengthId, Random.Range(
                Mathf.Min(minimumPaletteStrength, maximumPaletteStrength),
                Mathf.Max(minimumPaletteStrength, maximumPaletteStrength)));
            renderer.SetPropertyBlock(properties);
        }

        private void LoadFrames()
        {
            Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
            if (sheet == null)
            {
                Debug.LogWarning(
                    $"Ambient flocks could not load Resources/{resourcePath}.",
                    this);
                return;
            }

            int size = Mathf.Max(1, frameSize);
            int columns = Mathf.Max(1, sheet.width / size);
            int rows = Mathf.Max(1, sheet.height / size);
            frames = new Sprite[columns * rows];
            int index = 0;
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < columns; column++)
                {
                    frames[index] = Sprite.Create(
                        sheet,
                        new Rect(column * size, row * size, size, size),
                        new Vector2(0.5f, 0.5f),
                        Mathf.Max(1f, pixelsPerUnit),
                        0,
                        SpriteMeshType.FullRect);
                    frames[index].name = $"runtime_flock_{index:00}";
                    index++;
                }
            }
        }

        private void CreateSharedMaterial()
        {
            Shader shader = Resources.Load<Shader>(shaderResourcePath);
            usingTropicalShader = shader != null;
            shader ??= Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            sharedFlockMaterial = new Material(shader)
            {
                name = "Shared Tropical Bird Flock Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void AttachToSky()
        {
            if (starryNight == null || transform.parent == starryNight.transform)
                return;
            transform.SetParent(starryNight.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private void ScheduleNextSpawn(float minimumDelay = -1f)
        {
            float low = Mathf.Min(spawnIntervalRange.x, spawnIntervalRange.y);
            float high = Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y);
            float delay = Random.Range(Mathf.Max(0.1f, low), Mathf.Max(0.1f, high));
            if (minimumDelay >= 0f)
                delay = Mathf.Max(minimumDelay, delay);
            nextSpawnAt = Time.time + delay;
        }

        private float ViewHalfWidth()
        {
            return worldCamera != null && worldCamera.orthographic
                ? worldCamera.orthographicSize * worldCamera.aspect
                : 9f;
        }

        private float ViewportLocalY(float viewportY)
        {
            if (worldCamera == null)
                return 2f;
            float depth = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
            Vector3 world = worldCamera.ViewportToWorldPoint(
                new Vector3(0.5f, viewportY, depth));
            return transform.InverseTransformPoint(world).y;
        }

        private void OnDestroy()
        {
            foreach (ActiveFlock flock in activeFlocks)
                if (flock != null && flock.Object != null)
                    Destroy(flock.Object);
            activeFlocks.Clear();

            foreach (Sprite frame in frames)
                if (frame != null)
                    Destroy(frame);
            frames = System.Array.Empty<Sprite>();

            if (sharedFlockMaterial != null)
                Destroy(sharedFlockMaterial);
        }
    }

    public static class AmbientTropicalFlockBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<AmbientTropicalFlockDirector>() != null)
                return;

            GameObject host = new("Ambient Tropical Flock System");
            host.AddComponent<AmbientTropicalFlockDirector>();
        }
    }
}
