using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    public sealed class SpatialGrid
    {
        private readonly Dictionary<Vector2Int, List<int>> cells = new();
        private readonly float cellSize;

        public SpatialGrid(float cellSize)
        {
            this.cellSize = Mathf.Max(0.001f, cellSize);
        }

        public void Rebuild(Particle[] particles)
        {
            foreach (List<int> list in cells.Values)
            {
                list.Clear();
            }

            for (int i = 0; i < particles.Length; i++)
            {
                Vector2Int cell = PositionToCell(particles[i].Position);

                if (!cells.TryGetValue(cell, out List<int> list))
                {
                    list = new List<int>(16);
                    cells.Add(cell, list);
                }

                list.Add(i);
            }
        }

        public bool TryGetCell(Vector2Int coordinate, out List<int> indices)
        {
            return cells.TryGetValue(coordinate, out indices);
        }

        public Vector2Int PositionToCell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize)
            );
        }
    }
}