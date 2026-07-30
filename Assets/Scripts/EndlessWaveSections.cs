using System;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Builds three complete horizontal copies of the existing vertical wave stack
    /// and recycles only a fully off-camera section.
    ///
    /// Each horizontal copy preserves the exact layer index, vertical position,
    /// render depth, tank dimensions and particle state of its centre counterpart.
    /// The player and camera never wrap.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    [DisallowMultipleComponent]
    public sealed class EndlessWaveSections : MonoBehaviour
    {
        [Header("Section Geometry")]
        [Tooltip("Horizontal overlap between neighbouring ocean sections.")]
        [SerializeField, Min(0f)]
        private float seamOverlap = 3f;

        [Tooltip("How far beyond the camera a section must travel before recycling.")]
        [SerializeField, Min(0f)]
        private float recyclePadding = 1f;

        [SerializeField]
        private bool installAutomatically = true;

        [Header("Diagnostics")]
        [SerializeField]
        private bool logRecycling;

        private readonly List<WaveSection> sections = new();

        private Camera gameplayCamera;
        private float sectionStride;
        private bool ready;

        public static EndlessWaveSections Instance { get; private set; }

        public bool IsReady => ready;

        /// <summary>Raised immediately after a physical ocean section is recycled.</summary>
        public static event Action<IReadOnlyList<PixelWaterGPU>, float> SectionRecycled;

        public float MinimumWorldX =>
            sections.Count == 0
                ? float.NegativeInfinity
                : sections.Min(section => section.MinimumX);

        public float MaximumWorldX =>
            sections.Count == 0
                ? float.PositiveInfinity
                : sections.Max(section => section.MaximumX);

        private sealed class WaveSection
        {
            public readonly List<PixelWaterGPU> Layers = new();

            public int LogicalIndex;

            public float MinimumX =>
                Layers.Count == 0
                    ? 0f
                    : Layers
                        .Where(layer => layer != null)
                        .Min(layer => layer.TankMinimum.x);

            public float MaximumX =>
                Layers.Count == 0
                    ? 0f
                    : Layers
                        .Where(layer => layer != null)
                        .Max(layer => layer.TankMaximum.x);

            public float CentreX => (MinimumX + MaximumX) * 0.5f;

            /// <summary>
            /// Moves every corresponding vertical wave layer by the exact same
            /// horizontal distance.
            /// </summary>
            public void Shift(float horizontalDistance)
            {
                Vector2 delta = new(horizontalDistance, 0f);

                foreach (PixelWaterGPU layer in Layers)
                {
                    if (layer != null)
                        layer.ShiftCompleteSimulation(delta);
                }

                LogicalIndex += horizontalDistance > 0f ? 3 : -3;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private IEnumerator Start()
        {
            if (!installAutomatically)
                yield break;

            gameplayCamera = Camera.main;

            // Allow the original master water simulation to create all of its
            // independent vertical layers before copying the completed stack.
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            BuildThreeSections();
        }

        private void LateUpdate()
        {
            if (!ready || sections.Count != 3)
                return;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (gameplayCamera == null)
                return;

            float halfCameraWidth = gameplayCamera.orthographic
                ? gameplayCamera.orthographicSize * gameplayCamera.aspect
                : 8f;

            float cameraLeft =
                gameplayCamera.transform.position.x - halfCameraWidth;

            float cameraRight =
                gameplayCamera.transform.position.x + halfCameraWidth;

            WaveSection leftSection = sections
                .OrderBy(section => section.CentreX)
                .First();

            WaveSection rightSection = sections
                .OrderBy(section => section.CentreX)
                .Last();

            // Travelling right: move the completely hidden left section
            // three strides to the right.
            if (leftSection.MaximumX < cameraLeft - recyclePadding)
            {
                float recycleDistance = sectionStride * 3f;
                leftSection.Shift(recycleDistance);
                SectionRecycled?.Invoke(leftSection.Layers, recycleDistance);

                if (logRecycling)
                {
                    Debug.Log(
                        $"Recycled left ocean section to " +
                        $"x={leftSection.CentreX:0.00}",
                        this);
                }
            }
            // Travelling left: move the completely hidden right section
            // three strides to the left.
            else if (rightSection.MinimumX > cameraRight + recyclePadding)
            {
                float recycleDistance = -sectionStride * 3f;
                rightSection.Shift(recycleDistance);
                SectionRecycled?.Invoke(rightSection.Layers, recycleDistance);

                if (logRecycling)
                {
                    Debug.Log(
                        $"Recycled right ocean section to " +
                        $"x={rightSection.CentreX:0.00}",
                        this);
                }
            }
        }

        private void BuildThreeSections()
        {
            if (ready)
                return;

            sections.Clear();

            PixelWaterGPU[] allWater =
                FindObjectsByType<PixelWaterGPU>(
                    FindObjectsSortMode.None);

            // Find one centre simulation for each vertical layer.
            // Choosing the simulation closest to world X zero prevents an old
            // horizontal copy from accidentally becoming the source.
            List<PixelWaterGPU> centreLayers = allWater
                .Where(water => water != null)
                .GroupBy(water => water.IndependentLayerIndex)
                .Select(group => group
                    .OrderBy(water =>
                        Mathf.Abs(GetLayerCentreX(water)))
                    .First())
                .OrderBy(water => water.IndependentLayerIndex)
                .ToList();

            if (centreLayers.Count < 2)
            {
                Debug.LogError(
                    "EndlessWaveSections could not find the completed " +
                    "vertical wave stack.",
                    this);

                return;
            }

            // Use the foreground/bottom layer as the authoritative section width.
            // This specifically prevents the lower wave from developing a gap when
            // rear layers have small alternating horizontal offsets.
            PixelWaterGPU bottomLayer = centreLayers
                .FirstOrDefault(layer =>
                    layer.IndependentLayerIndex == 0);

            if (bottomLayer == null)
                bottomLayer = centreLayers[0];

            float sectionWidth =
                bottomLayer.TankMaximum.x -
                bottomLayer.TankMinimum.x;

            float maximumSafeOverlap =
                Mathf.Max(0f, sectionWidth - 0.25f);

            float appliedOverlap =
                Mathf.Clamp(
                    seamOverlap,
                    0f,
                    maximumSafeOverlap);

            sectionStride =
                Mathf.Max(
                    0.25f,
                    sectionWidth - appliedOverlap);

            WaveSection centreSection = new()
            {
                LogicalIndex = 0
            };

            centreSection.Layers.AddRange(centreLayers);
            sections.Add(centreSection);

            WaveSection leftSection = CloneSection(
                centreLayers,
                -sectionStride,
                -1,
                "Left Ocean Section");

            WaveSection rightSection = CloneSection(
                centreLayers,
                sectionStride,
                1,
                "Right Ocean Section");

            sections.Add(leftSection);
            sections.Add(rightSection);

            ready = true;

            Debug.Log(
                $"Three-section endless ocean ready. " +
                $"Layers per section: {centreLayers.Count}, " +
                $"width: {sectionWidth:0.000}, " +
                $"overlap: {appliedOverlap:0.000}, " +
                $"stride: {sectionStride:0.000}",
                this);
        }

        private WaveSection CloneSection(
            List<PixelWaterGPU> sourceLayers,
            float horizontalOffset,
            int logicalIndex,
            string sectionName)
        {
            WaveSection section = new()
            {
                LogicalIndex = logicalIndex
            };

            GameObject marker = new(sectionName);
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.zero;

            foreach (PixelWaterGPU source in sourceLayers)
            {
                if (source == null)
                    continue;

                GameObject sourceObject = source.gameObject;
                bool sourceWasActive = sourceObject.activeSelf;

                /*
                 * Instantiate the source while inactive so its PixelWaterGPU
                 * OnEnable method cannot create another vertical stack before
                 * ConfigureAsHorizontalSectionClone is called.
                 */
                sourceObject.SetActive(false);

                GameObject cloneObject = Instantiate(sourceObject);

                sourceObject.SetActive(sourceWasActive);

                cloneObject.name =
                    $"{sourceObject.name} [{sectionName}]";


                /*
                * PixelWaterGPU coordinates were copied from the centre simulation and are
                * already in world space. The clone GameObject must remain at XY zero or the
                * original transform offset can be applied again during OnEnable.
                */
                cloneObject.transform.SetParent(marker.transform, false);

                cloneObject.transform.position = new Vector3(
                    0f,
                    0f,
                    sourceObject.transform.position.z);

                cloneObject.transform.rotation =
                    sourceObject.transform.rotation;

                cloneObject.transform.localScale =
                    sourceObject.transform.localScale;

                DisableDuplicatedGameplayBehaviours(cloneObject);

                PixelWaterGPU clone =
                    cloneObject.GetComponent<PixelWaterGPU>();

                if (clone == null)
                {
                    Debug.LogError(
                        $"Horizontal section clone of {sourceObject.name} " +
                        "does not contain PixelWaterGPU.",
                        cloneObject);

                    Destroy(cloneObject);
                    continue;
                }

                // Preserve the exact vertical layer index and total layer count.
                // This preserves the same render queue/depth ordering as the centre.
                clone.ConfigureAsHorizontalSectionClone(
                    source.IndependentLayerIndex,
                    sourceLayers.Count);

                cloneObject.SetActive(true);

                /*
                 * Move the complete initialized simulation—not merely its Transform.
                 * This translates:
                 *
                 * - GPU particles
                 * - tank minimum/maximum
                 * - render bounds
                 * - emitter coordinates
                 * - sampled wave surface
                 * - seabed coordinates
                 *
                 * Every layer receives the identical horizontal offset, so the
                 * bottom layer cannot remain behind and create a visible gap.
                 */
                clone.ShiftCompleteSimulation(
                    new Vector2(horizontalOffset, 0f));

                section.Layers.Add(clone);
            }

            // Keep a stable layer order inside every horizontal section.
            section.Layers.Sort(
                (a, b) =>
                    a.IndependentLayerIndex.CompareTo(
                        b.IndependentLayerIndex));

            ValidateSectionLayering(section, sourceLayers, sectionName);

            return section;
        }

        private static void DisableDuplicatedGameplayBehaviours(
            GameObject cloneObject)
        {
            MonoBehaviour[] behaviours =
                cloneObject.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null ||
                    behaviour is PixelWaterGPU)
                {
                    continue;
                }

                if (behaviour is ProceduralWaveAudio)
                {
                    behaviour.enabled = false;
                    continue;
                }

                string typeName = behaviour.GetType().Name;

                if (typeName.EndsWith(
                        "Spawner",
                        StringComparison.Ordinal) ||
                    typeName.EndsWith(
                        "Manager",
                        StringComparison.Ordinal) ||
                    typeName.EndsWith(
                        "Controller",
                        StringComparison.Ordinal))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void ValidateSectionLayering(
            WaveSection section,
            List<PixelWaterGPU> sourceLayers,
            string sectionName)
        {
            if (section.Layers.Count != sourceLayers.Count)
            {
                Debug.LogWarning(
                    $"{sectionName} contains {section.Layers.Count} layers, " +
                    $"but the centre contains {sourceLayers.Count}.");
            }

            for (int i = 0; i < section.Layers.Count; i++)
            {
                PixelWaterGPU layer = section.Layers[i];

                if (layer == null)
                    continue;

                if (layer.IndependentLayerIndex != i)
                {
                    Debug.LogWarning(
                        $"{sectionName} layer order mismatch. " +
                        $"List position {i} contains vertical layer " +
                        $"{layer.IndependentLayerIndex}.",
                        layer);
                }
            }
        }

        private static float GetLayerCentreX(
            PixelWaterGPU layer)
        {
            return (
                layer.TankMinimum.x +
                layer.TankMaximum.x) * 0.5f;
        }

        /// <summary>
        /// Returns the current horizontal centre of every active ocean section,
        /// ordered from left to right.
        /// </summary>
        public IReadOnlyList<float> GetSectionCentres()
        {
            if (!ready || sections.Count == 0)
                return System.Array.Empty<float>();

            return sections
                .Where(section => section != null && section.Layers.Any(layer => layer != null))
                .OrderBy(section => section.CentreX)
                .Select(section => section.CentreX)
                .ToArray();
        }

        public IReadOnlyList<PixelWaterGPU> GetLayersNearest(
            float worldX)
        {
            if (!ready || sections.Count == 0)
            {
                return FindNearestLayerStack(worldX);
            }

            // Prefer a section that actually covers the requested X position.
            // During the three-unit overlaps, choose the section whose centre is
            // nearest so crossings remain predictable.
            WaveSection coveringSection = sections
                .Where(section =>
                    worldX >= section.MinimumX &&
                    worldX <= section.MaximumX)
                .OrderBy(section =>
                    Mathf.Abs(section.CentreX - worldX))
                .FirstOrDefault();

            WaveSection selectedSection =
                coveringSection ??
                sections
                    .OrderBy(section =>
                        Mathf.Abs(section.CentreX - worldX))
                    .First();

            return selectedSection.Layers
                .Where(layer => layer != null)
                .OrderBy(layer =>
                    layer.IndependentLayerIndex)
                .ToList();
        }

        public static List<PixelWaterGPU> LayersNearest(
            float worldX)
        {
            if (Instance != null)
            {
                return Instance
                    .GetLayersNearest(worldX)
                    .Where(layer => layer != null)
                    .OrderBy(layer =>
                        layer.IndependentLayerIndex)
                    .ToList();
            }

            return FindNearestLayerStack(worldX);
        }

        private static List<PixelWaterGPU> FindNearestLayerStack(
            float worldX)
        {
            return FindObjectsByType<PixelWaterGPU>(
                    FindObjectsSortMode.None)
                .Where(layer => layer != null)
                .GroupBy(layer =>
                    layer.IndependentLayerIndex)
                .Select(group =>
                    group
                        .OrderBy(layer =>
                            Mathf.Abs(
                                GetLayerCentreX(layer) -
                                worldX))
                        .First())
                .OrderBy(layer =>
                    layer.IndependentLayerIndex)
                .ToList();
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<EndlessWaveSections>() != null)
                return;

            new GameObject(
                    "Endless Three-Section Wave World")
                .AddComponent<EndlessWaveSections>();
        }
    }
}