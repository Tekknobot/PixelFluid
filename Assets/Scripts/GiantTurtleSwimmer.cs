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
        private float direction;
        private float stateUntil;
        private float nextAttackAt;
        private float animationClock;
        private bool hitApplied;

        public void Initialise(int requestedLane)
        {
            Resolve();
            if (sharedMoveFrames.Length == 0)
                sharedMoveFrames = LoadOrdered("SeaTurtles/giant_turtle_move");
            if (sharedAttackFrames.Length == 0)
                sharedAttackFrames = LoadOrdered("SeaTurtles/giant_turtle_attack");
            moveFrames = sharedMoveFrames;
            attackFrames = sharedAttackFrames;
            if (water.Count < 2 || moveFrames.Length == 0) { enabled = false; return; }
            lane = Mathf.Clamp(requestedLane, 0, water.Count - 2);
            renderItem.SetLane(lane);
            float x = CameraSafeSpawnUtility.ChooseOffscreenEntryX(water, renderer2D, out bool fromLeft);
            direction = fromLeft ? 1f : -1f;
            Vector2 p = new(x, LaneY(x));
            body.position = p; transform.position = p;
            renderer2D.flipX = direction < 0f;
            state = State.Cruise;
            nextAttackAt = Time.time + 2f;
        }

        private void Awake() => Resolve();
        private void Start() { if (moveFrames.Length == 0) Initialise(0); }
        private void Resolve()
        {
            body = GetComponent<Rigidbody2D>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody2D>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider2D>();

            box.isTrigger = true;
            box.size = new Vector2(1.25f, 0.72f);
            box.offset = new Vector2(0f, -0.03f);

            renderer2D = GetComponent<SpriteRenderer>();
            if (renderer2D == null)
                renderer2D = gameObject.AddComponent<SpriteRenderer>();

            renderItem = GetComponent<InterWaveRenderItem>();
            if (renderItem == null)
                renderItem = gameObject.AddComponent<InterWaveRenderItem>();

            water.Clear();
            water.AddRange(
                EndlessWaveSections.LayersNearest(transform.position.x));

            water.RemoveAll(layer =>
                layer == null ||
                !layer.isActiveAndEnabled);

            water.Sort((a, b) =>
                a.IndependentLayerIndex.CompareTo(
                    b.IndependentLayerIndex));
        }

        private void Update()
        {
            Sprite[] frames = state == State.WindUp || state == State.Charge ? attackFrames : moveFrames;
            if (frames.Length == 0) return;
            animationClock += Time.deltaTime * (frames == attackFrames ? attackFps : moveFps);
            renderer2D.sprite = frames[Mathf.FloorToInt(animationClock) % frames.Length];
        }
        private void FixedUpdate()
        {
            if (water.Count < 2 || state == State.Retreat) return;
            if (player == null || player.IsDead) player = FindPlayer();
            Vector2 p = body.position;
            UpdateState(p);
            float speed = state == State.Charge ? chargeSpeed : state == State.Alert ? alertSpeed : state == State.WindUp ? 0f : cruiseSpeed;
            p.x += direction * speed * Time.fixedDeltaTime;
            float min = water[0].TankMinimum.x + .6f, max = water[0].TankMaximum.x - .6f;
            if (p.x <= min) { p.x=min; SetDirection(1f); } else if (p.x >= max) { p.x=max; SetDirection(-1f); }
            float targetY = LaneY(p.x);
            p.y = Mathf.Lerp(p.y, targetY, 1f-Mathf.Exp(-verticalResponsiveness*Time.fixedDeltaTime));
            body.MovePosition(p);
            if (state == State.Charge && !hitApplied && player != null && Vector2.Distance(p, player.transform.position) <= contactRange)
            { hitApplied = player.TakeSharkHit(p); }
        }
        private void UpdateState(Vector2 p)
        {
            if (state == State.Alert && Time.time >= stateUntil) { state=State.WindUp; stateUntil=Time.time+windUpDuration; animationClock=0f; return; }
            if (state == State.WindUp)
            {
                FacePlayer(p);
                if (Time.time >= stateUntil) { state=State.Charge; stateUntil=Time.time+chargeDuration; hitApplied=false; animationClock=0f; }
                return;
            }
            if (state == State.Charge && Time.time >= stateUntil) { state=State.Recover; stateUntil=Time.time+recoveryDuration; nextAttackAt=Time.time+attackCooldown; return; }
            if (state == State.Recover) { if (Time.time >= stateUntil) state=State.Cruise; return; }
            if (player == null || Time.time < nextAttackAt) return;
            float d=Vector2.Distance(p,player.transform.position);
            if (d <= detectionRange) { state=State.Alert; stateUntil=Time.time+alertDuration; FacePlayer(p); }
        }
        private void FacePlayer(Vector2 p) { if(player==null)return; float dx=player.transform.position.x-p.x; if(Mathf.Abs(dx)>.08f) SetDirection(Mathf.Sign(dx)); }
        private void SetDirection(float d)
        {
            direction = Mathf.Sign(
                Mathf.Approximately(d, 0f)
                    ? 1f
                    : d);

            if (renderer2D != null)
                renderer2D.flipX = direction < 0f;
        }
        private TinyWaveSurfer FindPlayer() => FindObjectsByType<TinyWaveSurfer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(x=>x!=null&&!x.IsDead).OrderByDescending(x=>x.IsPlayerControlled).FirstOrDefault();
        private float LaneY(float x) => (water[lane].GetGameplaySurfaceHeight(x)+water[lane+1].GetGameplaySurfaceHeight(x))*.5f-Mathf.Abs(laneDepthBias);
        public void TakeThrownItemHit(Vector2 impact) { state=State.Recover; stateUntil=Time.time+recoveryDuration; nextAttackAt=Time.time+attackCooldown; SetDirection(Mathf.Sign(transform.position.x-impact.x)); }
        private static Sprite[] LoadOrdered(string path) => Resources.LoadAll<Sprite>(path).OrderBy(s=>s.name).ToArray();
    }
}
