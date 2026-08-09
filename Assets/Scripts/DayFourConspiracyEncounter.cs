using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Day 4 environmental story layer. The classified research island
    /// approaches through the ocean and gradually replaces the distant sky.
    /// A non-interactive facility display may also be shown at the Day 4 start
    /// so the player can inspect the recovered structure artwork.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayFourConspiracyEncounter : MonoBehaviour
    {
        [Header("Island Approach")]
        [SerializeField, Min(0f)] private float islandRevealAt = 240f;
        [SerializeField, Range(0.5f, 0.95f)] private float islandRevealJourneyProgress = 0.80f;
        [SerializeField, Min(2f)] private float islandOffscreenDistance = 18f;
        [SerializeField, Min(0f)] private float islandApproachSpeed = 0.16f;
        [SerializeField, Range(0f, 0.08f)] private float oppositePlayerMovement = 0.015f;
        [SerializeField, Min(0.5f)] private float islandDiscoveryDistance = 5f;
        [SerializeField, Min(1f)] private float skyFullyGoneDistance = 11f;
        [SerializeField] private Vector2 islandPositionOffset = new(0f, 0.35f);

        private SurfDayProgressionDirector director;
        private Transform player;
        private Transform island;
        private ProceduralStarryNight starryNight;
        private float initialIslandDistance;
        private float previousPlayerX;
        private bool islandAnnounced;
        private bool completed;

        public bool IslandRevealed => island != null;
        public float IslandDistance => island != null && player != null
            ? Mathf.Abs(player.position.x - island.position.x)
            : -1f;

        public static void Begin(
            SurfDayProgressionDirector progression,
            TinyWaveSurfer surfer,
            bool restoreIsland = false,
            float restoredIslandDistance = -1f)
        {
            if (progression == null || surfer == null)
                return;

            DayFourConspiracyEncounter existing =
                FindFirstObjectByType<DayFourConspiracyEncounter>();
            if (existing != null)
            {
                existing.director = progression;
                existing.player = surfer.transform;
                existing.previousPlayerX = surfer.transform.position.x;
                if (restoreIsland && existing.island == null)
                    existing.RevealIsland(false, restoredIslandDistance);
                return;
            }

            GameObject host = new("Day Four Classified Research Route");
            DayFourConspiracyEncounter encounter =
                host.AddComponent<DayFourConspiracyEncounter>();
            encounter.Initialise(
                progression,
                surfer.transform,
                restoreIsland,
                restoredIslandDistance);
        }

        private void Initialise(
            SurfDayProgressionDirector progression,
            Transform surfer,
            bool restoreIsland,
            float restoredIslandDistance)
        {
            director = progression;
            player = surfer;
            previousPlayerX = surfer.position.x;
            starryNight = FindFirstObjectByType<ProceduralStarryNight>();
            starryNight?.SetExternalVisibility(1f);

            // Defensive cleanup: Day 4 must never contain the Day 3 facility,
            // including a copy left over by a save restore or older patch.
            foreach (SecretFacilityEncounter facility in FindObjectsByType<SecretFacilityEncounter>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (facility != null && !facility.IsDayFourDisplayOnly)
                    Destroy(facility.gameObject);
            }

            if (restoreIsland || ShouldRevealIsland())
                RevealIsland(false, restoredIslandDistance);
        }

        private void OnDestroy()
        {
            // Do not leave the generated background transparent if the encounter
            // is destroyed during a restart, load, developer jump, or scene reset.
            if (starryNight != null)
                starryNight.SetExternalVisibility(1f);
        }

        private void Update()
        {
            if (director == null || player == null || completed)
                return;

            if (starryNight == null)
                starryNight = FindFirstObjectByType<ProceduralStarryNight>();

            if (island == null && ShouldRevealIsland())
                RevealIsland(true);

            if (island != null)
            {
                UpdateIslandApproach();
                UpdateSkyReplacement();
            }

            previousPlayerX = player.position.x;

            if (director.RunTime < director.DayDuration &&
                director.DistanceTravelled < director.DayDistance)
                return;

            completed = true;
            starryNight?.SetExternalVisibility(0f);
            director.CompleteDayFourAtIsland();
        }

        private bool ShouldRevealIsland()
        {
            if (director == null)
                return false;

            return director.RunTime >= islandRevealAt ||
                   director.DistanceTravelled >=
                   director.DayDistance * islandRevealJourneyProgress;
        }

        private void UpdateIslandApproach()
        {
            float playerDeltaX = player.position.x - previousPlayerX;

            // The island is a world-space ocean structure. It closes the gap very
            // slowly on its own, with a tiny counter-motion against Chuck's travel.
            // Its scale is deliberately never modified.
            Vector3 position = island.position;
            float directionToPlayer = Mathf.Sign(player.position.x - position.x);
            position.x += directionToPlayer * islandApproachSpeed * Time.deltaTime;
            position.x -= playerDeltaX * oppositePlayerMovement;
            island.position = position;
        }

        private void UpdateSkyReplacement()
        {
            if (starryNight == null || initialIslandDistance <= skyFullyGoneDistance)
                return;

            float distance = Mathf.Abs(player.position.x - island.position.x);
            float visibility = Mathf.InverseLerp(
                skyFullyGoneDistance,
                initialIslandDistance,
                distance);

            // Smooth the transition so the generated background recedes naturally
            // while the real island takes over the horizon.
            visibility = visibility * visibility * (3f - 2f * visibility);
            starryNight.SetExternalVisibility(visibility);
        }

        private void RevealIsland(bool announce, float restoredDistance = -1f)
        {
            if (island != null)
                return;

            Sprite sprite = Resources.Load<Sprite>("Structures/research_island");
            if (sprite == null)
            {
                Debug.LogError(
                    "Research island was not found at Resources/Structures/research_island.",
                    this);
                return;
            }

            Camera camera = Camera.main;
            float halfWidth = camera != null
                ? camera.orthographicSize * camera.aspect
                : 8f;

            Vector3 position = ResolveBetweenFourthAndFifthWaves(
                player.position,
                out PixelWaterGPU sortingWater,
                out int sortingLane);
            float startingDistance = halfWidth + islandOffscreenDistance;
            float timeApproachProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(islandRevealAt, director.DayDuration, director.RunTime));
            float distanceApproachProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    director.DayDistance * islandRevealJourneyProgress,
                    director.DayDistance,
                    director.DistanceTravelled));
            float approachProgress = Mathf.Max(
                timeApproachProgress,
                distanceApproachProgress);
            float deterministicDistance = Mathf.Lerp(
                startingDistance,
                islandDiscoveryDistance * 0.8f,
                approachProgress);
            float spawnDistance = restoredDistance > 0f
                ? restoredDistance
                : deterministicDistance;
            position.x = player.position.x + Mathf.Max(
                islandDiscoveryDistance * 0.8f,
                spawnDistance);
            position += (Vector3)islandPositionOffset;

            GameObject islandObject = new("Classified Research Island");
            islandObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = islandObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 0;

            InterWaveRenderItem renderItem = islandObject.AddComponent<InterWaveRenderItem>();
            renderItem.SetWaterAndLane(sortingWater, sortingLane);
            islandObject.transform.position = position;

            // No localScale assignment: the imported island size is preserved.
            island = islandObject.transform;
            initialIslandDistance = Mathf.Max(
                startingDistance,
                Mathf.Abs(player.position.x - island.position.x));
            previousPlayerX = player.position.x;
            starryNight?.SetExternalVisibility(1f);

            if (announce && !islandAnnounced)
            {
                islandAnnounced = true;
                director.AnnounceDayFourIsland();
            }
        }

        private static Vector3 ResolveBetweenFourthAndFifthWaves(
            Vector3 fallback,
            out PixelWaterGPU sortingWater,
            out int sortingLane)
        {
            List<PixelWaterGPU> waters = EndlessWaveSections.LayersNearest(fallback.x);
            waters.RemoveAll(water => water == null || !water.isActiveAndEnabled);
            waters.Sort((a, b) =>
                a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
            sortingLane = Mathf.Clamp(3, 0, Mathf.Max(0, waters.Count - 2));
            sortingWater = waters.Count > 0
                ? waters[Mathf.Clamp(sortingLane, 0, waters.Count - 1)]
                : null;
            float y = fallback.y;
            if (waters.Count >= 2)
            {
                y = Mathf.Lerp(
                    waters[sortingLane].GetGameplaySurfaceHeight(fallback.x),
                    waters[sortingLane + 1].GetGameplaySurfaceHeight(fallback.x),
                    0.5f);
            }
            else if (waters.Count == 1)
            {
                y = waters[0].GetGameplaySurfaceHeight(fallback.x);
            }

            return new Vector3(fallback.x, y, 0f);
        }
    }
}
