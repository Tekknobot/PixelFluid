using System.Linq;
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
        [SerializeField, Min(0f)] private float islandRevealAt = 480f;
        [SerializeField, Min(2f)] private float islandOffscreenDistance = 18f;
        [SerializeField, Min(0f)] private float islandApproachSpeed = 0.08f;
        [SerializeField, Range(0f, 0.08f)] private float oppositePlayerMovement = 0.015f;
        [SerializeField, Min(0.5f)] private float islandDiscoveryDistance = 5f;
        [SerializeField, Min(1f)] private float skyFullyGoneDistance = 11f;
        [SerializeField] private Vector2 islandPositionOffset = new(0f, 0.2f);

        private SurfDayProgressionDirector director;
        private Transform player;
        private Transform island;
        private ProceduralStarryNight starryNight;
        private float initialIslandDistance;
        private float previousPlayerX;
        private bool islandAnnounced;
        private bool completed;

        public static void Begin(SurfDayProgressionDirector progression, TinyWaveSurfer surfer)
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
                return;
            }

            GameObject host = new("Day Four Classified Research Route");
            DayFourConspiracyEncounter encounter =
                host.AddComponent<DayFourConspiracyEncounter>();
            encounter.Initialise(progression, surfer.transform);
        }

        private void Initialise(SurfDayProgressionDirector progression, Transform surfer)
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
                if (facility != null)
                    Destroy(facility.gameObject);
            }

            if (director.RunTime >= islandRevealAt)
                RevealIsland(false);
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

            if (island == null && director.RunTime >= islandRevealAt)
                RevealIsland(true);

            if (island != null)
            {
                UpdateIslandApproach();
                UpdateSkyReplacement();
            }

            previousPlayerX = player.position.x;

            if (island == null || director.RunTime < director.DayDuration)
                return;

            float horizontalDistance = Mathf.Abs(player.position.x - island.position.x);
            if (horizontalDistance > islandDiscoveryDistance)
                return;

            completed = true;
            starryNight?.SetExternalVisibility(0f);
            director.CompleteDayFourAtIsland();
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

        private void RevealIsland(bool announce)
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

            GameObject islandObject = new("Classified Research Island");
            islandObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = islandObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 0;

            // Same ocean lane/depth treatment as the secret facility.
            InterWaveRenderItem renderItem = islandObject.AddComponent<InterWaveRenderItem>();
            renderItem.SetLane(4);

            Camera camera = Camera.main;
            float halfWidth = camera != null
                ? camera.orthographicSize * camera.aspect
                : 8f;

            Vector3 position = ResolveBetweenFourthAndFifthWaves(player.position);
            position.x = player.position.x + halfWidth + islandOffscreenDistance;
            position += (Vector3)islandPositionOffset;
            islandObject.transform.position = position;

            // No localScale assignment: the imported island size is preserved.
            island = islandObject.transform;
            initialIslandDistance = Mathf.Abs(player.position.x - island.position.x);
            previousPlayerX = player.position.x;
            starryNight?.SetExternalVisibility(1f);

            if (announce && !islandAnnounced)
            {
                islandAnnounced = true;
                director.AnnounceDayFourIsland();
            }
        }

        private static Vector3 ResolveBetweenFourthAndFifthWaves(Vector3 fallback)
        {
            PixelWaterGPU[] waters = FindObjectsByType<PixelWaterGPU>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .OrderBy(water => water.transform.position.y)
                .ToArray();

            float y = fallback.y;
            if (waters.Length >= 5)
                y = (waters[3].transform.position.y + waters[4].transform.position.y) * 0.5f;
            else if (waters.Length > 0)
                y = waters[Mathf.Clamp(4, 0, waters.Length - 1)].transform.position.y;

            return new Vector3(fallback.x, y, 0f);
        }
    }
}
