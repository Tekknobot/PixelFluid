using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// A rescued swimmer's temporary surfer avatar. It follows the rescued
    /// inter-wave lane, rides toward a random level edge, continues beyond the
    /// water bounds, and then removes itself.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(InterWaveRenderItem))]
    public sealed class RescuedSurferExit : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float horizontalSpeed = 1.05f;
        [SerializeField, Min(0.1f)] private float exitDistance = 2.25f;
        [SerializeField, Min(1f)] private float animationFramesPerSecond = 10f;
        [SerializeField, Min(0.05f)] private float spriteScale = 0.62f;
        [SerializeField, Min(0.1f)] private float verticalResponsiveness = 9f;
        [SerializeField, Range(0f, 0.2f)] private float laneOffset = 0.02f;
        [SerializeField, Range(0f, 12f)] private float waveTiltAmount = 4f;
        [SerializeField, Min(0.02f)] private float slopeSampleDistance = 0.16f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private Sprite[] frames;
        private int laneIndex;
        private float direction;
        private float animationTime;
        private float minimumX;
        private float maximumX;
        private bool initialised;

        public static void Spawn(Vector3 position, int lane)
        {
            GameObject avatar = new("Rescued Surfer Exit");
            avatar.transform.position = position;
            avatar.AddComponent<SpriteRenderer>();
            avatar.AddComponent<InterWaveRenderItem>();
            RescuedSurferExit exit = avatar.AddComponent<RescuedSurferExit>();
            exit.Initialise(position, lane);
        }

        public void Initialise(Vector3 position, int requestedLane)
        {
            ResolveReferences();
            if (waterLayers.Count < 2 || frames == null || frames.Length == 0)
            {
                Debug.LogWarning("Rescued surfer could not initialise: water layers or rescued_surfer frames are missing.", this);
                Destroy(gameObject);
                return;
            }

            laneIndex = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            renderItem.SetLane(laneIndex);
            direction = Random.value < 0.5f ? -1f : 1f;

            minimumX = waterLayers[0].TankMinimum.x;
            maximumX = waterLayers[0].TankMaximum.x;

            transform.position = new Vector3(
                position.x,
                GetLaneCentreY(position.x) + laneOffset,
                position.z);
            transform.localScale = Vector3.one * spriteScale;

            spriteRenderer.sprite = frames[0];
            spriteRenderer.flipX = direction < 0f;
            spriteRenderer.sortingOrder = 2;
            initialised = true;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (renderItem == null)
                renderItem = GetComponent<InterWaveRenderItem>();

            if (frames == null || frames.Length == 0)
            {
                frames = Resources.LoadAll<Sprite>("Surfers/rescued_surfer")
                    .OrderBy(GetFrameNumber)
                    .ToArray();
            }

            if (waterLayers.Count == 0)
            {
                waterLayers.AddRange(
                    FindObjectsByType<PixelWaterGPU>(FindObjectsSortMode.None)
                        .Where(layer => layer != null)
                        .OrderBy(layer => layer.IndependentLayerIndex));
            }
        }

        private void Update()
        {
            if (!initialised)
                return;

            animationTime += Time.deltaTime;
            int frame = Mathf.FloorToInt(animationTime * animationFramesPerSecond) % frames.Length;
            spriteRenderer.sprite = frames[frame];

            Vector3 position = transform.position;
            position.x += direction * horizontalSpeed * Time.deltaTime;

            float desiredY = GetLaneCentreY(position.x) + laneOffset;
            float follow = 1f - Mathf.Exp(-verticalResponsiveness * Time.deltaTime);
            position.y = Mathf.Lerp(position.y, desiredY, follow);
            transform.position = position;

            float leftY = GetLaneCentreY(position.x - slopeSampleDistance);
            float rightY = GetLaneCentreY(position.x + slopeSampleDistance);
            float slope = Mathf.Atan2(rightY - leftY, slopeSampleDistance * 2f) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(slope, -waveTiltAmount, waveTiltAmount));

            if ((direction < 0f && position.x <= minimumX - exitDistance) ||
                (direction > 0f && position.x >= maximumX + exitDistance))
            {
                Destroy(gameObject);
            }
        }

        private float GetLaneCentreY(float worldX)
        {
            int lower = Mathf.Clamp(laneIndex, 0, waterLayers.Count - 2);
            return Mathf.Lerp(
                waterLayers[lower].GetGameplaySurfaceHeight(worldX),
                waterLayers[lower + 1].GetGameplaySurfaceHeight(worldX),
                0.5f);
        }

        private static int GetFrameNumber(Sprite sprite)
        {
            if (sprite == null || string.IsNullOrEmpty(sprite.name))
                return 0;

            int underscore = sprite.name.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(sprite.name.Substring(underscore + 1), out int value)
                ? value
                : 0;
        }
    }
}
