using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// GPU-rendered static sand grains. The grains form a deep basin that rises into
    /// a curved beach. The water compute shader uses the same height function for collision.
    /// </summary>
    [ExecuteAlways]
    public sealed class TropicalSeabed : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SandParticle
        {
            public Vector2 Position;
            public float Random;
            public float Depth;
        }

        [SerializeField] private Shader sandParticleShader;
        [SerializeField, Range(80, 260)] private int columns = 180;
        [SerializeField, Range(18, 90)] private int layers = 48;
        [SerializeField, Min(0.005f)] private float grainSpacing = 0.045f;
        [SerializeField, Min(0.002f)] private float grainSize = 0.028f;
        [SerializeField, Range(0f, 0.6f)] private float grainJitter = 0.28f;
        [SerializeField, Range(0f, 1f)] private float colourVariation = 0.22f;

        private ComputeBuffer sandBuffer;
        private Material material;
        private Bounds bounds;
        private int particleCount;
        private int configurationHash;

        private static readonly int SandParticlesID = Shader.PropertyToID("_SandParticles");

        public void Configure(
            Vector2 tankMin,
            Vector2 tankMax,
            float shoreStart,
            float shallowWidth,
            float beachHeight,
            float beachCurve,
            int surfWaveType,
            float breakPoint,
            bool horizontalSeabedEnabled,
            float horizontalSeabedHeight,
            Color drySand,
            Color wetSand,
            Color deepSand)
        {
            EnsureMaterial();
            if (material == null)
                return;

            int newHash;
            unchecked
            {
                newHash = 17;
                newHash = newHash * 31 + tankMin.GetHashCode();
                newHash = newHash * 31 + tankMax.GetHashCode();
                newHash = newHash * 31 + shoreStart.GetHashCode();
                newHash = newHash * 31 + shallowWidth.GetHashCode();
                newHash = newHash * 31 + beachHeight.GetHashCode();
                newHash = newHash * 31 + beachCurve.GetHashCode();
                newHash = newHash * 31 + surfWaveType;
                newHash = newHash * 31 + breakPoint.GetHashCode();
                newHash = newHash * 31 + horizontalSeabedEnabled.GetHashCode();
                newHash = newHash * 31 + horizontalSeabedHeight.GetHashCode();
                newHash = newHash * 31 + columns;
                newHash = newHash * 31 + layers;
                newHash = newHash * 31 + grainSpacing.GetHashCode();
                newHash = newHash * 31 + grainSize.GetHashCode();
                newHash = newHash * 31 + grainJitter.GetHashCode();
            }

            if (sandBuffer == null || configurationHash != newHash)
            {
                configurationHash = newHash;
                BuildParticles(
                    tankMin,
                    tankMax,
                    shoreStart,
                    shallowWidth,
                    beachHeight,
                    beachCurve,
                    surfWaveType,
                    breakPoint,
                    horizontalSeabedEnabled,
                    horizontalSeabedHeight);
            }

            material.SetBuffer(SandParticlesID, sandBuffer);
            material.SetFloat("_GrainSize", grainSize);
            material.SetFloat("_ColourVariation", colourVariation);
            material.SetColor("_DrySandColor", drySand);
            material.SetColor("_WetSandColor", wetSand);
            material.SetColor("_DeepSandColor", deepSand);
            material.SetVector("_TankMin", tankMin);
            material.SetVector("_TankMax", tankMax);
        }

        public void Draw()
        {
            if (material == null || sandBuffer == null || particleCount == 0)
                return;

            Graphics.DrawProcedural(material, bounds, MeshTopology.Triangles, 6, particleCount);
        }

        private void OnEnable() => EnsureMaterial();
        private void OnDisable() => Release();

        private void EnsureMaterial()
        {
            if (sandParticleShader == null)
                sandParticleShader = Shader.Find("PixelOcean/GPU Particle Sand");

            if (sandParticleShader != null && (material == null || material.shader != sandParticleShader))
            {
                if (material != null)
                    DestroyImmediate(material);
                material = new Material(sandParticleShader) { name = "Tropical Sand Particles Runtime" };
            }
        }

        private void BuildParticles(
            Vector2 tankMin,
            Vector2 tankMax,
            float shoreStart,
            float shallowWidth,
            float beachHeight,
            float beachCurve,
            int surfWaveType,
            float breakPoint,
            bool horizontalSeabedEnabled,
            float horizontalSeabedHeight)
        {
            sandBuffer?.Release();

            float width = tankMax.x - tankMin.x;
            float usableSpacing = Mathf.Max(0.01f, grainSpacing);
            int xCount = Mathf.Max(columns, Mathf.CeilToInt(width / usableSpacing));
            float xSpacing = width / Mathf.Max(1, xCount - 1);
            float layerSpacing = usableSpacing * 0.82f;

            SandParticle[] particles = new SandParticle[xCount * layers];
            var random = new System.Random(7351);
            int index = 0;

            for (int x = 0; x < xCount; x++)
            {
                float horizontal01 = x / (float)Mathf.Max(1, xCount - 1);
                float surfaceY = horizontalSeabedEnabled
                    ? horizontalSeabedHeight
                    : GetSeabedHeight(
                        horizontal01,
                        tankMin.y,
                        tankMax.y,
                        shoreStart,
                        shallowWidth,
                        beachHeight,
                        beachCurve,
                        surfWaveType,
                        breakPoint);

                for (int layer = 0; layer < layers; layer++)
                {
                    float rx = (float)random.NextDouble();
                    float ry = (float)random.NextDouble();
                    float jitterX = (rx - 0.5f) * xSpacing * grainJitter;
                    float jitterY = (ry - 0.5f) * layerSpacing * grainJitter;
                    float stagger = (layer & 1) == 0 ? 0f : xSpacing * 0.5f;

                    particles[index++] = new SandParticle
                    {
                        Position = new Vector2(
                            tankMin.x + x * xSpacing + stagger + jitterX,
                            surfaceY - layer * layerSpacing + jitterY),
                        Random = (float)random.NextDouble(),
                        Depth = layer / (float)Mathf.Max(1, layers - 1)
                    };
                }
            }

            particleCount = particles.Length;
            sandBuffer = new ComputeBuffer(particleCount, Marshal.SizeOf<SandParticle>());
            sandBuffer.SetData(particles);
            bounds = new Bounds(
                new Vector3((tankMin.x + tankMax.x) * 0.5f, (tankMin.y + tankMax.y) * 0.5f, 0.8f),
                new Vector3(width + 2f, tankMax.y - tankMin.y + layers * layerSpacing + 2f, 4f));
        }

        public static float GetSeabedHeight(
            float horizontal01,
            float tankBottom,
            float tankTop,
            float shoreStart,
            float shallowWidth,
            float beachHeight,
            float beachCurve,
            int surfWaveType,
            float breakPoint)
        {
            float tankHeight = Mathf.Max(0.001f, tankTop - tankBottom);
            float deepFloor = tankBottom + tankHeight * 0.055f;
            float targetHeight = tankHeight * Mathf.Clamp(beachHeight, 0.1f, 0.82f);

            if (surfWaveType >= 2)
            {
                // Reef profiles preserve deep water, rise sharply at the take-off zone,
                // and finish on a shallow shelf so the lip can pitch forward.
                float rampStart = Mathf.Clamp01(breakPoint - (surfWaveType == 3 ? 0.12f : 0.17f));
                float rampEnd = Mathf.Clamp01(breakPoint + (surfWaveType == 3 ? 0.055f : 0.085f));
                float reefRamp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rampStart, rampEnd, horizontal01));
                reefRamp = Mathf.Pow(reefRamp, surfWaveType == 3 ? 0.72f : 0.88f);
                float shelfRise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rampEnd, 1f, horizontal01));
                return deepFloor + targetHeight * (reefRamp * 0.82f + shelfRise * 0.18f);
            }

            float beachStart = Mathf.Clamp01(shoreStart - shallowWidth);
            float ramp = Mathf.InverseLerp(beachStart, 1f, horizontal01);
            float curve = surfWaveType == 0 ? Mathf.Max(1.15f, beachCurve) : Mathf.Max(0.65f, beachCurve);
            ramp = Mathf.Pow(Mathf.SmoothStep(0f, 1f, ramp), curve);
            return deepFloor + ramp * targetHeight;
        }

        private void Release()
        {
            sandBuffer?.Release();
            sandBuffer = null;
            particleCount = 0;

            if (material != null)
            {
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
                material = null;
            }
        }
    }
}
