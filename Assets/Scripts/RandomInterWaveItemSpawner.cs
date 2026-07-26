using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Populates the real gaps between the complete independent wave simulations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RandomInterWaveItemSpawner : MonoBehaviour
    {
        [Header("Population")]
        [SerializeField, Range(1, 12)] private int itemsPerLane = 3;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool maintainPopulation = true;
        [SerializeField, Range(0.1f, 10f)] private float respawnDelay = 1.25f;
        [SerializeField, Range(0f, 1f)] private float horizontalEdgePadding = 0.06f;
        [SerializeField, Range(0f, 0.45f)] private float laneVerticalJitter = 0.08f;

        [Header("Motion")]
        [SerializeField] private Vector2 driftSpeedRange = new(-0.32f, 0.32f);
        [SerializeField] private Vector2 bobHeightRange = new(0.005f, 0.02f);
        [SerializeField] private Vector2 bobSpeedRange = new(0.7f, 1.5f);
        [SerializeField] private Vector2 scaleRange = new(0.65f, 0.95f);

        [Header("Interaction")]
        [SerializeField] private bool addTriggerColliders = true;
        [SerializeField] private bool logInteractions = true;

        private readonly List<InterWaveWorldItem> liveItems = new();
        private readonly List<PixelWaterGPU> orderedWaterLayers = new();
        private PixelWaterGPU masterWater;
        private float nextRespawnTime;

        private IEnumerator Start()
        {
            // PixelWaterGPU creates its independent simulations during OnEnable.
            // Wait until all runtime layers exist before constructing the gaps.
            yield return null;
            yield return null;

            ResolveWaterLayers();
            if (orderedWaterLayers.Count < 2)
            {
                Debug.LogError("RandomInterWaveItemSpawner needs at least two independent PixelWaterGPU layers.", this);
                enabled = false;
                yield break;
            }

            if (spawnOnStart)
            {
                RebuildItems();
                Debug.Log($"Spawned {liveItems.Count} items between {orderedWaterLayers.Count} real wave simulations.", this);
            }
        }

        private void Update()
        {
            liveItems.RemoveAll(item => item == null);
            if (!maintainPopulation || Time.time < nextRespawnTime || orderedWaterLayers.Count < 2)
                return;

            int laneCount = orderedWaterLayers.Count - 1;
            int targetCount = laneCount * itemsPerLane;
            if (liveItems.Count >= targetCount)
                return;

            SpawnRandomItem(liveItems.Count % laneCount);
            nextRespawnTime = Time.time + respawnDelay;
        }

        private void ResolveWaterLayers()
        {
            orderedWaterLayers.Clear();
            orderedWaterLayers.AddRange(
                FindObjectsByType<PixelWaterGPU>(FindObjectsSortMode.None)
                    .Where(w => w != null)
                    .OrderBy(w => w.IndependentLayerIndex));

            masterWater = orderedWaterLayers.FirstOrDefault(w => !w.IsIndependentLayerClone)
                          ?? orderedWaterLayers.FirstOrDefault();
        }

        [ContextMenu("Rebuild Random Inter-Wave Items")]
        public void RebuildItems()
        {
            ClearItems();
            ResolveWaterLayers();
            if (orderedWaterLayers.Count < 2)
                return;

            for (int lane = 0; lane < orderedWaterLayers.Count - 1; lane++)
                for (int i = 0; i < itemsPerLane; i++)
                    SpawnRandomItem(lane);
        }

        [ContextMenu("Clear Random Inter-Wave Items")]
        public void ClearItems()
        {
            for (int i = liveItems.Count - 1; i >= 0; i--)
                if (liveItems[i] != null)
                    Destroy(liveItems[i].gameObject);
            liveItems.Clear();
        }

        private void SpawnRandomItem(int lane)
        {
            if (lane < 0 || lane >= orderedWaterLayers.Count - 1)
                return;

            PixelWaterGPU foregroundWater = orderedWaterLayers[lane];
            PixelWaterGPU backgroundWater = orderedWaterLayers[lane + 1];
            InterWaveWorldItem.ItemKind kind = ChooseKind();
            Sprite sprite = CreateSprite(kind);
            if (sprite == null)
                return;

            GameObject itemObject = new($"Inter-Wave {kind} Between Layers {lane} and {lane + 1}");
            itemObject.transform.SetParent(transform, false);
            itemObject.transform.position = RandomPositionInLane(foregroundWater, backgroundWater);

            float scale = Random.Range(scaleRange.x, scaleRange.y);
            itemObject.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = itemObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipX = Random.value < 0.5f;

            InterWaveRenderItem renderLane = itemObject.AddComponent<InterWaveRenderItem>();
            renderLane.SetLane(lane);

            BoxCollider2D itemCollider = itemObject.AddComponent<BoxCollider2D>();
            itemCollider.isTrigger = addTriggerColliders;
            itemCollider.size = sprite.bounds.size * 0.78f;

            Rigidbody2D body = itemObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            InterWaveWorldItem item = itemObject.AddComponent<InterWaveWorldItem>();
            float drift = Random.Range(driftSpeedRange.x, driftSpeedRange.y);
            if (Mathf.Abs(drift) < 0.12f)
                drift = drift < 0f ? -0.12f : 0.12f;

            int hitPoints = kind == InterWaveWorldItem.ItemKind.Whale ? 3 :
                            kind == InterWaveWorldItem.ItemKind.Shark ? 2 : 1;

            item.Initialise(
                kind,
                foregroundWater,
                backgroundWater,
                this,
                drift,
                Random.Range(bobHeightRange.x, bobHeightRange.y),
                Random.Range(bobSpeedRange.x, bobSpeedRange.y),
                hitPoints);

            liveItems.Add(item);
        }

        private Vector3 RandomPositionInLane(PixelWaterGPU foreground, PixelWaterGPU background)
        {
            float minX = Mathf.Max(foreground.TankMinimum.x, background.TankMinimum.x);
            float maxX = Mathf.Min(foreground.TankMaximum.x, background.TankMaximum.x);
            float width = Mathf.Max(0.1f, maxX - minX);
            minX += width * horizontalEdgePadding;
            maxX -= width * horizontalEdgePadding;

            float x = Random.Range(minX, maxX);
            float frontSurface = foreground.GetGameplaySurfaceHeight(x);
            float backSurface = background.GetGameplaySurfaceHeight(x);
            float y = Mathf.Lerp(frontSurface, backSurface, 0.5f) +
                      Random.Range(-laneVerticalJitter, laneVerticalJitter);

            return new Vector3(x, y, 0f);
        }

        private static InterWaveWorldItem.ItemKind ChooseKind()
        {
            float roll = Random.value;
            if (roll < 0.18f) return InterWaveWorldItem.ItemKind.Shark;
            if (roll < 0.28f) return InterWaveWorldItem.ItemKind.Whale;
            if (roll < 0.48f) return InterWaveWorldItem.ItemKind.Buoy;
            if (roll < 0.68f) return InterWaveWorldItem.ItemKind.Crate;
            if (roll < 0.86f) return InterWaveWorldItem.ItemKind.Bottle;
            return InterWaveWorldItem.ItemKind.Treasure;
        }

        private static Sprite CreateSprite(InterWaveWorldItem.ItemKind kind)
        {
            return kind switch
            {
                InterWaveWorldItem.ItemKind.Shark => RuntimePixelArt.CreateSharkSprite(),
                InterWaveWorldItem.ItemKind.Whale => RuntimePixelArt.CreateWhaleSprite(),
                InterWaveWorldItem.ItemKind.Buoy => RuntimePixelArt.CreateBuoySprite(),
                InterWaveWorldItem.ItemKind.Crate => RuntimePixelArt.CreateCrateSprite(),
                InterWaveWorldItem.ItemKind.Bottle => RuntimePixelArt.CreateBottleSprite(),
                InterWaveWorldItem.ItemKind.Treasure => RuntimePixelArt.CreateTreasureSprite(),
                _ => RuntimePixelArt.CreateCrateSprite()
            };
        }

        internal void NotifyItemInteracted(InterWaveWorldItem item)
        {
            liveItems.Remove(item);
            nextRespawnTime = Time.time + respawnDelay;
            if (logInteractions && item != null)
                Debug.Log($"Interacted with {item.Kind} between real wave layers.", item);
        }
    }
}
