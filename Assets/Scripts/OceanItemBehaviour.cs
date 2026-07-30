using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// A collectible ocean prop with individualised drift, buoyancy, spin and
    /// water-following behaviour. It is collected automatically on surfer contact.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D), typeof(Rigidbody2D))]
    public sealed class OceanItemBehaviour : MonoBehaviour
    {
        private OceanItemSpawner owner;
        private PixelWaterGPU foregroundWater;
        private PixelWaterGPU backgroundWater;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private int itemIndex;
        private float driftSpeed;
        private float bobHeight;
        private float bobSpeed;
        private float tiltStrength;
        private float spinSpeed;
        private float currentInfluence;
        private float depthOffset;
        private float phase;
        private float minX;
        private float maxX;
        private bool collected;

        public int ItemIndex => itemIndex;

        public bool UsesAnyWater(IReadOnlyList<PixelWaterGPU> layers)
        {
            if (layers == null) return false;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] == foregroundWater || layers[i] == backgroundWater)
                    return true;
            }
            return false;
        }

        public void ApplySectionShift(float horizontalDistance)
        {
            if (collected || Mathf.Abs(horizontalDistance) <= Mathf.Epsilon) return;

            minX += horizontalDistance;
            maxX += horizontalDistance;

            Vector2 shifted = new(transform.position.x + horizontalDistance, transform.position.y);
            transform.position = new Vector3(shifted.x, shifted.y, transform.position.z);
            if (body != null)
            {
                body.position = shifted;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        public void Initialise(
            OceanItemSpawner newOwner,
            int newItemIndex,
            PixelWaterGPU foreground,
            PixelWaterGPU background,
            float worldMinX,
            float worldMaxX)
        {
            owner = newOwner;
            itemIndex = newItemIndex;
            foregroundWater = foreground;
            backgroundWater = background != null ? background : foreground;
            minX = worldMinX;
            maxX = worldMaxX;

            // Every sprite receives a stable but different movement profile.
            System.Random random = new System.Random((newItemIndex + 1) * 7919);
            float signed = random.NextDouble() < 0.5 ? -1f : 1f;
            driftSpeed = signed * Mathf.Lerp(0.08f, 0.34f, (float)random.NextDouble());
            bobHeight = Mathf.Lerp(0.018f, 0.105f, (float)random.NextDouble());
            bobSpeed = Mathf.Lerp(0.65f, 1.8f, (float)random.NextDouble());
            tiltStrength = Mathf.Lerp(0.2f, 0.9f, (float)random.NextDouble());
            spinSpeed = signed * Mathf.Lerp(0f, 24f, (float)random.NextDouble());
            currentInfluence = Mathf.Lerp(0.02f, 0.2f, (float)random.NextDouble());
            phase = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());

            CacheDepthOffset();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void FixedUpdate()
        {
            if (collected || foregroundWater == null)
                return;

            if (backgroundWater == null)
                backgroundWater = foregroundWater;

            Vector2 position = body.position;
            Vector2 current = Vector2.Lerp(
                foregroundWater.GetGameplayWaveVelocity(position.x),
                backgroundWater.GetGameplayWaveVelocity(position.x),
                0.5f);

            position.x += (driftSpeed + current.x * currentInfluence) * Time.fixedDeltaTime;
            const float wrapPadding = 0.6f;
            if (position.x > maxX + wrapPadding)
                position.x = minX - wrapPadding;
            else if (position.x < minX - wrapPadding)
                position.x = maxX + wrapPadding;

            float surface = SampleSurface(position.x);
            float itemWave = Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;

            // A few families dive more deeply or rise more dramatically, giving
            // the large sprite set visibly different water behaviour.
            float secondaryMotion = 0f;
            switch (itemIndex % 4)
            {
                case 1:
                    secondaryMotion = Mathf.Sin(Time.time * bobSpeed * 0.45f + phase) * bobHeight * 0.75f;
                    break;
                case 2:
                    secondaryMotion = Mathf.Abs(Mathf.Sin(Time.time * bobSpeed * 0.55f + phase)) * bobHeight;
                    break;
                case 3:
                    secondaryMotion = -Mathf.Abs(Mathf.Sin(Time.time * bobSpeed * 0.38f + phase)) * bobHeight * 0.65f;
                    break;
            }

            float targetY = surface + depthOffset + itemWave + secondaryMotion;
            position.y = Mathf.Lerp(position.y, targetY,
                1f - Mathf.Exp(-6.5f * Time.fixedDeltaTime));
            body.MovePosition(position);

            float left = SampleSurface(position.x - 0.18f);
            float right = SampleSurface(position.x + 0.18f);
            float slope = Mathf.Atan2(right - left, 0.36f) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Clamp(slope * tiltStrength, -18f, 18f)
                                + Mathf.Sin(Time.time * bobSpeed + phase) * spinSpeed * 0.12f;
            body.MoveRotation(Mathf.LerpAngle(body.rotation, targetAngle,
                1f - Mathf.Exp(-5f * Time.fixedDeltaTime)));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryCollectFrom(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Stay is intentional: it also catches items that spawn or drift into
            // an already-overlapping surfer collider without requiring a new entry.
            TryCollectFrom(other);
        }

        private void TryCollectFrom(Collider2D other)
        {
            if (collected || other == null)
                return;

            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || surfer.IsDead || !surfer.IsPlayerControlled)
                return;

            Collect(surfer);
        }

        public bool Collect(TinyWaveSurfer surfer)
        {
            if (collected || surfer == null || surfer.IsDead)
                return false;

            if (spriteRenderer == null || !surfer.CollectThrowableItem(spriteRenderer.sprite))
                return false;

            collected = true;

            Collider2D itemCollider = GetComponent<Collider2D>();
            if (itemCollider != null)
                itemCollider.enabled = false;

            if (spriteRenderer != null)
                spriteRenderer.color = new Color(1f, 1f, 1f, 0.45f);

            owner?.NotifyCollected(itemIndex, this, transform.position);
            Destroy(gameObject, 0.08f);
            return true;
        }

        private float SampleSurface(float x)
        {
            return Mathf.Lerp(
                foregroundWater.GetGameplaySurfaceHeight(x),
                backgroundWater.GetGameplaySurfaceHeight(x),
                0.5f);
        }

        private void CacheDepthOffset()
        {
            if (foregroundWater == null)
                return;

            if (backgroundWater == null)
                backgroundWater = foregroundWater;

            depthOffset = transform.position.y - SampleSurface(transform.position.x);
        }
    }
}
