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
    /// Small runtime-drawn Day 6 hazards. They travel inside a selected water
    /// lane and share the surfer's normal invulnerability and hit response.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DaySixHazardProjectile : MonoBehaviour
    {
        private static readonly Dictionary<DaySixHazardKind, Sprite> CachedSprites = new();
        private readonly List<PixelWaterGPU> waterLayers = new();
        private DaySixHazardKind kind;
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D hitCollider;
        private float direction;
        private float speed;
        private float lifetime;
        private float age;
        private float phase;
        private int lane;

        public static DaySixHazardProjectile Spawn(
            DaySixHazardKind hazardKind,
            Vector3 position,
            float travelDirection,
            float travelSpeed,
            int laneIndex,
            PixelWaterGPU sortingWater)
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
            projectile.Initialise(hazardKind, renderer, collider, travelDirection, travelSpeed, laneIndex);
            return projectile;
        }

        private void Initialise(
            DaySixHazardKind hazardKind,
            SpriteRenderer renderer,
            CircleCollider2D collider,
            float travelDirection,
            float travelSpeed,
            int laneIndex)
        {
            kind = hazardKind;
            spriteRenderer = renderer;
            hitCollider = collider;
            direction = travelDirection < 0f ? -1f : 1f;
            speed = Mathf.Max(0.5f, travelSpeed);
            lane = Mathf.Max(0, laneIndex);
            phase = Random.Range(0f, Mathf.PI * 2f);
            spriteRenderer.sprite = GetSprite(kind);
            spriteRenderer.flipX = direction < 0f;
            spriteRenderer.sortingOrder = 4;
            hitCollider.radius = kind == DaySixHazardKind.ResortWake ? 0.42f : 0.25f;
            transform.localScale = kind == DaySixHazardKind.ResortWake
                ? new Vector3(1.35f, 0.72f, 1f)
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

            Vector3 position = transform.position;
            position.x += direction * speed * Time.deltaTime;
            position.y = ResolveLaneY(position.x) + VerticalMotion();
            transform.position = position;

            float spin = kind == DaySixHazardKind.ResortWake ? 0f :
                kind == DaySixHazardKind.Toast ? -520f : 330f;
            transform.Rotate(0f, 0f, spin * direction * Time.deltaTime);

            Color colour = spriteRenderer.color;
            colour.a = Mathf.Clamp01((lifetime - age) / 0.35f);
            spriteRenderer.color = colour;
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
                return transform.position.y;

            lane = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(
                waterLayers[lane].GetGameplaySurfaceHeight(worldX),
                waterLayers[lane + 1].GetGameplaySurfaceHeight(worldX),
                0.5f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
                return;

            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || !surfer.IsPlayerControlled)
                return;

            surfer.TakeSharkHit(transform.position);
            hitCollider.enabled = false;
            Destroy(gameObject);
        }

        private static Sprite GetSprite(DaySixHazardKind hazardKind)
        {
            if (CachedSprites.TryGetValue(hazardKind, out Sprite existing) && existing != null)
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
            CachedSprites[hazardKind] = sprite;
            return sprite;
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
            DaySixHazardKind.ResortWake => "Resort Wake",
            _ => hazardKind.ToString()
        };
    }
}
