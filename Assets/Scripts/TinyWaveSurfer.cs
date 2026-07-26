using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Autonomous 8x8 surfer. It rides to one edge, performs a turn trick,
    /// reverses direction, and rides the same wave back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TinyWaveSurfer : MonoBehaviour
    {
        private enum RiderState
        {
            Riding,
            TurningTrick,
            SwitchingWave
        }

        [Header("Wave Selection")]
        [SerializeField, Min(1f)] private float secondsPerSimulation = 10f;
        [Tooltip("Random variation added to each surfer's layer-jump interval so they never transfer together.")]
        [SerializeField, Min(0f)] private float simulationTimeVariation = 3.5f;
        [SerializeField, Min(0.1f)] private float switchDuration = 0.9f;
        [SerializeField] private bool cycleContinuously = true;
        [Tooltip("Choose a different simulation layer instead of always moving to the next one.")]
        [SerializeField] private bool jumpToRandomWaveLayer = true;
        [Tooltip("Extra height used while jumping between simulation layers.")]
        [SerializeField, Range(0.1f, 3f)] private float layerJumpHeight = 0.15f;
        [SerializeField] private bool sortWavesBackToFront = true;
        [SerializeField, Min(0)] private int startingWaveIndex;

        [Header("Back-and-Forth Ride")]
        [SerializeField, Min(0.1f)] private float horizontalRideSpeed = 1.2f;
        [SerializeField, Range(0.02f, 0.35f)] private float edgePadding = 0.12f;
        [SerializeField] private bool startMovingRight = true;
        [SerializeField, Min(0f)] private float surfaceOffset;
        [SerializeField, Range(1f, 30f)] private float surfaceFollow = 16f;
        [SerializeField, Range(0f, 2f)] private float waveVelocityInfluence = 0.22f;

        [Header("Edge Turn Trick")]
        [SerializeField, Range(0.1f, 2f)] private float turnJumpHeight = 0.26f;
        [SerializeField, Range(0.2f, 1.5f)] private float turnTrickDuration = 0.38f;
        [SerializeField, Range(90f, 1080f)] private float turnSpinDegrees = 360f;
        [SerializeField, Range(0f, 1f)] private float flipChance = 0.45f;

        [Header("8x8 Pixel Look")]
        [SerializeField, Min(0.005f)] private float pixelWorldSize = 0.025f;
        [SerializeField] private int sortingOrder = 1;
        [SerializeField] private Color bodyColor = new(0.12f, 0.08f, 0.06f, 1f);
        [SerializeField] private Color shirtColor = new(0.95f, 0.32f, 0.12f, 1f);
        [SerializeField] private Color boardColor = new(1f, 0.88f, 0.24f, 1f);

        private readonly List<PixelWaterGPU> simulations = new();
        private PixelWaterGPU currentWave;
        private SpriteRenderer spriteRenderer;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;

        private RiderState state;
        private int waveIndex;
        private float waveTimer;
        private float currentSimulationDuration;
        private float stateTimer;
        private float direction = 1f;
        private float localRideX;
        private float airStartY;
        private float renderDepth;
        private bool flipTrick;
        private Vector3 switchStart;
        private Vector3 switchTarget;

        public int CurrentWaveIndex => waveIndex;
        public PixelWaterGPU CurrentWave => currentWave;
        public float TravelDirection => direction;

        private void Awake()
        {
            EnsurePixelSprite();
            RefreshWaveList();
            direction = startMovingRight ? 1f : -1f;
            PickWave(startingWaveIndex, true);
            ScheduleNextLayerJump(0f);
        }

        private void OnDestroy()
        {
            if (runtimeSprite != null) Destroy(runtimeSprite);
            if (runtimeTexture != null) Destroy(runtimeTexture);
        }

        public void ConfigureGeneratedSurfer(
            int wave,
            bool movingRight,
            float speed,
            Color shirt,
            Color board,
            int order,
            float initialLayerJumpDelay,
            float personalIntervalOffset)
        {
            startingWaveIndex = wave;
            startMovingRight = movingRight;
            horizontalRideSpeed = speed;
            shirtColor = shirt;
            boardColor = board;
            sortingOrder = order;
            secondsPerSimulation = Mathf.Max(
                1f,
                secondsPerSimulation + personalIntervalOffset);

            if (spriteRenderer != null)
            {
                if (runtimeSprite != null) Destroy(runtimeSprite);
                if (runtimeTexture != null) Destroy(runtimeTexture);
                EnsurePixelSprite();
            }

            RefreshWaveList();
            direction = movingRight ? 1f : -1f;
            PickWave(wave, true);
            ScheduleNextLayerJump(initialLayerJumpDelay);
        }

        private void Update()
        {
            if (simulations.Count == 0)
            {
                RefreshWaveList();
                if (simulations.Count == 0) return;
                PickWave(startingWaveIndex, true);
            }

            simulations.RemoveAll(w => w == null || !w.isActiveAndEnabled);
            if (simulations.Count == 0) return;

            if (currentWave == null || !currentWave.isActiveAndEnabled)
                PickWave(Mathf.Clamp(waveIndex, 0, simulations.Count - 1), true);

            float dt = Time.deltaTime;
            waveTimer += dt;
            stateTimer += dt;

            if (state == RiderState.SwitchingWave)
            {
                UpdateWaveSwitch();
                return;
            }

            if (state == RiderState.TurningTrick)
                UpdateTurnTrick();
            else
                UpdateRide(dt);

            if (waveTimer >= currentSimulationDuration &&
                simulations.Count > 1 &&
                cycleContinuously &&
                state == RiderState.Riding)
            {
                BeginNextWave();
            }
        }

        [ContextMenu("Refresh Wave Simulations")]
        public void RefreshWaveList()
        {
            simulations.Clear();
            simulations.AddRange(FindObjectsByType<PixelWaterGPU>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None));

            simulations.RemoveAll(w =>
                w == null || !w.isActiveAndEnabled || w.gameObject == gameObject);

            if (sortWavesBackToFront)
            {
                simulations.Sort((a, b) =>
                {
                    int y = a.transform.position.y.CompareTo(b.transform.position.y);
                    return y != 0 ? y : a.transform.position.z.CompareTo(b.transform.position.z);
                });
            }
        }

        private void UpdateRide(float dt)
        {
            Vector2 min = currentWave.TankMinimum;
            Vector2 max = currentWave.TankMaximum;
            float width = Mathf.Max(0.01f, max.x - min.x);
            float left = min.x + width * edgePadding;
            float right = max.x - width * edgePadding;

            Vector2 waveVelocity = currentWave.GetGameplayWaveVelocity(localRideX);
            float waveAssist = waveVelocity.x * waveVelocityInfluence * direction;
            localRideX += direction *
                Mathf.Max(0.2f, horizontalRideSpeed + waveAssist) * dt;

            if (localRideX >= right)
            {
                localRideX = right;
                BeginTurnTrick();
                return;
            }

            if (localRideX <= left)
            {
                localRideX = left;
                BeginTurnTrick();
                return;
            }

            FollowSurface(dt);
        }

        private void FollowSurface(float dt)
        {
            float surfaceY = currentWave.GetGameplaySurfaceHeight(localRideX);
            const float sample = 0.09f;
            float leftY = currentWave.GetGameplaySurfaceHeight(localRideX - sample);
            float rightY = currentWave.GetGameplaySurfaceHeight(localRideX + sample);
            float slope = Mathf.Atan2(rightY - leftY, sample * 2f) * Mathf.Rad2Deg;

            Vector3 target = new(localRideX, surfaceY + surfaceOffset, renderDepth);
            transform.position = Vector3.Lerp(
                transform.position, target, 1f - Mathf.Exp(-surfaceFollow * dt));

            float facingScale = direction >= 0f ? pixelWorldSize : -pixelWorldSize;
            transform.localScale = new Vector3(facingScale, pixelWorldSize, 1f);
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.Euler(0f, 0f, slope),
                1f - Mathf.Exp(-surfaceFollow * 0.7f * dt));
        }

        private void BeginTurnTrick()
        {
            state = RiderState.TurningTrick;
            stateTimer = 0f;
            airStartY = currentWave.GetGameplaySurfaceHeight(localRideX) + surfaceOffset;
            flipTrick = Random.value < flipChance;
        }

        private void UpdateTurnTrick()
        {
            float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, turnTrickDuration));
            float surfaceY = currentWave.GetGameplaySurfaceHeight(localRideX);
            float arc = Mathf.Sin(t * Mathf.PI) * turnJumpHeight;

            transform.position = new Vector3(
                localRideX,
                Mathf.Max(surfaceY + surfaceOffset, airStartY + arc),
                renderDepth);

            float spinDirection = direction >= 0f ? -1f : 1f;
            transform.rotation = Quaternion.Euler(
                0f, 0f, turnSpinDegrees * spinDirection * t);

            float facing = direction >= 0f ? 1f : -1f;
            float flip = flipTrick ? Mathf.Cos(t * Mathf.PI * 2f) : 1f;
            float xScale = facing * pixelWorldSize *
                Mathf.Max(0.18f, Mathf.Abs(flip)) *
                Mathf.Sign(Mathf.Approximately(flip, 0f) ? 1f : flip);
            transform.localScale = new Vector3(xScale, pixelWorldSize, 1f);

            if (t >= 1f)
            {
                direction *= -1f;
                state = RiderState.Riding;
                stateTimer = 0f;
                transform.rotation = Quaternion.identity;
                transform.localScale = new Vector3(
                    direction >= 0f ? pixelWorldSize : -pixelWorldSize,
                    pixelWorldSize,
                    1f);
            }
        }

        [ContextMenu("Ride Next Wave")]
        public void BeginNextWave()
        {
            if (simulations.Count <= 1)
            {
                waveTimer = 0f;
                return;
            }

            int next;
            if (jumpToRandomWaveLayer && simulations.Count > 2)
            {
                next = waveIndex;
                int safety = 0;
                while (next == waveIndex && safety++ < 12)
                    next = Random.Range(0, simulations.Count);
            }
            else
            {
                next = (waveIndex + 1) % simulations.Count;
            }

            currentWave = simulations[next];
            waveIndex = next;
            waveTimer = 0f;
            ScheduleNextLayerJump(0f);
            stateTimer = 0f;
            state = RiderState.SwitchingWave;
            renderDepth = currentWave.transform.position.z - 0.02f;

            switchStart = transform.position;
            switchTarget = GetStartingPosition(currentWave);
            switchTarget.z = renderDepth;
        }

        private void UpdateWaveSwitch()
        {
            float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, switchDuration));
            float eased = t * t * (3f - 2f * t);
            Vector3 p = Vector3.Lerp(switchStart, switchTarget, eased);
            float layerDistance = Mathf.Abs(switchTarget.y - switchStart.y);
            float jump = layerJumpHeight + layerDistance * 0.35f;
            p.y += Mathf.Sin(t * Mathf.PI) * jump;
            transform.position = p;

            float spinDirection = direction >= 0f ? -1f : 1f;
            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                540f * spinDirection * eased);

            float tuck = 1f - Mathf.Sin(t * Mathf.PI) * 0.35f;
            float facing = direction >= 0f ? 1f : -1f;
            transform.localScale = new Vector3(
                facing * pixelWorldSize * tuck,
                pixelWorldSize * tuck,
                1f);

            if (t >= 1f)
            {
                localRideX = switchTarget.x;
                transform.position = switchTarget;
                transform.rotation = Quaternion.identity;
                state = RiderState.Riding;
                stateTimer = 0f;
            }
        }

        private void ScheduleNextLayerJump(float initialDelay)
        {
            float variation = Random.Range(
                -simulationTimeVariation,
                simulationTimeVariation);

            currentSimulationDuration = Mathf.Max(
                1f,
                secondsPerSimulation + variation);

            // A negative timer creates a unique initial delay without forcing
            // every surfer to jump at scene start.
            waveTimer = -Mathf.Max(0f, initialDelay);
        }

        private void PickWave(int index, bool snap)
        {
            if (simulations.Count == 0) return;

            waveIndex = Mathf.Abs(index) % simulations.Count;
            currentWave = simulations[waveIndex];
            stateTimer = 0f;
            state = RiderState.Riding;
            renderDepth = currentWave.transform.position.z - 0.02f;

            Vector2 min = currentWave.TankMinimum;
            Vector2 max = currentWave.TankMaximum;
            float width = max.x - min.x;
            localRideX = direction > 0f
                ? min.x + width * (edgePadding + 0.08f)
                : max.x - width * (edgePadding + 0.08f);

            Vector3 start = GetStartingPosition(currentWave);
            start.x = localRideX;
            start.z = renderDepth;
            if (snap) transform.position = start;
        }

        private Vector3 GetStartingPosition(PixelWaterGPU wave)
        {
            Vector2 min = wave.TankMinimum;
            Vector2 max = wave.TankMaximum;
            float x = Mathf.Lerp(min.x, max.x, 0.5f);
            float y = wave.GetGameplaySurfaceHeight(x) + surfaceOffset;
            return new Vector3(x, y, wave.transform.position.z - 0.02f);
        }

        private void EnsurePixelSprite()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            runtimeTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = "Tiny Surfer 8x8",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[64];
            void Set(int x, int y, Color c)
            {
                if (x >= 0 && x < 8 && y >= 0 && y < 8)
                    pixels[y * 8 + x] = c;
            }

            for (int x = 1; x <= 6; x++) Set(x, 1, boardColor);
            Set(3, 2, bodyColor); Set(5, 2, bodyColor);
            Set(3, 3, bodyColor); Set(4, 3, bodyColor);
            Set(4, 4, shirtColor); Set(4, 5, shirtColor);
            Set(3, 5, shirtColor); Set(5, 5, shirtColor);
            Set(2, 5, bodyColor); Set(6, 5, bodyColor);
            Set(4, 6, bodyColor); Set(4, 7, bodyColor); Set(3, 7, bodyColor);

            runtimeTexture.SetPixels(pixels);
            runtimeTexture.Apply(false, false);

            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.18f),
                1f, 0, SpriteMeshType.FullRect);

            spriteRenderer.sprite = runtimeSprite;
            spriteRenderer.sortingOrder = sortingOrder;
            transform.localScale = new Vector3(
                direction >= 0f ? pixelWorldSize : -pixelWorldSize,
                pixelWorldSize,
                1f);
        }
    }

    public static class TinyWaveSurferBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateTinySurfers()
        {
            if (Object.FindFirstObjectByType<PixelWaterGPU>() == null)
                return;

            if (Object.FindObjectsByType<TinyWaveSurfer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length > 0)
                return;

            Color[] shirts =
            {
                new(0.95f, 0.30f, 0.12f, 1f),
                new(0.12f, 0.68f, 0.95f, 1f),
                new(0.66f, 0.20f, 0.90f, 1f),
                new(0.15f, 0.85f, 0.42f, 1f),
                new(0.95f, 0.75f, 0.12f, 1f),
                new(0.95f, 0.25f, 0.62f, 1f)
            };

            Color[] boards =
            {
                new(1f, 0.88f, 0.24f, 1f),
                new(0.95f, 0.95f, 1f, 1f),
                new(0.20f, 0.95f, 0.85f, 1f),
                new(1f, 0.42f, 0.18f, 1f),
                new(0.45f, 0.85f, 1f, 1f),
                new(0.85f, 0.95f, 0.28f, 1f)
            };

            const int surferCount = 6;
            for (int i = 0; i < surferCount; i++)
            {
                GameObject go = new($"Tiny 8x8 Surfer {i + 1}");
                TinyWaveSurfer surfer = go.AddComponent<TinyWaveSurfer>();
                surfer.ConfigureGeneratedSurfer(
                    i,
                    (i & 1) == 0,
                    0.95f + i * 0.11f,
                    shirts[i],
                    boards[i],
                    100 + i,
                    1.25f + i * 1.85f,
                    (i - 2.5f) * 0.55f);
            }
        }
    }
}
