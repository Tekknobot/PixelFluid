using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PixelOcean
{
    public sealed class PixelWaterGPU : MonoBehaviour
    {

    public enum WaveCascadeMode
    {
        Disabled,
        TripleEcho,
        QuadEcho,
        HeavyVolume
    }

        public enum SurfWaveType
        {
            LongboardSwell = 0,
            BeachBreak = 1,
            ReefBarrel = 2,
            HeavyTube = 3
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct GPUParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Density;
            public float Foam;
        }

        [Header("Assets")]
        [SerializeField] private ComputeShader simulationShader;
        [SerializeField] private Material renderingMaterial;

        [Header("Spawn")]
        [SerializeField, Min(1)] private int columns = 110;
        [SerializeField, Min(1)] private int rows = 72;
        [SerializeField, Min(0.005f)] private float particleSpacing = 0.052f;
        [SerializeField] private Vector2 spawnOrigin = new(-4.1f, -2.2f);

        [Header("Tank")]
        [SerializeField] private Vector2 tankMinimum = new(-4.5f, -2.5f);
        [SerializeField] private Vector2 tankMaximum = new(4.5f, 2.5f);
        [SerializeField, Min(0.001f)] private float particleRadius = 0.022f;

        [Header("Fluid")]
        [SerializeField, Min(0.01f)] private float interactionRadius = 0.075f;
        [SerializeField, Min(0f)] private float pressureStrength = 38f;
        [SerializeField, Range(0f, 10f)] private float viscosity = 1.4f;
        [SerializeField] private Vector2 gravity = new(0f, -9.81f);
        [SerializeField, Range(0f, 1f)] private float boundaryBounce = 0.08f;
        [SerializeField, Min(1f)] private float maximumSpeed = 22f;
        [SerializeField, Range(1, 8)] private int substeps = 3;
        [SerializeField, Range(30, 240)] private int simulationRate = 120;

        [Header("Surf Wave Generator")]
        [SerializeField] private bool waveEmitterEnabled = true;
        [SerializeField] private SurfWaveType surfWaveType = SurfWaveType.ReefBarrel;
        [SerializeField, Min(0.1f)] private float waveEmitterWidth = 1.65f;
        [SerializeField, Range(0f, 40f)] private float waveHorizontalForce = 21f;
        [SerializeField, Range(0f, 30f)] private float waveVerticalForce = 6.5f;
        [SerializeField, Range(0.05f, 3f)] private float waveFrequency = 0.28f;
        [SerializeField, Range(0f, 4f)] private float waveVerticalVariation = 0.28f;
        [SerializeField, Range(1f, 8f)] private float wavePulseSharpness = 3.2f;

        [Header("Layered Big Wave")]
        [SerializeField] private bool layeredWaveEnabled = false;
        [SerializeField, Range(2, 5)] private int waveLayerCount = 3;
        [SerializeField, Range(0f, 1.5f)] private float waveLayerPhaseOffset = 0.34f;
        [SerializeField, Range(0f, 30f)] private float deepSurgeForce = 13f;
        [SerializeField, Range(0f, 30f)] private float bodyPushForce = 10f;
        [SerializeField, Range(0f, 35f)] private float crestLiftForce = 17f;
        [SerializeField, Range(0.05f, 0.65f)] private float crestLayerThickness = 0.24f;
        [SerializeField, Range(0f, 2f)] private float layerCompression = 0.72f;
        [SerializeField, Range(0f, 25f)] private float layerForwardStacking = 9f;
        [SerializeField, Range(0f, 20f)] private float lipThrowBoost = 8f;
        [SerializeField, Range(0.4f, 2.5f)] private float bigWaveScale = 1.35f;


        [Header("Independent Big Wave Emitters")]
        [SerializeField] private bool synchronizeBigWaveEmitters = true;
        [SerializeField, Range(0.5f, 3f)] private float independentEmitterScale = 1.7f;
        [SerializeField, Range(1f, 4f)] private float independentEmitterWidthScale = 1.35f;
        [SerializeField, Range(0.5f, 2f)] private float independentEmitterLiftScale = 1.35f;
        [SerializeField, Range(0.5f, 2f)] private float independentEmitterPushScale = 1.25f;

        [Header("Independent Wave Simulation Layers")]
        [SerializeField] private bool createIndependentWaveLayers = false;
        [SerializeField, Range(1, 5)] private int independentLayerCount = 1;
        [SerializeField, Range(0.02f, 1f)] private float independentLayerDelay = 0.18f;
        [SerializeField, Range(0f, 1.5f)] private float independentLayerBackOffset = 0.08f;
        [SerializeField, Range(0f, 0.5f)] private float independentLayerVerticalOffset = 0.34f;
        [SerializeField, Range(0f, 0.5f)] private float independentLayerDepthOffset = 0.06f;
        [SerializeField, Range(0.75f, 1f)] private float independentLayerScaleFalloff = 1f;
        [SerializeField, Range(0.5f, 2f)] private float independentLayerRiseCurve = 1f;
        [SerializeField, Range(0f, 0.5f)] private float independentLayerShade = 0.12f;
        [SerializeField, Range(0f, 0.25f)] private float independentLayerAlphaLoss = 0.035f;
        [SerializeField, Range(0.1f, 1f)] private float independentLayerForceFalloff = 1f;

        private int independentLayerIndex;
        private float independentWaveDelay;
        private bool isIndependentLayerClone;
        private bool independentLayersCreated;
        private Material runtimeLayerMaterial;
        private Material sourceRenderingMaterial;
        private Vector2 appliedTransformPosition;
        private bool transformOriginInitialised;

        [Header("Runtime Layer Position")]
        [Tooltip("Adjust this while Play Mode is running. It moves this simulation's real GPU particles, tank, emitter and seabed together.")]
        [SerializeField] private Vector2 runtimeLayerPosition;
        [Tooltip("Adjust this while Play Mode is running to place this layer visually in front of or behind the other simulations.")]
        [SerializeField, Range(-2f, 2f)] private float runtimeLayerRenderDepth;
        [Tooltip("Optional live timing adjustment for this individual simulation.")]
        [SerializeField, Range(0f, 3f)] private float runtimeLayerWaveDelay;

        private Vector2 appliedRuntimeLayerPosition;
        private float appliedRuntimeLayerWaveDelay;


        [Header("Cascade Echo Wave")]
        [SerializeField] private WaveCascadeMode cascadeMode = WaveCascadeMode.Disabled;
        [SerializeField, Range(1, 4)] private int cascadeEchoCount = 3;
        [SerializeField, Range(0.02f, 0.8f)] private float cascadeDelay = 0.18f;
        [SerializeField, Range(0f, 3f)] private float cascadeBackOffset = 0.42f;
        [SerializeField, Range(0f, 1f)] private float cascadeVerticalOffset = 0.12f;
        [SerializeField, Range(0.1f, 1f)] private float cascadeAmplitudeFalloff = 0.88f;
        [SerializeField, Range(0.1f, 1f)] private float cascadeSpeedFalloff = 0.94f;
        [SerializeField, Range(0f, 2f)] private float cascadeCurlRetention = 0.96f;
        [SerializeField, Range(0f, 30f)] private float cascadeVolumeForce = 9.5f;
        [SerializeField, Range(0f, 20f)] private float cascadeStackLift = 7.5f;
        [SerializeField, Range(0.05f, 1f)] private float cascadeBandThickness = 0.28f;
        [SerializeField, Range(0f, 2f)] private float cascadeBlend = 1f;


        [Header("Surf Break / Tube")]
        [SerializeField, Range(0.45f, 0.90f)] private float breakPoint = 0.73f;
        [SerializeField, Range(0.08f, 0.35f)] private float breakZoneWidth = 0.17f;
        [SerializeField, Range(0f, 35f)] private float shoalingStrength = 10f;
        [SerializeField, Range(0f, 35f)] private float curlForwardStrength = 17f;
        [SerializeField, Range(0f, 25f)] private float curlDownStrength = 7.5f;
        [SerializeField, Range(0f, 30f)] private float curlRotationStrength = 12f;
        [SerializeField, Range(0.15f, 1.5f)] private float curlRadius = 0.68f;
        [SerializeField, Range(0.1f, 1.2f)] private float crestSurfaceBand = 0.38f;

        [Header("Tropical Shore")]
        [SerializeField, Range(0.35f, 0.85f)] private float shoreStart = 0.62f;
        [SerializeField, Range(0.08f, 0.5f)] private float shallowZoneWidth = 0.30f;
        [SerializeField, Range(0.12f, 0.80f)] private float beachHeight = 0.64f;
        [Header("Horizontal Seabed")]
        [SerializeField] private bool horizontalSeabedEnabled = true;
        [Tooltip("World-space height of the perfectly flat seabed. Adjustable live in Play Mode.")]
        [SerializeField] private float horizontalSeabedHeight = -2.05f;

        [SerializeField, Range(0.35f, 2.5f)] private float beachCurve = 0.82f;
        [SerializeField, Range(0f, 1f)] private float shoreFriction = 0.32f;
        [SerializeField] private Color shallowWaterColor = new(0.30f, 0.94f, 0.84f, 0.98f);
        [SerializeField] private Color sandColor = new(0.88f, 0.76f, 0.50f, 1f);
        [SerializeField] private Color wetSandColor = new(0.53f, 0.40f, 0.22f, 1f);
        [SerializeField] private Color deepSandColor = new(0.25f, 0.19f, 0.10f, 1f);

        [Header("Water Colour")]
        [SerializeField] private Color deepWaterColor = new(0.015f, 0.16f, 0.20f, 0.98f);
        [SerializeField] private Color mainWaterColor = new(0.02f, 0.50f, 0.58f, 0.97f);
        [SerializeField] private Color surfaceWaterColor = new(0.16f, 0.82f, 0.80f, 0.99f);
        [SerializeField] private Color foamColor = new(0.95f, 0.99f, 1.00f, 1.00f);
        [SerializeField, Range(0f, 1f)] private float surfaceBand = 0.38f;
        [SerializeField, Range(0f, 2f)] private float colourBrightness = 1f;

        [Header("Foam")]
        [SerializeField, Min(0f)] private float foamSpeedThreshold = 4.5f;
        [SerializeField, Range(0f, 10f)] private float foamGeneration = 2.2f;
        [SerializeField, Range(0f, 5f)] private float foamDecay = 1.05f;
        [SerializeField, Range(0f, 5f)] private float foamTurbulence = 1.2f;
        [SerializeField, Range(0f, 2f)] private float foamRenderStrength = 1f;
        [SerializeField, Range(0.1f, 2f)] private float foamBottomSuppression = 0.55f;
        [SerializeField, Range(0f, 1f)] private float foamSurfaceDensity = 0.62f;

        [Header("Rendering")]
        [SerializeField, Min(0.001f)] private float renderedParticleSize = 0.035f;
        [SerializeField, Range(0f, 1f)] private float edgeSoftness = 0.28f;

        private ComputeBuffer[] particleBuffers;
        private ComputeBuffer cellHeads;
        private ComputeBuffer nextParticle;
        private int readIndex;
        private int particleCount;
        private int gridCellCount;
        private Vector2Int gridSize;
        private int clearGridKernel;
        private int buildGridKernel;
        private int simulateKernel;
        private int shiftParticlesKernel;
        private Bounds renderBounds;
        private float simulationTime;

        [Header("Surfboard Coupling")]
        [SerializeField, Range(0.01f, 0.25f)] private float boardParticlePadding = 0.002f;
        [SerializeField, Range(1f, 80f)] private float boardParticlePush = 8f;
        [SerializeField, Range(0f, 1f)] private float boardVelocityTransfer = 0.18f;
        [SerializeField, Range(0f, 40f)] private float boardParticleDrain = 14f;
        [SerializeField, Range(0f, 1f)] private float boardSurfaceGrip = 0.08f;
        [SerializeField, Range(0f, 1f)] private float boardParticleRadiusScale = 0.28f;

        private Transform surfboardTransform;
        private Rigidbody surfboardBody;
        private Vector2 surfboardLocalCenterOffset;
        private Vector2 surfboardHalfExtents = new(0.70f, 0.085f);

        private const int SurfaceBinCount = 160;
        private readonly float[] sampledSurface = new float[SurfaceBinCount];
        private readonly Vector2[] sampledVelocity = new Vector2[SurfaceBinCount];
        private readonly int[] sampledCounts = new int[SurfaceBinCount];
        private bool hasParticleSurfaceSamples;
        private bool readbackPending;
        private float nextReadbackTime;

        private static readonly int ReadParticlesID = Shader.PropertyToID("_ReadParticles");
        private static readonly int WriteParticlesID = Shader.PropertyToID("_WriteParticles");
        private static readonly int ParticlesID = Shader.PropertyToID("_Particles");
        private static readonly int CellHeadsID = Shader.PropertyToID("_CellHeads");
        private static readonly int NextParticleID = Shader.PropertyToID("_NextParticle");

        private void EnsureUniqueRenderingMaterial()
        {
            if (renderingMaterial == null)
                return;

            if (sourceRenderingMaterial == null)
                sourceRenderingMaterial = renderingMaterial;

            if (runtimeLayerMaterial == null)
            {
                runtimeLayerMaterial = new Material(sourceRenderingMaterial)
                {
                    name = $"{sourceRenderingMaterial.name} ({gameObject.name})"
                };

                renderingMaterial = runtimeLayerMaterial;
            }
        }

        private void ApplyInitialTransformOrigin()
        {
            if (transformOriginInitialised)
                return;

            Vector3 worldPosition = transform.position;
            Vector2 originOffset = new(worldPosition.x, worldPosition.y);

            spawnOrigin += originOffset;
            tankMinimum += originOffset;
            tankMaximum += originOffset;
            beachHeight += originOffset.y;
            horizontalSeabedHeight += originOffset.y;

            runtimeLayerRenderDepth += worldPosition.z;
            appliedTransformPosition = originOffset;
            transformOriginInitialised = true;
        }

        private void ShiftSimulationWorldSpace(Vector2 positionDelta)
        {
            if (particleBuffers == null ||
                positionDelta.sqrMagnitude <= 0.0000001f)
                return;

            int groups = Mathf.CeilToInt(particleCount / 128f);

            simulationShader.SetInt("_ParticleCount", particleCount);
            simulationShader.SetVector("_ShiftOffset", positionDelta);

            for (int i = 0; i < particleBuffers.Length; i++)
            {
                simulationShader.SetBuffer(
                    shiftParticlesKernel,
                    WriteParticlesID,
                    particleBuffers[i]);

                simulationShader.Dispatch(
                    shiftParticlesKernel,
                    groups,
                    1,
                    1);
            }

            spawnOrigin += positionDelta;
            tankMinimum += positionDelta;
            tankMaximum += positionDelta;
            beachHeight += positionDelta.y;
            horizontalSeabedHeight += positionDelta.y;

            renderBounds.center += new Vector3(
                positionDelta.x,
                positionDelta.y,
                0f);

            hasParticleSurfaceSamples = false;
            readbackPending = false;
        }

        private void ApplyTransformOriginChanges()
        {
            Vector3 worldPosition = transform.position;
            Vector2 currentPosition = new(worldPosition.x, worldPosition.y);
            Vector2 positionDelta = currentPosition - appliedTransformPosition;

            if (positionDelta.sqrMagnitude > 0.0000001f)
            {
                ShiftSimulationWorldSpace(positionDelta);
                appliedTransformPosition = currentPosition;
            }

            runtimeLayerRenderDepth = worldPosition.z;
        }

        private void OnEnable()
        {
            EnsureUniqueRenderingMaterial();
            ApplyInitialTransformOrigin();
            ApplyIndependentBigWaveEmitter();
            EnsureTropicalSeabed();
            Initialise();

            // Single-simulator mode: duplicate this GameObject manually
            // when you want additional independent water layers.
        }
        private void OnDisable()
        {
            ReleaseBuffers();

            if (runtimeLayerMaterial != null)
            {
                if (renderingMaterial == runtimeLayerMaterial)
                    renderingMaterial = sourceRenderingMaterial;

                Destroy(runtimeLayerMaterial);
                runtimeLayerMaterial = null;
            }
        }

        [ContextMenu("Surf Preset/Cascade Echo Big Wave")]
        private void UseCascadeEchoWave()
        {
            cascadeMode = WaveCascadeMode.QuadEcho;
            cascadeEchoCount = 3;
            cascadeDelay = 0.18f;
            cascadeBackOffset = 0.42f;
            cascadeVerticalOffset = 0.12f;
            cascadeAmplitudeFalloff = 0.88f;
            cascadeSpeedFalloff = 0.94f;
            cascadeCurlRetention = 0.96f;
            cascadeVolumeForce = 9.5f;
            cascadeStackLift = 7.5f;
            cascadeBandThickness = 0.28f;
            cascadeBlend = 1f;

            layeredWaveEnabled = false;
            waveFrequency = 0.16f;
            waveEmitterWidth = 2.25f;
            waveHorizontalForce = 18f;
            waveVerticalForce = 8.5f;
            wavePulseSharpness = 3.8f;
            breakZoneWidth = 0.24f;
            curlRadius = 0.86f;
        }

        private void ApplyIndependentBigWaveEmitter()
        {
            if (!synchronizeBigWaveEmitters)
                return;

            // Every independent simulation receives the same emitter profile.
            // The only difference between simulations is their start delay and
            // visual depth, so each layer produces a complete real wave.
            waveEmitterEnabled = true;
            cascadeMode = WaveCascadeMode.Disabled;

            layeredWaveEnabled = true;
            waveLayerCount = 4;
            waveLayerPhaseOffset = 0.24f;

            waveFrequency = 0.14f;
            wavePulseSharpness = 4.2f;
            waveEmitterWidth = Mathf.Max(
                waveEmitterWidth,
                1.8f * independentEmitterWidthScale);

            waveHorizontalForce = Mathf.Max(
                waveHorizontalForce,
                19f * independentEmitterPushScale);

            waveVerticalForce = Mathf.Max(
                waveVerticalForce,
                7.5f * independentEmitterLiftScale);

            deepSurgeForce = Mathf.Max(
                deepSurgeForce,
                18f * independentEmitterScale);

            bodyPushForce = Mathf.Max(
                bodyPushForce,
                14f * independentEmitterScale);

            crestLiftForce = Mathf.Max(
                crestLiftForce,
                20f * independentEmitterLiftScale);

            crestLayerThickness = Mathf.Max(
                crestLayerThickness,
                0.28f);

            layerCompression = Mathf.Max(
                layerCompression,
                0.92f);

            layerForwardStacking = Mathf.Max(
                layerForwardStacking,
                11f * independentEmitterPushScale);

            lipThrowBoost = Mathf.Max(
                lipThrowBoost,
                10f * independentEmitterScale);

            bigWaveScale = Mathf.Max(
                bigWaveScale,
                independentEmitterScale);

            breakZoneWidth = Mathf.Max(
                breakZoneWidth,
                0.26f);

            shoalingStrength = Mathf.Max(
                shoalingStrength,
                9.5f);

            curlForwardStrength = Mathf.Max(
                curlForwardStrength,
                12f);

            curlDownStrength = Mathf.Max(
                curlDownStrength,
                8f);

            curlRotationStrength = Mathf.Max(
                curlRotationStrength,
                10f);

            curlRadius = Mathf.Max(
                curlRadius,
                0.92f);
        }

        [ContextMenu("Surf Preset/Independent Layered Real Big Wave")]
        private void UseIndependentLayeredRealBigWave()
        {
            createIndependentWaveLayers = true;
            independentLayerCount = 4;
            independentLayerDelay = 0.18f;
            independentLayerBackOffset = 0.34f;
            independentLayerVerticalOffset = 0.18f;
            independentLayerDepthOffset = 0.06f;
            independentLayerScaleFalloff = 0.94f;
            independentLayerRiseCurve = 1.15f;
            independentLayerShade = 0.12f;
            independentLayerAlphaLoss = 0.035f;
            independentLayerForceFalloff = 1f;

            synchronizeBigWaveEmitters = true;
            independentEmitterScale = 1.7f;
            independentEmitterWidthScale = 1.35f;
            independentEmitterLiftScale = 1.35f;
            independentEmitterPushScale = 1.25f;

            ApplyIndependentBigWaveEmitter();
        }

        [ContextMenu("Surf Preset/Horizontal Depth Wave Lines")]
        private void UseHorizontalDepthWaveLines()
        {
            createIndependentWaveLayers = true;
            independentLayerCount = 4;
            independentLayerDelay = 0.18f;

            // Fixed parallel rows:
            // master at the bottom, then one equal Y step per rear layer.
            independentLayerBackOffset = 0.08f;
            independentLayerVerticalOffset = 0.34f;
            independentLayerDepthOffset = 0.08f;
            independentLayerScaleFalloff = 1f;
            independentLayerRiseCurve = 1f;
            independentLayerShade = 0.12f;
            independentLayerAlphaLoss = 0.035f;
            independentLayerForceFalloff = 1f;

            synchronizeBigWaveEmitters = true;
            ApplyIndependentBigWaveEmitter();
        }

        [ContextMenu("Surf Preset/Single Simulator Horizontal Seabed")]
        private void UseSingleSimulatorHorizontalSeabed()
        {
            createIndependentWaveLayers = false;
            independentLayerCount = 1;
            cascadeMode = WaveCascadeMode.Disabled;

            horizontalSeabedEnabled = true;
            horizontalSeabedHeight = -2.05f;

            layeredWaveEnabled = true;
            synchronizeBigWaveEmitters = true;
            ApplyIndependentBigWaveEmitter();
        }







        private Color GetIndependentLayerColour(Color source)
        {
            if (independentLayerIndex <= 0)
                return source;

            float shade = Mathf.Clamp01(
                independentLayerShade * independentLayerIndex);

            float luminance =
                source.r * 0.2126f +
                source.g * 0.7152f +
                source.b * 0.0722f;

            Color result = Color.Lerp(
                source,
                new Color(luminance, luminance, luminance, source.a),
                shade * 0.35f);

            result.r *= 1f - shade;
            result.g *= 1f - shade;
            result.b *= 1f - shade;
            result.a = Mathf.Clamp01(
                source.a -
                independentLayerAlphaLoss * independentLayerIndex);

            return result;
        }

        private void ConfigureIndependentSimulationLayer(
            int layerIndex,
            float delay,
            Vector3 worldOffset,
            float forceScale,
            float perspectiveScale,
            Material sourceMaterial)
        {
            independentLayerIndex = layerIndex;
            independentWaveDelay = Mathf.Max(0f, delay);
            isIndependentLayerClone = true;
            createIndependentWaveLayers = false;
            independentLayersCreated = true;

            // Disable the old same-buffer fake layers. Each clone now owns
            // a complete, isolated particle simulation.
            cascadeMode = WaveCascadeMode.Disabled;
            layeredWaveEnabled = true;
            ApplyIndependentBigWaveEmitter();

            // Pixel particles are simulated and rendered directly in world
            // coordinates, so moving only the GameObject transform does not move
            // the water. Offset the simulation's actual coordinate system before
            // Initialise() creates its buffers.
            Vector2 simulationOffset =
                new Vector2(worldOffset.x, worldOffset.y);

            runtimeLayerPosition = simulationOffset;
            appliedRuntimeLayerPosition = simulationOffset;
            runtimeLayerRenderDepth =
                independentLayerDepthOffset * layerIndex;
            runtimeLayerWaveDelay = independentWaveDelay;
            appliedRuntimeLayerWaveDelay = runtimeLayerWaveDelay;

            spawnOrigin += simulationOffset;
            tankMinimum += simulationOffset;
            tankMaximum += simulationOffset;
            beachHeight += simulationOffset.y;

            // Keep the GameObject transform unchanged. The particle positions,
            // tank boundaries, emitter, seabed and render bounds now occupy the
            // correct horizontal line themselves.
            transform.position = Vector3.zero;

            waveHorizontalForce *= forceScale;
            waveVerticalForce *= forceScale;
            shoalingStrength *= forceScale;
            curlForwardStrength *= forceScale;
            curlDownStrength *= forceScale;
            curlRotationStrength *= forceScale;

            // Reapply the synchronized emitter after optional per-layer scaling.
            // With the default falloff of 1, every layer emits the exact same
            // full-sized wave.
            ApplyIndependentBigWaveEmitter();

            if (sourceMaterial != null)
            {
                runtimeLayerMaterial = new Material(sourceMaterial);
                renderingMaterial = runtimeLayerMaterial;
                runtimeLayerMaterial.SetFloat(
                    "_LayerDepthOffset",
                    runtimeLayerRenderDepth);
            }
        }

        private void CreateIndependentSimulationLayers()
        {
            if (independentLayersCreated ||
                !createIndependentWaveLayers ||
                independentLayerCount <= 1)
                return;

            independentLayersCreated = true;
            Material sourceMaterial = renderingMaterial;

            for (int layer = 1;
                 layer < independentLayerCount;
                 layer++)
            {
                GameObject layerObject = new GameObject(
                    $"Water Simulation Layer {layer}");

                layerObject.SetActive(false);
                layerObject.transform.SetParent(transform.parent, false);
                layerObject.transform.position = transform.position;
                layerObject.transform.rotation = transform.rotation;
                layerObject.transform.localScale = transform.localScale;

                PixelWaterGPU layerSimulation =
                    layerObject.AddComponent<PixelWaterGPU>();

                JsonUtility.FromJsonOverwrite(
                    JsonUtility.ToJson(this),
                    layerSimulation);

                float forceScale = Mathf.Pow(
                    independentLayerForceFalloff,
                    layer);

                // Every simulation occupies its own straight horizontal world-space line.
                // Layer 0 is the lowest foreground line. Each delayed layer is
                // moved upward by one fixed step and rendered farther behind.
                float horizontalLineY =
                    independentLayerVerticalOffset * layer;

                Vector3 lineOffset = new Vector3(
                    -independentLayerBackOffset * layer,
                    horizontalLineY,
                    0f);

                layerSimulation.ConfigureIndependentSimulationLayer(
                    layer,
                    independentLayerDelay * layer,
                    lineOffset,
                    forceScale,
                    1f,
                    sourceMaterial);

                layerObject.SetActive(true);
            }

            independentLayerIndex = 0;
            independentWaveDelay = 0f;
            runtimeLayerPosition = Vector2.zero;
            appliedRuntimeLayerPosition = Vector2.zero;
            runtimeLayerRenderDepth = 0f;
            runtimeLayerWaveDelay = 0f;
            appliedRuntimeLayerWaveDelay = 0f;

            if (renderingMaterial != null &&
                renderingMaterial.HasProperty("_LayerDepthOffset"))
            {
                renderingMaterial.SetFloat("_LayerDepthOffset", 0f);
            }
        }



        private void OnValidate()
        {
            independentLayerCount = Mathf.Clamp(independentLayerCount, 1, 5);
            independentLayerScaleFalloff = Mathf.Clamp(
                independentLayerScaleFalloff,
                0.75f,
                1f);
            independentLayerRiseCurve = Mathf.Clamp(
                independentLayerRiseCurve,
                0.5f,
                2f);

            cascadeEchoCount = Mathf.Clamp(cascadeEchoCount, 1, 4);
            cascadeDelay = Mathf.Max(0.02f, cascadeDelay);
            cascadeBandThickness = Mathf.Clamp(cascadeBandThickness, 0.05f, 1f);

            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            interactionRadius = Mathf.Max(0.01f, interactionRadius);
            particleSpacing = Mathf.Max(0.005f, particleSpacing);
            renderedParticleSize = Mathf.Max(0.001f, renderedParticleSize);
            waveEmitterWidth = Mathf.Max(0.1f, waveEmitterWidth);
            breakZoneWidth = Mathf.Max(0.08f, breakZoneWidth);
            curlRadius = Mathf.Max(0.15f, curlRadius);
            crestSurfaceBand = Mathf.Max(0.1f, crestSurfaceBand);
            waveLayerCount = Mathf.Clamp(waveLayerCount, 2, 5);
            crestLayerThickness = Mathf.Clamp(crestLayerThickness, 0.05f, 0.65f);
            shallowZoneWidth = Mathf.Max(0.08f, shallowZoneWidth);
            beachHeight = Mathf.Clamp(beachHeight, 0.12f, 0.80f);
            beachCurve = Mathf.Max(0.35f, beachCurve);
        }

        [ContextMenu("Surf Preset/Longboard Swell")]
        private void UseLongboardSwell() => ApplySurfPreset(SurfWaveType.LongboardSwell);

        [ContextMenu("Surf Preset/Beach Break")]
        private void UseBeachBreak() => ApplySurfPreset(SurfWaveType.BeachBreak);

        [ContextMenu("Surf Preset/Reef Barrel")]
        private void UseReefBarrel() => ApplySurfPreset(SurfWaveType.ReefBarrel);

        [ContextMenu("Surf Preset/Heavy Tube")]
        private void UseHeavyTube() => ApplySurfPreset(SurfWaveType.HeavyTube);

        [ContextMenu("Surf Preset/Layered Big Wave")]
        private void UseLayeredBigWave()
        {
            ApplySurfPreset(SurfWaveType.HeavyTube);
            layeredWaveEnabled = true;
            waveLayerCount = 4;
            waveLayerPhaseOffset = 0.29f;
            deepSurgeForce = 16f;
            bodyPushForce = 13f;
            crestLiftForce = 21f;
            crestLayerThickness = 0.22f;
            layerCompression = 0.88f;
            layerForwardStacking = 12f;
            lipThrowBoost = 11f;
            bigWaveScale = 1.55f;
            waveFrequency = 0.18f;
            waveEmitterWidth = 2.05f;
            waveHorizontalForce = 22f;
            waveVerticalForce = 8.5f;
            wavePulseSharpness = 4.2f;
            breakZoneWidth = 0.20f;
            curlRadius = 0.82f;
        }

        private void ApplySurfPreset(SurfWaveType preset)
        {
            surfWaveType = preset;
            switch (preset)
            {
                case SurfWaveType.LongboardSwell:
                    waveHorizontalForce = 14f; waveVerticalForce = 3.8f; waveFrequency = 0.20f;
                    waveVerticalVariation = 0.16f; wavePulseSharpness = 2.4f;
                    breakPoint = 0.80f; breakZoneWidth = 0.25f; shoalingStrength = 5f;
                    curlForwardStrength = 4f; curlDownStrength = 2f; curlRotationStrength = 2f; curlRadius = 0.9f;
                    break;
                case SurfWaveType.BeachBreak:
                    waveHorizontalForce = 18f; waveVerticalForce = 5.4f; waveFrequency = 0.31f;
                    waveVerticalVariation = 0.22f; wavePulseSharpness = 3f;
                    breakPoint = 0.70f; breakZoneWidth = 0.23f; shoalingStrength = 8f;
                    curlForwardStrength = 10f; curlDownStrength = 4.5f; curlRotationStrength = 6f; curlRadius = 0.78f;
                    break;
                case SurfWaveType.ReefBarrel:
                    waveHorizontalForce = 21f; waveVerticalForce = 6.5f; waveFrequency = 0.28f;
                    waveVerticalVariation = 0.28f; wavePulseSharpness = 3.2f;
                    breakPoint = 0.73f; breakZoneWidth = 0.17f; shoalingStrength = 10f;
                    curlForwardStrength = 17f; curlDownStrength = 7.5f; curlRotationStrength = 12f; curlRadius = 0.68f;
                    break;
                default:
                    waveHorizontalForce = 25f; waveVerticalForce = 8f; waveFrequency = 0.24f;
                    waveVerticalVariation = 0.20f; wavePulseSharpness = 4.2f;
                    breakPoint = 0.76f; breakZoneWidth = 0.14f; shoalingStrength = 14f;
                    curlForwardStrength = 23f; curlDownStrength = 11f; curlRotationStrength = 17f; curlRadius = 0.60f;
                    break;
            }

            if (Application.isPlaying)
                ResetSimulation();
        }

        [ContextMenu("Reset GPU Simulation")]
        public void ResetSimulation()
        {
            ReleaseBuffers();
            Initialise();
        }

        private void Initialise()
        {
            if (!SystemInfo.supportsComputeShaders || simulationShader == null || renderingMaterial == null)
            {
                enabled = false;
                Debug.LogError("Pixel Water GPU requires compute shader support and assigned GPU assets.", this);
                return;
            }

            particleCount = columns * rows;
            GPUParticle[] initialParticles = new GPUParticle[particleCount];

            int index = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float stagger = (y & 1) == 0 ? 0f : particleSpacing * 0.5f;
                    initialParticles[index++] = new GPUParticle
                    {
                        Position = spawnOrigin + new Vector2(x * particleSpacing + stagger, y * particleSpacing),
                        Velocity = Vector2.zero,
                        Density = 0f,
                        Foam = 0f
                    };
                }
            }

            int stride = Marshal.SizeOf<GPUParticle>();
            particleBuffers = new[]
            {
                new ComputeBuffer(particleCount, stride),
                new ComputeBuffer(particleCount, stride)
            };
            particleBuffers[0].SetData(initialParticles);
            particleBuffers[1].SetData(initialParticles);

            gridSize = new Vector2Int(
                Mathf.Max(1, Mathf.CeilToInt((tankMaximum.x - tankMinimum.x) / interactionRadius)),
                Mathf.Max(1, Mathf.CeilToInt((tankMaximum.y - tankMinimum.y) / interactionRadius))
            );
            gridCellCount = gridSize.x * gridSize.y;
            cellHeads = new ComputeBuffer(gridCellCount, sizeof(int));
            nextParticle = new ComputeBuffer(particleCount, sizeof(int));

            clearGridKernel = simulationShader.FindKernel("ClearGrid");
            buildGridKernel = simulationShader.FindKernel("BuildGrid");
            simulateKernel = simulationShader.FindKernel("Simulate");
            shiftParticlesKernel =
                simulationShader.FindKernel("ShiftParticles");
            readIndex = 0;
            simulationTime = 0f;
            appliedRuntimeLayerPosition = runtimeLayerPosition;
            appliedRuntimeLayerWaveDelay = runtimeLayerWaveDelay;

            renderBounds = new Bounds(
                (Vector3)((tankMinimum + tankMaximum) * 0.5f),
                new Vector3(tankMaximum.x - tankMinimum.x + 2f, tankMaximum.y - tankMinimum.y + 2f, 10f)
            );
        }

        private void ApplyRuntimeLayerInspectorChanges()
        {
            Vector2 positionDelta =
                runtimeLayerPosition -
                appliedRuntimeLayerPosition;

            if (positionDelta.sqrMagnitude > 0.0000001f)
            {
                ShiftSimulationWorldSpace(positionDelta);
                appliedRuntimeLayerPosition = runtimeLayerPosition;
            }

            if (!Mathf.Approximately(
                    runtimeLayerWaveDelay,
                    appliedRuntimeLayerWaveDelay))
            {
                independentWaveDelay =
                    Mathf.Max(0f, runtimeLayerWaveDelay);

                appliedRuntimeLayerWaveDelay =
                    runtimeLayerWaveDelay;
            }
        }


        private void Update()
        {
            if (particleBuffers == null)
                return;

            ApplyTransformOriginChanges();
            ApplyRuntimeLayerInspectorChanges();

            float fixedStep = 1f / Mathf.Max(30, simulationRate);
            float frameTime = Mathf.Min(Time.deltaTime, 1f / 20f);
            float substepDelta = Mathf.Min(fixedStep, frameTime / Mathf.Max(1, substeps));

            for (int i = 0; i < substeps; i++)
            {
                simulationTime += substepDelta;
                DispatchSimulation(substepDelta);
            }

            ScheduleSurfaceReadback();

            renderingMaterial.SetBuffer(ParticlesID, particleBuffers[readIndex]);
            renderingMaterial.SetFloat("_ParticleSize", renderedParticleSize);
            renderingMaterial.SetFloat(
                "_LayerDepthOffset",
                runtimeLayerRenderDepth);
            renderingMaterial.SetColor("_DeepWaterColor", GetIndependentLayerColour(deepWaterColor));
            renderingMaterial.SetColor("_MainWaterColor", GetIndependentLayerColour(mainWaterColor));
            renderingMaterial.SetColor("_SurfaceWaterColor", GetIndependentLayerColour(surfaceWaterColor));
            renderingMaterial.SetColor("_FoamColor", GetIndependentLayerColour(foamColor));
            renderingMaterial.SetColor("_ShallowWaterColor", GetIndependentLayerColour(shallowWaterColor));
            renderingMaterial.SetVector("_TankMin", tankMinimum);
            renderingMaterial.SetVector("_TankMax", tankMaximum);
            renderingMaterial.SetFloat("_SurfaceBand", surfaceBand);
            renderingMaterial.SetFloat("_ColourBrightness", colourBrightness);
            renderingMaterial.SetFloat("_FoamRenderStrength", foamRenderStrength);
            renderingMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
            renderingMaterial.SetFloat("_FoamBottomSuppression", foamBottomSuppression);
            renderingMaterial.SetFloat("_FoamSurfaceDensity", foamSurfaceDensity);
            renderingMaterial.SetFloat("_ShoreStart", shoreStart);
            renderingMaterial.SetFloat("_ShallowZoneWidth", shallowZoneWidth);

            TropicalSeabed seabed = GetComponentInChildren<TropicalSeabed>(true);
            if (seabed != null)
            {
                seabed.Configure(
                    tankMinimum, tankMaximum, shoreStart, shallowZoneWidth,
                    beachHeight, beachCurve, (int)surfWaveType, breakPoint,
                    horizontalSeabedEnabled, horizontalSeabedHeight,
                    sandColor, wetSandColor, deepSandColor);
                seabed.Draw();
            }

            Graphics.DrawProcedural(renderingMaterial, renderBounds, MeshTopology.Triangles, 6, particleCount);
        }


        private void EnsureTropicalSeabed()
        {
            TropicalSeabed seabed = GetComponentInChildren<TropicalSeabed>(true);
            if (seabed == null)
            {
                GameObject seabedObject = new("Tropical Seabed");
                seabedObject.transform.SetParent(transform, false);
                seabed = seabedObject.AddComponent<TropicalSeabed>();
            }

            seabed.Configure(
                tankMinimum, tankMaximum, shoreStart, shallowZoneWidth,
                beachHeight, beachCurve, (int)surfWaveType, breakPoint,
                horizontalSeabedEnabled, horizontalSeabedHeight,
                sandColor, wetSandColor, deepSandColor);
        }

        private void DispatchSimulation(float deltaTime)
        {
            ComputeBuffer readBuffer = particleBuffers[readIndex];
            ComputeBuffer writeBuffer = particleBuffers[1 - readIndex];

            simulationShader.SetInt("_ParticleCount", particleCount);
            simulationShader.SetInts("_GridSize", gridSize.x, gridSize.y);
            simulationShader.SetVector("_TankMin", tankMinimum);
            simulationShader.SetVector("_TankMax", tankMaximum);
            simulationShader.SetFloat("_CellSize", interactionRadius);
            simulationShader.SetFloat("_InteractionRadius", interactionRadius);
            simulationShader.SetFloat("_DeltaTime", deltaTime);
            simulationShader.SetVector("_Gravity", gravity);
            simulationShader.SetFloat("_PressureStrength", pressureStrength);
            simulationShader.SetFloat("_Viscosity", viscosity);
            simulationShader.SetFloat("_BoundaryBounce", boundaryBounce);
            simulationShader.SetFloat("_ParticleRadius", particleRadius);
            simulationShader.SetFloat("_MaximumSpeed", maximumSpeed);
            simulationShader.SetFloat("_FoamSpeedThreshold", foamSpeedThreshold);
            simulationShader.SetFloat("_FoamGeneration", foamGeneration);
            simulationShader.SetFloat("_FoamDecay", foamDecay);
            simulationShader.SetFloat("_FoamTurbulence", foamTurbulence);
            simulationShader.SetFloat("_FoamBottomSuppression", foamBottomSuppression);
            simulationShader.SetFloat("_FoamSurfaceDensity", foamSurfaceDensity);
            float layerSimulationTime =
                Mathf.Max(0f, simulationTime - independentWaveDelay);

            simulationShader.SetFloat(
                "_SimulationTime",
                layerSimulationTime);
            simulationShader.SetInt(
                "_WaveEnabled",
                waveEmitterEnabled &&
                simulationTime >= independentWaveDelay
                    ? 1
                    : 0);
            simulationShader.SetFloat("_WaveEmitterWidth", waveEmitterWidth);
            simulationShader.SetFloat("_WaveHorizontalForce", waveHorizontalForce);
            simulationShader.SetFloat("_WaveVerticalForce", waveVerticalForce);
            simulationShader.SetFloat("_WaveFrequency", waveFrequency);
            simulationShader.SetFloat("_WaveVerticalVariation", waveVerticalVariation);
            simulationShader.SetFloat("_WavePulseSharpness", wavePulseSharpness);
            simulationShader.SetInt("_CascadeEnabled", cascadeMode == WaveCascadeMode.Disabled ? 0 : 1);
            simulationShader.SetInt("_CascadeEchoCount", cascadeEchoCount);
            simulationShader.SetFloat("_CascadeDelay", cascadeDelay);
            simulationShader.SetFloat("_CascadeBackOffset", cascadeBackOffset);
            simulationShader.SetFloat("_CascadeVerticalOffset", cascadeVerticalOffset);
            simulationShader.SetFloat("_CascadeAmplitudeFalloff", cascadeAmplitudeFalloff);
            simulationShader.SetFloat("_CascadeSpeedFalloff", cascadeSpeedFalloff);
            simulationShader.SetFloat("_CascadeCurlRetention", cascadeCurlRetention);
            simulationShader.SetFloat("_CascadeVolumeForce", cascadeVolumeForce);
            simulationShader.SetFloat("_CascadeStackLift", cascadeStackLift);
            simulationShader.SetFloat("_CascadeBandThickness", cascadeBandThickness);
            simulationShader.SetFloat("_CascadeBlend", cascadeBlend);

            simulationShader.SetInt("_LayeredWaveEnabled", layeredWaveEnabled ? 1 : 0);
            simulationShader.SetInt("_WaveLayerCount", waveLayerCount);
            simulationShader.SetFloat("_WaveLayerPhaseOffset", waveLayerPhaseOffset);
            simulationShader.SetFloat("_DeepSurgeForce", deepSurgeForce);
            simulationShader.SetFloat("_BodyPushForce", bodyPushForce);
            simulationShader.SetFloat("_CrestLiftForce", crestLiftForce);
            simulationShader.SetFloat("_CrestLayerThickness", crestLayerThickness);
            simulationShader.SetFloat("_LayerCompression", layerCompression);
            simulationShader.SetFloat("_LayerForwardStacking", layerForwardStacking);
            simulationShader.SetFloat("_LipThrowBoost", lipThrowBoost);
            simulationShader.SetFloat("_BigWaveScale", bigWaveScale);
            simulationShader.SetInt("_SurfWaveType", (int)surfWaveType);
            simulationShader.SetFloat("_BreakPoint", breakPoint);
            simulationShader.SetFloat("_BreakZoneWidth", breakZoneWidth);
            simulationShader.SetFloat("_ShoalingStrength", shoalingStrength);
            simulationShader.SetFloat("_CurlForwardStrength", curlForwardStrength);
            simulationShader.SetFloat("_CurlDownStrength", curlDownStrength);
            simulationShader.SetFloat("_CurlRotationStrength", curlRotationStrength);
            simulationShader.SetFloat("_CurlRadius", curlRadius);
            simulationShader.SetFloat("_CrestSurfaceBand", crestSurfaceBand);
            simulationShader.SetFloat("_ShoreStart", shoreStart);
            simulationShader.SetFloat("_ShallowZoneWidth", shallowZoneWidth);
            simulationShader.SetFloat("_BeachHeight", beachHeight);
            simulationShader.SetFloat("_BeachCurve", beachCurve);
            simulationShader.SetInt(
                "_HorizontalSeabedEnabled",
                horizontalSeabedEnabled ? 1 : 0);
            simulationShader.SetFloat(
                "_HorizontalSeabedHeight",
                horizontalSeabedHeight);
            simulationShader.SetFloat("_ShoreFriction", shoreFriction);

            // Only the foreground/master simulation interacts with the board.
            // Rear horizontal rows remain fully independent visual wave fields.
            bool boardActive =
                independentLayerIndex == 0 &&
                surfboardTransform != null &&
                surfboardBody != null;
            simulationShader.SetInt("_BoardEnabled", boardActive ? 1 : 0);
            if (boardActive)
            {
                Vector3 right3 = surfboardTransform.right;
                Vector3 up3 = surfboardTransform.up;
                Vector2 right = new(right3.x, right3.y);
                Vector2 up = new(up3.x, up3.y);
                Vector3 boardPosition3 = surfboardTransform.TransformPoint(
                    new Vector3(surfboardLocalCenterOffset.x, surfboardLocalCenterOffset.y, 0f));
                Vector3 boardVelocity3 = surfboardBody.linearVelocity;
                simulationShader.SetVector("_BoardCenter", new Vector2(boardPosition3.x, boardPosition3.y));
                simulationShader.SetVector("_BoardRight", right);
                simulationShader.SetVector("_BoardUp", up);
                simulationShader.SetVector("_BoardHalfExtents", surfboardHalfExtents);
                simulationShader.SetVector("_BoardVelocity", new Vector2(boardVelocity3.x, boardVelocity3.y));
                simulationShader.SetFloat("_BoardAngularVelocity", surfboardBody.angularVelocity.z);
            }
            simulationShader.SetFloat("_BoardParticlePadding", boardParticlePadding);
            simulationShader.SetFloat("_BoardParticlePush", boardParticlePush);
            simulationShader.SetFloat("_BoardVelocityTransfer", boardVelocityTransfer);
            simulationShader.SetFloat("_BoardParticleDrain", boardParticleDrain);
            simulationShader.SetFloat("_BoardSurfaceGrip", boardSurfaceGrip);
            simulationShader.SetFloat("_BoardParticleRadiusScale", boardParticleRadiusScale);

            simulationShader.SetBuffer(clearGridKernel, CellHeadsID, cellHeads);
            simulationShader.Dispatch(clearGridKernel, Mathf.CeilToInt(gridCellCount / 64f), 1, 1);

            simulationShader.SetBuffer(buildGridKernel, ReadParticlesID, readBuffer);
            simulationShader.SetBuffer(buildGridKernel, CellHeadsID, cellHeads);
            simulationShader.SetBuffer(buildGridKernel, NextParticleID, nextParticle);
            simulationShader.Dispatch(buildGridKernel, Mathf.CeilToInt(particleCount / 64f), 1, 1);

            simulationShader.SetBuffer(simulateKernel, ReadParticlesID, readBuffer);
            simulationShader.SetBuffer(simulateKernel, WriteParticlesID, writeBuffer);
            simulationShader.SetBuffer(simulateKernel, CellHeadsID, cellHeads);
            simulationShader.SetBuffer(simulateKernel, NextParticleID, nextParticle);
            simulationShader.Dispatch(simulateKernel, Mathf.CeilToInt(particleCount / 64f), 1, 1);

            readIndex = 1 - readIndex;
        }

        private void ReleaseBuffers()
        {
            if (particleBuffers != null)
            {
                foreach (ComputeBuffer buffer in particleBuffers)
                    buffer?.Release();
            }

            particleBuffers = null;
            cellHeads?.Release();
            nextParticle?.Release();
            cellHeads = null;
            nextParticle = null;
        }

        public Vector2 TankMinimum => tankMinimum;
        public Vector2 TankMaximum => tankMaximum;
        public SurfWaveType ActiveSurfWaveType => surfWaveType;

        public float GetSeabedHeightAtWorldX(float worldX)
        {
            if (horizontalSeabedEnabled)
                return horizontalSeabedHeight;

            float horizontal01 = Mathf.InverseLerp(
                tankMinimum.x,
                tankMaximum.x,
                worldX);

            return TropicalSeabed.GetSeabedHeight(
                horizontal01,
                tankMinimum.y,
                tankMaximum.y,
                shoreStart,
                shallowZoneWidth,
                beachHeight,
                beachCurve,
                (int)surfWaveType,
                breakPoint);
        }

        /// <summary>
        /// Returns the actual asynchronously sampled GPU particle surface when available.
        /// A procedural fallback is used only during the first few frames.
        /// </summary>
        public float GetGameplaySurfaceHeight(float worldX)
        {
            if (hasParticleSurfaceSamples)
            {
                float u = Mathf.InverseLerp(tankMinimum.x, tankMaximum.x, worldX) * (SurfaceBinCount - 1);
                int a = Mathf.Clamp(Mathf.FloorToInt(u), 0, SurfaceBinCount - 1);
                int b = Mathf.Min(a + 1, SurfaceBinCount - 1);
                float t = u - a;
                return Mathf.Lerp(sampledSurface[a], sampledSurface[b], t);
            }

            return GetProceduralSurfaceFallback(worldX);
        }

        public Vector2 GetGameplayWaveVelocity(float worldX)
        {
            if (hasParticleSurfaceSamples)
            {
                float u = Mathf.InverseLerp(tankMinimum.x, tankMaximum.x, worldX) * (SurfaceBinCount - 1);
                int a = Mathf.Clamp(Mathf.FloorToInt(u), 0, SurfaceBinCount - 1);
                int b = Mathf.Min(a + 1, SurfaceBinCount - 1);
                float t = u - a;
                return Vector2.Lerp(sampledVelocity[a], sampledVelocity[b], t);
            }

            float x01 = Mathf.InverseLerp(tankMinimum.x, tankMaximum.x, worldX);
            float phase = simulationTime * waveFrequency * Mathf.PI * 2f - x01 * 8.2f;
            float breakMask = 1f - Mathf.Clamp01(Mathf.Abs(x01 - breakPoint) / Mathf.Max(0.04f, breakZoneWidth));
            float forward = Mathf.Lerp(2.2f, 6.5f, Mathf.Max(0f, Mathf.Sin(phase))) +
                            breakMask * curlForwardStrength * 0.12f;
            float vertical = Mathf.Cos(phase) * 1.2f +
                             breakMask * Mathf.Max(0f, Mathf.Sin(phase)) * 1.8f;
            return new Vector2(forward, vertical);
        }

        private float GetProceduralSurfaceFallback(float worldX)
        {
            float baseSurface = Mathf.Clamp(spawnOrigin.y + (rows - 1) * particleSpacing,
                tankMinimum.y + 0.8f, tankMaximum.y - 0.25f);
            float x01 = Mathf.InverseLerp(tankMinimum.x, tankMaximum.x, worldX);
            float phase = simulationTime * waveFrequency * Mathf.PI * 2f - x01 * 8.2f;
            float envelope = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.03f, breakPoint + 0.12f, x01));
            float shoreFade = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.88f, 1f, x01));
            float amplitude = Mathf.Lerp(0.12f,
                surfWaveType >= SurfWaveType.ReefBarrel ? 0.42f : 0.28f,
                envelope) * shoreFade;
            float swell = Mathf.Sin(phase) * amplitude;
            float crest = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase + 0.45f)),
                wavePulseSharpness) * amplitude * 0.55f;
            return Mathf.Max(GetSeabedHeightAtWorldX(worldX) + 0.18f,
                baseSurface + swell + crest);
        }

        private void ScheduleSurfaceReadback()
        {
            if (readbackPending || Time.unscaledTime < nextReadbackTime ||
                particleBuffers == null || particleBuffers[readIndex] == null)
                return;

            readbackPending = true;
            nextReadbackTime = Time.unscaledTime + 0.055f;
            AsyncGPUReadback.Request(particleBuffers[readIndex], request =>
            {
                readbackPending = false;
                if (!this || request.hasError || !request.done)
                    return;

                var data = request.GetData<GPUParticle>();
                BuildSurfaceBins(data);
            });
        }

        private void BuildSurfaceBins(Unity.Collections.NativeArray<GPUParticle> particles)
        {
            float width = Mathf.Max(0.001f, tankMaximum.x - tankMinimum.x);
            for (int i = 0; i < SurfaceBinCount; i++)
            {
                float x = Mathf.Lerp(tankMinimum.x, tankMaximum.x,
                    i / (float)(SurfaceBinCount - 1));
                sampledSurface[i] = GetSeabedHeightAtWorldX(x) + particleRadius;
                sampledVelocity[i] = Vector2.zero;
                sampledCounts[i] = 0;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                GPUParticle p = particles[i];
                int bin = Mathf.Clamp(
                    Mathf.RoundToInt((p.Position.x - tankMinimum.x) / width *
                                     (SurfaceBinCount - 1)),
                    0, SurfaceBinCount - 1);

                if (p.Position.y >= sampledSurface[bin])
                {
                    sampledSurface[bin] = p.Position.y;
                    sampledVelocity[bin] = p.Velocity;
                    sampledCounts[bin] = 1;
                }
            }

            // Fill occasional empty bins from neighbours and lightly smooth the noisy particle crest.
            for (int pass = 0; pass < 2; pass++)
            {
                float previous = sampledSurface[0];
                for (int i = 1; i < SurfaceBinCount - 1; i++)
                {
                    float current = sampledSurface[i];
                    float smoothed = (previous + current * 2f + sampledSurface[i + 1]) * 0.25f;
                    previous = current;
                    sampledSurface[i] = Mathf.Max(
                        GetSeabedHeightAtWorldX(Mathf.Lerp(tankMinimum.x, tankMaximum.x,
                            i / (float)(SurfaceBinCount - 1))) + particleRadius,
                        smoothed);
                }
            }

            hasParticleSurfaceSamples = true;
        }

        public void RegisterSurfboard(
            Transform boardTransform,
            Rigidbody boardRigidbody,
            Vector2 halfExtents,
            Vector2 localCenterOffset)
        {
            surfboardTransform = boardTransform;
            surfboardBody = boardRigidbody;
            surfboardHalfExtents = halfExtents;
            surfboardLocalCenterOffset = localCenterOffset;
        }

        // Compatibility overload for older callers.
        public void RegisterSurfboard(
            Transform boardTransform,
            Rigidbody boardRigidbody,
            Vector2 halfExtents)
        {
            RegisterSurfboard(
                boardTransform,
                boardRigidbody,
                halfExtents,
                Vector2.zero);
        }

        public void UnregisterSurfboard(Transform boardTransform)
        {
            if (surfboardTransform != boardTransform)
                return;

            surfboardTransform = null;
            surfboardBody = null;
            surfboardLocalCenterOffset = Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireCube((tankMinimum + tankMaximum) * 0.5f, tankMaximum - tankMinimum);

            if (waveEmitterEnabled)
            {
                Gizmos.DrawWireCube(
                    new Vector3(tankMinimum.x + waveEmitterWidth * 0.5f, (tankMinimum.y + tankMaximum.y) * 0.5f, 0f),
                    new Vector3(waveEmitterWidth, tankMaximum.y - tankMinimum.y, 0f)
                );
            }
        }
    }
}
