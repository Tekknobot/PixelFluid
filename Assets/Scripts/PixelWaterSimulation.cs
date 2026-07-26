using System;
using UnityEngine;

namespace PixelOcean
{
    public sealed class PixelWaterSimulation : MonoBehaviour
    {
        [Header("Particle Setup")]
        [SerializeField, Min(1)] private int columns = 45;
        [SerializeField, Min(1)] private int rows = 28;
        [SerializeField, Min(0.01f)] private float particleSpacing = 0.08f;
        [SerializeField] private Vector2 spawnOrigin = new(-4.2f, 0.2f);

        [Header("Simulation")]
        [SerializeField, Min(0.001f)] private float simulationTimeStep = 1f / 120f;
        [SerializeField, Range(1, 8)] private int substeps = 1;
        [SerializeField] private Vector2 gravity = new(0f, -9.81f);
        [SerializeField, Range(0f, 1f)] private float boundaryBounce = 0.15f;
        [SerializeField, Range(0f, 10f)] private float boundaryFriction = 2f;

        [Header("Tank Bounds")]
        [SerializeField] private Vector2 tankMinimum = new(-5f, -4f);
        [SerializeField] private Vector2 tankMaximum = new(5f, 4f);
        [SerializeField, Min(0.001f)] private float particleRadius = 0.035f;

        [Header("Fluid Interaction")]
        [SerializeField, Min(0.01f)] private float interactionRadius = 0.09f;
        [SerializeField, Range(1, 8)] private int solverIterations = 2;
        [SerializeField, Range(0f, 1f)] private float separationStrength = 0.6f;
        [SerializeField, Range(0f, 1f)] private float viscosity = 0.04f;

        [Header("Fluid Response")]
        [SerializeField, Range(0f, 5f)] private float velocityDamping = 0.12f;
        [SerializeField, Min(1f)] private float maximumSpeed = 25f;

        public Particle[] Particles { get; private set; } = Array.Empty<Particle>();
        public int ParticleCount => Particles.Length;
        public float ParticleRadius => particleRadius;
        public Vector2 TankMinimum => tankMinimum;
        public Vector2 TankMaximum => tankMaximum;

        private int simulationFrame;

        private SpatialGrid spatialGrid;
        private Vector2[] positionCorrections = System.Array.Empty<Vector2>();
        private int[] neighbourCounts = System.Array.Empty<int>();

        private Vector2[] previousPositions = System.Array.Empty<Vector2>();
        private Vector2[] velocityChanges = System.Array.Empty<Vector2>();

        private void Awake()
        {
            ResetSimulation();
        }

        private void Update()
        {
            float frameDelta = Mathf.Min(Time.deltaTime, 1f / 30f);
            float substepDelta = frameDelta / Mathf.Max(1, substeps);

            for (int i = 0; i < substeps; i++)
            {
                Simulate(substepDelta);
            }
        }

        [ContextMenu("Reset Simulation")]
        public void ResetSimulation()
        {
            int safeColumns = Mathf.Max(1, columns);
            int safeRows = Mathf.Max(1, rows);

            Particles = new Particle[safeColumns * safeRows];

            int index = 0;

            for (int y = 0; y < safeRows; y++)
            {
                for (int x = 0; x < safeColumns; x++)
                {
                    Vector2 offset = new(
                        x * particleSpacing,
                        y * particleSpacing
                    );

                    // Slight staggering prevents particles from forming
                    // perfectly rigid vertical columns.
                    if ((y & 1) == 1)
                    {
                        offset.x += particleSpacing * 0.5f;
                    }

                    Particles[index] = new Particle(spawnOrigin + offset);
                    index++;
                }
            }

            spatialGrid = new SpatialGrid(interactionRadius);
            positionCorrections = new Vector2[Particles.Length];
            neighbourCounts = new int[Particles.Length]; 

            previousPositions = new Vector2[Particles.Length];
            velocityChanges = new Vector2[Particles.Length];                       
        }

        private void Simulate(float deltaTime)
        {
            for (int i = 0; i < Particles.Length; i++)
            {
                Particle particle = Particles[i];

                particle.Velocity += gravity * deltaTime;
                particle.Position += particle.Velocity * deltaTime;

                Particles[i] = particle;
            }

            for (int iteration = 0; iteration < solverIterations; iteration++)
            {
                spatialGrid.Rebuild(Particles);
                SolveParticleSeparation();
                ResolveAllTankCollisions(deltaTime);
            }

            simulationFrame++;

            if ((simulationFrame & 1) == 0)
            {
                ApplyViscosity();
            }

            for (int i = 0; i < Particles.Length; i++)
            {
                Particle particle = Particles[i];
                ResolveTankCollision(ref particle, deltaTime);
                Particles[i] = particle;
            }
        }

        private void SolveParticleSeparation()
        {
            System.Array.Clear(positionCorrections, 0, positionCorrections.Length);
            System.Array.Clear(neighbourCounts, 0, neighbourCounts.Length);

            float radiusSquared = interactionRadius * interactionRadius;

            for (int i = 0; i < Particles.Length; i++)
            {
                Vector2 position = Particles[i].Position;
                Vector2Int centreCell = spatialGrid.PositionToCell(position);

                for (int cellY = -1; cellY <= 1; cellY++)
                {
                    for (int cellX = -1; cellX <= 1; cellX++)
                    {
                        Vector2Int cell =
                            centreCell + new Vector2Int(cellX, cellY);

                        if (!spatialGrid.TryGetCell(cell, out var indices))
                        {
                            continue;
                        }

                        for (int n = 0; n < indices.Count; n++)
                        {
                            int j = indices[n];

                            if (j <= i)
                            {
                                continue;
                            }

                            Vector2 difference =
                                Particles[j].Position - position;

                            float distanceSquared = difference.sqrMagnitude;

                            if (distanceSquared >= radiusSquared)
                            {
                                continue;
                            }

                            Vector2 direction;
                            float distance;

                            if (distanceSquared < 0.0000001f)
                            {
                                float angle = (i * 0.754877666f + j) * 6.283185f;

                                direction = new Vector2(
                                    Mathf.Cos(angle),
                                    Mathf.Sin(angle)
                                );

                                distance = 0f;
                            }
                            else
                            {
                                distance = Mathf.Sqrt(distanceSquared);
                                direction = difference / distance;
                            }

                            float overlap = interactionRadius - distance;

                            Vector2 correction =
                                direction * overlap * 0.5f * separationStrength;

                            positionCorrections[i] -= correction;
                            positionCorrections[j] += correction;

                            neighbourCounts[i]++;
                            neighbourCounts[j]++;
                        }
                    }
                }
            }

            for (int i = 0; i < Particles.Length; i++)
            {
                Particle particle = Particles[i];
                particle.Position += positionCorrections[i];
                Particles[i] = particle;
            }
        }

        private void ApplyViscosity()
        {
            System.Array.Clear(
                velocityChanges,
                0,
                velocityChanges.Length
            );

            float radiusSquared = interactionRadius * interactionRadius;

            spatialGrid.Rebuild(Particles);

            for (int i = 0; i < Particles.Length; i++)
            {
                Vector2Int centreCell =
                    spatialGrid.PositionToCell(Particles[i].Position);

                for (int cellY = -1; cellY <= 1; cellY++)
                {
                    for (int cellX = -1; cellX <= 1; cellX++)
                    {
                        Vector2Int cell =
                            centreCell + new Vector2Int(cellX, cellY);

                        if (!spatialGrid.TryGetCell(cell, out var indices))
                        {
                            continue;
                        }

                        for (int n = 0; n < indices.Count; n++)
                        {
                            int j = indices[n];

                            if (j <= i)
                            {
                                continue;
                            }

                            Vector2 offset =
                                Particles[j].Position -
                                Particles[i].Position;

                            float distanceSquared = offset.sqrMagnitude;

                            if (distanceSquared >= radiusSquared)
                            {
                                continue;
                            }

                            float distance =
                                Mathf.Sqrt(Mathf.Max(distanceSquared, 0.000001f));

                            float influence =
                                1f - distance / interactionRadius;

                            Vector2 velocityDifference =
                                Particles[j].Velocity -
                                Particles[i].Velocity;

                            Vector2 change =
                                velocityDifference *
                                influence *
                                viscosity *
                                0.5f;

                            velocityChanges[i] += change;
                            velocityChanges[j] -= change;
                        }
                    }
                }
            }

            for (int i = 0; i < Particles.Length; i++)
            {
                Particle particle = Particles[i];
                particle.Velocity += velocityChanges[i];
                Particles[i] = particle;
            }
        }

        private void ClampAllParticlePositions()
        {
            float minimumX = tankMinimum.x + particleRadius;
            float maximumX = tankMaximum.x - particleRadius;
            float minimumY = tankMinimum.y + particleRadius;
            float maximumY = tankMaximum.y - particleRadius;

            for (int i = 0; i < Particles.Length; i++)
            {
                Particle particle = Particles[i];

                particle.Position.x = Mathf.Clamp(
                    particle.Position.x,
                    minimumX,
                    maximumX
                );

                particle.Position.y = Mathf.Clamp(
                    particle.Position.y,
                    minimumY,
                    maximumY
                );

                Particles[i] = particle;
            }
        }

        private void ResolveBoundaryVelocities()
        {
            float minimumX = tankMinimum.x + particleRadius;
            float maximumX = tankMaximum.x - particleRadius;
            float minimumY = tankMinimum.y + particleRadius;
            float maximumY = tankMaximum.y - particleRadius;

            const float boundaryTolerance = 0.0001f;

            for (int i = 0; i < Particles.Length; i++)
            {
                Particle particle = Particles[i];

                if (particle.Position.x <= minimumX + boundaryTolerance &&
                    particle.Velocity.x < 0f)
                {
                    particle.Velocity.x =
                        -particle.Velocity.x * boundaryBounce;

                    particle.Velocity.y *= 1f - viscosity;
                }

                if (particle.Position.x >= maximumX - boundaryTolerance &&
                    particle.Velocity.x > 0f)
                {
                    particle.Velocity.x =
                        -particle.Velocity.x * boundaryBounce;

                    particle.Velocity.y *= 1f - viscosity;
                }

                if (particle.Position.y <= minimumY + boundaryTolerance &&
                    particle.Velocity.y < 0f)
                {
                    particle.Velocity.y =
                        -particle.Velocity.y * boundaryBounce;

                    particle.Velocity.x *= 1f - viscosity;
                }

                if (particle.Position.y >= maximumY - boundaryTolerance &&
                    particle.Velocity.y > 0f)
                {
                    particle.Velocity.y =
                        -particle.Velocity.y * boundaryBounce;

                    particle.Velocity.x *= 1f - viscosity;
                }

                Particles[i] = particle;
            }
        }

        private void ResolveAllTankCollisions(float deltaTime)
        {
            for (int i = 0; i < Particles.Length; i++)
            {
                Particle particle = Particles[i];
                ResolveTankCollision(ref particle, deltaTime);
                Particles[i] = particle;
            }
        }

        private void ResolveTankCollision(ref Particle particle, float deltaTime)
        {
            float minimumX = tankMinimum.x + particleRadius;
            float maximumX = tankMaximum.x - particleRadius;
            float minimumY = tankMinimum.y + particleRadius;
            float maximumY = tankMaximum.y - particleRadius;

            float frictionMultiplier =
                Mathf.Clamp01(1f - boundaryFriction * deltaTime);

            if (particle.Position.x < minimumX)
            {
                particle.Position.x = minimumX;
                particle.Velocity.x =
                    Mathf.Abs(particle.Velocity.x) * boundaryBounce;
                particle.Velocity.y *= frictionMultiplier;
            }
            else if (particle.Position.x > maximumX)
            {
                particle.Position.x = maximumX;
                particle.Velocity.x =
                    -Mathf.Abs(particle.Velocity.x) * boundaryBounce;
                particle.Velocity.y *= frictionMultiplier;
            }

            if (particle.Position.y < minimumY)
            {
                particle.Position.y = minimumY;
                particle.Velocity.y =
                    Mathf.Abs(particle.Velocity.y) * boundaryBounce;
                particle.Velocity.x *= frictionMultiplier;
            }
            else if (particle.Position.y > maximumY)
            {
                particle.Position.y = maximumY;
                particle.Velocity.y =
                    -Mathf.Abs(particle.Velocity.y) * boundaryBounce;
                particle.Velocity.x *= frictionMultiplier;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;

            Vector3 centre = new(
                (tankMinimum.x + tankMaximum.x) * 0.5f,
                (tankMinimum.y + tankMaximum.y) * 0.5f,
                0f
            );

            Vector3 size = new(
                tankMaximum.x - tankMinimum.x,
                tankMaximum.y - tankMinimum.y,
                0f
            );

            Gizmos.DrawWireCube(centre, size);
        }
    }
}