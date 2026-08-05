using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(InterWaveRenderItem), typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GiantTurtleSwimmer : MonoBehaviour
    {
        private enum State { Cruise, Alert, WindUp, Charge, Recover, Retreat }

        [Header("Heavy Territorial Motion")]
        [SerializeField, Min(0.05f)] private float cruiseSpeed = 0.34f;
        [SerializeField, Min(0.05f)] private float alertSpeed = 0.48f;
        [SerializeField, Min(0.1f)] private float chargeSpeed = 2.05f;
        [SerializeField, Range(1f, 20f)] private float verticalResponsiveness = 5.5f;
        [SerializeField, Range(0f, 0.3f)] private float laneDepthBias = 0.11f;
        [SerializeField, Min(0f)] private float spawnSettleDuration = 0.3f;
        [SerializeField, Range(1f, 20f)] private float surfaceSmoothing = 5f;
        [SerializeField, Min(0.05f)] private float maximumVerticalSpeed = 0.8f;

        [Header("Section Recycling Stability")]
        [Tooltip("How often the turtle checks whether the endless-wave section beneath it changed.")]
        [SerializeField, Min(0.02f)] private float waterRefreshInterval = 0.12f;

        [Tooltip("Rebind before reaching a recycled section's exact edge.")]
        [SerializeField, Min(0f)] private float sectionRebindPadding = 0.45f;

        [Tooltip("Rejects a single extreme surface-height change caused by a section being repositioned.")]
        [SerializeField, Min(0.05f)] private float maximumSurfaceSampleJump = 0.65f;

        [Tooltip("Prevents a recycled section from snapping the turtle horizontally across the world.")]
        [SerializeField, Min(0.1f)] private float maximumBoundaryCorrection = 0.35f;

        [Header("Territorial Attack")]
        [SerializeField, Min(0.5f)] private float detectionRange = 5.2f;
        [SerializeField, Min(0.2f)] private float attackRange = 3.1f;
        [SerializeField, Min(0.1f)] private float contactRange = 0.86f;
        [SerializeField, Min(0.1f)] private float alertDuration = 0.55f;
        [SerializeField, Min(0.1f)] private float windUpDuration = 0.72f;
        [SerializeField, Min(0.1f)] private float chargeDuration = 1.15f;
        [SerializeField, Min(0.1f)] private float recoveryDuration = 2.1f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 5.2f;

        [Header("Animation")]
        [SerializeField, Min(1f)] private float moveFps = 8f;
        [SerializeField, Min(1f)] private float attackFps = 11f;

        private readonly List<PixelWaterGPU> water = new();

        private SpriteRenderer renderer2D;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;

        private static Sprite[] sharedMoveFrames = Array.Empty<Sprite>();
        private static Sprite[] sharedAttackFrames = Array.Empty<Sprite>();

        private Sprite[] moveFrames = Array.Empty<Sprite>();
        private Sprite[] attackFrames = Array.Empty<Sprite>();

        private TinyWaveSurfer player;
        private State state;
        private int lane;
        private int requestedLane;
        private float direction;
        private float stateUntil;
        private float nextAttackAt;
        private float animationClock;
        private bool hitApplied;
        private float settleUntil;
        private float smoothedLaneY;
        private float nextWaterRefreshAt;
        private bool initialised;

        public void Initialise(int requestedLaneIndex)
        {
            Resolve();

            requestedLane = Mathf.Max(0, requestedLaneIndex);

            if (sharedMoveFrames.Length == 0)
                sharedMoveFrames = LoadOrdered("SeaTurtles/giant_turtle_move");

            if (sharedAttackFrames.Length == 0)
                sharedAttackFrames = LoadOrdered("SeaTurtles/giant_turtle_attack");

            moveFrames = sharedMoveFrames;
            attackFrames = sharedAttackFrames;

            if (moveFrames.Length == 0)
            {
                enabled = false;
                return;
            }

            renderer2D.sprite = moveFrames[0];

            if (!RefreshWaterBinding(transform.position.x, true))
            {
                // EndlessWaveSections may still be constructing its lanes.
                // Keep this component alive so Start/FixedUpdate can retry.
                initialised = false;
                nextWaterRefreshAt = Time.time + waterRefreshInterval;
                return;
            }

            float x = CameraSafeSpawnUtility.ChooseOffscreenEntryX(
                water,
                renderer2D,
                out bool fromLeft);

            direction = fromLeft ? 1f : -1f;

            float spawnY = SafeLaneY(x, transform.position.y);
            Vector2 spawnPosition = new(x, spawnY);

            smoothedLaneY = spawnY;
            settleUntil = Time.time + spawnSettleDuration;

            body.position = spawnPosition;
            transform.position = spawnPosition;

            renderer2D.flipX = direction < 0f;
            state = State.Cruise;
            nextAttackAt = Time.time + 2f;
            nextWaterRefreshAt = Time.time + waterRefreshInterval;
            initialised = true;
        }

        private void Awake()
        {
            Resolve();
        }

        private void Start()
        {
            if (moveFrames.Length == 0)
                Initialise(0);
        }

        private void Resolve()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.25f, 0.72f);
            box.offset = new Vector2(0f, -0.03f);

            renderer2D = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
        }

        private void Update()
        {
            Sprite[] frames =
                state == State.WindUp || state == State.Charge
                    ? attackFrames
                    : moveFrames;

            if (frames.Length == 0)
                return;

            animationClock += Time.deltaTime *
                              (ReferenceEquals(frames, attackFrames) ? attackFps : moveFps);

            renderer2D.sprite =
                frames[Mathf.FloorToInt(animationClock) % frames.Length];
        }

        private void FixedUpdate()
        {
            if (state == State.Retreat)
                return;

            Vector2 position = body.position;

            if (!initialised)
            {
                if (Time.time >= nextWaterRefreshAt)
                {
                    nextWaterRefreshAt = Time.time + waterRefreshInterval;

                    if (RefreshWaterBinding(position.x, true))
                    {
                        float y = SafeLaneY(position.x, position.y);
                        position.y = y;
                        body.position = position;
                        transform.position = position;
                        smoothedLaneY = y;
                        settleUntil = Time.time + spawnSettleDuration;
                        initialised = true;
                    }
                }

                return;
            }

            if (NeedsWaterRefresh(position.x) || Time.time >= nextWaterRefreshAt)
            {
                nextWaterRefreshAt = Time.time + waterRefreshInterval;
                RefreshWaterBinding(position.x, false);
            }

            if (water.Count < 2)
                return;

            if (player == null || player.IsDead)
                player = FindPlayer();

            UpdateState(position);

            float speed =
                state == State.Charge ? chargeSpeed :
                state == State.Alert ? alertSpeed :
                state == State.WindUp ? 0f :
                cruiseSpeed;

            if (Time.time < settleUntil)
                speed = 0f;

            position.x += direction * speed * Time.fixedDeltaTime;

            // Re-evaluate after horizontal movement. This is important when the
            // turtle crosses into a neighboring section during the same physics tick.
            if (NeedsWaterRefresh(position.x))
                RefreshWaterBinding(position.x, false);

            ApplyStableHorizontalBounds(ref position);

            float sampledLaneY = SafeLaneY(position.x, smoothedLaneY);

            // A recycled section can briefly report a stale or newly reset surface.
            // Limit the accepted change, then continue smoothing normally.
            float sampleDelta = sampledLaneY - smoothedLaneY;
            sampleDelta = Mathf.Clamp(
                sampleDelta,
                -maximumSurfaceSampleJump,
                maximumSurfaceSampleJump);

            sampledLaneY = smoothedLaneY + sampleDelta;

            float smoothing = Mathf.Max(0.01f, surfaceSmoothing);
            float sampleBlend =
                1f - Mathf.Exp(-smoothing * Time.fixedDeltaTime);

            smoothedLaneY =
                Mathf.Lerp(smoothedLaneY, sampledLaneY, sampleBlend);

            float verticalSpeed = Mathf.Max(
                maximumVerticalSpeed,
                verticalResponsiveness * Mathf.Abs(smoothedLaneY - position.y));

            float verticalStep = verticalSpeed * Time.fixedDeltaTime;

            position.y = Mathf.MoveTowards(
                position.y,
                smoothedLaneY,
                verticalStep);

            body.MovePosition(position);

            if (state == State.Charge &&
                !hitApplied &&
                player != null &&
                Vector2.Distance(position, player.transform.position) <= contactRange)
            {
                hitApplied = player.TakeSharkHit(position);
            }
        }

        private bool RefreshWaterBinding(float worldX, bool forceSurfaceReset)
        {
            List<PixelWaterGPU> candidates =
                EndlessWaveSections
                    .LayersNearest(worldX)
                    .Where(layer =>
                        layer != null &&
                        layer.isActiveAndEnabled)
                    .OrderBy(layer => layer.IndependentLayerIndex)
                    .ToList();

            if (candidates.Count < 2)
                return false;

            bool bindingChanged =
                water.Count != candidates.Count ||
                !water.SequenceEqual(candidates);

            water.Clear();
            water.AddRange(candidates);

            lane = Mathf.Clamp(
                requestedLane,
                0,
                water.Count - 2);

            renderItem.SetLane(lane);

            if (forceSurfaceReset || bindingChanged || !IsFinite(smoothedLaneY))
            {
                float sampled = SafeLaneY(worldX, body != null ? body.position.y : transform.position.y);

                if (forceSurfaceReset || !IsFinite(smoothedLaneY))
                {
                    smoothedLaneY = sampled;
                }
                else
                {
                    // Rebinding should not create a visible vertical snap.
                    smoothedLaneY = Mathf.MoveTowards(
                        smoothedLaneY,
                        sampled,
                        maximumSurfaceSampleJump);
                }

                settleUntil = Mathf.Max(
                    settleUntil,
                    Time.time + spawnSettleDuration);
            }

            return true;
        }

        private bool NeedsWaterRefresh(float worldX)
        {
            if (water.Count < 2)
                return true;

            for (int i = 0; i < water.Count; i++)
            {
                PixelWaterGPU layer = water[i];

                if (layer == null || !layer.isActiveAndEnabled)
                    return true;
            }

            float minimumX = water[0].TankMinimum.x;
            float maximumX = water[0].TankMaximum.x;

            if (!IsFinite(minimumX) || !IsFinite(maximumX) || maximumX <= minimumX)
                return true;

            return worldX <= minimumX + sectionRebindPadding ||
                   worldX >= maximumX - sectionRebindPadding;
        }

        private void ApplyStableHorizontalBounds(ref Vector2 position)
        {
            if (water.Count == 0)
                return;

            float minimumX = water[0].TankMinimum.x + 0.6f;
            float maximumX = water[0].TankMaximum.x - 0.6f;

            if (!IsFinite(minimumX) ||
                !IsFinite(maximumX) ||
                maximumX <= minimumX)
            {
                return;
            }

            if (position.x < minimumX)
            {
                float distanceOutside = minimumX - position.x;

                // Small normal edge contact: correct and turn around.
                // Large distance: a section was recycled; do not teleport.
                if (distanceOutside <= maximumBoundaryCorrection)
                {
                    position.x = minimumX;
                    SetDirection(1f);
                }
                else
                {
                    RefreshWaterBinding(position.x, false);
                }
            }
            else if (position.x > maximumX)
            {
                float distanceOutside = position.x - maximumX;

                if (distanceOutside <= maximumBoundaryCorrection)
                {
                    position.x = maximumX;
                    SetDirection(-1f);
                }
                else
                {
                    RefreshWaterBinding(position.x, false);
                }
            }
        }

        private float SafeLaneY(float x, float fallback)
        {
            if (water.Count < 2)
                return fallback;

            lane = Mathf.Clamp(lane, 0, water.Count - 2);

            float upper = water[lane].GetGameplaySurfaceHeight(x);
            float lower = water[lane + 1].GetGameplaySurfaceHeight(x);

            if (!IsFinite(upper) || !IsFinite(lower))
                return fallback;

            return (upper + lower) * 0.5f - Mathf.Abs(laneDepthBias);
        }

        private void UpdateState(Vector2 position)
        {
            if (state == State.Alert && Time.time >= stateUntil)
            {
                state = State.WindUp;
                stateUntil = Time.time + windUpDuration;
                animationClock = 0f;
                return;
            }

            if (state == State.WindUp)
            {
                FacePlayer(position);

                if (Time.time >= stateUntil)
                {
                    state = State.Charge;
                    stateUntil = Time.time + chargeDuration;
                    hitApplied = false;
                    animationClock = 0f;
                }

                return;
            }

            if (state == State.Charge && Time.time >= stateUntil)
            {
                state = State.Recover;
                stateUntil = Time.time + recoveryDuration;
                nextAttackAt = Time.time + attackCooldown;
                return;
            }

            if (state == State.Recover)
            {
                if (Time.time >= stateUntil)
                    state = State.Cruise;

                return;
            }

            if (player == null || Time.time < nextAttackAt)
                return;

            float distance =
                Vector2.Distance(position, player.transform.position);

            if (distance <= detectionRange)
            {
                state = State.Alert;
                stateUntil = Time.time + alertDuration;
                FacePlayer(position);
            }
        }

        private void FacePlayer(Vector2 position)
        {
            if (player == null)
                return;

            float deltaX = player.transform.position.x - position.x;

            if (Mathf.Abs(deltaX) > 0.08f)
                SetDirection(Mathf.Sign(deltaX));
        }

        private void SetDirection(float value)
        {
            direction = Mathf.Sign(
                Mathf.Approximately(value, 0f)
                    ? 1f
                    : value);

            if (renderer2D != null)
                renderer2D.flipX = direction < 0f;
        }

        private TinyWaveSurfer FindPlayer()
        {
            return FindObjectsByType<TinyWaveSurfer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(candidate =>
                    candidate != null &&
                    !candidate.IsDead)
                .OrderByDescending(candidate =>
                    candidate.IsPlayerControlled)
                .FirstOrDefault();
        }

        public void TakeThrownItemHit(Vector2 impact)
        {
            state = State.Recover;
            stateUntil = Time.time + recoveryDuration;
            nextAttackAt = Time.time + attackCooldown;

            SetDirection(
                Mathf.Sign(transform.position.x - impact.x));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private static Sprite[] LoadOrdered(string path)
        {
            return Resources
                .LoadAll<Sprite>(path)
                .OrderBy(sprite => sprite.name)
                .ToArray();
        }
    }
}
