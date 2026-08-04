using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Random = UnityEngine.Random;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(Rigidbody2D))]

    public sealed class BoomboxSurferSwimmer : MonoBehaviour
    {
        public event Action<BoomboxSurferSwimmer> Released;

        [Header("Surfing Movement")]
        [SerializeField] private Vector2 speedRange = new(0.34f, 0.52f);
        [SerializeField, Range(0f, 0.4f)] private float laneWander = 0.12f;
        [SerializeField, Min(0.1f)] private float verticalResponsiveness = 5.5f;
        [SerializeField] private Vector2 bobHeightRange = new(0.025f, 0.07f);
        [SerializeField] private Vector2 bobSpeedRange = new(2.1f, 3.4f);
        [SerializeField, Range(0f, 20f)] private float maximumTilt = 8f;

        [Header("Unique Boombox Behaviour")]
        [SerializeField] private Vector2 laneChangeDelayRange = new(5f, 10f);
        [SerializeField, Min(0.2f)] private float laneChangeDuration = 1.2f;
        [SerializeField, Min(1f)] private float playerGrooveRange = 4.5f;
        [SerializeField, Range(0f, 1f)] private float grooveTowardPlayerChance = 0.65f;

        [Header("Natural Player Following")]
        [SerializeField, Min(0.5f)] private float comfortableFollowDistance = 2.25f;
        [SerializeField, Min(1f)] private float maximumFollowDistance = 10.5f;
        [SerializeField, Range(1f, 4f)] private float catchUpSpeedMultiplier = 1.65f;
        [SerializeField, Min(0.1f)] private float waterSectionRefreshInterval = 0.5f;

        [Header("Death Surfer Spatial Music")]
        [SerializeField, Range(0f, 1f)] private float maximumVolume = 0.9f;
        [SerializeField, Min(0f)] private float fullVolumeDistance = 1.25f;
        [SerializeField, Min(0.1f)] private float silentDistance = 10f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.72f;
        [SerializeField, Range(0f, 1f)] private float stereoPanStrength = 0.55f;
        [SerializeField] private Vector2 lowPassCutoffRange = new(1200f, 22000f);

        [Header("Cassette Track Switching")]
        [SerializeField, Min(0.05f)] private float cassetteInsertDelay = 0.22f;
        [SerializeField, Min(0.1f)] private float trackSwitchCooldown = 0.45f;
        [SerializeField, Range(0f, 1f)] private float cassetteVolume = 0.9f;

        [Header("Summon / Release Presentation")]
        [SerializeField, Range(0.1f, 2f)] private float releaseDuration = 0.55f;
        [SerializeField, Range(0f, 3f)] private float releaseDriftSpeed = 0.8f;

        private readonly List<PixelWaterGPU> waterLayers = new();
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private InterWaveRenderItem renderItem;
        private AudioSource musicSource;
        private AudioLowPassFilter lowPassFilter;
        private AudioSource cassetteSource;
        private AudioClip cassetteInsertClip;
        private AudioClip[] musicTracks;
        private int currentTrackIndex;
        private float nextTrackSwitchTime;
        private bool playerTouching;
        private Coroutine cassetteRoutine;
        private Transform player;
        private int laneIndex;
        private int targetLaneIndex;
        private float direction;
        private float speed;
        private float laneOffset;
        private float bobHeight;
        private float bobSpeed;
        private float bobPhase;
        private float nextLaneChangeTime;
        private float laneChangeElapsed;
        private bool changingLane;
        private bool initialised;
        private bool releasing;
        private float nextWaterRefreshTime;

        public bool IsReleasing => releasing;

        public void Initialise(int requestedLane, AudioClip musicClip)
        {
            ResolveReferences();
            if (waterLayers.Count < 2)
            {
                Debug.LogWarning("BoomboxSurferSwimmer needs at least two water layers.", this);
                enabled = false;
                return;
            }

            laneIndex = Mathf.Clamp(requestedLane, 0, waterLayers.Count - 2);
            targetLaneIndex = laneIndex;
            renderItem.SetLane(laneIndex);
            direction = Random.value < 0.5f ? -1f : 1f;
            speed = Random.Range(speedRange.x, speedRange.y);
            laneOffset = Random.Range(-laneWander, laneWander);
            bobHeight = Random.Range(bobHeightRange.x, bobHeightRange.y);
            bobSpeed = Random.Range(bobSpeedRange.x, bobSpeedRange.y);
            bobPhase = Random.Range(0f, Mathf.PI * 2f);

            float minX = GetMinimumX(laneIndex);
            float maxX = GetMaximumX(laneIndex);
            Vector2 position = body.position;
            position.x = direction > 0f ? minX + 0.6f : maxX - 0.6f;
            position.y = GetLaneCentreY(laneIndex, position.x) + laneOffset;
            body.position = position;
            transform.position = position;

            LoadCassetteLibrary(musicClip);
            musicSource.clip = musicTracks != null && musicTracks.Length > 0
                ? musicTracks[currentTrackIndex]
                : musicClip;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = spatialBlend;
            musicSource.rolloffMode = AudioRolloffMode.Linear;
            musicSource.minDistance = Mathf.Max(0.05f, fullVolumeDistance);
            musicSource.maxDistance = Mathf.Max(musicSource.minDistance + 0.1f, silentDistance);
            musicSource.dopplerLevel = 0.15f;
            musicSource.volume = 0f;
            if (musicClip != null)
                musicSource.Play();

            ScheduleLaneChange();
            initialised = true;
        }

        public void InitialiseSummoned(
            int requestedLane,
            AudioClip musicClip,
            Transform summonedBy,
            Vector3 summonPosition)
        {
            Initialise(requestedLane, musicClip);

            if (!initialised)
                return;

            player = summonedBy;

            Vector2 position = summonPosition;
            float minimumX = GetMinimumX(laneIndex);
            float maximumX = GetMaximumX(laneIndex);
            position.x = Mathf.Clamp(
                position.x,
                minimumX,
                maximumX);
            position.y =
                GetLaneCentreY(laneIndex, position.x) +
                laneOffset;

            body.position = position;
            transform.position = position;

            if (summonedBy != null)
            {
                direction =
                    summonedBy.position.x >= position.x
                        ? 1f
                        : -1f;
            }
        }

        public void BeginRelease()
        {
            if (releasing)
                return;

            StartCoroutine(ReleaseRoutine());
        }

        private IEnumerator ReleaseRoutine()
        {
            releasing = true;
            changingLane = false;

            float elapsed = 0f;
            Color startColour =
                spriteRenderer != null
                    ? spriteRenderer.color
                    : Color.white;
            float startVolume =
                musicSource != null
                    ? musicSource.volume
                    : 0f;

            while (elapsed < releaseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(
                    elapsed /
                    Mathf.Max(0.01f, releaseDuration));
                float eased =
                    Mathf.SmoothStep(0f, 1f, t);

                Vector2 position =
                    body != null
                        ? body.position
                        : (Vector2)transform.position;

                position.x +=
                    direction *
                    releaseDriftSpeed *
                    Time.unscaledDeltaTime;
                position.y -=
                    releaseDriftSpeed *
                    0.18f *
                    Time.unscaledDeltaTime;

                if (body != null)
                    body.position = position;
                transform.position = position;

                if (musicSource != null)
                    musicSource.volume =
                        Mathf.Lerp(
                            startVolume,
                            0f,
                            eased);

                if (spriteRenderer != null)
                {
                    Color faded = startColour;
                    faded.a =
                        Mathf.Lerp(
                            startColour.a,
                            0f,
                            eased);
                    spriteRenderer.color = faded;
                }

                yield return null;
            }

            Released?.Invoke(this);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Released?.Invoke(this);
            Released = null;
        }

        private void Awake() => ResolveReferences();

        private void Update()
        {
            if (!initialised || releasing || !playerTouching || Time.unscaledTime < nextTrackSwitchTime)
                return;

            int direction = ReadCassetteDirection();
            if (direction == 0)
                return;

            nextTrackSwitchTime = Time.unscaledTime + trackSwitchCooldown;
            SwitchCassette(direction);
        }
        private void Start() { if (!initialised) Initialise(1, Resources.Load<AudioClip>("Audio/Music/Death Surfer")); }

        private void ResolveReferences()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            spriteRenderer = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            musicSource = GetComponent<AudioSource>();
            AudioSource[] sources = GetComponents<AudioSource>();
            cassetteSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
            cassetteSource.playOnAwake = false;
            cassetteSource.loop = false;
            cassetteSource.spatialBlend = spatialBlend;
            cassetteSource.rolloffMode = AudioRolloffMode.Linear;
            cassetteSource.minDistance = Mathf.Max(0.05f, fullVolumeDistance);
            cassetteSource.maxDistance = Mathf.Max(cassetteSource.minDistance + 0.1f, silentDistance);
            cassetteSource.volume = cassetteVolume;
            lowPassFilter = GetComponent<AudioLowPassFilter>();
            if (lowPassFilter == null)
                lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
            waterLayers.Clear();
            waterLayers.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
        }

        private void FixedUpdate()
        {
            if (!initialised ||
                releasing ||
                waterLayers.Count < 2)
            {
                return;
            }

            FindPlayer();
            RefreshWaterLayersWhenNeeded();

            Vector2 position = body.position;
            float movementSpeed = speed;

            if (player != null)
            {
                float horizontalDelta = player.position.x - position.x;
                float absoluteDelta = Mathf.Abs(horizontalDelta);

                // The board still swims independently nearby, but turns and catches up
                // before it can be left behind by the endless world recycling.
                if (absoluteDelta > comfortableFollowDistance)
                    direction = Mathf.Sign(horizontalDelta);

                if (absoluteDelta > maximumFollowDistance)
                {
                    float catchUp = Mathf.InverseLerp(
                        maximumFollowDistance,
                        maximumFollowDistance * 2f,
                        absoluteDelta);
                    movementSpeed *= Mathf.Lerp(1f, catchUpSpeedMultiplier, catchUp);
                }
            }

            position.x += direction * movementSpeed * Time.fixedDeltaTime;

            int boundsLane = changingLane ? Mathf.Min(laneIndex, targetLaneIndex) : laneIndex;
            float minX = GetMinimumX(boundsLane);
            float maxX = GetMaximumX(boundsLane);
            if (position.x <= minX)
            {
                position.x = minX;
                direction = player != null && player.position.x < position.x ? -1f : 1f;
            }
            else if (position.x >= maxX)
            {
                position.x = maxX;
                direction = player != null && player.position.x > position.x ? 1f : -1f;
            }

            if (!changingLane && Time.time >= nextLaneChangeTime)
                BeginLaneChange();

            float sampledX = Mathf.Clamp(position.x, minX, maxX);
            float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
            float desiredY = UpdateLaneTransition(sampledX) + laneOffset + bob;
            position.y = Mathf.Lerp(position.y, desiredY,
                1f - Mathf.Exp(-verticalResponsiveness * Time.fixedDeltaTime));

            body.MovePosition(position);
            spriteRenderer.flipX = direction < 0f;
            transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Sin(Time.time * bobSpeed * 0.72f + bobPhase) * maximumTilt);
            UpdateSpatialMusic(position);
        }


        private void RefreshWaterLayersWhenNeeded()
        {
            if (Time.time < nextWaterRefreshTime)
                return;

            nextWaterRefreshTime = Time.time + Mathf.Max(0.1f, waterSectionRefreshInterval);
            float sampleX = player != null ? player.position.x : transform.position.x;
            var nearest = EndlessWaveSections.LayersNearest(sampleX);
            if (nearest == null || nearest.Count < 2)
                return;

            waterLayers.Clear();
            waterLayers.AddRange(nearest);
            int laneCount = waterLayers.Count - 1;
            laneIndex = Mathf.Clamp(laneIndex, 0, laneCount - 1);
            targetLaneIndex = Mathf.Clamp(targetLaneIndex, 0, laneCount - 1);
            renderItem.SetLane(changingLane ? targetLaneIndex : laneIndex);
        }

        private void FindPlayer()
        {
            if (player != null)
                return;
            TinyWaveSurfer surfer = FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None)
                .Where(candidate => candidate != null && !candidate.IsDead)
                .OrderByDescending(candidate => candidate.IsPlayerControlled)
                .FirstOrDefault();
            if (surfer != null)
                player = surfer.transform;
        }

        private void BeginLaneChange()
        {
            int laneCount = waterLayers.Count - 1;
            if (laneCount <= 1) { ScheduleLaneChange(); return; }

            int desired = laneIndex;
            if (player != null && Vector2.Distance(body.position, player.position) <= playerGrooveRange && Random.value <= grooveTowardPlayerChance)
            {
                TinyWaveSurfer surfer = player.GetComponent<TinyWaveSurfer>();
                if (surfer != null)
                    desired = Mathf.Clamp(surfer.CurrentWaveIndex, 0, laneCount - 1);
            }

            if (desired == laneIndex)
                desired = laneIndex <= 0 ? 1 : laneIndex >= laneCount - 1 ? laneCount - 2 : laneIndex + (Random.value < 0.5f ? -1 : 1);

            targetLaneIndex = Mathf.Clamp(desired, 0, laneCount - 1);
            changingLane = targetLaneIndex != laneIndex;
            laneChangeElapsed = 0f;
            laneOffset = Random.Range(-laneWander, laneWander);
            if (!changingLane)
                ScheduleLaneChange();
        }

        private float UpdateLaneTransition(float worldX)
        {
            if (!changingLane)
                return GetLaneCentreY(laneIndex, worldX);

            laneChangeElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(laneChangeElapsed / Mathf.Max(0.2f, laneChangeDuration));
            float eased = t * t * (3f - 2f * t);
            if (t >= 0.5f)
                renderItem.SetLane(targetLaneIndex);
            float y = Mathf.Lerp(GetLaneCentreY(laneIndex, worldX), GetLaneCentreY(targetLaneIndex, worldX), eased);
            if (t >= 1f)
            {
                laneIndex = targetLaneIndex;
                changingLane = false;
                renderItem.SetLane(laneIndex);
                ScheduleLaneChange();
            }
            return y;
        }

        private void ScheduleLaneChange() => nextLaneChangeTime = Time.time + Random.Range(
            Mathf.Min(laneChangeDelayRange.x, laneChangeDelayRange.y),
            Mathf.Max(laneChangeDelayRange.x, laneChangeDelayRange.y));

        private void UpdateSpatialMusic(Vector2 position)
        {
            if (musicSource == null || player == null)
                return;

            float distance = Vector2.Distance(position, player.position);
            float t = Mathf.InverseLerp(fullVolumeDistance, silentDistance, distance);
            float presence = 1f - Mathf.SmoothStep(0f, 1f, t);
            musicSource.volume = maximumVolume * presence;

            float horizontal = player.position.x - position.x;
            musicSource.panStereo = Mathf.Clamp(horizontal / Mathf.Max(1f, silentDistance), -1f, 1f) * stereoPanStrength;
            if (lowPassFilter != null)
                lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassCutoffRange.y, lowPassCutoffRange.x, t);
        }


        private void LoadCassetteLibrary(AudioClip initialClip)
        {
            cassetteInsertClip = Resources.Load<AudioClip>("Audio/SFX/cassette_insert");

            AudioClip deathSurfer = Resources.Load<AudioClip>("Audio/Music/Death Surfer");
            AudioClip highLife = Resources.Load<AudioClip>("Audio/Music/Daft Punk - 8 - High Life");
            AudioClip windItUp = Resources.Load<AudioClip>("Audio/Music/The Prodigy - 3 - Wind It Up");

            musicTracks = new[] { deathSurfer ?? initialClip, highLife, windItUp }
                .Where(clip => clip != null)
                .Distinct()
                .ToArray();

            currentTrackIndex = 0;
            if (initialClip != null)
            {
                int match = Array.IndexOf(musicTracks, initialClip);
                if (match >= 0)
                    currentTrackIndex = match;
            }
        }

        private void SwitchCassette(int step)
        {
            if (musicTracks == null || musicTracks.Length < 2)
                return;

            currentTrackIndex = (currentTrackIndex + step + musicTracks.Length) % musicTracks.Length;

            if (cassetteRoutine != null)
                StopCoroutine(cassetteRoutine);
            cassetteRoutine = StartCoroutine(InsertCassetteRoutine(musicTracks[currentTrackIndex]));
        }

        private IEnumerator InsertCassetteRoutine(AudioClip nextTrack)
        {
            if (musicSource != null)
                musicSource.Pause();

            if (cassetteSource != null && cassetteInsertClip != null)
                cassetteSource.PlayOneShot(cassetteInsertClip, cassetteVolume);

            yield return new WaitForSecondsRealtime(cassetteInsertDelay);

            if (musicSource != null && nextTrack != null && !releasing)
            {
                musicSource.clip = nextTrack;
                musicSource.time = 0f;
                musicSource.Play();
            }

            cassetteRoutine = null;
        }

        private static int ReadCassetteDirection()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return 0;

            bool noHorizontal = !gamepad.dpad.left.isPressed && !gamepad.dpad.right.isPressed;
            bool noButtons = !gamepad.buttonSouth.isPressed && !gamepad.buttonNorth.isPressed &&
                             !gamepad.buttonEast.isPressed && !gamepad.buttonWest.isPressed &&
                             !gamepad.leftShoulder.isPressed && !gamepad.rightShoulder.isPressed;

            if (!noHorizontal || !noButtons)
                return 0;

            if (gamepad.dpad.up.wasPressedThisFrame && !gamepad.dpad.down.isPressed)
                return 1;
            if (gamepad.dpad.down.wasPressedThisFrame && !gamepad.dpad.up.isPressed)
                return -1;
#endif
            return 0;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer == null || surfer.IsDead || !surfer.IsPlayerControlled)
                return;

            player = surfer.transform;
            playerTouching = true;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer != null && !surfer.IsDead && surfer.IsPlayerControlled)
            {
                player = surfer.transform;
                playerTouching = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            TinyWaveSurfer surfer = other.GetComponentInParent<TinyWaveSurfer>();
            if (surfer != null && surfer.transform == player)
                playerTouching = false;
        }

        private float GetMinimumX(int lane) => Mathf.Max(waterLayers[lane].TankMinimum.x, waterLayers[lane + 1].TankMinimum.x) + 0.15f;
        private float GetMaximumX(int lane) => Mathf.Min(waterLayers[lane].TankMaximum.x, waterLayers[lane + 1].TankMaximum.x) - 0.15f;
        private float GetLaneCentreY(int lane, float x) => (waterLayers[lane].GetGameplaySurfaceHeight(x) + waterLayers[lane + 1].GetGameplaySurfaceHeight(x)) * 0.5f;
    }
}
