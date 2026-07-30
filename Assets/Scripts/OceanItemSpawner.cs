using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Loads and spawns every sprite under Resources/OceanItems. All items are
    /// half-sized, water responsive and collected automatically on surfer contact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OceanItemSpawner : MonoBehaviour
    {
        [Header("Population")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField, Range(0.1f, 1f)] private float itemScale = 1f;
        [SerializeField, Min(0f)] private float horizontalPadding = 0.8f;
        [SerializeField, Range(0f, 0.3f)] private float laneJitter = 0.08f;
        [SerializeField, Min(0f)] private float respawnDelay = 10f;

        [Header("Pickup Audio")]
        [SerializeField] private AudioClip oceanItemPickupClip;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.85f;

        private readonly Dictionary<int, OceanItemBehaviour> liveItems = new();
        private readonly List<PixelWaterGPU> waters = new();
        private Sprite[] itemSprites;


        private void OnEnable()
        {
            EndlessWaveSections.SectionRecycled += HandleSectionRecycled;
        }

        private void OnDisable()
        {
            EndlessWaveSections.SectionRecycled -= HandleSectionRecycled;
        }

        private void HandleSectionRecycled(IReadOnlyList<PixelWaterGPU> recycledLayers, float horizontalDistance)
        {
            List<int> missing = null;
            foreach (KeyValuePair<int, OceanItemBehaviour> pair in liveItems.ToArray())
            {
                OceanItemBehaviour item = pair.Value;
                if (item == null)
                {
                    missing ??= new List<int>();
                    missing.Add(pair.Key);
                    continue;
                }

                if (item.UsesAnyWater(recycledLayers))
                    item.ApplySectionShift(horizontalDistance);
            }

            if (missing != null)
                foreach (int index in missing) liveItems.Remove(index);
        }

        private IEnumerator Start()
        {
            if (oceanItemPickupClip == null)
                oceanItemPickupClip = Resources.Load<AudioClip>("Audio/SFX/ocean_item_pickup");

            yield return null;
            yield return null;
            ResolveWaters();
            if (spawnOnStart && FindFirstObjectByType<SurfDayProgressionDirector>() == null)
                SpawnAllItems();
        }

        public void SpawnProgressionItems(int requestedCount)
        {
            ResolveWaters();
            itemSprites = Resources.LoadAll<Sprite>("OceanItems")
                .OrderBy(sprite => ExtractNumber(sprite.name))
                .ToArray();

            if (itemSprites.Length == 0)
            {
                Debug.LogWarning("No sprites were found in Resources/OceanItems.", this);
                return;
            }

            int count = Mathf.Clamp(requestedCount, 1, itemSprites.Length);
            for (int slot = 0; slot < count; slot++)
            {
                int index = Mathf.Min(itemSprites.Length - 1,
                    Mathf.FloorToInt(slot * itemSprites.Length / (float)count));
                if (!liveItems.ContainsKey(index) || liveItems[index] == null)
                    SpawnItem(index);
            }
        }

        [ContextMenu("Spawn All Ocean Items")]
        public void SpawnAllItems()
        {
            ResolveWaters();
            itemSprites = Resources.LoadAll<Sprite>("OceanItems")
                .OrderBy(sprite => ExtractNumber(sprite.name))
                .ToArray();

            if (itemSprites.Length == 0)
            {
                Debug.LogWarning("No sprites were found in Resources/OceanItems.", this);
                return;
            }

            for (int i = 0; i < itemSprites.Length; i++)
                if (!liveItems.ContainsKey(i) || liveItems[i] == null)
                    SpawnItem(i);

            Debug.Log($"Spawned all {itemSprites.Length} ocean items at half scale.", this);
        }

        internal void NotifyCollected(
            int index,
            OceanItemBehaviour item,
            Vector3 pickupPosition)
        {
            if (liveItems.TryGetValue(index, out OceanItemBehaviour current) && current == item)
                liveItems.Remove(index);

            PlayPickupSound(pickupPosition);

            if (respawnDelay >= 0f)
                StartCoroutine(RespawnAfterDelay(index));
        }

        private void PlayPickupSound(Vector3 position)
        {
            if (oceanItemPickupClip == null)
                oceanItemPickupClip = Resources.Load<AudioClip>("Audio/SFX/ocean_item_pickup");

            if (oceanItemPickupClip == null)
                return;

            GameObject soundObject = new GameObject("Ocean Item Pickup SFX");
            soundObject.transform.position = position;

            AudioSource source = soundObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = pickupVolume;
            source.clip = oceanItemPickupClip;
            source.Play();

            Destroy(soundObject, oceanItemPickupClip.length + 0.1f);
        }

        private IEnumerator RespawnAfterDelay(int index)
        {
            if (respawnDelay > 0f)
                yield return new WaitForSeconds(respawnDelay);

            if (!liveItems.ContainsKey(index) && itemSprites != null && index < itemSprites.Length)
                SpawnItem(index);
        }

        private void SpawnItem(int index)
        {
            if (itemSprites == null || index < 0 || index >= itemSprites.Length || waters.Count == 0)
                return;

            IReadOnlyList<float> sectionCentres = EndlessWaveSections.Instance != null
                ? EndlessWaveSections.Instance.GetSectionCentres()
                : null;
            float sectionX = sectionCentres != null && sectionCentres.Count > 0
                ? sectionCentres[index % sectionCentres.Count]
                : transform.position.x;

            List<PixelWaterGPU> localWaters = EndlessWaveSections.LayersNearest(sectionX)
                .Where(water => water != null && water.isActiveAndEnabled)
                .OrderBy(water => water.IndependentLayerIndex)
                .ToList();
            if (localWaters.Count == 0)
                localWaters = waters;

            int laneCount = Mathf.Max(1, localWaters.Count - 1);
            int lane = (index / Mathf.Max(1, sectionCentres != null ? sectionCentres.Count : 1)) % laneCount;
            PixelWaterGPU foreground = localWaters[Mathf.Clamp(lane, 0, localWaters.Count - 1)];
            PixelWaterGPU background = localWaters[Mathf.Clamp(lane + 1, 0, localWaters.Count - 1)];

            float minX = Mathf.Max(foreground.TankMinimum.x, background.TankMinimum.x) + horizontalPadding;
            float maxX = Mathf.Min(foreground.TankMaximum.x, background.TankMaximum.x) - horizontalPadding;
            if (maxX <= minX)
            {
                minX = foreground.TankMinimum.x;
                maxX = foreground.TankMaximum.x;
            }

            // Golden-ratio spacing keeps all sprites distributed without clumps.
            float t = Mathf.Repeat((index + 1) * 0.61803398875f, 1f);
            float x = Mathf.Lerp(minX, maxX, t);
            float frontY = foreground.GetGameplaySurfaceHeight(x);
            float backY = background.GetGameplaySurfaceHeight(x);
            float y = Mathf.Lerp(frontY, backY, 0.5f) + Random.Range(-laneJitter, laneJitter);

            GameObject itemObject = new($"Ocean Item {index + 1} - {itemSprites[index].name}");
            itemObject.transform.SetParent(transform, true);
            itemObject.transform.position = new Vector3(x, y, 0f);
            itemObject.transform.localScale = Vector3.one * itemScale;

            SpriteRenderer renderer = itemObject.AddComponent<SpriteRenderer>();
            renderer.sprite = itemSprites[index];
            renderer.flipX = (index & 1) == 1;

            InterWaveRenderItem renderItem = itemObject.AddComponent<InterWaveRenderItem>();
            renderItem.SetLane(lane);

            BoxCollider2D collider = itemObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = itemSprites[index].bounds.size * 0.78f;

            Rigidbody2D body = itemObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = false;

            OceanItemBehaviour behaviour = itemObject.AddComponent<OceanItemBehaviour>();
            behaviour.Initialise(this, index, foreground, background, minX, maxX);
            liveItems[index] = behaviour;
        }

        private void ResolveWaters()
        {
            waters.Clear();
            waters.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
            waters.RemoveAll(water => water == null || !water.isActiveAndEnabled);
            waters.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
        }

        private static int ExtractNumber(string value)
        {
            string digits = new string(value.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int number) ? number : int.MaxValue;
        }
    }
}
