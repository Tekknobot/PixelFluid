using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// A telegraphed beam whose endpoints remain attached to inter-wave lanes.
    /// It supports horizontal lane sweeps and diagonal lane-to-lane crosses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AionLaneLaser : MonoBehaviour
    {
        private readonly List<PixelWaterGPU> waterLayers = new();
        private readonly HashSet<TinyWaveSurfer> damagedSurfers = new();
        private AionFinalBoss owner;
        private LineRenderer glow;
        private LineRenderer core;
        private CapsuleCollider2D hitbox;
        private InterWaveRenderItem renderItem;
        private Material glowMaterial;
        private Material coreMaterial;
        private int startLane;
        private int endLane;
        private float telegraphDuration;
        private float activeDuration;
        private float age;
        private Color beamColour;
        private bool activated;

        public AionFinalBoss Owner => owner;

        public static AionLaneLaser Spawn(
            AionFinalBoss source,
            int fromLane,
            int toLane,
            float warningSeconds,
            float firingSeconds,
            Color colour)
        {
            GameObject beam = new("AION Reality Laser");
            LineRenderer glowRenderer = beam.AddComponent<LineRenderer>();
            GameObject coreObject = new("Laser Core");
            coreObject.transform.SetParent(beam.transform, false);
            LineRenderer coreRenderer = coreObject.AddComponent<LineRenderer>();
            CapsuleCollider2D collider = beam.AddComponent<CapsuleCollider2D>();
            Rigidbody2D body = beam.AddComponent<Rigidbody2D>();
            AionLaneLaser laser = beam.AddComponent<AionLaneLaser>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            collider.isTrigger = true;
            collider.direction = CapsuleDirection2D.Horizontal;
            collider.enabled = false;

            laser.Configure(
                source,
                glowRenderer,
                coreRenderer,
                collider,
                fromLane,
                toLane,
                warningSeconds,
                firingSeconds,
                colour);
            return laser;
        }

        private void Configure(
            AionFinalBoss source,
            LineRenderer glowRenderer,
            LineRenderer coreRenderer,
            CapsuleCollider2D collider,
            int fromLane,
            int toLane,
            float warningSeconds,
            float firingSeconds,
            Color colour)
        {
            owner = source;
            glow = glowRenderer;
            core = coreRenderer;
            hitbox = collider;
            startLane = Mathf.Max(0, fromLane);
            endLane = Mathf.Max(0, toLane);
            telegraphDuration = Mathf.Max(0.25f, warningSeconds);
            activeDuration = Mathf.Max(0.18f, firingSeconds);
            beamColour = colour;

            Shader spriteShader = Shader.Find("Sprites/Default");
            glowMaterial = new Material(spriteShader)
            {
                name = "AION Laser Glow",
                hideFlags = HideFlags.HideAndDontSave
            };
            coreMaterial = new Material(spriteShader)
            {
                name = "AION Laser Core",
                hideFlags = HideFlags.HideAndDontSave
            };

            ConfigureLine(glow, glowMaterial, 0.06f, 76);
            ConfigureLine(core, coreMaterial, 0.018f, 77);
            RefreshGeometry();

            int renderLane = Mathf.Max(0, Mathf.RoundToInt((startLane + endLane) * 0.5f));
            PixelWaterGPU sortingWater = GetSortingWater(renderLane);
            ApplyWaterSortingLayer(sortingWater);
            // Add the inter-wave component only after the LineRenderers own their
            // materials; its OnEnable snapshot must not capture empty materials.
            renderItem = gameObject.AddComponent<InterWaveRenderItem>();
            renderItem.SetWaterAndLane(sortingWater, renderLane);
        }

        private void ApplyWaterSortingLayer(PixelWaterGPU water)
        {
            if (water == null)
                return;

            Renderer waterRenderer = water.GetComponent<Renderer>();
            if (waterRenderer == null)
                waterRenderer = water.GetComponentInChildren<Renderer>();
            if (waterRenderer == null)
                return;

            glow.sortingLayerID = waterRenderer.sortingLayerID;
            core.sortingLayerID = waterRenderer.sortingLayerID;
        }

        private static void ConfigureLine(
            LineRenderer line,
            Material material,
            float width,
            int sortingOrder)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.startWidth = width;
            line.endWidth = width;
            line.material = material;
            line.sortingOrder = sortingOrder;
        }

        private void Update()
        {
            if (owner == null || owner.IsDefeated)
            {
                Destroy(gameObject);
                return;
            }

            age += Time.deltaTime;
            RefreshGeometry();

            if (!activated && age >= telegraphDuration)
            {
                activated = true;
                hitbox.enabled = true;
                damagedSurfers.Clear();
            }

            float activeAge = age - telegraphDuration;
            float total = telegraphDuration + activeDuration + 0.16f;
            if (age >= total)
            {
                Destroy(gameObject);
                return;
            }

            if (!activated)
            {
                float pulse = 0.45f + 0.30f * Mathf.Sin(age * 22f);
                Color warning = Color.Lerp(beamColour, Color.white, 0.38f);
                warning.a = pulse;
                SetLineAppearance(warning, 0.028f, 0.010f);
            }
            else
            {
                float fade = activeAge <= activeDuration
                    ? 1f
                    : 1f - Mathf.InverseLerp(activeDuration, activeDuration + 0.16f, activeAge);
                float crackle = 0.88f + Mathf.Sin(age * 45f) * 0.12f;
                Color hot = beamColour;
                hot.a = fade;
                SetLineAppearance(hot, 0.17f * crackle, 0.055f * crackle);
                hitbox.enabled = activeAge <= activeDuration;
            }
        }

        private void SetLineAppearance(Color colour, float glowWidth, float coreWidth)
        {
            Color outer = colour;
            outer.a *= 0.72f;
            glow.startColor = glow.endColor = outer;
            core.startColor = core.endColor = Color.Lerp(colour, Color.white, 0.72f);
            glow.startWidth = glow.endWidth = glowWidth;
            core.startWidth = core.endWidth = coreWidth;
        }

        private void RefreshGeometry()
        {
            Camera camera = Camera.main;
            float centreX = camera != null ? camera.transform.position.x : transform.position.x;
            float halfWidth = camera != null && camera.orthographic
                ? camera.orthographicSize * camera.aspect + 1.25f
                : 10f;
            float leftX = centreX - halfWidth;
            float rightX = centreX + halfWidth;
            Vector2 start = new(leftX, ResolveLaneY(startLane, leftX));
            Vector2 end = new(rightX, ResolveLaneY(endLane, rightX));

            glow.SetPosition(0, start);
            glow.SetPosition(1, end);
            core.SetPosition(0, start);
            core.SetPosition(1, end);

            Vector2 delta = end - start;
            transform.position = (start + end) * 0.5f;
            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            hitbox.size = new Vector2(delta.magnitude, activated ? 0.24f : 0.08f);
            hitbox.offset = Vector2.zero;
        }

        private float ResolveLaneY(int lane, float worldX)
        {
            RefreshWater(worldX);
            if (waterLayers.Count < 2)
                return transform.position.y;

            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(
                waterLayers[clamped].GetGameplaySurfaceHeight(worldX),
                waterLayers[clamped + 1].GetGameplaySurfaceHeight(worldX),
                0.5f);
        }

        private PixelWaterGPU GetSortingWater(int lane)
        {
            RefreshWater(transform.position.x);
            return waterLayers.Count == 0
                ? null
                : waterLayers[Mathf.Clamp(lane, 0, waterLayers.Count - 1)];
        }

        private void RefreshWater(float worldX)
        {
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(worldX));
            waterLayers.RemoveAll(water => water == null || !water.isActiveAndEnabled);
            waterLayers.Sort((left, right) =>
                left.IndependentLayerIndex.CompareTo(right.IndependentLayerIndex));
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!activated || !hitbox.enabled || other == null)
                return;

            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || !surfer.IsPlayerControlled ||
                !damagedSurfers.Add(surfer))
                return;

            surfer.TakeSharkHit(transform.position);
            ExplosionBasicEffect.Spawn(surfer.transform.position);
        }

        private void OnDestroy()
        {
            if (glowMaterial != null)
                Destroy(glowMaterial);
            if (coreMaterial != null)
                Destroy(coreMaterial);
        }
    }
}
