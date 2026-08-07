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
        [SerializeField, Min(2f)] private float offscreenDistance = 14f;
        [SerializeField, Min(0.5f)] private float discoveryDistance = 3.25f;
        [SerializeField] private Vector2 positionOffset = new(0f, 0.35f);

        private SurfDayProgressionDirector director;
        private Transform player;
        private bool discovered;
        private bool dayFourDisplayOnly;

        public static void Begin(SurfDayProgressionDirector director, TinyWaveSurfer surfer)
        {
            if (director == null || surfer == null ||
                FindFirstObjectByType<SecretFacilityEncounter>() != null)
                return;

            GameObject host = new("Secret Military Research Facility");
            SecretFacilityEncounter encounter = host.AddComponent<SecretFacilityEncounter>();
            encounter.Initialise(director, surfer.transform);
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

            InterWaveRenderItem renderItem = gameObject.AddComponent<InterWaveRenderItem>();
            renderItem.SetLane(4);

            transform.localScale = Vector3.one;
            transform.position = ResolveDayFourDisplayPosition(surfer.position);
        }

        private Vector3 ResolveDayFourDisplayPosition(Vector3 playerPosition)
        {
            Camera camera = Camera.main;
            float halfWidth = camera != null
                ? camera.orthographicSize * camera.aspect
                : 8f;

            PixelWaterGPU[] waters = FindObjectsByType<PixelWaterGPU>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .OrderBy(water => water.transform.position.y)
                .ToArray();

            float y = playerPosition.y;
            if (waters.Length >= 5)
                y = (waters[3].transform.position.y + waters[4].transform.position.y) * 0.5f;
            else if (waters.Length > 0)
                y = waters[Mathf.Clamp(4, 0, waters.Length - 1)].transform.position.y;

            // Keep the full artwork inside the opening camera view, slightly ahead
            // of Chuck, so the player can immediately inspect it on Day 4.
            float x = playerPosition.x + halfWidth * 0.42f;
            return new Vector3(x + positionOffset.x, y + positionOffset.y, 0f);
        }

        private void Initialise(SurfDayProgressionDirector progression, Transform surfer)
        {
            director = progression;
            player = surfer;

            Sprite sprite = Resources.Load<Sprite>("Structures/secret_facility");
            if (sprite == null)
            {
                Debug.LogError("Secret facility sprite was not found at Resources/Structures/secret_facility.", this);
                Destroy(gameObject);
                return;
            }

            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 0;

            InterWaveRenderItem renderItem = gameObject.AddComponent<InterWaveRenderItem>();
            renderItem.SetLane(4); // Between the fourth and fifth wave simulations.

            transform.localScale = Vector3.one;
            transform.position = ResolveSpawnPosition(surfer.position);
        }

        private Vector3 ResolveSpawnPosition(Vector3 playerPosition)
        {
            Camera camera = Camera.main;
            float halfWidth = camera != null
                ? camera.orthographicSize * camera.aspect
                : 8f;

            // Put it beyond the right edge so the player discovers it naturally.
            float x = playerPosition.x + halfWidth + offscreenDistance;

            PixelWaterGPU[] waters = FindObjectsByType<PixelWaterGPU>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .OrderBy(water => water.transform.position.y)
                .ToArray();

            float y = playerPosition.y;
            if (waters.Length >= 5)
                y = (waters[3].transform.position.y + waters[4].transform.position.y) * 0.5f;
            else if (waters.Length > 0)
                y = waters[Mathf.Clamp(4, 0, waters.Length - 1)].transform.position.y;

            return new Vector3(x + positionOffset.x, y + positionOffset.y, 0f);
        }

        private void Update()
        {
            if (dayFourDisplayOnly)
                return;

            if (discovered || player == null || director == null)
                return;

            float horizontalDistance = Mathf.Abs(player.position.x - transform.position.x);
            if (horizontalDistance > discoveryDistance)
                return;

            discovered = true;
            director.CompleteDayThreeAtFacility();
        }
    }
}
