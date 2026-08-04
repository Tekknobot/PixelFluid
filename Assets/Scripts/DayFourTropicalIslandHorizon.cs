using System;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Controls the authored tropical_island_0 child beneath Starry Night.
    /// It is visible normally through Days 1 and 2, remains visible during the
    /// beginning of Day 3, then drifts away and fades during the final portion
    /// of Day 3. It remains absent from Day 4 onward. Its scale is never changed.
    /// </summary>
    [DefaultExecutionOrder(10020)]
    [DisallowMultipleComponent]
    public sealed class DayFourTropicalIslandHorizon : MonoBehaviour
    {
        [Header("Existing Background Island")]
        [SerializeField] private string islandObjectName = "tropical_island_0";
        [SerializeField, Min(0.1f)] private float searchInterval = 0.5f;

        [Header("Day 3 Departure")]
        [Tooltip("The tropical island begins receding eight minutes into Day 3.")]
        [SerializeField, Min(0f)] private float departureAtSeconds = 8f * 60f;
        [Tooltip("It finishes disappearing at the twelve-minute end of Day 3.")]
        [SerializeField, Min(0.1f)] private float departureDuration = 4f * 60f;
        [SerializeField, Min(0f)] private float departureHorizontalDistance = 5.5f;
        [SerializeField, Min(0f)] private float departureVerticalDistance = 0.55f;

        [Header("Subtle Player Parallax")]
        [SerializeField, Range(0f, 0.05f)] private float parallaxFactor = 0.01f;
        [SerializeField, Min(0f)] private float maximumParallaxOffset = 2.5f;
        [SerializeField, Min(0.1f)] private float movementSmoothTime = 1.8f;

        private SurfDayProgressionDirector director;
        private TinyWaveSurfer surfer;
        private Transform island;
        private SpriteRenderer[] islandRenderers;
        private Color[] authoredColours;
        private Vector3 authoredLocalPosition;
        private Vector3 authoredLocalScale;
        private float playerAnchorX;
        private Vector2 currentOffset;
        private Vector2 offsetVelocity;
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

            island.localScale = authoredLocalScale;

            int day = director != null ? director.CurrentDay : 1;
            float runTime = director != null ? director.RunTime : 0f;

            float departure = 0f;
            float visibility = 1f;

            if (day == 3)
            {
                departure = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(
                        departureAtSeconds,
                        departureAtSeconds + departureDuration,
                        runTime));
                visibility = 1f - departure;
            }
            else if (day >= 4)
            {
                departure = 1f;
                visibility = 0f;
            }

            ApplyVisibility(visibility);
            UpdatePosition(departure, visibility);
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
            currentOffset = Vector2.zero;
            offsetVelocity = Vector2.zero;
            captured = true;
        }

        private void UpdatePosition(float departure, float visibility)
        {
            if (!captured)
                return;

            float playerOffset = 0f;
            if (visibility > 0.001f && surfer != null)
            {
                float playerTravel = surfer.transform.position.x - playerAnchorX;
                playerOffset = Mathf.Clamp(
                    -playerTravel * parallaxFactor,
                    -maximumParallaxOffset,
                    maximumParallaxOffset);
            }

            // Recede opposite Chuck's current travel direction. When he is nearly
            // stationary, use the right side so the landmark still leaves by sunset.
            float travelSign = 1f;
            if (surfer != null)
            {
                float travel = surfer.transform.position.x - playerAnchorX;
                if (Mathf.Abs(travel) > 0.1f)
                    travelSign = Mathf.Sign(travel);
            }

            Vector2 targetOffset = new(
                playerOffset - travelSign * departureHorizontalDistance * departure,
                departureVerticalDistance * departure);

            currentOffset = Vector2.SmoothDamp(
                currentOffset,
                targetOffset,
                ref offsetVelocity,
                movementSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            island.localPosition = authoredLocalPosition +
                new Vector3(currentOffset.x, currentOffset.y, 0f);
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

            GameObject host = new("Tropical Island Timeline");
            host.AddComponent<DayFourTropicalIslandHorizon>();
        }
    }
}
