using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    public enum DaySixHazardKind
    {
        Toast,
        Spore,
        ResortWake,
        Flush
    }

    /// <summary>
    /// Day 6 hazards use authored resource variations where available and a
    /// runtime-drawn fallback otherwise. They stay in a selected water lane and
    /// share the surfer's normal invulnerability and hit response.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DaySixHazardProjectile : MonoBehaviour
    {
        private const int AuthoredProjectileCellSize = 32;
        private const float MushroomAnimationFramesPerSecond = 12f;
        private static readonly Dictionary<DaySixHazardKind, Sprite> CachedFallbackSprites = new();
        private static readonly Dictionary<DaySixHazardKind, Sprite[]> ResourceSpritePools = new();
        private static readonly Dictionary<DaySixHazardKind, int> ResourceSpritePoolIndices = new();
        private static readonly List<Sprite> RuntimeSheetSlices = new();
        private static System.Random ResourceSpriteRandom = new(6106);
        private static Sprite[] mushroomAnimationFrames = System.Array.Empty<Sprite>();
        private static bool mushroomAnimationLoaded;
        private readonly List<PixelWaterGPU> waterLayers = new();
        private DaySixHazardKind kind;
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D hitCollider;
        private float direction;
        private float speed;
        private float lifetime;
        private float age;
        private float phase;
        private float laneHeightOffset;
        private float lastResolvedLaneY;
        private Sprite[] animationFrames = System.Array.Empty<Sprite>();
        private int lane;
        private bool resolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticProjectilePools()
        {
            // This also runs when Enter Play Mode skips domain reload. Asset edits
            // must therefore never leave an old empty pool or fallback sprite cached.
            CachedFallbackSprites.Clear();
            ResourceSpritePools.Clear();
            ResourceSpritePoolIndices.Clear();
            RuntimeSheetSlices.Clear();
            ResourceSpriteRandom = new System.Random(6106);
            mushroomAnimationFrames = System.Array.Empty<Sprite>();
            mushroomAnimationLoaded = false;
        }

        public static DaySixHazardProjectile Spawn(
            DaySixHazardKind hazardKind,
            Vector3 position,
            float travelDirection,
            float travelSpeed,
            int laneIndex,
            PixelWaterGPU sortingWater,
            float projectileLaneHeightOffset = 0f)
        {
            GameObject projectileObject = new($"Day 6 {ReadableName(hazardKind)} Hazard");
            projectileObject.transform.position = position;
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            InterWaveRenderItem renderItem = projectileObject.AddComponent<InterWaveRenderItem>();
            DaySixHazardProjectile projectile = projectileObject.AddComponent<DaySixHazardProjectile>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            collider.isTrigger = true;
            renderItem.SetWaterAndLane(sortingWater, laneIndex);
            projectile.Initialise(
                hazardKind,
                renderer,
                collider,
                travelDirection,
                travelSpeed,
                laneIndex,
                projectileLaneHeightOffset);
            return projectile;
        }

        private void Initialise(
            DaySixHazardKind hazardKind,
            SpriteRenderer renderer,
            CircleCollider2D collider,
            float travelDirection,
            float travelSpeed,
            int laneIndex,
            float projectileLaneHeightOffset)
        {
            kind = hazardKind;
            spriteRenderer = renderer;
            hitCollider = collider;
            direction = travelDirection < 0f ? -1f : 1f;
            speed = Mathf.Max(0.5f, travelSpeed);
            lane = Mathf.Max(0, laneIndex);
            laneHeightOffset = projectileLaneHeightOffset;
            lastResolvedLaneY = transform.position.y - laneHeightOffset;
            phase = Random.Range(0f, Mathf.PI * 2f);
            if (kind == DaySixHazardKind.Spore)
                animationFrames = GetMushroomAnimationFrames();
            spriteRenderer.sprite = animationFrames.Length > 0
                ? animationFrames[0]
                : GetSprite(kind);
            spriteRenderer.flipX = direction < 0f;
            spriteRenderer.sortingOrder = 4;
            hitCollider.radius = kind == DaySixHazardKind.ResortWake ? 0.34f :
                kind == DaySixHazardKind.Toast || kind == DaySixHazardKind.Spore
                    ? 0.30f
                    : 0.25f;
            transform.localScale = kind == DaySixHazardKind.ResortWake ||
                                   kind == DaySixHazardKind.Toast ||
                                   kind == DaySixHazardKind.Spore
                ? Vector3.one * 0.78f
                : Vector3.one;
            lifetime = kind == DaySixHazardKind.ResortWake ? 3.2f : 2.6f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            UpdateSpriteAnimation();

            Vector3 position = transform.position;
            position.x += direction * speed * Time.deltaTime;
            position.y = ResolveLaneY(position.x) + laneHeightOffset + VerticalMotion();
            transform.position = position;

            float spin = kind == DaySixHazardKind.ResortWake ? 250f :
                kind == DaySixHazardKind.Toast ? -520f : 330f;
            transform.Rotate(0f, 0f, spin * direction * Time.deltaTime);

            Color colour = spriteRenderer.color;
            colour.a = Mathf.Clamp01((lifetime - age) / 0.35f);
            spriteRenderer.color = colour;
        }

        private void UpdateSpriteAnimation()
        {
            if (animationFrames == null || animationFrames.Length <= 1)
                return;

            int frameIndex = Mathf.FloorToInt(age * MushroomAnimationFramesPerSecond) %
                             animationFrames.Length;
            Sprite frame = animationFrames[frameIndex];
            if (frame != null && spriteRenderer.sprite != frame)
                spriteRenderer.sprite = frame;
        }

        private float VerticalMotion()
        {
            return kind switch
            {
                DaySixHazardKind.Spore => Mathf.Sin(age * 7f + phase) * 0.12f,
                DaySixHazardKind.Flush => Mathf.Sin(age * 10f + phase) * 0.08f,
                _ => 0f
            };
        }

        private float ResolveLaneY(float worldX)
        {
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(worldX));
            waterLayers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            waterLayers.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
            if (waterLayers.Count < 2)
                return lastResolvedLaneY;

            lane = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            lastResolvedLaneY = Mathf.Lerp(
                waterLayers[lane].GetGameplaySurfaceHeight(worldX),
                waterLayers[lane + 1].GetGameplaySurfaceHeight(worldX),
                0.5f);
            return lastResolvedLaneY;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved || other == null)
                return;

            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || !surfer.IsPlayerControlled)
                return;

            resolved = true;
            hitCollider.enabled = false;
            surfer.TakeSharkHit(transform.position);

            SpawnImpactExplosion();

            Destroy(gameObject);
        }

        private void SpawnImpactExplosion()
        {
            // Keep the effect in the same inter-wave lane as the projectile so
            // foreground water continues to occlude it correctly.
            ResolveLaneY(transform.position.x);
            PixelWaterGPU sortingWater = waterLayers.Count > 0
                ? waterLayers[Mathf.Clamp(lane, 0, waterLayers.Count - 1)]
                : null;
            ExplosionBasicEffect.SpawnInterWave(
                transform.position,
                spriteRenderer,
                sortingWater,
                lane);
        }

        private static Sprite GetSprite(DaySixHazardKind hazardKind)
        {
            Sprite resourceSprite = GetNextResourceSprite(hazardKind);
            if (resourceSprite != null)
                return resourceSprite;

            if (CachedFallbackSprites.TryGetValue(hazardKind, out Sprite existing) && existing != null)
                return existing;

            const int size = 16;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Day 6 {ReadableName(hazardKind)} Runtime Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            switch (hazardKind)
            {
                case DaySixHazardKind.Toast:
                    Fill(pixels, size, 3, 4, 10, 8, new Color32(91, 49, 28, 255));
                    Fill(pixels, size, 4, 5, 8, 6, new Color32(242, 177, 72, 255));
                    Fill(pixels, size, 5, 6, 6, 4, new Color32(255, 216, 116, 255));
                    break;
                case DaySixHazardKind.Spore:
                    DrawDiamond(pixels, size, 8, 8, 6, new Color32(80, 28, 92, 255));
                    DrawDiamond(pixels, size, 8, 8, 4, new Color32(218, 94, 237, 255));
                    DrawDiamond(pixels, size, 8, 8, 2, new Color32(255, 206, 250, 255));
                    break;
                case DaySixHazardKind.ResortWake:
                    Fill(pixels, size, 1, 6, 14, 5, new Color32(30, 111, 142, 255));
                    Fill(pixels, size, 2, 7, 12, 3, new Color32(103, 225, 232, 255));
                    Fill(pixels, size, 4, 8, 8, 1, new Color32(235, 255, 245, 255));
                    break;
                default:
                    DrawDiamond(pixels, size, 8, 8, 7, new Color32(24, 79, 126, 255));
                    DrawDiamond(pixels, size, 8, 8, 5, new Color32(59, 187, 226, 255));
                    Fill(pixels, size, 7, 2, 2, 12, new Color32(214, 255, 255, 255));
                    Fill(pixels, size, 2, 7, 12, 2, new Color32(214, 255, 255, 255));
                    break;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                32f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = $"day6_{ReadableName(hazardKind).ToLowerInvariant()}";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            CachedFallbackSprites[hazardKind] = sprite;
            return sprite;
        }

        private static Sprite GetNextResourceSprite(DaySixHazardKind hazardKind)
        {
            string resourcePath = hazardKind switch
            {
                DaySixHazardKind.Toast => "Day6/Toast",
                DaySixHazardKind.ResortWake => "Day6/IceCube",
                _ => null
            };
            if (string.IsNullOrEmpty(resourcePath))
                return null;

            if (!ResourceSpritePools.TryGetValue(hazardKind, out Sprite[] pool))
            {
                pool = LoadAuthoredSpritePool(resourcePath);
                System.Array.Sort(pool, (left, right) =>
                    string.CompareOrdinal(left.name, right.name));
                ShuffleResourcePool(pool);
                ResourceSpritePools[hazardKind] = pool;
                ResourceSpritePoolIndices[hazardKind] = 0;

                if (pool.Length == 0)
                {
                    Debug.LogWarning(
                        $"DaySixHazardProjectile found no sprites in Resources/{resourcePath}; " +
                        "using the runtime fallback projectile.");
                    return null;
                }
            }

            if (pool.Length == 0)
                return null;

            int index = ResourceSpritePoolIndices.TryGetValue(hazardKind, out int savedIndex)
                ? savedIndex
                : 0;
            if (index >= pool.Length)
            {
                Sprite previous = pool[pool.Length - 1];
                ShuffleResourcePool(pool);
                if (pool.Length > 1 && pool[0] == previous)
                    (pool[0], pool[1]) = (pool[1], pool[0]);
                index = 0;
            }

            Sprite selected = pool[index];
            ResourceSpritePoolIndices[hazardKind] = index + 1;
            return selected;
        }

        private static Sprite[] GetMushroomAnimationFrames()
        {
            if (mushroomAnimationLoaded)
                return mushroomAnimationFrames;

            mushroomAnimationLoaded = true;
            mushroomAnimationFrames = LoadAuthoredSpritePool("Day6/Mushroom");
            System.Array.Sort(mushroomAnimationFrames, CompareAnimationFrames);
            if (mushroomAnimationFrames.Length == 0)
            {
                Debug.LogWarning(
                    "DaySixHazardProjectile found no animation frames in " +
                    "Resources/Day6/Mushroom; using the runtime fallback spore.");
            }
            return mushroomAnimationFrames;
        }

        private static int CompareAnimationFrames(Sprite left, Sprite right)
        {
            int textureOrder = string.CompareOrdinal(
                left != null && left.texture != null ? left.texture.name : string.Empty,
                right != null && right.texture != null ? right.texture.name : string.Empty);
            if (textureOrder != 0)
                return textureOrder;

            // Unity sprite rect Y starts at the bottom, while animation sheets
            // are normally authored from the top-left across each row.
            int rowOrder = right.rect.y.CompareTo(left.rect.y);
            return rowOrder != 0 ? rowOrder : left.rect.x.CompareTo(right.rect.x);
        }

        private static Sprite[] LoadAuthoredSpritePool(string resourcePath)
        {
            Sprite[] imported = System.Array.FindAll(
                Resources.LoadAll<Sprite>(resourcePath),
                sprite => sprite != null);

            // A correctly sliced Multiple sprite sheet already exposes every cell.
            // A Single sprite import exposes one oversized sprite instead, while a
            // Default texture exposes no sprites. Handle both cases by slicing every
            // 32x32 texture in the folder at runtime.
            bool oneOversizedSprite = imported.Length == 1 &&
                (imported[0].rect.width > AuthoredProjectileCellSize + 0.5f ||
                 imported[0].rect.height > AuthoredProjectileCellSize + 0.5f);
            if (imported.Length > 0 && !oneOversizedSprite)
                return imported;

            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcePath);
            List<Sprite> slices = new();
            foreach (Texture2D texture in textures)
            {
                if (texture == null ||
                    texture.width < AuthoredProjectileCellSize ||
                    texture.height < AuthoredProjectileCellSize ||
                    texture.width % AuthoredProjectileCellSize != 0 ||
                    texture.height % AuthoredProjectileCellSize != 0)
                    continue;

                int columns = texture.width / AuthoredProjectileCellSize;
                int rows = texture.height / AuthoredProjectileCellSize;
                if (columns * rows <= 1 && imported.Length > 0)
                    return imported;

                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        Sprite slice = Sprite.Create(
                            texture,
                            new Rect(
                                column * AuthoredProjectileCellSize,
                                row * AuthoredProjectileCellSize,
                                AuthoredProjectileCellSize,
                                AuthoredProjectileCellSize),
                            new Vector2(0.5f, 0.5f),
                            AuthoredProjectileCellSize,
                            0,
                            SpriteMeshType.FullRect);
                        slice.name = $"{texture.name}_runtime_{row:00}_{column:00}";
                        slice.hideFlags = HideFlags.HideAndDontSave;
                        slices.Add(slice);
                        RuntimeSheetSlices.Add(slice);
                    }
                }
            }

            return slices.Count > 0 ? slices.ToArray() : imported;
        }

        private static void ShuffleResourcePool(Sprite[] pool)
        {
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int swapIndex = ResourceSpriteRandom.Next(i + 1);
                (pool[i], pool[swapIndex]) = (pool[swapIndex], pool[i]);
            }
        }

        private static void Fill(
            Color32[] pixels,
            int width,
            int x,
            int y,
            int fillWidth,
            int fillHeight,
            Color32 colour)
        {
            for (int row = y; row < y + fillHeight; row++)
                for (int column = x; column < x + fillWidth; column++)
                    if (column >= 0 && column < width && row >= 0 && row < width)
                        pixels[row * width + column] = colour;
        }

        private static void DrawDiamond(
            Color32[] pixels,
            int width,
            int centreX,
            int centreY,
            int radius,
            Color32 colour)
        {
            for (int y = centreY - radius; y <= centreY + radius; y++)
            {
                for (int x = centreX - radius; x <= centreX + radius; x++)
                {
                    if (Mathf.Abs(x - centreX) + Mathf.Abs(y - centreY) <= radius &&
                        x >= 0 && x < width && y >= 0 && y < width)
                        pixels[y * width + x] = colour;
                }
            }
        }

        private static string ReadableName(DaySixHazardKind hazardKind) => hazardKind switch
        {
            DaySixHazardKind.Spore => "Mushroom Spore",
            DaySixHazardKind.ResortWake => "Resort Ice Cube",
            _ => hazardKind.ToString()
        };
    }
}
