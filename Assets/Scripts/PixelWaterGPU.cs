using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PixelOcean
{
    public sealed class PixelWaterGPU : MonoBehaviour
    {
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
        [SerializeField, Min(1)] private int columns = 96;
        [SerializeField, Min(1)] private int rows = 54;
        [SerializeField, Min(0.005f)] private float particleSpacing = 0.055f;
        [SerializeField] private Vector2 spawnOrigin = new(-3.5f, -0.8f);

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

        [Header("Wave Emitter")]
        [SerializeField] private bool waveEmitterEnabled = true;
        [SerializeField, Min(0.1f)] private float waveEmitterWidth = 1.25f;
        [SerializeField, Range(0f, 40f)] private float waveHorizontalForce = 16f;
        [SerializeField, Range(0f, 30f)] private float waveVerticalForce = 7f;
        [SerializeField, Range(0.05f, 3f)] private float waveFrequency = 0.55f;
        [SerializeField, Range(0f, 4f)] private float waveVerticalVariation = 1.15f;

        [Header("Water Colour")]
        [SerializeField] private Color deepWaterColor = new(0.01f, 0.10f, 0.32f, 0.96f);
        [SerializeField] private Color mainWaterColor = new(0.02f, 0.38f, 0.90f, 0.96f);
        [SerializeField] private Color surfaceWaterColor = new(0.10f, 0.75f, 1.00f, 0.98f);
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
        private Bounds renderBounds;
        private float simulationTime;

        private static readonly int ReadParticlesID = Shader.PropertyToID("_ReadParticles");
        private static readonly int WriteParticlesID = Shader.PropertyToID("_WriteParticles");
        private static readonly int ParticlesID = Shader.PropertyToID("_Particles");
        private static readonly int CellHeadsID = Shader.PropertyToID("_CellHeads");
        private static readonly int NextParticleID = Shader.PropertyToID("_NextParticle");

        private void OnEnable() => Initialise();
        private void OnDisable() => ReleaseBuffers();

        private void OnValidate()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            interactionRadius = Mathf.Max(0.01f, interactionRadius);
            particleSpacing = Mathf.Max(0.005f, particleSpacing);
            renderedParticleSize = Mathf.Max(0.001f, renderedParticleSize);
            waveEmitterWidth = Mathf.Max(0.1f, waveEmitterWidth);
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
            readIndex = 0;
            simulationTime = 0f;

            renderBounds = new Bounds(
                (Vector3)((tankMinimum + tankMaximum) * 0.5f),
                new Vector3(tankMaximum.x - tankMinimum.x + 2f, tankMaximum.y - tankMinimum.y + 2f, 10f)
            );
        }

        private void Update()
        {
            if (particleBuffers == null)
                return;

            float fixedStep = 1f / Mathf.Max(30, simulationRate);
            float frameTime = Mathf.Min(Time.deltaTime, 1f / 20f);
            float substepDelta = Mathf.Min(fixedStep, frameTime / Mathf.Max(1, substeps));

            for (int i = 0; i < substeps; i++)
            {
                simulationTime += substepDelta;
                DispatchSimulation(substepDelta);
            }

            renderingMaterial.SetBuffer(ParticlesID, particleBuffers[readIndex]);
            renderingMaterial.SetFloat("_ParticleSize", renderedParticleSize);
            renderingMaterial.SetColor("_DeepWaterColor", deepWaterColor);
            renderingMaterial.SetColor("_MainWaterColor", mainWaterColor);
            renderingMaterial.SetColor("_SurfaceWaterColor", surfaceWaterColor);
            renderingMaterial.SetColor("_FoamColor", foamColor);
            renderingMaterial.SetVector("_TankMin", tankMinimum);
            renderingMaterial.SetVector("_TankMax", tankMaximum);
            renderingMaterial.SetFloat("_SurfaceBand", surfaceBand);
            renderingMaterial.SetFloat("_ColourBrightness", colourBrightness);
            renderingMaterial.SetFloat("_FoamRenderStrength", foamRenderStrength);
            renderingMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
            renderingMaterial.SetFloat("_FoamBottomSuppression", foamBottomSuppression);
            renderingMaterial.SetFloat("_FoamSurfaceDensity", foamSurfaceDensity);

            Graphics.DrawProcedural(renderingMaterial, renderBounds, MeshTopology.Triangles, 6, particleCount);
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
            simulationShader.SetFloat("_SimulationTime", simulationTime);
            simulationShader.SetInt("_WaveEnabled", waveEmitterEnabled ? 1 : 0);
            simulationShader.SetFloat("_WaveEmitterWidth", waveEmitterWidth);
            simulationShader.SetFloat("_WaveHorizontalForce", waveHorizontalForce);
            simulationShader.SetFloat("_WaveVerticalForce", waveVerticalForce);
            simulationShader.SetFloat("_WaveFrequency", waveFrequency);
            simulationShader.SetFloat("_WaveVerticalVariation", waveVerticalVariation);

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
