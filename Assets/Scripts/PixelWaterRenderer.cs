using UnityEngine;

namespace PixelOcean
{
    [RequireComponent(typeof(PixelWaterSimulation))]
    public sealed class PixelWaterRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private Color waterColor =
            new(0.05f, 0.55f, 1f, 1f);

        [SerializeField, Min(0.01f)] private float particleSize = 0.075f;
        [SerializeField] private int sortingOrder;

        private PixelWaterSimulation simulation;
        private Mesh mesh;
        private Material material;

        private Vector3[] vertices = System.Array.Empty<Vector3>();
        private Vector2[] ultravioletCoordinates = System.Array.Empty<Vector2>();
        private Color[] colors = System.Array.Empty<Color>();
        private int[] triangles = System.Array.Empty<int>();

        private static readonly int MainTextureId =
            Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            simulation = GetComponent<PixelWaterSimulation>();
            CreateMaterial();
            CreateMesh();
        }

        private void LateUpdate()
        {
            if (simulation == null || simulation.Particles == null)
            {
                return;
            }

            EnsureMeshCapacity(simulation.ParticleCount);
            UpdateParticleMesh();
        }

        private void CreateMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError(
                    "PixelWaterRenderer could not find the Sprites/Default shader.",
                    this
                );

                enabled = false;
                return;
            }

            material = new Material(shader)
            {
                name = "Pixel Water Runtime Material",
                hideFlags = HideFlags.HideAndDontSave
            };

            material.SetTexture(MainTextureId, Texture2D.whiteTexture);
            material.renderQueue = 3000 + sortingOrder;
        }

        private void CreateMesh()
        {
            mesh = new Mesh
            {
                name = "Pixel Water Particle Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };

            mesh.MarkDynamic();

            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();

            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();

            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = sortingOrder;
        }

        private void EnsureMeshCapacity(int particleCount)
        {
            int requiredVertexCount = particleCount * 4;
            int requiredTriangleIndexCount = particleCount * 6;

            if (vertices.Length == requiredVertexCount)
            {
                return;
            }

            vertices = new Vector3[requiredVertexCount];
            ultravioletCoordinates = new Vector2[requiredVertexCount];
            colors = new Color[requiredVertexCount];
            triangles = new int[requiredTriangleIndexCount];

            for (int i = 0; i < particleCount; i++)
            {
                int vertexIndex = i * 4;
                int triangleIndex = i * 6;

                ultravioletCoordinates[vertexIndex] = new Vector2(0f, 0f);
                ultravioletCoordinates[vertexIndex + 1] = new Vector2(0f, 1f);
                ultravioletCoordinates[vertexIndex + 2] = new Vector2(1f, 1f);
                ultravioletCoordinates[vertexIndex + 3] = new Vector2(1f, 0f);

                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 1;
                triangles[triangleIndex + 2] = vertexIndex + 2;

                triangles[triangleIndex + 3] = vertexIndex;
                triangles[triangleIndex + 4] = vertexIndex + 2;
                triangles[triangleIndex + 5] = vertexIndex + 3;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = ultravioletCoordinates;
            mesh.colors = colors;
            mesh.triangles = triangles;
        }

        private void UpdateParticleMesh()
        {
            float halfSize = particleSize * 0.5f;
            Particle[] particles = simulation.Particles;

            for (int i = 0; i < particles.Length; i++)
            {
                Vector2 position = particles[i].Position;
                int vertexIndex = i * 4;

                vertices[vertexIndex] =
                    new Vector3(position.x - halfSize, position.y - halfSize, 0f);

                vertices[vertexIndex + 1] =
                    new Vector3(position.x - halfSize, position.y + halfSize, 0f);

                vertices[vertexIndex + 2] =
                    new Vector3(position.x + halfSize, position.y + halfSize, 0f);

                vertices[vertexIndex + 3] =
                    new Vector3(position.x + halfSize, position.y - halfSize, 0f);

                colors[vertexIndex] = waterColor;
                colors[vertexIndex + 1] = waterColor;
                colors[vertexIndex + 2] = waterColor;
                colors[vertexIndex + 3] = waterColor;
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                Destroy(mesh);
                Destroy(material);
            }
            else
            {
                DestroyImmediate(mesh);
                DestroyImmediate(material);
            }
        }
    }
}