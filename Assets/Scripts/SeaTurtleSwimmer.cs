using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(InterWaveRenderItem), typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class SeaTurtleSwimmer : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float swimSpeed = .46f;
        [SerializeField, Min(.1f)] private float formationCatchup = 1.7f;
        [SerializeField, Min(.1f)] private float threatAvoidanceRadius = 2.4f;
        [SerializeField, Min(.1f)] private float playerDraftRange = 3.2f;
        [SerializeField, Range(0f, 1f)] private float draftChance = .35f;
        [SerializeField, Min(1f)] private float fps = 9f;

        [Header("Spawn Stability")]
        [SerializeField, Min(0f)] private float spawnSettleDuration = 0.25f;
        [SerializeField, Range(1f, 20f)] private float surfaceSmoothing = 6f;
        [SerializeField, Min(0.05f)] private float maximumVerticalSpeed = 0.9f;

        private const float SharedThreatRefreshInterval = 0.35f;
        private const float SharedPlayerRefreshInterval = 0.5f;

        private static readonly List<MonoBehaviour> SharedThreats = new(32);
        private static Sprite[] sharedFrames = Array.Empty<Sprite>();
        private static TinyWaveSurfer sharedPlayer;
        private static float nextSharedThreatRefresh;
        private static float nextSharedPlayerRefresh;

        private readonly List<PixelWaterGPU> water = new();
        private SpriteRenderer renderer2D;
        private InterWaveRenderItem renderItem;
        private Rigidbody2D body;
        private Sprite[] frames = Array.Empty<Sprite>();
        private Transform leader;
        private Vector2 formationOffset;
        private TinyWaveSurfer player;
        private int lane;
        private float direction;
        private float clock;
        private float phase;
        private bool drafting;
        private float settleUntil;
        private float smoothedLaneY;

        public void Initialise(int requestedLane, Transform groupLeader, Vector2 offset, float travelDirection)
        {
            Resolve();

            if (sharedFrames.Length == 0)
                sharedFrames = Resources.LoadAll<Sprite>("SeaTurtles/sea_turtle_move").OrderBy(s => s.name).ToArray();

            frames = sharedFrames;
            if (water.Count < 2 || frames.Length == 0)
            {
                enabled = false;
                return;
            }

            renderer2D.sprite = frames[0];
            lane = Mathf.Clamp(requestedLane, 0, water.Count - 2);
            leader = groupLeader;
            formationOffset = offset;
            direction = Mathf.Approximately(travelDirection, 0f) ? 1f : Mathf.Sign(travelDirection);
            renderItem.SetLane(lane);
            phase = UnityEngine.Random.Range(0f, 10f);
            drafting = UnityEngine.Random.value < draftChance;

            Vector2 p;
            if (leader != null && leader != transform)
            {
                // Followers must begin in formation with the leader. Choosing a new
                // camera-safe edge per turtle made members spawn on opposite sides,
                // then violently correct toward the leader on their first physics tick.
                p = (Vector2)leader.position + formationOffset;
            }
            else
            {
                float x = ChooseEntryXForDirection(direction);
                p = new Vector2(x, LaneY(x));
            }

            smoothedLaneY = p.y;
            settleUntil = Time.time + spawnSettleDuration;
            body.position = p;
            transform.position = p;
            renderer2D.flipX = direction < 0f;
        }

        private void Awake() => Resolve();

        private void Resolve()
        {
            renderer2D = GetComponent<SpriteRenderer>();
            renderItem = GetComponent<InterWaveRenderItem>();
            body = GetComponent<Rigidbody2D>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.28f;

            water.Clear();
            water.AddRange(EndlessWaveSections.LayersNearest(transform.position.x));
            water.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            water.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
        }

        private void Update()
        {
            if (frames.Length == 0)
                return;

            clock += Time.deltaTime * fps;
            renderer2D.sprite = frames[Mathf.FloorToInt(clock) % frames.Length];
        }

        private void FixedUpdate()
        {
            if (water.Count < 2)
                return;

            RefreshSharedPlayerIfNeeded();
            player = sharedPlayer;

            Vector2 p = body.position;
            Vector2 desiredVelocity = Time.time < settleUntil
                ? Vector2.zero
                : new Vector2(direction * swimSpeed, 0f);

            bool settling = Time.time < settleUntil;
            if (!settling)
            {
                if (leader != null && leader != transform)
                {
                    Vector2 desired = (Vector2)leader.position + formationOffset;
                    desiredVelocity += Vector2.ClampMagnitude((desired - p) * formationCatchup, swimSpeed * .75f);
                }

                if (drafting && player != null && !player.IsDead &&
                    ((Vector2)player.transform.position - p).sqrMagnitude <= playerDraftRange * playerDraftRange)
                {
                    Vector2 behind = (Vector2)player.transform.position + new Vector2(-direction * .8f, .12f);
                    desiredVelocity += Vector2.ClampMagnitude((behind - p) * .5f, .25f);
                }

                RefreshSharedThreatsIfNeeded();
                desiredVelocity += ThreatAvoidance(p);
            }
            p += desiredVelocity * Time.fixedDeltaTime;

            float min = water[0].TankMinimum.x - .8f;
            float max = water[0].TankMaximum.x + .8f;
            if (p.x < min || p.x > max)
            {
                Destroy(gameObject);
                return;
            }

            float sampledLaneY = LaneY(p.x);
            float surfaceBlend = 1f - Mathf.Exp(-surfaceSmoothing * Time.fixedDeltaTime);
            smoothedLaneY = Mathf.Lerp(smoothedLaneY, sampledLaneY, surfaceBlend);

            float bob = Time.time < settleUntil ? 0f : Mathf.Sin(Time.time * 1.2f + phase) * .04f;
            float targetY = smoothedLaneY + formationOffset.y + bob;
            p.y = Mathf.MoveTowards(p.y, targetY, maximumVerticalSpeed * Time.fixedDeltaTime);
            body.MovePosition(p);

            if (Mathf.Abs(desiredVelocity.x) > .03f)
                renderer2D.flipX = desiredVelocity.x < 0f;
        }


        private float ChooseEntryXForDirection(float travelDirection)
        {
            float halfWidth = renderer2D != null ? Mathf.Max(0.1f, renderer2D.bounds.extents.x) : 0.3f;
            float margin = halfWidth + 0.6f;
            float min = water[0].TankMinimum.x + margin;
            float max = water[0].TankMaximum.x - margin;
            return travelDirection >= 0f ? min : max;
        }

        private Vector2 ThreatAvoidance(Vector2 p)
        {
            Vector2 result = Vector2.zero;
            float radiusSquared = threatAvoidanceRadius * threatAvoidanceRadius;

            for (int i = SharedThreats.Count - 1; i >= 0; i--)
            {
                MonoBehaviour threat = SharedThreats[i];
                if (threat == null || !threat.isActiveAndEnabled)
                    continue;

                Vector2 away = p - (Vector2)threat.transform.position;
                float squaredDistance = away.sqrMagnitude;
                if (squaredDistance >= radiusSquared || squaredDistance <= .0001f)
                    continue;

                float distance = Mathf.Sqrt(squaredDistance);
                result += away / distance * (1f - distance / threatAvoidanceRadius) * .7f;
            }

            return result;
        }

        private static void RefreshSharedThreatsIfNeeded()
        {
            if (Time.time < nextSharedThreatRefresh)
                return;

            nextSharedThreatRefresh = Time.time + SharedThreatRefreshInterval;
            SharedThreats.Clear();
            AddActiveThreats(FindObjectsByType<SharkLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            AddActiveThreats(FindObjectsByType<GiantSquidLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            AddActiveThreats(FindObjectsByType<BloodSharkLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            AddActiveThreats(FindObjectsByType<TransparentSquidLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            AddActiveThreats(FindObjectsByType<StingrayLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        private static void AddActiveThreats<T>(T[] threats) where T : MonoBehaviour
        {
            for (int i = 0; i < threats.Length; i++)
            {
                if (threats[i] != null && threats[i].isActiveAndEnabled)
                    SharedThreats.Add(threats[i]);
            }
        }

        private static void RefreshSharedPlayerIfNeeded()
        {
            if (sharedPlayer != null && !sharedPlayer.IsDead && Time.time < nextSharedPlayerRefresh)
                return;

            nextSharedPlayerRefresh = Time.time + SharedPlayerRefreshInterval;
            sharedPlayer = null;
            TinyWaveSurfer[] surfers = FindObjectsByType<TinyWaveSurfer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < surfers.Length; i++)
            {
                TinyWaveSurfer candidate = surfers[i];
                if (candidate == null || candidate.IsDead)
                    continue;

                if (sharedPlayer == null || candidate.IsPlayerControlled)
                    sharedPlayer = candidate;

                if (candidate.IsPlayerControlled)
                    break;
            }
        }

        private float LaneY(float x) =>
            (water[lane].GetGameplaySurfaceHeight(x) + water[lane + 1].GetGameplaySurfaceHeight(x)) * .5f;
    }
}
