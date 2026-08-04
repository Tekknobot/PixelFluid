using System;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Controls the existing tropical_island_0 child beneath the Starry Night
    /// background. It remains hidden until eight minutes into Day 4, then fades
    /// onto the horizon and uses extremely subtle opposite-direction parallax.
    /// The island's authored scale is never changed.
    /// </summary>
    [DefaultExecutionOrder(10020)]
    [DisallowMultipleComponent]
    public sealed class DayFourTropicalIslandHorizon : MonoBehaviour
    {
        [Header("Existing Background Island")]
        [SerializeField] private string islandObjectName = "tropical_island_0";
        [SerializeField, Min(0.1f)] private float searchInterval = 0.5f;

        [Header("Day 4 Reveal")]
        [SerializeField, Min(0f)] private float revealAtSeconds = 8f * 60f;
        [SerializeField, Min(0.1f)] private float revealDuration = 24f;

        [Header("Very Distant Parallax")]
        [Tooltip("Fraction of player travel applied in the opposite direction. Keep extremely small.")]
        [SerializeField, Range(0f, 0.05f)] private float parallaxFactor = 0.012f;
        [SerializeField, Min(0f)] private float maximumHorizontalOffset = 3.5f;
        [SerializeField, Min(0.1f)] private float parallaxSmoothTime = 1.8f;

        private SurfDayProgressionDirector director;
        private TinyWaveSurfer surfer;
        private Transform island;
        private SpriteRenderer[] islandRenderers;
        private Color[] authoredColours;
        private Vector3 authoredLocalPosition;
        private Vector3 authoredLocalScale;
        private float playerAnchorX;
        private float currentOffset;
        private float offsetVelocity;
        private float nextSearchAt;
        private bool captured;

        private void Awake()
        {
            director = FindFirstObjectByType<SurfDayProgressionDirector>();
            surfer = FindFirstObjectByType<TinyWaveSurfer>();
            TryFindIsland();
        }

        private void Update()
        {
            if (director == null)
                director = FindFirstObjectByType<SurfDayProgressionDirector>();

            if (surfer == null)
                surfer = FindFirstObjectByType<TinyWaveSurfer>();

            if (island == null)
            {
                if (Time.unscaledTime >= nextSearchAt)
                {
                    nextSearchAt = Time.unscaledTime + searchInterval;
                    TryFindIsland();
                }

                return;
            }

            // Never alter the artist-authored scale, even if another system touches it.
            island.localScale = authoredLocalScale;

            int day = director != null ? director.CurrentDay : 1;
            float runTime = director != null ? director.RunTime : 0f;

            float visibility;
            if (day < 4)
            {
                visibility = 0f;
            }
            else if (day == 4)
            {
                visibility = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(revealAtSeconds, revealAtSeconds + revealDuration, runTime));
            }
            else
            {
                visibility = 1f;
            }

            ApplyVisibility(visibility);
            UpdateParallax(visibility);
        }

        private void TryFindIsland()
        {
            Transform found = FindTransformByName(islandObjectName);
            if (found == null)
                return;

            island = found;
            islandRenderers = island.GetComponentsInChildren<SpriteRenderer>(true);
            authoredColours = new Color[islandRenderers.Length];
            for (int i = 0; i < islandRenderers.Length; i++)
                authoredColours[i] = islandRenderers[i].color;

            authoredLocalPosition = island.localPosition;
            authoredLocalScale = island.localScale;
            playerAnchorX = surfer != null ? surfer.transform.position.x : 0f;
            currentOffset = 0f;
            offsetVelocity = 0f;
            captured = true;
        }

        private void UpdateParallax(float visibility)
        {
            if (!captured)
                return;

            float targetOffset = 0f;
            if (visibility > 0.001f && surfer != null)
            {
                float playerTravel = surfer.transform.position.x - playerAnchorX;
                targetOffset = Mathf.Clamp(
                    -playerTravel * parallaxFactor,
                    -maximumHorizontalOffset,
                    maximumHorizontalOffset);
            }

            currentOffset = Mathf.SmoothDamp(
                currentOffset,
                targetOffset,
                ref offsetVelocity,
                parallaxSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            Vector3 position = authoredLocalPosition;
            position.x += currentOffset;
            island.localPosition = position;
        }

        private void ApplyVisibility(float alpha)
        {
            if (islandRenderers == null || authoredColours == null)
                return;

            alpha = Mathf.Clamp01(alpha);
            for (int i = 0; i < islandRenderers.Length; i++)
            {
                SpriteRenderer renderer = islandRenderers[i];
                if (renderer == null)
                    continue;

                Color colour = authoredColours[i];
                colour.a *= alpha;
                renderer.color = colour;
                renderer.enabled = alpha > 0.001f;
            }
        }

        private static Transform FindTransformByName(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
                return null;

            Transform[] transforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return null;
        }

        private void OnDisable()
        {
            if (!captured || island == null)
                return;

            island.localPosition = authoredLocalPosition;
            island.localScale = authoredLocalScale;
        }
    }

    public static class DayFourTropicalIslandHorizonBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<DayFourTropicalIslandHorizon>() != null)
                return;

            GameObject host = new("Day 4 Tropical Island Horizon");
            host.AddComponent<DayFourTropicalIslandHorizon>();
        }
    }
}
