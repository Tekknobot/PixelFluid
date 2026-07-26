using UnityEngine;

namespace PixelOcean
{
    public struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;

        public float Density;
        public float Pressure;

        public Particle(Vector2 position)
        {
            Position = position;
            Velocity = Vector2.zero;
            Density = 0f;
            Pressure = 0f;
        }
    }
}