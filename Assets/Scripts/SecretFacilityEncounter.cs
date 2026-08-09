using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Day 3's final discovery. The facility appears beyond the camera between
    /// wave simulations four and five, and the chapter ends only when Chuck reaches it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SecretFacilityEncounter : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float offscreenDistance = 6f;
        [SerializeField, Min(0.5f)] private float discoveryDistance = 3.25f;
        [SerializeField, Min(0.25f)] private float approachSpeed = 4f;
        [SerializeField, Min(0f)] private float minimumVisibleSeconds = 1.25f;
        [SerializeField] private Vector2 positionOffset = new(0f, 0.35f);

        private SurfDayProgressionDirector director;
        private Transform player;
        private bool discovered;
        private bool dayFourDisplayOnly;
        private SpriteRenderer facilityRenderer;
        private float previousPlayerX;
        private float visibleSince = -1f;

        public bool IsDayFourDisplayOnly => dayFourDisplayOnly;

        public static SecretFacilityEncounter Begin(
            SurfDayProgressionDirector director,
            TinyWaveSurfer surfer)
        {
            if (director == null || surfer == null)
                return null;

            SecretFacilityEncounter existing =
                FindObjectsByType<SecretFacilityEncounter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(encounter =>
                    encounter != null && !encounter.dayFourDisplayOnly);
            if (existing != null)
            {
                SpriteRenderer existingRenderer =
                    existing.GetComponent<SpriteRenderer>();
                if (!existing.discovered && existingRenderer != null &&
                    existingRenderer.sprite != null)
                {
                    existing.director = director;
                    existing.player = surfer.transform;
                    existing.facilityRenderer = existingRenderer;
                    existing.previousPlayerX = surfer.transform.position.x;
                    existing.gameObject.SetActive(true);
                    existing.enabled = true;
                    return existing;
                }

                Destroy(existing.gameObject);
            }

            // A display-only copy belongs to Day 4 and must never block the real
            // Day 3 discovery encounter if one survived a load or developer jump.
            foreach (SecretFacilityEncounter display in
                     FindObjectsByType<SecretFacilityEncounter>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (display != null && display.dayFourDisplayOnly)
                    Destroy(display.gameObject);
            }

            GameObject host = new("Secret Military Research Facility");
            SecretFacilityEncounter encounter = host.AddComponent<SecretFacilityEncounter>();
            return encounter.Initialise(director, surfer.transform)
                ? encounter
                : null;
        }


        public static void BeginDayFourDisplay(TinyWaveSurfer surfer)
        {
            if (surfer == null ||
                FindObjectsByType<SecretFacilityEncounter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(encounter =>
                        encounter != null && encounter.dayFourDisplayOnly))
            {
                return;
            }

            GameObject host = new("Secret Facility — Day Four Display");
            SecretFacilityEncounter encounter = host.AddComponent<SecretFacilityEncounter>();
            encounter.InitialiseDayFourDisplay(surfer.transform);
        }

        private void InitialiseDayFourDisplay(Transform surfer)
        {
            dayFourDisplayOnly = true;
            director = null;
            player = surfer;

            Sprite sprite = Resources.Load<Sprite>("Structures/secret_facility");
            if (sprite == null)
            {
                Debug.LogError(
                    "Secret facility sprite was not found at Resources/Structures/secret_facility.",
                    this);
                Destroy(gameObject);
                return;
            }

            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 0;

            transform.localScale = Vector3.one;
            transform.position = ResolveDayFourDisplayPosition(
                surfer.position,
                out PixelWaterGPU sortingWater,
                out int sortingLane);
            InterWaveRenderItem renderItem = gameObject.AddComponent<InterWaveRenderItem>();
            renderItem.SetWaterAndLane(sortingWater, sortingLane);
        }

        private Vector3 ResolveDayFourDisplayPosition(
            Vector3 playerPosition,
            out PixelWaterGPU sortingWater,
            out int sortingLane)
        {
            Camera camera = Camera.main;
            float halfWidth = camera != null
                ? camera.orthographicSize * camera.aspect
                : 8f;

            float y = ResolveBetweenFourthAndFifthWaves(
                playerPosition,
                out sortingWater,
                out sortingLane);

            // Keep the full artwork inside the opening camera view, slightly ahead
            // of Chuck, so the player can immediately inspect it on Day 4.
            float x = playerPosition.x + halfWidth * 0.42f;
            return new Vector3(x + positionOffset.x, y + positionOffset.y, 0f);
        }

        private bool Initialise(
            SurfDayProgressionDirector progression,
            Transform surfer)
        {
            director = progression;
            player = surfer;

            Sprite sprite = Resources.Load<Sprite>("Structures/secret_facility");
            if (sprite == null)
            {
                Debug.LogError("Secret facility sprite was not found at Resources/Structures/secret_facility.", this);
                Destroy(gameObject);
                return false;
            }

            facilityRenderer = gameObject.AddComponent<SpriteRenderer>();
            facilityRenderer.sprite = sprite;
            facilityRenderer.sortingOrder = 0;

            transform.localScale = Vector3.one;
            transform.position = ResolveSpawnPosition(
                surfer.position,
                out PixelWaterGPU sortingWater,
                out int sortingLane);
            previousPlayerX = surfer.position.x;
            visibleSince = -1f;
            InterWaveRenderItem renderItem = gameObject.AddComponent<InterWaveRenderItem>();
            renderItem.SetWaterAndLane(sortingWater, sortingLane);
            return true;
        }

        private Vector3 ResolveSpawnPosition(
            Vector3 playerPosition,
            out PixelWaterGPU sortingWater,
            out int sortingLane)
        {
            Camera camera = Camera.main;
            float halfWidth = camera != null
                ? camera.orthographicSize * camera.aspect
                : 8f;

            // Put it beyond the right edge so the player discovers it naturally.
            float x = playerPosition.x + halfWidth + offscreenDistance;

            float y = ResolveBetweenFourthAndFifthWaves(
                playerPosition,
                out sortingWater,
                out sortingLane);

            return new Vector3(x + positionOffset.x, y + positionOffset.y, 0f);
        }

        private static float ResolveBetweenFourthAndFifthWaves(
            Vector3 playerPosition,
            out PixelWaterGPU sortingWater,
            out int sortingLane)
        {
            List<PixelWaterGPU> waters = EndlessWaveSections.LayersNearest(playerPosition.x);
            waters.RemoveAll(water => water == null || !water.isActiveAndEnabled);
            waters.Sort((a, b) =>
                a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
            sortingLane = Mathf.Clamp(3, 0, Mathf.Max(0, waters.Count - 2));
            sortingWater = waters.Count > 0
                ? waters[Mathf.Clamp(sortingLane, 0, waters.Count - 1)]
                : null;

            if (waters.Count >= 2)
            {
                // Use the simulations' stable world-space centres rather than
                // their animated surface samples. Day 3 and Day 4 create this
                // artwork at different moments; sampling moving crests made the
                // same facility jump vertically between the two days.
                float foregroundCentre = Mathf.Lerp(
                    waters[sortingLane].TankMinimum.y,
                    waters[sortingLane].TankMaximum.y,
                    0.5f);
                float backgroundCentre = Mathf.Lerp(
                    waters[sortingLane + 1].TankMinimum.y,
                    waters[sortingLane + 1].TankMaximum.y,
                    0.5f);
                return Mathf.Lerp(
                    foregroundCentre,
                    backgroundCentre,
                    0.5f);
            }

            return waters.Count == 1
                ? Mathf.Lerp(
                    waters[0].TankMinimum.y,
                    waters[0].TankMaximum.y,
                    0.5f)
                : playerPosition.y;
        }

        private void Update()
        {
            if (dayFourDisplayOnly)
                return;

            if (discovered || player == null || director == null)
                return;

            // DistanceTravelled counts useful surfing in either horizontal
            // direction. Keep the revealed facility camera-relative while it
            // approaches, so reaching the 800 m story gate always produces the
            // discovery instead of leaving a static object far behind/ahead.
            float playerDeltaX = player.position.x - previousPlayerX;
            previousPlayerX = player.position.x;
            Vector3 position = transform.position;
            position.x += playerDeltaX;
            float approachTargetX = player.position.x + discoveryDistance * 0.68f;
            position.x = Mathf.MoveTowards(
                position.x,
                approachTargetX,
                Mathf.Max(0.25f, approachSpeed) * Time.deltaTime);
            transform.position = position;

            Camera camera = Camera.main;
            bool visible = camera == null;
            if (camera != null)
            {
                Vector3 viewport = camera.WorldToViewportPoint(transform.position);
                visible = viewport.z > 0f && viewport.x >= 0.04f &&
                    viewport.x <= 0.96f;
            }

            if (visible)
            {
                if (visibleSince < 0f)
                    visibleSince = Time.time;
            }
            else
            {
                visibleSince = -1f;
            }

            float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
            if (horizontalDistance > discoveryDistance || visibleSince < 0f ||
                Time.time - visibleSince < minimumVisibleSeconds)
                return;

            discovered = true;
            director.CompleteDayThreeAtFacility();
        }
    }
}
