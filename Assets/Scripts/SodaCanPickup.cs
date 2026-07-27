using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [RequireComponent(
        typeof(SpriteRenderer),
        typeof(Rigidbody2D),
        typeof(InterWaveRenderItem))]
    public sealed class SodaCanPickup : MonoBehaviour
    {
        [Header("Natural Entry")]
        [SerializeField, Min(0.1f)]
        private float offscreenDistance = 1.25f;

        [SerializeField, Min(0.05f)]
        private float fadeInDuration = 0.75f;

        [SerializeField]
        private Vector2 entrySpeedRange = new Vector2(0.35f, 0.55f);

        [Header("Floating")]
        [SerializeField]
        private Vector2 floatingSpeedRange = new Vector2(0.16f, 0.34f);

        [SerializeField]
        private float bobHeight = 0.045f;

        [SerializeField]
        private float bobSpeed = 2.2f;

        private readonly List<PixelWaterGPU> layers = new();

        private Rigidbody2D body;
        private SpriteRenderer sr;
        private SodaCanSpawner owner;

        private int lane;
        private float dir;
        private float speed;
        private float phase;
        private float fadeTimer;

        private bool ready;
        private bool entering;
        private bool collected;

        public void Initialise(int laneIndex, SodaCanSpawner spawner)
        {
            owner = spawner;
            lane = laneIndex;

            Resolve();

            if (layers.Count < 2 || lane < 0 || lane + 1 >= layers.Count)
            {
                Debug.LogWarning(
                    "SodaCanPickup could not find a valid inter-wave lane.",
                    this);

                Destroy(gameObject);
                return;
            }

            GetComponent<InterWaveRenderItem>().SetLane(lane);

            dir = Random.value < 0.5f ? -1f : 1f;
            speed = Random.Range(entrySpeedRange.x, entrySpeedRange.y);
            phase = Random.value * Mathf.PI * 2f;

            float minX = MinX();
            float maxX = MaxX();

            Vector2 position = body.position;

            // A positive direction means it enters from the left.
            if (dir > 0f)
                position.x = minX - offscreenDistance;
            else
                position.x = maxX + offscreenDistance;

            // Use the closest valid water-edge position to calculate its height.
            float sampledX = Mathf.Clamp(position.x, minX, maxX);
            position.y = Centre(sampledX);

            body.position = position;
            transform.position = position;

            fadeTimer = 0f;
            entering = true;
            collected = false;
            ready = true;

            SetAlpha(0f);
        }

        private void Awake()
        {
            Resolve();
        }

        private void Resolve()
        {
            body = GetComponent<Rigidbody2D>();
            sr = GetComponent<SpriteRenderer>();

            layers.Clear();

            layers.AddRange(
                FindObjectsByType<PixelWaterGPU>(
                        FindObjectsSortMode.None)
                    .Where(layer => layer != null)
                    .OrderBy(layer => layer.IndependentLayerIndex));
        }

        private void FixedUpdate()
        {
            if (!ready || collected || layers.Count < 2)
                return;

            float minX = MinX();
            float maxX = MaxX();

            Vector2 position = body.position;
            position.x += dir * speed * Time.fixedDeltaTime;

            if (entering)
            {
                UpdateEntryFade();

                bool enteredFromLeft =
                    dir > 0f && position.x >= minX;

                bool enteredFromRight =
                    dir < 0f && position.x <= maxX;

                if (enteredFromLeft || enteredFromRight)
                {
                    entering = false;

                    // Change to the slower normal floating speed.
                    speed = Random.Range(
                        floatingSpeedRange.x,
                        floatingSpeedRange.y);

                    SetAlpha(1f);
                }
            }
            else
            {
                if (position.x <= minX)
                {
                    position.x = minX;
                    dir = 1f;
                }
                else if (position.x >= maxX)
                {
                    position.x = maxX;
                    dir = -1f;
                }
            }

            // Clamp the sample position because the can begins outside the tank.
            float sampledX = Mathf.Clamp(position.x, minX, maxX);

            float desiredY =
                Centre(sampledX) +
                Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;

            position.y = Mathf.Lerp(
                position.y,
                desiredY,
                1f - Mathf.Exp(-5f * Time.fixedDeltaTime));

            body.MovePosition(position);

            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Sin(Time.time * 2f + phase) * 8f);

            // Do not allow pickup while it is still entering.
            if (!entering)
                CheckForSurferPickup(position);
        }

        private void UpdateEntryFade()
        {
            fadeTimer += Time.fixedDeltaTime;

            float alpha = Mathf.Clamp01(
                fadeTimer / Mathf.Max(0.05f, fadeInDuration));

            // Smooth the fade instead of using a purely linear transition.
            alpha = alpha * alpha * (3f - 2f * alpha);

            SetAlpha(alpha);
        }

        private void CheckForSurferPickup(Vector2 canPosition)
        {
            TinyWaveSurfer[] surfers =
                FindObjectsByType<TinyWaveSurfer>(
                    FindObjectsSortMode.None);

            foreach (TinyWaveSurfer surfer in surfers)
            {
                if (surfer == null ||
                    surfer.IsDead ||
                    surfer.IsSwitchingWave)
                {
                    continue;
                }

                bool isOnAdjacentWave =
                    surfer.CurrentWaveIndex == lane ||
                    surfer.CurrentWaveIndex == lane + 1;

                if (!isOnAdjacentWave)
                    continue;

                float distance = Vector2.Distance(
                    canPosition,
                    surfer.transform.position);

                if (distance < 0.5f && surfer.CollectSodaCan())
                {
                    StartCoroutine(CollectFx(surfer.transform));
                    break;
                }
            }
        }

        private System.Collections.IEnumerator CollectFx(
            Transform target)
        {
            collected = true;

            Collider2D pickupCollider = GetComponent<Collider2D>();

            if (pickupCollider != null)
                pickupCollider.enabled = false;

            owner?.NotifyCollected(gameObject);

            Vector3 startPosition = transform.position;
            Vector3 startScale = transform.localScale;

            const float duration = 0.28f;

            for (float elapsed = 0f;
                 elapsed < duration;
                 elapsed += Time.deltaTime)
            {
                float t = elapsed / duration;

                if (target != null)
                {
                    transform.position = Vector3.Lerp(
                        startPosition,
                        target.position + Vector3.up * 0.22f,
                        t);
                }

                transform.localScale =
                    startScale * (1f + t);

                SetAlpha(1f - t);

                yield return null;
            }

            Destroy(gameObject);
        }

        private void SetAlpha(float alpha)
        {
            if (sr == null)
                return;

            Color colour = sr.color;
            colour.a = Mathf.Clamp01(alpha);
            sr.color = colour;
        }

        private float Centre(float x)
        {
            return Mathf.Lerp(
                layers[lane].GetGameplaySurfaceHeight(x),
                layers[lane + 1].GetGameplaySurfaceHeight(x),
                0.5f);
        }

        private float MinX()
        {
            float sharedMinimum = Mathf.Max(
                layers[lane].TankMinimum.x,
                layers[lane + 1].TankMinimum.x);

            float sharedMaximum = Mathf.Min(
                layers[lane].TankMaximum.x,
                layers[lane + 1].TankMaximum.x);

            return Mathf.Lerp(
                sharedMinimum,
                sharedMaximum,
                0.07f);
        }

        private float MaxX()
        {
            float sharedMaximum = Mathf.Min(
                layers[lane].TankMaximum.x,
                layers[lane + 1].TankMaximum.x);

            float sharedMinimum = Mathf.Max(
                layers[lane].TankMinimum.x,
                layers[lane + 1].TankMinimum.x);

            return Mathf.Lerp(
                sharedMaximum,
                sharedMinimum,
                0.07f);
        }
    }
}