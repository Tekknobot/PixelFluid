using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(InterWaveRenderItem))]
    public sealed class StrugglingSwimmerDrifter : MonoBehaviour
    {
        private static AudioClip swimmerSavedClip;
        [Header("Natural Entry")]
        [SerializeField, Min(0.1f)] private float offscreenDistance = 1.25f;
        [SerializeField, Min(0.05f)] private float fadeInDuration = 0.85f;
        [SerializeField] private Vector2 entrySpeedRange = new(0.24f, 0.38f);

        [Header("Struggling Movement")]
        [SerializeField] private Vector2 horizontalSpeedRange = new(0.09f, 0.20f);
        [SerializeField] private Vector2 directionChangeDelayRange = new(0.8f, 2.2f);
        [SerializeField, Range(0f, 0.35f)] private float horizontalPadding = 0.10f;
        [SerializeField, Range(0f, 0.4f)] private float laneWander = 0.14f;
        [SerializeField, Min(0.1f)] private float verticalResponsiveness = 4.5f;
        [SerializeField] private Vector2 bobHeightRange = new(0.035f, 0.09f);
        [SerializeField] private Vector2 bobSpeedRange = new(2.6f, 4.2f);
        [SerializeField, Range(0f, 18f)] private float maximumTilt = 10f;

        [Header("Wave Layer Changes")]
        [SerializeField] private Vector2 laneChangeDelayRange = new(3.5f, 7.5f);
        [SerializeField, Min(0.2f)] private float laneChangeDuration = 1.45f;
        [SerializeField, Range(0f, 1f)] private float laneChangeChance = 0.72f;

        [Header("Rescue")]
        [SerializeField, Min(0.05f)] private float rescueRadius = 0.52f;
        [SerializeField, Min(0.05f)] private float rescueReactionDuration = 0.34f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private StrugglingSwimmerSpawner owner;
        private int laneIndex;
        private int targetLaneIndex;
        private float laneChangeElapsed;
        private float nextLaneChangeTime;
        private float direction;
        private float speed;
        private float nextDirectionChange;
        private float targetLaneOffset;
        private float bobHeight;
        private float bobSpeed;
        private float bobPhase;
        private float fadeTimer;
        private bool initialised;
        private bool entering;
        private bool changingLane;
        private bool saved;

        public void Initialise(int requestedLane, StrugglingSwimmerSpawner spawner = null)
        {
            owner = spawner;
            ResolveReferences();
            if (waterLayers.Count < 2)
            {
                enabled = false;
                return;
            }

            laneIndex = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            targetLaneIndex = laneIndex;
            renderItem.SetLane(laneIndex);

            direction = Random.value < 0.5f ? -1f : 1f;
            speed = Random.Range(entrySpeedRange.x, entrySpeedRange.y);
            bobHeight = Random.Range(bobHeightRange.x, bobHeightRange.y);
            bobSpeed = Random.Range(bobSpeedRange.x, bobSpeedRange.y);
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            ChooseNewWanderTarget();
            ScheduleNextLaneChange();

            float minX = GetMinimumX(laneIndex);
            float maxX = GetMaximumX(laneIndex);
            Vector2 position = body.position;
            position.x = direction > 0f ? minX - offscreenDistance : maxX + offscreenDistance;
            position.y = GetLaneCentreY(laneIndex, Mathf.Clamp(position.x, minX, maxX)) + targetLaneOffset;

            body.position = position;
            transform.position = position;
            fadeTimer = 0f;
            entering = true;
            changingLane = false;
            saved = false;
            initialised = true;
            SetAlpha(0f);
        }

        private void Awake() => ResolveReferences();
        private void Start() { if (!initialised) Initialise(0); }

        private void ResolveReferences()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            waterLayers.Clear();
            waterLayers.AddRange(FindObjectsByType<PixelWaterGPU>(FindObjectsSortMode.None)
                .Where(layer => layer != null)
                .OrderBy(layer => layer.IndependentLayerIndex));
        }

        private void FixedUpdate()
        {
            if (!initialised || saved || waterLayers.Count < 2)
                return;

            float minX = GetMinimumX(laneIndex);
            float maxX = GetMaximumX(laneIndex);
            Vector2 position = body.position;
            position.x += direction * speed * Time.fixedDeltaTime;

            if (entering)
            {
                fadeTimer += Time.fixedDeltaTime;
                float fade = Mathf.Clamp01(fadeTimer / Mathf.Max(0.05f, fadeInDuration));
                SetAlpha(fade * fade * (3f - 2f * fade));

                bool reachedWater = direction > 0f ? position.x >= minX : position.x <= maxX;
                if (reachedWater)
                {
                    entering = false;
                    speed = Random.Range(horizontalSpeedRange.x, horizontalSpeedRange.y);
                    SetAlpha(1f);
                    ScheduleNextLaneChange();
                }
            }
            else
            {
                if (!changingLane && Time.time >= nextLaneChangeTime)
                {
                    if (Random.value <= laneChangeChance)
                        BeginRandomLaneChange();
                    else
                        ScheduleNextLaneChange();
                }

                if (Time.time >= nextDirectionChange)
                {
                    // Keep travelling across the scene. Random updates alter speed and
                    // vertical struggle, but turning is reserved for reachable edges.
                    speed = Random.Range(horizontalSpeedRange.x, horizontalSpeedRange.y);
                    ChooseNewWanderTarget();
                }

                // Use the shared horizontal area while changing layers.
                int boundsLane = changingLane ? Mathf.Min(laneIndex, targetLaneIndex) : laneIndex;
                minX = GetMinimumX(boundsLane);
                maxX = GetMaximumX(boundsLane);
                if (position.x <= minX)
                {
                    position.x = minX;
                    direction = 1f;
                }
                else if (position.x >= maxX)
                {
                    position.x = maxX;
                    direction = -1f;
                }
            }

            float sampledX = Mathf.Clamp(position.x, minX, maxX);
            float irregularBob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
            irregularBob += Mathf.Sin(Time.time * bobSpeed * 0.47f + bobPhase * 1.7f) * bobHeight * 0.45f;

            float laneY = UpdateLaneTransition(sampledX);
            float desiredY = laneY + targetLaneOffset + irregularBob;
            position.y = Mathf.Lerp(position.y, desiredY,
                1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime));

            body.MovePosition(position);
            spriteRenderer.flipX = direction < 0f;
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Sin(Time.time * bobSpeed * 0.8f + bobPhase) * maximumTilt);

            if (!entering)
                TryRescue(position);
        }

        private void BeginRandomLaneChange()
        {
            int laneCount = waterLayers.Count - 1;
            if (laneCount <= 1)
                return;

            if (laneIndex <= 0)
                targetLaneIndex = 1;
            else if (laneIndex >= laneCount - 1)
                targetLaneIndex = laneCount - 2;
            else
                targetLaneIndex = laneIndex + (Random.value < 0.5f ? -1 : 1);

            changingLane = targetLaneIndex != laneIndex;
            laneChangeElapsed = 0f;
            ChooseNewWanderTarget();
        }

        private float UpdateLaneTransition(float worldX)
        {
            if (!changingLane)
                return GetLaneCentreY(laneIndex, worldX);

            laneChangeElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(laneChangeElapsed / Mathf.Max(0.2f, laneChangeDuration));
            float eased = t * t * (3f - 2f * t);
            float fromY = GetLaneCentreY(laneIndex, worldX);
            float toY = GetLaneCentreY(targetLaneIndex, worldX);

            if (t >= 0.5f)
                renderItem.SetLane(targetLaneIndex);

            if (t >= 1f)
            {
                laneIndex = targetLaneIndex;
                changingLane = false;
                laneChangeElapsed = 0f;
                renderItem.SetLane(laneIndex);
                ScheduleNextLaneChange();
            }

            return Mathf.Lerp(fromY, toY, eased);
        }

        private void ScheduleNextLaneChange()
        {
            float minimum = Mathf.Max(0.25f, Mathf.Min(laneChangeDelayRange.x, laneChangeDelayRange.y));
            float maximum = Mathf.Max(minimum, Mathf.Max(laneChangeDelayRange.x, laneChangeDelayRange.y));
            nextLaneChangeTime = Time.time + Random.Range(minimum, maximum);
        }

        private void TryRescue(Vector2 swimmerPosition)
        {
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
            {
                if (surfer == null || surfer.IsDead || surfer.IsSwitchingWave)
                    continue;

                bool matchesCurrent = surfer.CurrentWaveIndex == laneIndex || surfer.CurrentWaveIndex == laneIndex + 1;
                bool matchesTarget = changingLane &&
                    (surfer.CurrentWaveIndex == targetLaneIndex || surfer.CurrentWaveIndex == targetLaneIndex + 1);
                if (!matchesCurrent && !matchesTarget)
                    continue;
                if (Vector2.Distance(swimmerPosition, surfer.transform.position) > rescueRadius)
                    continue;

                Rescue(surfer.transform);
                return;
            }
        }

        private void Rescue(Transform surfer)
        {
            if (saved)
                return;

            saved = true;
            PlaySwimmerSavedSfx();

            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null)
                trigger.enabled = false;

            owner?.NotifySaved(gameObject);
            StartCoroutine(RescueReaction(surfer));
        }

        private System.Collections.IEnumerator RescueReaction(Transform surfer)
        {
            Vector3 startPosition = transform.position;
            Vector3 initialScale = transform.localScale;
            Color startColour = spriteRenderer.color;
            float elapsed = 0f;

            while (elapsed < rescueReactionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rescueReactionDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 target = surfer != null ? surfer.position + Vector3.up * 0.25f : startPosition + Vector3.up * 0.4f;
                transform.position = Vector3.Lerp(startPosition, target, eased);
                transform.localScale = initialScale * Mathf.Lerp(1f, 1.55f, t);
                Color colour = Color.Lerp(startColour, new Color(0.55f, 1f, 0.72f, 1f), t);
                colour.a = 1f - t;
                spriteRenderer.color = colour;
                yield return null;
            }

            if (surfer != null)
                SpawnSavedText(surfer.position + Vector3.up * 0.42f);
            Destroy(gameObject);
        }

        private static void PlaySwimmerSavedSfx()
        {
            if (swimmerSavedClip == null)
                swimmerSavedClip = Resources.Load<AudioClip>("Audio/SFX/swimmer_saved");

            if (swimmerSavedClip == null)
            {
                Debug.LogWarning(
                    "Could not load Resources/Audio/SFX/swimmer_saved.wav.");
                return;
            }

            GameObject soundObject = new($"SFX - {swimmerSavedClip.name}");
            AudioSource source = soundObject.AddComponent<AudioSource>();
            source.clip = swimmerSavedClip;
            source.volume = 1f;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.Play();
            Destroy(soundObject, swimmerSavedClip.length + 0.1f);
        }

        private static void SpawnSavedText(Vector3 position)
        {
            GameObject textObject = new("Swimmer Saved Message");
            textObject.transform.position = position;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = "SAVED!";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 32;
            text.characterSize = 0.045f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.55f, 1f, 0.72f, 1f);
            MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 20000;
            textObject.AddComponent<SavedSwimmerTextFx>();
        }

        private void SetAlpha(float alpha)
        {
            if (spriteRenderer == null) return;
            Color colour = spriteRenderer.color;
            colour.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = colour;
        }

        private void ChooseNewWanderTarget()
        {
            targetLaneOffset = Random.Range(-laneWander, laneWander);
            float minimumDelay = Mathf.Max(0.1f, Mathf.Min(directionChangeDelayRange.x, directionChangeDelayRange.y));
            float maximumDelay = Mathf.Max(minimumDelay, Mathf.Max(directionChangeDelayRange.x, directionChangeDelayRange.y));
            nextDirectionChange = Time.time + Random.Range(minimumDelay, maximumDelay);
        }

        private float GetLaneCentreY(int lane, float x)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            return Mathf.Lerp(waterLayers[clamped].GetGameplaySurfaceHeight(x),
                waterLayers[clamped + 1].GetGameplaySurfaceHeight(x), 0.5f);
        }

        private float GetMinimumX(int lane)
        {
            GetPlayableHorizontalBounds(lane, out float minimum, out _);
            return minimum;
        }

        private float GetMaximumX(int lane)
        {
            GetPlayableHorizontalBounds(lane, out _, out float maximum);
            return maximum;
        }

        private void GetPlayableHorizontalBounds(int lane, out float minimum, out float maximum)
        {
            int clamped = Mathf.Clamp(lane, 0, waterLayers.Count - 2);
            float sharedMinimum = Mathf.Max(
                waterLayers[clamped].TankMinimum.x,
                waterLayers[clamped + 1].TankMinimum.x);
            float sharedMaximum = Mathf.Min(
                waterLayers[clamped].TankMaximum.x,
                waterLayers[clamped + 1].TankMaximum.x);

            minimum = Mathf.Lerp(sharedMinimum, sharedMaximum, horizontalPadding);
            maximum = Mathf.Lerp(sharedMaximum, sharedMinimum, horizontalPadding);

            if (minimum > maximum)
            {
                float centre = (minimum + maximum) * 0.5f;
                minimum = centre - 0.05f;
                maximum = centre + 0.05f;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class SavedSwimmerTextFx : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 0.85f;
        [SerializeField] private float riseSpeed = 0.34f;
        private TextMesh textMesh;
        private float elapsed;
        private void Awake() => textMesh = GetComponent<TextMesh>();
        private void Update()
        {
            elapsed += Time.deltaTime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
            if (textMesh != null)
            {
                Color colour = textMesh.color;
                colour.a = 1f - Mathf.Clamp01(elapsed / lifetime);
                textMesh.color = colour;
            }
            if (elapsed >= lifetime) Destroy(gameObject);
        }
    }
}
