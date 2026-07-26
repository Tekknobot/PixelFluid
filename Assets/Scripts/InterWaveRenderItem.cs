using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Places SpriteRenderer and MeshRenderer materials in a transparent render
    /// queue between two interleaved PixelWaterGPU render passes.
    /// Lane 0 is the lowest/background gap, increasing toward the foreground.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterWaveRenderItem : MonoBehaviour
    {
        private sealed class RendererMaterialState
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
        }

        [SerializeField] private PixelWaterGPU water;
        [SerializeField, Min(0)] private int laneIndex;
        [SerializeField] private bool includeChildren = true;
        [SerializeField] private bool applyEveryFrame;

        private readonly List<RendererMaterialState> rendererStates = new();
        private readonly List<Material> runtimeMaterials = new();
        private int lastAppliedQueue = -1;

        private void OnEnable()
        {
            CaptureOriginalMaterials();
            ApplyRenderQueue();
        }

        private void LateUpdate()
        {
            if (applyEveryFrame)
                ApplyRenderQueue();
        }

        private void CaptureOriginalMaterials()
        {
            if (rendererStates.Count > 0)
                return;

            Renderer[] renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                    continue;

                rendererStates.Add(new RendererMaterialState
                {
                    Renderer = targetRenderer,
                    OriginalMaterials = targetRenderer.sharedMaterials
                });
            }
        }

        [ContextMenu("Apply Inter-Wave Render Queue")]
        public void ApplyRenderQueue()
        {
            if (water == null)
                water = FindFirstObjectByType<PixelWaterGPU>();

            if (water == null)
            {
                Debug.LogWarning(
                    "InterWaveRenderItem could not find a PixelWaterGPU simulation.",
                    this);
                return;
            }

            CaptureOriginalMaterials();

            int queue = water.GetInterleavedObjectRenderQueue(laneIndex);
            if (queue == lastAppliedQueue && runtimeMaterials.Count > 0)
                return;

            RestoreOriginalMaterials();
            ReleaseRuntimeMaterials();

            foreach (RendererMaterialState state in rendererStates)
            {
                if (state.Renderer == null)
                    continue;

                Material[] assignedMaterials =
                    new Material[state.OriginalMaterials.Length];

                for (int i = 0; i < state.OriginalMaterials.Length; i++)
                {
                    Material source = state.OriginalMaterials[i];
                    if (source == null)
                        continue;

                    Material runtimeMaterial = new Material(source)
                    {
                        name = $"{source.name} Inter-Wave Lane {laneIndex}",
                        hideFlags = HideFlags.HideAndDontSave,
                        renderQueue = queue
                    };

                    assignedMaterials[i] = runtimeMaterial;
                    runtimeMaterials.Add(runtimeMaterial);
                }

                state.Renderer.sharedMaterials = assignedMaterials;
            }

            lastAppliedQueue = queue;
        }

        public void SetLane(int newLaneIndex)
        {
            laneIndex = Mathf.Max(0, newLaneIndex);
            lastAppliedQueue = -1;
            ApplyRenderQueue();
        }

        private void OnDisable()
        {
            RestoreOriginalMaterials();
            ReleaseRuntimeMaterials();
            lastAppliedQueue = -1;
        }

        private void OnDestroy()
        {
            RestoreOriginalMaterials();
            ReleaseRuntimeMaterials();
        }

        private void RestoreOriginalMaterials()
        {
            foreach (RendererMaterialState state in rendererStates)
            {
                if (state.Renderer != null)
                    state.Renderer.sharedMaterials = state.OriginalMaterials;
            }
        }

        private void ReleaseRuntimeMaterials()
        {
            foreach (Material material in runtimeMaterials)
            {
                if (material == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }

            runtimeMaterials.Clear();
        }
    }
}
