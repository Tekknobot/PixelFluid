using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    /// <summary>
    /// Autonomous 8x8 surfer. It rides to one edge, performs a turn trick,
    /// reverses direction, and rides the same wave back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TinyWaveSurfer : MonoBehaviour
    {
        private enum RiderState
        {
            Riding,
            TurningTrick,
            SwitchingWave,
            Dead
        }

        [Header("Wave Selection")]
        [SerializeField, Min(1f)] private float secondsPerSimulation = 10f;
        [Tooltip("Random variation added to each surfer's layer-jump interval so they never transfer together.")]
        [SerializeField, Min(0f)] private float simulationTimeVariation = 3.5f;
        [SerializeField, Min(0.1f)] private float switchDuration = 0.9f;
        [SerializeField] private bool cycleContinuously = true;
        [Tooltip("Choose a different simulation layer instead of always moving to the next one.")]
        [SerializeField] private bool jumpToRandomWaveLayer = true;
        [Tooltip("Extra height used while jumping between simulation layers.")]
        [SerializeField, Range(0.1f, 3f)] private float layerJumpHeight = 0.55f;
        [SerializeField] private bool sortWavesBackToFront = true;
        [SerializeField, Min(0)] private int startingWaveIndex;

        [SerializeField, Range(0f, 2f)]
        private float glideWaveSwitchDistanceMultiplier = 0.5f;

        [Header("Back-and-Forth Ride")]
        [SerializeField, Min(0.1f)] private float horizontalRideSpeed = 1.2f;
        [SerializeField, Range(0.02f, 0.35f)] private float edgePadding = 0.12f;
        [SerializeField] private bool startMovingRight = true;
        [SerializeField, Min(0f)] private float surfaceOffset;
        [SerializeField, Range(1f, 30f)] private float surfaceFollow = 16f;
        [SerializeField, Range(0f, 2f)] private float waveVelocityInfluence = 0.22f;

        [Header("Edge Turn Trick")]
        [SerializeField, Range(0.1f, 2f)] private float turnJumpHeight = 0.26f;
        [SerializeField, Range(0.2f, 1.5f)] private float turnTrickDuration = 0.38f;
        [SerializeField, Range(90f, 1080f)] private float turnSpinDegrees = 360f;
        [SerializeField, Range(0f, 1f)] private float flipChance = 0.45f;

        [Header("Player Control")]
        [SerializeField] private bool playerControlled;
        [SerializeField] private bool aiControlled;
        [SerializeField, Min(0.25f)] private float playerScrollSpeed = 2.4f;
        [SerializeField, Range(1f, 4f)] private float playerBoostMultiplier = 1.75f;
        [Tooltip("World-space padding that keeps the surfer clear of the camera's left and right screen edges.")]
        [SerializeField, Range(0f, 1f)] private float playerCameraEdgePadding = 0.12f;
        [SerializeField] private bool lockPlayerToScreenX = false;
        [SerializeField] private float playerScreenX = 0f;

        [Header("Player Movement Feel")]
        [Tooltip("How quickly movement builds toward full speed.")]
        [SerializeField, Min(0.1f)] private float playerAcceleration = 7.5f;
        [Tooltip("How quickly the surfer slows when movement is released.")]
        [SerializeField, Min(0.1f)] private float playerDeceleration = 10f;
        [Tooltip("Analog-stick dead zone.")]
        [SerializeField, Range(0f, 0.5f)] private float gamepadDeadZone = 0.18f;
        [Tooltip("Allows horizontal steering while airborne.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.45f;

        [Header("Charged Water Skid")]
        [Tooltip("Keyboard E / controller east face button (Xbox B). Hold to charge, release to skid.")]
        [SerializeField] private bool enableChargedWaterSkid = true;
        [SerializeField, Min(0.1f)] private float maximumSkidChargeTime = 1.5f;
        [SerializeField, Min(0.05f)] private float minimumSkidChargeTime = 0.12f;
        [SerializeField, Min(0.1f)] private float minimumSkidDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float maximumSkidDuration = 3.35f;
        [SerializeField, Min(0.1f)] private float minimumSkidSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float maximumSkidSpeed = 9f;
        [SerializeField, Range(0f, 0.2f)] private float chargeShakeAmount = 0.035f;
        [SerializeField, Range(1f, 80f)] private float chargeShakeFrequency = 32f;
        [SerializeField, Range(0f, 45f)] private float skidLeanDegrees = 18f;

        [Header("Player Air Tricks")]
        [Tooltip("Maximum extra rotation controlled with the right stick while airborne.")]
        [SerializeField, Range(0f, 1080f)] private float playerAirTrickDegrees = 540f;
        [Tooltip("Smooths the jump arc at takeoff and landing.")]
        [SerializeField, Range(0.1f, 3f)] private float playerJumpArcPower = 1.35f;

        [Header("Aerial Trick Chain")]
        [Tooltip("Allows the three unique air tricks to be chained during one forward surf jump.")]
        [SerializeField] private bool enableAerialTrickChain = true;
        [Tooltip("Maximum number of unique tricks in one jump. There are currently three trick animations.")]
        [SerializeField, Range(1, 3)] private int maximumTricksPerChain = 3;
        [Tooltip("Extra airtime granted by each newly chained trick. Later tricks receive diminishing benefit.")]
        [SerializeField, Range(0f, 0.5f)] private float trickChainAirtimeBonus = 0.24f;
        [Tooltip("Legacy value retained for existing Inspector data. Chained tricks no longer add horizontal movement.")]
        [SerializeField, Range(0f, 1.5f)] private float trickChainDistanceBonus = 0f;
        [Tooltip("Legacy timing value retained for existing Inspector data. Trick transitions now wait for the complete clip.")]
        [SerializeField, Range(0.1f, 1f)] private float trickChainInputLock = 0.34f;
        [Tooltip("Legacy landing-window value retained for existing Inspector data.")]
        [SerializeField, Range(0.5f, 0.95f)] private float latestTrickChainTime = 0.78f;
        [Tooltip("Second jump reaches roughly half the height of the initial jump. Velocity uses sqrt(0.5) because jump height scales with velocity squared.")]
        [SerializeField, Range(0.25f, 1.5f)] private float secondTrickJumpStrength = 0.7071f;
        [Tooltip("Third jump reaches roughly half the height of the second jump, or one quarter of the initial jump.")]
        [SerializeField, Range(0.25f, 1.5f)] private float thirdTrickJumpStrength = 0.5f;
        [Tooltip("Legacy value retained for existing Inspector data. Only the initial jump establishes horizontal momentum.")]
        [SerializeField, Range(0f, 4f)] private float chainedTrickForwardBoost = 0f;
        [Tooltip("Gravity applied while a trick animation is playing. Lower values soften descent without creating a mid-air freeze.")]
        [SerializeField, Range(0.1f, 1f)] private float activeTrickGravityMultiplier = 0.48f;

        [Header("Forward Obstacle Surf Jump")]
        [Tooltip("Hold left/right and press Jump (controller A / keyboard Space) to launch forward over hazards.")]
        [SerializeField] private bool enableForwardObstacleJump = true;
        [SerializeField, Min(0.15f)] private float obstacleJumpDuration = 1.62f;
        [Tooltip("Minimum height produced by a quick tap of Jump.")]
        [SerializeField, Min(0.05f)] private float minimumObstacleJumpHeight = 0.42f;
        [Tooltip("Maximum height produced by a fully charged Jump hold.")]
        [SerializeField, Min(0.05f)] private float maximumObstacleJumpHeight = 1.3f;
        [Tooltip("Seconds of holding Jump required to reach maximum height.")]
        [SerializeField, Min(0.05f)] private float fullJumpChargeTime = 0.65f;
        [Tooltip("Shapes charge sensitivity. X is held-time percentage; Y is jump-power percentage.")]
        [SerializeField] private AnimationCurve jumpChargeCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.35f, 0.16f),
            new Keyframe(0.7f, 0.58f),
            new Keyframe(1f, 1f));
        [SerializeField, Min(0.1f)] private float obstacleJumpDistance = 2.15f;
        [Tooltip("Minimum directional input needed to choose the forward obstacle jump.")]
        [SerializeField, Range(0.05f, 1f)] private float obstacleJumpInputThreshold = 0.25f;
        [Tooltip("How strongly horizontal travel eases into the landing.")]
        [SerializeField, Range(0.5f, 4f)] private float obstacleLandingEase = 2.2f;
        [Tooltip("Portion of the jump where jellyfish contact is safely cleared.")]
        [SerializeField, Range(0.1f, 0.9f)] private float obstacleClearanceStart = 0.16f;
        [SerializeField, Range(0.1f, 0.95f)] private float obstacleClearanceEnd = 0.84f;

        [Header("Random Initial Ocean Spawn")]
        [Tooltip("When enabled, the player surfer starts on a random horizontal section and random wave layer after the endless ocean has finished building.")]
        [SerializeField] private bool randomizeInitialOceanSpawn;
        [Tooltip("Keeps the initial position away from the far outside edges of the three-section ocean.")]
        [SerializeField, Range(0f, 0.45f)] private float randomSpawnEdgePadding = 0.08f;
        [Tooltip("Minimum world-space clearance from sharks and giant squids when choosing the initial player spawn.")]
        [SerializeField, Min(0f)] private float enemySafeSpawnRadius = 3.5f;
        [Tooltip("How many random ocean positions are tested before using the safest position found.")]
        [SerializeField, Range(1, 100)] private int safeSpawnAttempts = 40;

        [Header("Shark Death Response")]
        [SerializeField, Min(0.25f)] private float deathDuration = 1.6f;
        [SerializeField, Min(0f)] private float deathKnockUp = 0.7f;
        [SerializeField, Min(0f)] private float deathSinkSpeed = 0.65f;
        [SerializeField, Range(90f, 1080f)] private float deathSpinSpeed = 520f;
        [SerializeField] private bool respawnAfterDeath = true;
        [SerializeField, Min(0f)] private float respawnDelay = 0.7f;

        [Header("Death Blood Particles")]
        [SerializeField] private bool emitBloodOnDeath = true;
        [SerializeField, Range(4, 80)] private int deathBloodParticleCount = 32;
        [SerializeField, Min(0.05f)] private float deathBloodLifetime = 2.35f;
        [SerializeField, Min(0f)] private float deathBloodMinSpeed = 0.12f;
        [SerializeField, Min(0f)] private float deathBloodMaxSpeed = 0.48f;
        [SerializeField, Range(0.005f, 0.2f)] private float deathBloodMinSize = 0.020f;
        [SerializeField, Range(0.005f, 0.25f)] private float deathBloodMaxSize = 0.020f;
        [SerializeField] private float deathBloodGravity = 0.01f;
        [SerializeField] private Vector2 deathBloodOffset = new(0f, 0.08f);
        [SerializeField, ColorUsage(true, true)] private Color deathBloodColor = new(0.9f, 0f, 0f, 0.7f);

        [Header("Health and Hit Reaction")]
        [SerializeField, Min(1)] private int maximumHealth = 3;
        [SerializeField, Min(1)] private int sharkHitDamage = 1;
        [SerializeField, Min(0f)] private float hitInvulnerability = 0.8f;
        [SerializeField, Min(0.02f)] private float hitFlashDuration = 0.34f;
        [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.055f;
        [SerializeField, Min(0f)] private float hitBumpDistance = 0.24f;
        [SerializeField, Min(0f)] private float hitBumpHeight = 0.10f;
        [SerializeField] private Color hitFlashColor = new(1f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color heartPickupFlashColor = new(0.45f, 1f, 0.58f, 1f);
        [SerializeField, Min(0.05f)] private float heartPickupReactionDuration = 0.42f;

        [Header("Surfer Sprite")]
        [SerializeField] private string surferSpriteResource = "Surfers/chuck";
        [SerializeField, Min(0.05f)] private float spriteWorldScale = 0.65f;
        [SerializeField] private int sortingOrder = 1;
        [Tooltip("Local SpriteRenderer order inside the surfer render queue. Kept below lane creatures so a shark in front can cover this surfer.")]
        [SerializeField] private int surferWithinWaveSortingOrder = 0;

        [Header("Long Idle Animation")]
        [SerializeField] private bool playProneAfterLongIdle = true;
        [SerializeField, Min(0.5f)] private float proneIdleDelay = 7f;

        [Header("Speech Bubbles")]
        [SerializeField] private bool enableSpeechBubbles = false;
        [SerializeField, Min(0.5f)] private float idleSpeechDelay = 4.5f;
        [SerializeField, Min(1f)] private float idleSpeechCooldown = 10f;
        [SerializeField, Min(0.25f)] private float sharkSpeechRange = 2.25f;
        [SerializeField, Min(0.1f)] private float sharkCheckInterval = 0.2f;
        [SerializeField, Min(1f)] private float sharkSpeechCooldown = 6f;
        [SerializeField] private string[] idleSpeechLines =
        {
            "JUST BREATHE.",
            "THE OCEAN REMEMBERS.",
            "QUIET OUT HERE.",
            "ONE MORE WAVE.",
            "THE TIDE IS CHANGING.",
            "KEEP PADDLING.",
            "JUST RIDE.",
            "DON'T THINK.",
            "THIS FEELS DIFFERENT.",
            "I'VE BEEN HERE BEFORE.",
            "WHERE DID EVERYONE GO?",
            "THE SEA NEVER SLEEPS.",
            "WAIT FOR THE SET.",
            "THE WIND JUST TURNED.",
            "TOO QUIET.",
            "STAY LOOSE.",
            "FIND THE LINE.",
            "LET IT CARRY ME.",
            "NOT A BAD PLACE TO BE.",
            "THE HORIZON KEEPS MOVING.",
            "I COULD STAY OUT HERE.",
            "SOMETHING'S WATCHING.",
            "THE WATER FEELS COLD.",
            "NO RUSH.",
            "LISTEN TO THE WATER.",
            "HERE COMES ANOTHER ONE.",
            "KEEP YOUR BALANCE.",
            "DON'T LOOK DOWN.",
            "THE CURRENT IS STRONG.",
            "MAYBE THIS IS ENOUGH.",
            "I NEEDED THIS.",
            "THE SHORE FEELS FAR AWAY.",
            "JUST ME AND THE SWELL.",
            "THE SKY LOOKS ENDLESS.",
            "RIDE IT CLEAN.",
            "WAIT... NOW.",
            "EVERY WAVE IS DIFFERENT.",
            "STILL HERE.",
            "KEEP MOVING FORWARD.",
            "THE NEXT ONE'S MINE."
        };
        [SerializeField] private string[] sharkSpeechLines =
        {
            "THAT FIN IS CLOSE.",
            "NOT ALONE OUT HERE.",
            "EASY... EASY...",
            "I SHOULD MOVE.",
            "NOT NOW.",
            "I SAW THAT.",
            "TOO CLOSE.",
            "STAY CALM.",
            "DON'T PANIC.",
            "KEEP MOVING.",
            "PLEASE LEAVE.",
            "I'M NOT FOOD.",
            "JUST PASS BY...",
            "NO SUDDEN MOVES.",
            "THAT'S A BIG ONE.",
            "WRONG WAVE, FRIEND.",
            "KEEP YOUR DISTANCE.",
            "DON'T TURN TOWARD ME.",
            "I KNOW YOU'RE THERE.",
            "THIS IS YOUR OCEAN.",
            "WE CAN SHARE, RIGHT?",
            "JUST KEEP SWIMMING.",
            "WHY IS IT CIRCLING?",
            "I DON'T LIKE THIS.",
            "STAY ON THE BOARD.",
            "DON'T FALL NOW.",
            "MOVE. MOVE. MOVE.",
            "THE SHORE IS THAT WAY.",
            "YOU DIDN'T SEE ME.",
            "PLEASE BE A DOLPHIN."
        };

        [Header("Single-Frame Water Motion")]
        [SerializeField, Range(0f, 0.12f)] private float idleBobHeight = 0.018f;
        [SerializeField, Range(0.1f, 8f)] private float idleBobFrequency = 2.4f;
        [SerializeField, Range(0f, 12f)] private float directionalLean = 3.5f;
        [SerializeField, Range(0f, 0.12f)] private float stanceSquash = 0.035f;
        [SerializeField] private Color bodyColor = new(0.12f, 0.08f, 0.06f, 1f);
        [SerializeField] private Color shirtColor = new(0.95f, 0.32f, 0.12f, 1f);
        [SerializeField] private Color boardColor = new(1f, 0.88f, 0.24f, 1f);

        private Animator surferAnimator;

        private static readonly int IdleStateHash =
            Animator.StringToHash("Idle");
        private static readonly int MoveStateHash =
            Animator.StringToHash("chuck_move");
        private static readonly int SurfJumpStateHash =
            Animator.StringToHash("chuck_jump");
        private static readonly int WaveSwitchStateHash =
            Animator.StringToHash("chuck_wave_switch");
        private static readonly int PushStateHash =
            Animator.StringToHash("chuck_surf_jump");            
        private static readonly int HandstandStateHash =
            Animator.StringToHash("chuck_handstand");
        private static readonly int FlipStateHash =
            Animator.StringToHash("chuck_flip");
        private static readonly int RotationStateHash =
            Animator.StringToHash("chuck_rotation");
        private static readonly int DeathStateHash =
            Animator.StringToHash("chuck_death");
        private static readonly int ProneStateHash =
            Animator.StringToHash("chuck_prone");

        private int currentAnimationStateHash;
        private float playerIdleTimer;
        private SurferSpeechBubble speechBubble;
        private float nextIdleSpeechTime;
        private float nextSharkSpeechTime;
        private float nextSharkCheckTime;
        private bool sharkWasNearby;

        private readonly List<PixelWaterGPU> simulations = new();
        private PixelWaterGPU currentWave;
        private SpriteRenderer spriteRenderer;
        private Sprite runtimeSprite;
        private Texture2D runtimeTexture;
        private Material originalSpriteMaterial;
        private Material surferWaveMaterial;
        private ParticleSystem deathBloodParticles;
        private Material deathBloodMaterial;
        private AudioSource deathAudioSource;
        [Header("Reaction Audio")]
        [SerializeField] private AudioClip humanDeathClip;
        [SerializeField] private AudioClip maleHurtClip;
        [SerializeField] private AudioClip healthUpClip;
        [SerializeField, Range(0f, 1f)] private float hurtSoundVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float healthUpSoundVolume = 1f;
        private int lastWaveRenderQueue = -1;

        private RiderState state;
        private int waveIndex;
        private float waveTimer;
        private float currentSimulationDuration;
        private float stateTimer;
        private float direction = 1f;
        private float localRideX;
        private float airStartY;
        private float renderDepth;
        private bool flipTrick;
        private Vector3 switchStart;
        private Vector3 switchTarget;
        private bool previousJumpHeld;
        private bool previousLayerUpHeld;
        private bool previousLayerDownHeld;
        private bool layerSwitchInputLocked;
        private float playerHorizontalVelocity;
        private float playerTrickInput;
        private float aiDecisionTimer;
        private float aiJumpPulse;
        private float aiAttackPulse;
        private float aiSpecialHold;
        private float aiHorizontal = 1f;
        private int aiLayerDirection;
        private float aiTrick;
        private float deathTimer;
        private float respawnTimer;
        private bool managedDeathReported;
        private Vector2 deathVelocity;
        private Vector3 livingScale;
        private Color livingColor = Color.white;
        private int currentHealth;
        private float invulnerableUntil;
        private float spriteReactionTimer;
        private float spriteReactionDuration;
        private float spriteFlashInterval;
        private Color spriteReactionColor = Color.white;
        private SurferHealthBar healthBar;
        private readonly Queue<Sprite> throwableItems = new Queue<Sprite>();
        private bool previousAttackHeld;
        private bool previousSpecialHeld;
        private bool specialCharging;
        private float specialChargeTime;
        private bool specialSkidding;
        private float specialSkidTimer;
        private float specialSkidDuration;
        private float specialSkidSpeed;
        private float specialSkidCurrentSpeed;
        private bool glideWaveSwitchActive;
        private float glideWaveSwitchSpeed;
        private bool obstacleJumpActive;
        private bool airTrickActive;
        private float airTrickTimer;
        private int currentAirTrickStateHash;
        private int queuedAirTrickStateHash;
        [SerializeField, Min(0.1f)] private float airTrickDuration = 1.25f;
        private int aerialTrickChainCount;
        private float aerialTrickAirtimeBonus;
        private float obstacleAirVerticalVelocity;
        private float obstacleAirHorizontalVelocity;
        private float obstacleAirGravity;
        private float obstacleAirTakeoffVelocity;
        private float obstacleAirElapsed;
        private float obstacleJumpStartX;
        private float obstacleJumpTargetX;
        private float scoredJumpStartX;
        private float obstacleJumpProgress;
        private float activeObstacleJumpHeight;
        private bool jumpCharging;
        private float jumpChargeTime;
        private float jumpChargeHorizontalInput;
        private float scoredJumpPeakY;
        private bool scoredHandstand;
        private bool scoredRotation;
        private bool scoredFlip;

        public int CurrentWaveIndex => waveIndex;
        public PixelWaterGPU CurrentWave => currentWave;
        public float TravelDirection => direction;
        public bool IsDead => state == RiderState.Dead;
        public bool IsPlayerControlled => playerControlled && !aiControlled;
        public bool IsAIControlled => aiControlled;
        public int CurrentHealth => currentHealth;
        public int MaximumHealth => Mathf.Max(1, maximumHealth);
        public bool IsSwitchingWave => state == RiderState.SwitchingWave;
        public bool IsObstacleJumping => obstacleJumpActive && state == RiderState.TurningTrick;
        public bool HasObstacleClearance => IsObstacleJumping &&
            obstacleJumpProgress >= obstacleClearanceStart &&
            obstacleJumpProgress <= obstacleClearanceEnd;
        // Kept for compatibility with any UI or scripts that previously displayed cans.
        // It now represents every collected throwable ocean item.
        public int SodaCanCount => throwableItems.Count;
        public int ThrowableItemCount => throwableItems.Count;

        /// <summary>
        /// Returns the collected throwable sprites in pickup order for lightweight HUDs.
        /// A copy is returned so UI code cannot modify gameplay inventory.
        /// </summary>
        public Sprite[] GetThrowableInventorySnapshot()
        {
            return throwableItems.ToArray();
        }

        [Tooltip("Enable this when the original sprite artwork faces right.")]
        [SerializeField] private bool spriteFacesRight = true;

        private void ApplyFacing(float xScale, float yScale)
        {
            bool movingRight = direction > 0f;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = spriteFacesRight
                    ? !movingRight
                    : movingRight;
            }

            transform.localScale = new Vector3(
                Mathf.Abs(xScale),
                Mathf.Abs(yScale),
                1f);
        }

        private void Awake()
        {
            deathAudioSource = GetComponent<AudioSource>();
            if (deathAudioSource == null)
                deathAudioSource = gameObject.AddComponent<AudioSource>();

            deathAudioSource.playOnAwake = false;
            deathAudioSource.loop = false;
            deathAudioSource.spatialBlend = 0f;

            if (humanDeathClip == null)
                humanDeathClip = Resources.Load<AudioClip>("Audio/SFX/human_death");
            if (maleHurtClip == null)
                maleHurtClip = Resources.Load<AudioClip>("Audio/SFX/male_hurt");
            if (healthUpClip == null)
                healthUpClip = Resources.Load<AudioClip>("Audio/SFX/health_up");
            EnsurePixelSprite();
            EnsureSurferAnimator();
            EnsureSharkHitCollider();
            EnsureSpeechBubble();
            EnsureHealthBar();
            if (FindFirstObjectByType<SodaCanSpawner>() == null) new GameObject("Soda Can Spawner").AddComponent<SodaCanSpawner>();
            currentHealth = Mathf.Max(1, maximumHealth);
            healthBar?.SetHealth(currentHealth, MaximumHealth);

            livingScale = transform.localScale;

            if (spriteRenderer != null)
                livingColor = spriteRenderer.color;

            RefreshWaveList();

            direction =
                startMovingRight ? 1f : -1f;

            PickWave(startingWaveIndex, true);

            ApplyCurrentWaveSorting(true);

            ScheduleNextLayerJump(0f);
        }

        private IEnumerator Start()
        {
            if (!randomizeInitialOceanSpawn)
                yield break;

            // EndlessWaveSections builds after the independent vertical wave layers,
            // so wait until all three horizontal ocean sections are available.
            float timeout = 5f;
            while (timeout > 0f)
            {
                EndlessWaveSections endless = EndlessWaveSections.Instance;
                if (endless != null && endless.IsReady)
                    break;

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            // Population spawners also wait for the ocean. Give them two frames to
            // create sharks and squids before selecting a safe player position.
            yield return null;
            yield return null;

            SpawnAtRandomOceanPosition();
        }

        private void LateUpdate()
        {
            UpdateSpriteReaction(Time.deltaTime);

            if (speechBubble != null)
                speechBubble.RefreshSorting();

            // Re-apply after movement/animation so changing wave layers immediately
            // changes the surfer's transparent queue as well.
            ApplyCurrentWaveSorting();
        }

        private void OnDestroy()
        {
            if (spriteRenderer != null && originalSpriteMaterial != null)
                spriteRenderer.sharedMaterial = originalSpriteMaterial;

            if (surferWaveMaterial != null)
                Destroy(surferWaveMaterial);

            if (deathBloodMaterial != null)
                Destroy(deathBloodMaterial);

            if (runtimeSprite != null) Destroy(runtimeSprite);
            if (runtimeTexture != null) Destroy(runtimeTexture);
        }

        private void ApplyCurrentWaveSorting(bool force = false)
        {
            if (spriteRenderer == null)
                return;

            if (currentWave == null)
            {
                if (simulations.Count == 0)
                    RefreshWaveList();

                if (simulations.Count == 0)
                    return;

                waveIndex = Mathf.Clamp(
                    waveIndex,
                    0,
                    simulations.Count - 1);

                currentWave = simulations[waveIndex];
            }

            // Queue layout uses four slots per wave depth:
            // water = N, surfer = N + 1, shark lane = N + 2,
            // next foreground water = N + 4.
            // This guarantees a surfer is above its own water, behind a shark
            // in the lane immediately in front, and in front of a shark in the
            // lane immediately behind.
            int waterQueue =
                currentWave.GetWaveLayerRenderQueue();

            int surferQueue = Mathf.Clamp(
                waterQueue + 1,
                2501,
                4999);

            if (surferWaveMaterial == null)
            {
                originalSpriteMaterial =
                    spriteRenderer.sharedMaterial;

                Material source = originalSpriteMaterial;

                if (source == null)
                {
                    Shader spriteShader =
                        Shader.Find("Sprites/Default");

                    if (spriteShader == null)
                    {
                        Debug.LogWarning(
                            "Sprites/Default shader could not be found.",
                            this);

                        return;
                    }

                    source = new Material(spriteShader);
                }

                surferWaveMaterial = new Material(source)
                {
                    name = $"{name} Dynamic Wave Sorting",
                    hideFlags = HideFlags.HideAndDontSave
                };

                spriteRenderer.sharedMaterial =
                    surferWaveMaterial;

                force = true;
            }

            if (force ||
                surferQueue != lastWaveRenderQueue)
            {
                surferWaveMaterial.renderQueue =
                    surferQueue;

                lastWaveRenderQueue =
                    surferQueue;
            }

            spriteRenderer.sortingOrder =
                surferWithinWaveSortingOrder;
        }

        private void EnsureSharkHitCollider()
        {
            Collider2D collider = GetComponent<Collider2D>();
            if (collider == null)
            {
                CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = 0.28f;
                collider = circle;
            }

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }


        private void EnsureHealthBar()
        {
            healthBar = GetComponent<SurferHealthBar>();
            if (healthBar == null)
                healthBar = gameObject.AddComponent<SurferHealthBar>();
        }

        public bool TakeSharkHit(Vector2 sharkPosition)
        {
            if (state == RiderState.Dead || Time.time < invulnerableUntil)
                return false;

            invulnerableUntil = Time.time + hitInvulnerability;
            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, sharkHitDamage));
            healthBar?.SetHealth(currentHealth, MaximumHealth);

            float away = transform.position.x >= sharkPosition.x ? 1f : -1f;
            transform.position += new Vector3(away * hitBumpDistance, hitBumpHeight, 0f);
            localRideX = transform.position.x;
            // A strong red-only flash clearly communicates damage without washing the sprite white.
            BeginSpriteReaction(hitFlashColor, hitFlashDuration, hitFlashInterval);
            if (maleHurtClip != null && deathAudioSource != null)
                deathAudioSource.PlayOneShot(maleHurtClip, hurtSoundVolume);
            if (speechBubble != null) speechBubble.HideImmediate();

            if (currentHealth <= 0)
                return DieFromShark(sharkPosition);

            return true;
        }


        /// <summary>
        /// Plays one beat of the giant squid's combo. Every beat produces its own
        /// bump, red flash and hurt sound, but only the beat with applyDamage=true
        /// removes health. This intentionally bypasses the normal shark-hit
        /// invulnerability gate for the reaction only.
        /// </summary>
        public bool TakeSquidComboBeat(Vector2 squidPosition, bool applyDamage)
        {
            if (state == RiderState.Dead)
                return false;

            if (applyDamage)
            {
                currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, sharkHitDamage));
                healthBar?.SetHealth(currentHealth, MaximumHealth);

                // Preserve normal protection from unrelated hazards after the
                // combo begins, without suppressing the squid's later visual beats.
                invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + hitInvulnerability);
            }

            float away = transform.position.x >= squidPosition.x ? 1f : -1f;
            float beatBumpX = hitBumpDistance * (applyDamage ? 0.72f : 0.34f);
            float beatBumpY = hitBumpHeight * (applyDamage ? 0.72f : 0.38f);
            transform.position += new Vector3(away * beatBumpX, beatBumpY, 0f);
            localRideX = transform.position.x;

            // Restart a short reaction on every combo beat instead of waiting for
            // the normal hurt cooldown or the previous flash to finish.
            BeginSpriteReaction(hitFlashColor, 0.14f, 0.035f);
            if (maleHurtClip != null && deathAudioSource != null)
                deathAudioSource.PlayOneShot(maleHurtClip, hurtSoundVolume);
            if (speechBubble != null)
                speechBubble.HideImmediate();

            if (applyDamage && currentHealth <= 0)
                return DieFromShark(squidPosition);

            return true;
        }


        public bool CollectSodaCan()
        {
            Sprite canSprite = Resources.Load<Sprite>("Items/soda_can");
            return CollectThrowableItem(canSprite);
        }

        /// <summary>
        /// Adds any collected ocean sprite to the shared throwable inventory.
        /// Each pickup retains its own artwork and is thrown later in pickup order.
        /// </summary>
        public bool CollectThrowableItem(Sprite itemSprite)
        {
            if (IsDead || itemSprite == null)
                return false;

            throwableItems.Enqueue(itemSprite);
            BeginSpriteReaction(new Color(0.35f, 0.85f, 1f, 1f), 0.35f, 0.06f);
            transform.localScale = livingScale * 1.14f;
            return true;
        }

        private void ThrowSodaCan(bool aimAtUfo)
        {
            if (throwableItems.Count <= 0 || IsDead || state != RiderState.Riding) return;

            Transform nearest = null;
            float best = float.MaxValue;

            // Holding Up while pressing Action reserves the throw for the UFO.
            // This prevents a nearby shark from stealing the upward shot.
            if (aimAtUfo)
            {
                DayTwoHelicopterMissile missile = FindFirstObjectByType<DayTwoHelicopterMissile>();
                DayTwoHelicopterController helicopter = FindFirstObjectByType<DayTwoHelicopterController>();
                AlienUfoController ufo = FindFirstObjectByType<AlienUfoController>();

                // Incoming missiles have priority, then the helicopter before it
                // fires, then the Day 1 UFO. This keeps Up + Action as the dedicated
                // sky-defense throw without allowing sea creatures to steal it.
                if (missile != null && missile.CanBeHit)
                    nearest = missile.transform;
                else if (helicopter != null && helicopter.CanBeHit)
                    nearest = helicopter.transform;
                else if (ufo != null && ufo.CanBeHit)
                    nearest = ufo.transform;
                else
                    return; // Do not spend an item when no sky target can be hit.
            }
            else
            {
                // Exploding ducklings are immediate threats and can be intercepted.
                foreach (RubberDucklingSwimmer duckling in FindObjectsByType<RubberDucklingSwimmer>(FindObjectsSortMode.None))
                {
                    if (duckling == null || !duckling.isActiveAndEnabled || !duckling.CanBeHit) continue;
                    float d = Vector2.Distance(transform.position, duckling.transform.position);
                    if (d < best) { best = d; nearest = duckling.transform; }
                }

                // When no duckling is incoming, prioritize the active Day 2 boss.
                if (nearest == null)
                {
                    foreach (RubberDuckBossSwimmer boss in FindObjectsByType<RubberDuckBossSwimmer>(FindObjectsSortMode.None))
                    {
                        if (boss == null || !boss.isActiveAndEnabled || boss.IsDefeated) continue;
                        float d = Vector2.Distance(transform.position, boss.transform.position);
                        if (d < best) { best = d; nearest = boss.transform; }
                    }
                }

                // Active bosses own the combat throw target over ordinary hazards.
                if (nearest == null)
                foreach (GodzillaLaneSwimmer boss in FindObjectsByType<GodzillaLaneSwimmer>(FindObjectsSortMode.None))
                {
                    if (boss == null || !boss.isActiveAndEnabled || boss.IsDefeated)
                        continue;

                    float d = Vector2.Distance(transform.position, boss.transform.position);
                    if (d < best)
                    {
                        best = d;
                        nearest = boss.transform;
                    }
                }

                // Only use ordinary sea hazards when no active boss exists.
                if (nearest == null)
                {
                    foreach (SharkLaneSwimmer shark in FindObjectsByType<SharkLaneSwimmer>(FindObjectsSortMode.None))
                    {
                        if (shark == null) continue;
                        float d = Vector2.Distance(transform.position, shark.transform.position);
                        if (d < best) { best = d; nearest = shark.transform; }
                    }

                    foreach (GiantSquidLaneSwimmer squid in FindObjectsByType<GiantSquidLaneSwimmer>(FindObjectsSortMode.None))
                    {
                        if (squid == null) continue;
                        float d = Vector2.Distance(transform.position, squid.transform.position);
                        if (d < best) { best = d; nearest = squid.transform; }
                    }

                    foreach (JellyfishSwimmer jellyfish in FindObjectsByType<JellyfishSwimmer>(FindObjectsSortMode.None))
                    {
                        if (jellyfish == null) continue;
                        float d = Vector2.Distance(transform.position, jellyfish.transform.position);
                        if (d < best) { best = d; nearest = jellyfish.transform; }
                    }

                    foreach (BloodfishSwimmer bloodfish in FindObjectsByType<BloodfishSwimmer>(FindObjectsSortMode.None))
                    {
                        if (bloodfish == null) continue;
                        float d = Vector2.Distance(transform.position, bloodfish.transform.position);
                        if (d < best) { best = d; nearest = bloodfish.transform; }
                    }

                    foreach (StingrayLaneSwimmer stingray in FindObjectsByType<StingrayLaneSwimmer>(FindObjectsSortMode.None))
                    {
                        if (stingray == null) continue;
                        float d = Vector2.Distance(transform.position, stingray.transform.position);
                        if (d < best) { best = d; nearest = stingray.transform; }
                    }
                }
            }

            Sprite sprite = throwableItems.Dequeue();

            GameObject projectile = new GameObject(aimAtUfo ? "UFO Thrown Item Shot" : $"Thrown Ocean Item - {sprite.name}");
            projectile.AddComponent<SpriteRenderer>().sortingOrder = sortingOrder + 20;
            projectile.AddComponent<CircleCollider2D>();
            projectile.AddComponent<Rigidbody2D>();
            SodaCanProjectile can = projectile.AddComponent<SodaCanProjectile>();
            can.Launch(
                (Vector2)transform.position + new Vector2(direction * .22f, .22f),
                nearest,
                sprite,
                direction,
                aimAtUfo);
        }

        private void PerformAction(bool aimAtUfo)
        {
            // Action alone throws the next stored ocean item toward sea hazards.
            // Up + Action fires it into the sky at the UFO. Ocean props collect automatically.
            ThrowSodaCan(aimAtUfo);
        }

        private static bool ReadAttackInput()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboard = Keyboard.current != null &&
                (Keyboard.current.fKey.isPressed || Keyboard.current.xKey.isPressed);
            bool gamepad = Gamepad.current != null &&
                Gamepad.current.buttonWest.isPressed;
            if (keyboard || gamepad)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.X);
#else
            return false;
#endif
        }

        public bool HealFromHeart(int amount = 1)
        {
            if (state == RiderState.Dead || currentHealth >= MaximumHealth)
                return false;

            currentHealth = Mathf.Min(MaximumHealth, currentHealth + Mathf.Max(1, amount));
            healthBar?.SetHealth(currentHealth, MaximumHealth);
            BeginSpriteReaction(heartPickupFlashColor, heartPickupReactionDuration, 0.07f);
            if (healthUpClip != null && deathAudioSource != null)
                deathAudioSource.PlayOneShot(healthUpClip, healthUpSoundVolume);
            transform.localScale = livingScale * 1.16f;
            return true;
        }

        private void BeginSpriteReaction(Color colour, float duration, float interval)
        {
            spriteReactionColor = colour;
            spriteReactionDuration = Mathf.Max(0.02f, duration);
            spriteReactionTimer = spriteReactionDuration;
            spriteFlashInterval = Mathf.Max(0.02f, interval);
        }

        private void UpdateSpriteReaction(float dt)
        {
            if (spriteRenderer == null || state == RiderState.Dead)
                return;

            if (spriteReactionTimer <= 0f)
            {
                spriteRenderer.color = livingColor;
                transform.localScale = Vector3.Lerp(transform.localScale, livingScale, 1f - Mathf.Exp(-18f * dt));
                return;
            }

            spriteReactionTimer = Mathf.Max(0f, spriteReactionTimer - dt);
            bool flashOn = Mathf.FloorToInt(spriteReactionTimer / spriteFlashInterval) % 2 == 0;
            spriteRenderer.color = flashOn ? spriteReactionColor : livingColor;
            float t = 1f - spriteReactionTimer / spriteReactionDuration;
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.16f;
            transform.localScale = livingScale * pulse;
        }

        private void EnsureSpeechBubble()
        {
            if (!enableSpeechBubbles) return;
            speechBubble = GetComponent<SurferSpeechBubble>();
            if (speechBubble == null)
                speechBubble = gameObject.AddComponent<SurferSpeechBubble>();
            nextIdleSpeechTime = Time.time + idleSpeechDelay;
        }

        private void UpdateSpeechBubbles()
        {
            if (!enableSpeechBubbles || speechBubble == null)
                return;

            // Dialogue is only allowed while the surfer is steadily riding.
            // Hide any active bubble immediately during tricks/jumps, wave
            // crossings, or death so it never follows the surfer through the air.
            if (state != RiderState.Riding)
            {
                speechBubble.HideImmediate();
                return;
            }

            if (Time.time >= nextSharkCheckTime)
            {
                nextSharkCheckTime = Time.time + sharkCheckInterval;
                bool sharkNearby = false;
                float closestDistance = sharkSpeechRange;
                foreach (SharkLaneSwimmer shark in FindObjectsByType<SharkLaneSwimmer>(FindObjectsSortMode.None))
                {
                    if (shark == null || !shark.isActiveAndEnabled) continue;
                    float distance = Vector2.Distance(transform.position, shark.transform.position);
                    if (distance <= closestDistance)
                    {
                        closestDistance = distance;
                        sharkNearby = true;
                    }
                }

                if (sharkNearby && (!sharkWasNearby || Time.time >= nextSharkSpeechTime))
                {
                    ShowRandomSpeech(sharkSpeechLines, 2.2f);
                    nextSharkSpeechTime = Time.time + sharkSpeechCooldown;
                    nextIdleSpeechTime = Time.time + idleSpeechCooldown;
                }
                sharkWasNearby = sharkNearby;
            }

            if (playerControlled && playerIdleTimer >= idleSpeechDelay &&
                Time.time >= nextIdleSpeechTime && !sharkWasNearby)
            {
                ShowRandomSpeech(idleSpeechLines, 2.8f);
                nextIdleSpeechTime = Time.time + idleSpeechCooldown;
            }
        }

        private void ShowRandomSpeech(string[] lines, float duration)
        {
            if (lines == null || lines.Length == 0 || speechBubble == null) return;
            string line = lines[Random.Range(0, lines.Length)];
            if (!string.IsNullOrWhiteSpace(line))
                speechBubble.Show(line, duration);
        }

        private void EnsureSurferAnimator()
        {
            surferAnimator = GetComponent<Animator>();

            if (surferAnimator == null)
                surferAnimator = gameObject.AddComponent<Animator>();

            RuntimeAnimatorController controller =
                Resources.Load<RuntimeAnimatorController>("Animations/chuck");

            if (controller == null)
            {
                Debug.LogWarning(
                    "TinyWaveSurfer could not load Resources/Animations/chuck.controller.",
                    this);
                surferAnimator.enabled = false;
                return;
            }

            surferAnimator.runtimeAnimatorController = controller;
            surferAnimator.applyRootMotion = false;
            surferAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            surferAnimator.enabled = true;

            currentAnimationStateHash = 0;
            playerIdleTimer = 0f;
            UpdateAnimation(false, true);
        }

        private void UpdateAnimation(bool moving, bool force = false)
        {
            if (surferAnimator == null ||
                !surferAnimator.enabled ||
                surferAnimator.runtimeAnimatorController == null ||
                state == RiderState.Dead)
            {
                return;
            }

            bool useProne =
                playerControlled &&
                playProneAfterLongIdle &&
                !moving &&
                state == RiderState.Riding &&
                playerIdleTimer >= proneIdleDelay;

            int desiredStateHash = state == RiderState.SwitchingWave
                ? WaveSwitchStateHash
                : specialSkidding && state == RiderState.Riding
                    ? PushStateHash
                    : airTrickActive && obstacleJumpActive && state == RiderState.TurningTrick
                        ? (currentAirTrickStateHash != 0
                            ? currentAirTrickStateHash
                            : HandstandStateHash)
                        : obstacleJumpActive && state == RiderState.TurningTrick
                            ? SurfJumpStateHash
                            : playerControlled && state == RiderState.TurningTrick
                                ? RotationStateHash
                                : useProne
                                    ? ProneStateHash
                                    : moving
                                        ? MoveStateHash
                                        : IdleStateHash;

            if (!force && currentAnimationStateHash == desiredStateHash)
                return;

            if (!surferAnimator.HasState(0, desiredStateHash))
            {
                Debug.LogWarning(
                    $"Animator state does not exist on layer 0. Hash: {desiredStateHash}",
                    this);

                return;
            }

            currentAnimationStateHash = desiredStateHash;

            float flowAnimationMultiplier = AirTrickScoreSystem.Instance != null
                ? AirTrickScoreSystem.Instance.OnFireAnimationMultiplier
                : 1f;
            if (desiredStateHash == HandstandStateHash)
                surferAnimator.speed = 1.15f * flowAnimationMultiplier;
            else if (desiredStateHash == RotationStateHash)
                surferAnimator.speed = 1.3f * flowAnimationMultiplier;
            else if (desiredStateHash == FlipStateHash)
                surferAnimator.speed = 1.55f * flowAnimationMultiplier;
            else
                surferAnimator.speed = 1f;

            surferAnimator.CrossFade(desiredStateHash, 0.02f, 0, 0f);
        }

        private void PlayDeathAnimation()
        {
            if (surferAnimator == null ||
                !surferAnimator.enabled ||
                surferAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            currentAnimationStateHash = DeathStateHash;
            surferAnimator.Play(DeathStateHash, 0, 0f);
        }

        public bool DieFromShark(Vector2 sharkPosition)
        {
            if (state == RiderState.Dead)
                return false;

            state = RiderState.Dead;
            playerIdleTimer = 0f;
            GodzillaLaneSwimmer.NotifyPlayerDeath(transform.position);
            if (speechBubble != null) speechBubble.HideImmediate();
            PlayDeathAnimation();
            EmitDeathBlood();
            if (humanDeathClip != null && deathAudioSource != null)
                deathAudioSource.PlayOneShot(humanDeathClip);
            deathTimer = 0f;
            respawnTimer = 0f;
            managedDeathReported = false;
            float away = transform.position.x >= sharkPosition.x ? 1f : -1f;
            deathVelocity = new Vector2(away * 0.8f, deathKnockUp);
            livingScale = transform.localScale;
            if (spriteRenderer != null)
                livingColor = spriteRenderer.color;

            Collider2D collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
            return true;
        }

        public bool DieFromAbduction(Vector2 ufoPosition)
        {
            if (state == RiderState.Dead)
                return false;

            state = RiderState.Dead;
            playerIdleTimer = 0f;
            GodzillaLaneSwimmer.NotifyPlayerDeath(transform.position);
            if (speechBubble != null) speechBubble.HideImmediate();
            PlayDeathAnimation();
            if (humanDeathClip != null && deathAudioSource != null)
                deathAudioSource.PlayOneShot(humanDeathClip);
            deathTimer = 0f;
            respawnTimer = 0f;
            managedDeathReported = false;
            deathVelocity = new Vector2((ufoPosition.x - transform.position.x) * 0.35f, deathKnockUp * 1.55f);
            livingScale = transform.localScale;
            if (spriteRenderer != null)
                livingColor = spriteRenderer.color;

            Collider2D collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
            return true;
        }

        private void EmitDeathBlood()
        {
            if (!emitBloodOnDeath || deathBloodParticleCount <= 0)
                return;

            EnsureDeathBloodEmitter();

            if (deathBloodParticles == null)
                return;

            deathBloodParticles.transform.position =
                transform.position + (Vector3)deathBloodOffset;

            float minSpeed = Mathf.Min(
                deathBloodMinSpeed,
                deathBloodMaxSpeed);

            float maxSpeed = Mathf.Max(
                deathBloodMinSpeed,
                deathBloodMaxSpeed);

            float minSize = Mathf.Min(
                deathBloodMinSize,
                deathBloodMaxSize);

            float maxSize = Mathf.Max(
                deathBloodMinSize,
                deathBloodMaxSize);

            // Use ordinary non-HDR red.
            Color bloodRed = deathBloodColor;

            ParticleSystem.EmitParams emit =
                new ParticleSystem.EmitParams
                {
                    startColor = bloodRed,
                    startLifetime = deathBloodLifetime
                };

            for (int i = 0; i < deathBloodParticleCount; i++)
            {
                float angle =
                    Random.Range(0f, 360f) *
                    Mathf.Deg2Rad;

                float speed =
                    Random.Range(minSpeed, maxSpeed);

                emit.velocity = new Vector3(
                    Mathf.Cos(angle) * speed,
                    Mathf.Sin(angle) * speed,
                    Random.Range(-0.03f, 0.03f));

                emit.startSize =
                    Random.Range(minSize, maxSize);

                deathBloodParticles.Emit(emit, 1);
            }
        }

        private void EnsureDeathBloodEmitter()
        {
            if (deathBloodParticles != null)
                return;

            GameObject emitterObject =
                new GameObject("Death Blood Particles");

            emitterObject.transform.SetParent(null);
            emitterObject.transform.position =
                transform.position;

            deathBloodParticles =
                emitterObject.AddComponent<ParticleSystem>();

            Color bloodRed =
                deathBloodColor;

            // Main particle settings.
            ParticleSystem.MainModule main =
                deathBloodParticles.main;

            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace =
                ParticleSystemSimulationSpace.World;

            main.startLifetime =
                deathBloodLifetime;

            main.startSpeed = 0f;

            main.startSize =
                new ParticleSystem.MinMaxCurve(
                    deathBloodMinSize,
                    deathBloodMaxSize);

            main.startColor = bloodRed;
            main.gravityModifier = deathBloodGravity;

            main.maxParticles =
                Mathf.Max(
                    64,
                    deathBloodParticleCount * 3);

            main.stopAction =
                ParticleSystemStopAction.None;

            // Particles are emitted manually.
            ParticleSystem.EmissionModule emission =
                deathBloodParticles.emission;

            emission.enabled = false;

            ParticleSystem.ShapeModule shape =
                deathBloodParticles.shape;

            shape.enabled = false;

            // Keep their launch velocity simple.
            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity =
                deathBloodParticles.limitVelocityOverLifetime;

            limitVelocity.enabled = false;

            // Fade from fully visible red to transparent red.
            ParticleSystem.ColorOverLifetimeModule colourOverLifetime =
                deathBloodParticles.colorOverLifetime;

            colourOverLifetime.enabled = true;

            Gradient fadeGradient = new Gradient();

            fadeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(bloodRed, 0f),
                    new GradientColorKey(bloodRed, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.65f, 0.45f),
                    new GradientAlphaKey(0.25f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });

            colourOverLifetime.color =
                new ParticleSystem.MinMaxGradient(
                    fadeGradient);

            // Shrink the particles while they fade.
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                deathBloodParticles.sizeOverLifetime;

            sizeOverLifetime.enabled = true;

            AnimationCurve sizeCurve =
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.65f, 0.85f),
                    new Keyframe(1f, 0f));

            sizeOverLifetime.size =
                new ParticleSystem.MinMaxCurve(
                    1f,
                    sizeCurve);

            ParticleSystemRenderer particleRenderer =
                emitterObject.GetComponent<ParticleSystemRenderer>();

            particleRenderer.renderMode =
                ParticleSystemRenderMode.Billboard;

            particleRenderer.sortingOrder =
                surferWithinWaveSortingOrder + 3;

            // Find a shader supported by the active render pipeline.
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning(
                    "Could not find a compatible shader for the death blood particles.",
                    this);

                Destroy(emitterObject);
                deathBloodParticles = null;
                return;
            }

            deathBloodMaterial =
                new Material(shader)
                {
                    name = $"{name} Basic Blood Material",
                    hideFlags = HideFlags.HideAndDontSave
                };

            // Ordinary red, with no HDR or emission.
            if (deathBloodMaterial.HasProperty("_BaseColor"))
            {
                deathBloodMaterial.SetColor(
                    "_BaseColor",
                    bloodRed);
            }

            if (deathBloodMaterial.HasProperty("_Color"))
            {
                deathBloodMaterial.SetColor(
                    "_Color",
                    bloodRed);
            }

            // Disable emission so fading alpha actually removes the particles.
            deathBloodMaterial.DisableKeyword("_EMISSION");

            if (deathBloodMaterial.HasProperty("_EmissionColor"))
            {
                deathBloodMaterial.SetColor(
                    "_EmissionColor",
                    Color.black);
            }

            // Force ordinary transparent alpha blending.
            if (deathBloodMaterial.HasProperty("_Surface"))
                deathBloodMaterial.SetFloat("_Surface", 1f);

            if (deathBloodMaterial.HasProperty("_Blend"))
                deathBloodMaterial.SetFloat("_Blend", 0f);

            if (deathBloodMaterial.HasProperty("_SrcBlend"))
            {
                deathBloodMaterial.SetFloat(
                    "_SrcBlend",
                    (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (deathBloodMaterial.HasProperty("_DstBlend"))
            {
                deathBloodMaterial.SetFloat(
                    "_DstBlend",
                    (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (deathBloodMaterial.HasProperty("_ZWrite"))
                deathBloodMaterial.SetFloat("_ZWrite", 0f);

            deathBloodMaterial.DisableKeyword(
                "_ALPHAPREMULTIPLY_ON");

            deathBloodMaterial.DisableKeyword(
                "_ALPHAMODULATE_ON");

            deathBloodMaterial.renderQueue =
                (int)UnityEngine.Rendering.RenderQueue.Transparent;

            particleRenderer.sharedMaterial =
                deathBloodMaterial;
        }

        private void UpdateDeathResponse(float dt)
        {
            deathTimer += dt;
            Vector3 position = transform.position;
            deathVelocity.y -= deathSinkSpeed * dt;
            position += (Vector3)(deathVelocity * dt);
            transform.position = position;
            transform.Rotate(0f, 0f, deathSpinSpeed * dt * (deathVelocity.x >= 0f ? -1f : 1f));

            float normalized = Mathf.Clamp01(deathTimer / Mathf.Max(0.01f, deathDuration));
            if (spriteRenderer != null)
            {
                Color faded = livingColor;
                faded.a = 1f - normalized;
                spriteRenderer.color = faded;
            }

            if (deathTimer < deathDuration)
                return;

            if (IsPlayerControlled && SurfRunLifeManager.Instance != null)
            {
                if (!managedDeathReported)
                {
                    managedDeathReported = true;
                    SurfRunLifeManager.Instance.HandleFinishedPlayerDeath(this);
                }
                return;
            }

            if (!respawnAfterDeath)
            {
                gameObject.SetActive(false);
                return;
            }

            respawnTimer += dt;
            if (respawnTimer >= respawnDelay)
                RespawnAfterShark();
        }

        public void RespawnForManagedRun()
        {
            RespawnAfterShark();
        }

        private void RespawnAfterShark()
        {
            if (simulations.Count == 0)
                RefreshWaveList();
            if (simulations.Count == 0)
                return;

            int safeWave = Mathf.Clamp(waveIndex, 0, simulations.Count - 1);
            PickWave(safeWave, true);
            Vector3 p = GetStartingPosition(currentWave);
            p.x = ClampPlayerXToSandbox((currentWave.TankMinimum.x + currentWave.TankMaximum.x) * 0.5f);
            localRideX = p.x;
            transform.position = p;
            transform.rotation = Quaternion.identity;
            transform.localScale = livingScale;
            direction = 1f;
            state = RiderState.Riding;
            UpdateAnimation(false, true);
            deathTimer = 0f;
            respawnTimer = 0f;
            managedDeathReported = false;
            currentHealth = MaximumHealth;
            healthBar?.SetHealth(currentHealth, MaximumHealth);
            if (spriteRenderer != null) spriteRenderer.color = livingColor;
            Collider2D collider = GetComponent<Collider2D>();
            if (collider != null) collider.enabled = true;
        }

        public void ConfigureGeneratedSurfer(
            int wave,
            bool movingRight,
            float speed,
            Color shirt,
            Color board,
            int order,
            float initialLayerJumpDelay,
            float personalIntervalOffset)
        {
            startingWaveIndex = wave;
            startMovingRight = movingRight;
            horizontalRideSpeed = speed;
            shirtColor = shirt;
            boardColor = board;
            sortingOrder = order;
            secondsPerSimulation = Mathf.Max(
                1f,
                secondsPerSimulation + personalIntervalOffset);

            // sortingOrder remains available as legacy configuration, while the
            // active queue/order is derived from the wave being ridden.
            RefreshWaveList();
            direction = movingRight ? 1f : -1f;
            PickWave(wave, true);
            ScheduleNextLayerJump(initialLayerJumpDelay);
        }

        public void ConfigureSinglePlayer(float scrollSpeed, float boostMultiplier)
        {
            playerControlled = true;
            aiControlled = false;
            randomizeInitialOceanSpawn = true;
            playerIdleTimer = 0f;
            playerScrollSpeed = Mathf.Max(0.25f, scrollSpeed);
            playerBoostMultiplier = Mathf.Max(1f, boostMultiplier);
            cycleContinuously = false;
            jumpToRandomWaveLayer = false;
            direction = 1f;
            // Sandbox mode moves the surfer through the existing simulation.
            // The wave rows remain fixed and are never wrapped or shifted.
            lockPlayerToScreenX = false;
            RefreshWaveList();
            PickWave(Mathf.Clamp(startingWaveIndex, 0, Mathf.Max(0, simulations.Count - 1)), true);
            localRideX = ClampPlayerXToSandbox(localRideX);
            Vector3 p = transform.position;
            p.x = localRideX;
            transform.position = p;
        }


        public void ConfigureAIPlayer(float scrollSpeed, float boostMultiplier)
        {
            ConfigureSinglePlayer(scrollSpeed, boostMultiplier);
            aiControlled = true;
            randomizeInitialOceanSpawn = true;
            aiDecisionTimer = Random.Range(0.6f, 1.4f);
            aiHorizontal = Random.value < 0.5f ? -1f : 1f;
            gameObject.name = "AI Player Surfer";
        }
        private void Update()
        {
            if (state == RiderState.Dead)
            {
                if (speechBubble != null) speechBubble.HideImmediate();
                UpdateDeathResponse(Time.deltaTime);
                return;
            }

            UpdateSpeechBubbles();

            if (simulations.Count == 0)
            {
                RefreshWaveList();
                if (simulations.Count == 0) return;
                PickWave(startingWaveIndex, true);
            }

            simulations.RemoveAll(w => w == null || !w.isActiveAndEnabled);
            if (simulations.Count == 0) return;

            if (currentWave == null || !currentWave.isActiveAndEnabled)
                PickWave(Mathf.Clamp(waveIndex, 0, simulations.Count - 1), true);

            if (playerControlled)
            {
                UpdatePlayerControl(Time.deltaTime);
                return;
            }

            float dt = Time.deltaTime;
            waveTimer += dt;
            stateTimer += dt;

            if (state == RiderState.SwitchingWave)
            {
                UpdateAnimation(true);
                UpdateWaveSwitch();
                return;
            }

            UpdateAnimation(true);

            if (state == RiderState.TurningTrick)
                UpdateTurnTrick();
            else
                UpdateRide(dt);

            if (waveTimer >= currentSimulationDuration &&
                simulations.Count > 1 &&
                cycleContinuously &&
                state == RiderState.Riding)
            {
                BeginNextWave();
            }
        }

        [ContextMenu("Refresh Wave Simulations")]
        public void RefreshWaveList()
        {
            PixelWaterGPU previouslySelectedWave = currentWave;

            int desiredLayer = previouslySelectedWave != null
                ? previouslySelectedWave.IndependentLayerIndex
                : waveIndex;

            simulations.Clear();
            simulations.AddRange(EndlessWaveSections.LayersNearest(localRideX));
            simulations.RemoveAll(w => w == null || !w.isActiveAndEnabled || w.gameObject == gameObject);
            simulations.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));

            if (simulations.Count > 0)
            {
                int selectedIndex = simulations.FindIndex(w => w.IndependentLayerIndex == desiredLayer);
                if (selectedIndex < 0) selectedIndex = Mathf.Clamp(desiredLayer, 0, simulations.Count - 1);
                waveIndex = selectedIndex;
                currentWave = simulations[selectedIndex];
            }
        }

        private void UpdatePlayerControl(float dt)
        {
            ReadPlayerInput(out float horizontal, out bool jumpHeld,
                out bool layerUpHeld, out bool layerDownHeld, out bool boostHeld,
                out float trickInput);
            playerTrickInput = trickInput;

            bool attackHeld = aiControlled ? aiAttackPulse > 0f : ReadAttackInput();
            if (attackHeld && !previousAttackHeld)
            {
                if (obstacleJumpActive && state == RiderState.TurningTrick)
                    TriggerAirTrick(FlipStateHash); // Xbox X / keyboard F or X
                else
                    PerformAction(layerUpHeld);
            }
            previousAttackHeld = attackHeld;

            bool specialHeld = aiControlled ? aiSpecialHold > 0f : ReadSpecialInput();
            bool specialPressed = specialHeld && !previousSpecialHeld;
            UpdateChargedWaterSkid(specialHeld, horizontal, dt);

            // B/E has its own deterministic airborne trick. On the water it
            // retains the charged-skid and glide-wave-switch behaviour.
            if (specialPressed && obstacleJumpActive && state == RiderState.TurningTrick)
            {
                TriggerAirTrick(RotationStateHash); // Xbox B / keyboard E or B
            }
            else if (specialPressed && specialSkidding && state == RiderState.Riding &&
                !layerSwitchInputLocked)
            {
                bool glideUp = layerUpHeld && !layerDownHeld;
                bool glideDown = layerDownHeld && !layerUpHeld;
                if (glideUp || glideDown)
                {
                    BeginAdjacentWave(glideUp ? +1 : -1, true);
                    layerSwitchInputLocked = true;
                }
            }

            previousSpecialHeld = specialHeld;

            if (!specialCharging && !specialSkidding)
            {
                float targetSpeed = horizontal * playerScrollSpeed *
                    (boostHeld ? playerBoostMultiplier : 1f);
                float response = Mathf.Abs(targetSpeed) > 0.01f
                    ? playerAcceleration
                    : playerDeceleration;
                playerHorizontalVelocity = Mathf.MoveTowards(
                    playerHorizontalVelocity,
                    targetSpeed,
                    response * dt);

                bool canMove = state == RiderState.Riding ||
                    (state == RiderState.TurningTrick && !obstacleJumpActive);
                if (canMove)
                {
                    float control = state == RiderState.TurningTrick ? airControl : 1f;
                    localRideX += playerHorizontalVelocity * control * dt;
                    if (Mathf.Abs(playerHorizontalVelocity) > 0.02f)
                        direction = Mathf.Sign(playerHorizontalVelocity);
                }
            }

            RebindToNearestHorizontalSection();

            bool moving = Mathf.Abs(playerHorizontalVelocity) > 0.03f &&
                state == RiderState.Riding;
            bool hasPlayerActivity = moving || jumpHeld || layerUpHeld || layerDownHeld ||
                specialCharging || specialSkidding || Mathf.Abs(trickInput) > 0.05f;

            if (hasPlayerActivity || state != RiderState.Riding)
                playerIdleTimer = 0f;
            else
                playerIdleTimer += dt;

            UpdateAnimation(moving);
            localRideX = ClampPlayerXToSandbox(localRideX);

            // Jump is now the required commit button for player wave changes.
            // Hold a vertical direction, then press Jump:
            //   Up + Space / controller A   -> trick-jump one wave upward
            //   Down + Space / controller A -> trick-jump one wave downward
            // Pressing Jump without a vertical direction performs a normal trick
            // jump and remains on the current wave. Up/Down by themselves do not
            // switch waves anymore.
            bool jumpPressed = jumpHeld && !previousJumpHeld;
            bool jumpReleased = !jumpHeld && previousJumpHeld;

            // Once the takeoff button has been released, pressing Space / controller A
            // again during the same forward surf jump performs the handstand trick.
            if (jumpPressed && obstacleJumpActive && state == RiderState.TurningTrick)
            {
                TriggerAirTrick(HandstandStateHash); // Xbox A / keyboard Space
            }
            else if (state == RiderState.Riding && !specialCharging)
            {
                bool wantsUp = layerUpHeld && !layerDownHeld;
                bool wantsDown = layerDownHeld && !layerUpHeld;

                // Wave-layer changes remain immediate combinations. Forward jumps
                // instead charge while Jump is held and launch on release, giving
                // keyboard and digital gamepad buttons pressure-like sensitivity.
                if (jumpPressed && (wantsUp || wantsDown))
                {
                    CancelJumpCharge();
                    BeginAdjacentWave(wantsUp ? +1 : -1);
                    layerSwitchInputLocked = true;
                }
                else if (jumpPressed)
                {
                    float takeoffInput = specialSkidding
                        ? direction
                        : horizontal;

                    if (enableForwardObstacleJump &&
                        (specialSkidding || Mathf.Abs(takeoffInput) >= obstacleJumpInputThreshold))
                    {
                        BeginJumpCharge(takeoffInput);
                    }
                    else
                    {
                        BeginTurnTrick();
                    }
                }

                if (jumpCharging && jumpHeld)
                {
                    jumpChargeTime = Mathf.Min(
                        fullJumpChargeTime,
                        jumpChargeTime + dt);

                    if (Mathf.Abs(horizontal) >= obstacleJumpInputThreshold)
                        jumpChargeHorizontalInput = horizontal;
                }

                if (jumpCharging && jumpReleased)
                    ReleaseChargedForwardJump();
            }

            // Release Jump before another normal jump or wave-switch jump can
            // begin. The direction may remain held, which makes the combination
            // responsive without allowing repeated automatic layer changes.
            if (!jumpHeld)
                layerSwitchInputLocked = false;

            previousJumpHeld = jumpHeld;
            previousLayerUpHeld = layerUpHeld;
            previousLayerDownHeld = layerDownHeld;

            stateTimer += dt;
            if (state == RiderState.SwitchingWave)
                UpdateWaveSwitch();
            else if (state == RiderState.TurningTrick)
                UpdateTurnTrick();
            else
            {
                localRideX = ClampPlayerXToSandbox(localRideX);
                FollowSurface(dt);
            }
        }

        private bool ReadSpecialInput()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboardHeld = Keyboard.current != null &&
                (Keyboard.current.eKey.isPressed || Keyboard.current.bKey.isPressed);
            bool gamepadHeld = Gamepad.current != null && Gamepad.current.buttonEast.isPressed;
            return keyboardHeld || gamepadHeld;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.B) ||
                Input.GetKey(KeyCode.JoystickButton1);
#else
            return false;
#endif
        }

        private void UpdateChargedWaterSkid(bool specialHeld, float horizontalInput, float dt)
        {
            if (!enableChargedWaterSkid || state != RiderState.Riding || currentWave == null)
            {
                specialCharging = false;
                specialChargeTime = 0f;
                return;
            }

            if (specialHeld && !specialSkidding)
            {
                specialCharging = true;
                specialChargeTime = Mathf.Min(maximumSkidChargeTime, specialChargeTime + dt);
                playerHorizontalVelocity = Mathf.MoveTowards(
                    playerHorizontalVelocity, 0f, playerDeceleration * 2f * dt);

                if (Mathf.Abs(horizontalInput) >= gamepadDeadZone)
                    direction = Mathf.Sign(horizontalInput);

                return;
            }

            if (specialCharging && !specialHeld && previousSpecialHeld)
            {
                float heldTime = specialChargeTime;
                specialCharging = false;
                specialChargeTime = 0f;

                if (heldTime >= minimumSkidChargeTime)
                {
                    float charge01 = Mathf.Clamp01(heldTime / Mathf.Max(0.01f, maximumSkidChargeTime));
                    specialSkidDuration = Mathf.Lerp(minimumSkidDuration, maximumSkidDuration, charge01);
                    specialSkidSpeed = Mathf.Lerp(minimumSkidSpeed, maximumSkidSpeed, charge01);
                    specialSkidTimer = specialSkidDuration;
                    specialSkidding = true;
                    playerHorizontalVelocity = 0f;

                    // Immediately enter the push animation when propulsion begins.
                    currentAnimationStateHash = 0;
                    UpdateAnimation(false, true);
                    if (speechBubble != null) speechBubble.HideImmediate();
                }
            }

            if (!specialSkidding)
                return;

            specialSkidTimer -= dt;
            float skid01 = Mathf.Clamp01(specialSkidTimer / Mathf.Max(0.01f, specialSkidDuration));
            float easedSpeed = specialSkidSpeed * Mathf.SmoothStep(0.15f, 1f, skid01);
            specialSkidCurrentSpeed = easedSpeed;
            localRideX += direction * easedSpeed * dt;
            localRideX = ClampPlayerXToSandbox(localRideX);
            RebindToNearestHorizontalSection();

            if (specialSkidTimer <= 0f)
            {
                specialSkidding = false;
                specialSkidTimer = 0f;
                specialSkidCurrentSpeed = 0f;
                playerHorizontalVelocity = direction * Mathf.Min(playerScrollSpeed, specialSkidSpeed * 0.2f);
            }
        }

        private void BeginAdjacentWave(int step, bool preserveGlidePush = false)
        {
            if (state != RiderState.Riding)
                return;

            if (speechBubble != null) speechBubble.HideImmediate();

            // Refresh first because the independent layers are generated at
            // runtime. This also restores a strict 0..N layer-index order.
            RefreshWaveList();

            if (simulations.Count <= 1)
                return;

            step = Mathf.Clamp(step, -1, 1);
            if (step == 0)
                return;

            // Never trust an old numeric index. Resolve the surfer's current
            // wave by reference, then move exactly one entry in the sorted list.
            int currentIndex = simulations.IndexOf(currentWave);
            if (currentIndex < 0)
                currentIndex = Mathf.Clamp(waveIndex, 0, simulations.Count - 1);

            int nextIndex = currentIndex + step;

            // Wrap only when pressing outward at either end of the full stack.
            if (nextIndex < 0)
                nextIndex = simulations.Count - 1;
            else if (nextIndex >= simulations.Count)
                nextIndex = 0;

            PixelWaterGPU nextWave = simulations[nextIndex];
            if (nextWave == null || nextWave == currentWave)
                return;

            layerSwitchInputLocked = true;
            currentWave = nextWave;
            waveIndex = nextIndex;

            ApplyCurrentWaveSorting(true);

            stateTimer = 0f;
            state = RiderState.SwitchingWave;
            renderDepth = currentWave.transform.position.z - 0.02f;

            switchStart = transform.position;
            switchTarget = GetStartingPosition(currentWave);

            glideWaveSwitchActive = preserveGlidePush && specialSkidding;
            glideWaveSwitchSpeed = glideWaveSwitchActive
                ? Mathf.Max(specialSkidCurrentSpeed,
                    Mathf.Abs(playerHorizontalVelocity),
                    specialSkidSpeed * 0.15f)
                : 0f;

            // Project the surfer forward during a glide transfer. The charged
            // push remains active after landing instead of being converted into
            // an ordinary wave-switch hop.
            float projectedX = localRideX;
            if (glideWaveSwitchActive && !lockPlayerToScreenX)
                projectedX += direction * glideWaveSwitchSpeed * switchDuration * glideWaveSwitchDistanceMultiplier;

            switchTarget.x = ClampPlayerXToSandbox(projectedX);
            switchTarget.z = renderDepth;

            transform.rotation = Quaternion.identity;
            UpdateAnimation(false, true);
        }

        private void RebindToNearestHorizontalSection()
        {
            EndlessWaveSections endless = EndlessWaveSections.Instance;
            if (endless == null || !endless.IsReady || currentWave == null)
                return;

            // Keep the same vertical layer while crossing a horizontal seam.
            float padding = 0.02f;
            if (localRideX >= currentWave.TankMinimum.x - padding &&
                localRideX <= currentWave.TankMaximum.x + padding)
                return;

            int verticalLayer = currentWave.IndependentLayerIndex;
            List<PixelWaterGPU> nearest = EndlessWaveSections.LayersNearest(localRideX);
            PixelWaterGPU replacement = nearest.Find(w => w.IndependentLayerIndex == verticalLayer);
            if (replacement == null || replacement == currentWave)
                return;

            simulations.Clear();
            simulations.AddRange(nearest);
            simulations.Sort((a, b) => a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
            currentWave = replacement;
            waveIndex = simulations.IndexOf(replacement);
            renderDepth = currentWave.transform.position.z - 0.02f;
            ApplyCurrentWaveSorting(true);
        }

        private float ClampPlayerXToSandbox(float desiredX)
        {
            EndlessWaveSections endless = EndlessWaveSections.Instance;
            if (endless != null && endless.IsReady)
            {
                // Only clamp against the far outside edges of the active three
                // sections. Normal section seams remain completely traversable.
                float minimumX = endless.MinimumWorldX + playerCameraEdgePadding;
                float maximumX = endless.MaximumWorldX - playerCameraEdgePadding;
                return minimumX <= maximumX
                    ? Mathf.Clamp(desiredX, minimumX, maximumX)
                    : desiredX;
            }

            if (currentWave == null)
                return desiredX;

            Vector2 waveMin = currentWave.TankMinimum;
            Vector2 waveMax = currentWave.TankMaximum;
            float waveWidth = Mathf.Max(0.01f, waveMax.x - waveMin.x);
            float wavePadding = waveWidth * edgePadding;
            return Mathf.Clamp(desiredX, waveMin.x + wavePadding, waveMax.x - wavePadding);
        }

        private void ReadPlayerInput(out float horizontal, out bool jump,
            out bool layerUp, out bool layerDown, out bool boost, out float trick)
        {
            if (aiControlled)
            {
                UpdateAIIntent(Time.deltaTime);
                horizontal = aiHorizontal;
                jump = aiJumpPulse > 0f;
                layerUp = aiLayerDirection > 0;
                layerDown = aiLayerDirection < 0;
                boost = Random.value < 0.35f;
                trick = aiTrick;
                return;
            }
#if ENABLE_INPUT_SYSTEM
            horizontal = 0f;
            jump = layerUp = layerDown = boost = false;
            trick = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                horizontal += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                jump |= keyboard.spaceKey.isPressed;
                layerUp |= keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                layerDown |= keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                boost |= keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                trick -= keyboard.qKey.isPressed ? 1f : 0f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float stickX = gamepad.leftStick.x.ReadValue();
                if (Mathf.Abs(stickX) >= gamepadDeadZone)
                    horizontal = stickX;

                if (gamepad.dpad.left.isPressed)
                    horizontal = -1f;
                else if (gamepad.dpad.right.isPressed)
                    horizontal = 1f;

                jump |= gamepad.buttonSouth.isPressed;          // Xbox A

                // D-pad Up/Down and the left stick vertical axis are wave-jump
                // modifiers. They only cause a layer change when controller A
                // is newly pressed, so vertical stick input never switches waves
                // by itself.
                float stickY = gamepad.leftStick.y.ReadValue();
                layerUp |= gamepad.dpad.up.isPressed || stickY >= gamepadDeadZone;
                layerDown |= gamepad.dpad.down.isPressed || stickY <= -gamepadDeadZone;

                boost |= gamepad.rightTrigger.ReadValue() > 0.2f;

                float rightX = gamepad.rightStick.x.ReadValue();
                if (Mathf.Abs(rightX) >= gamepadDeadZone)
                    trick = rightX;
            }

            horizontal = Mathf.Clamp(horizontal, -1f, 1f);
            trick = Mathf.Clamp(trick, -1f, 1f);
            return;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            horizontal = Input.GetAxisRaw("Horizontal");
            jump = Input.GetKey(KeyCode.Space) || Input.GetButton("Jump");
            layerUp = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            layerDown = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            trick = Input.GetKey(KeyCode.Q) ? -1f : 0f;
#else
            horizontal = trick = 0f;
            jump = layerUp = layerDown = boost = false;
#endif
        }


        private void UpdateAIIntent(float dt)
        {
            aiDecisionTimer -= dt;
            aiJumpPulse = Mathf.Max(0f, aiJumpPulse - dt);
            aiAttackPulse = Mathf.Max(0f, aiAttackPulse - dt);
            aiSpecialHold = Mathf.Max(0f, aiSpecialHold - dt);

            if (currentWave != null)
            {
                float width = Mathf.Max(0.01f, currentWave.TankMaximum.x - currentWave.TankMinimum.x);
                float leftTurn = currentWave.TankMinimum.x + width * 0.16f;
                float rightTurn = currentWave.TankMaximum.x - width * 0.16f;
                if (localRideX <= leftTurn) aiHorizontal = 1f;
                if (localRideX >= rightTurn) aiHorizontal = -1f;
            }

            if (aiDecisionTimer > 0f || state != RiderState.Riding)
                return;

            aiDecisionTimer = Random.Range(0.7f, 2.2f);
            aiLayerDirection = 0;
            aiTrick = Random.Range(-1f, 1f);

            float choice = Random.value;
            if (choice < 0.24f)
            {
                aiJumpPulse = 0.12f;
            }
            else if (choice < 0.45f && simulations.Count > 1)
            {
                aiLayerDirection = Random.value < 0.5f ? -1 : 1;
                aiJumpPulse = 0.12f;
            }
            else if (choice < 0.62f && throwableItems.Count > 0)
            {
                aiAttackPulse = 0.12f;
            }
            else if (choice < 0.76f && enableChargedWaterSkid)
            {
                aiSpecialHold = Random.Range(0.25f, maximumSkidChargeTime);
            }
            else if (choice < 0.88f)
            {
                aiHorizontal *= -1f;
            }
        }
        private void UpdateRide(float dt)
        {
            Vector2 min = currentWave.TankMinimum;
            Vector2 max = currentWave.TankMaximum;
            float width = Mathf.Max(0.01f, max.x - min.x);
            float left = min.x + width * edgePadding;
            float right = max.x - width * edgePadding;

            Vector2 waveVelocity = currentWave.GetGameplayWaveVelocity(localRideX);
            float waveAssist = waveVelocity.x * waveVelocityInfluence * direction;
            localRideX += direction *
                Mathf.Max(0.2f, horizontalRideSpeed + waveAssist) * dt;

            if (localRideX >= right)
            {
                localRideX = right;
                BeginTurnTrick();
                return;
            }

            if (localRideX <= left)
            {
                localRideX = left;
                BeginTurnTrick();
                return;
            }

            FollowSurface(dt);
        }

        private void FollowSurface(float dt)
        {
            float surfaceY = currentWave.GetGameplaySurfaceHeight(localRideX);

            const float sample = 0.09f;
            float leftY = currentWave.GetGameplaySurfaceHeight(localRideX - sample);
            float rightY = currentWave.GetGameplaySurfaceHeight(localRideX + sample);

            float slope = Mathf.Atan2(
                rightY - leftY,
                sample * 2f) * Mathf.Rad2Deg;

            float speedRatio = playerControlled
                ? Mathf.Clamp01(
                    Mathf.Abs(direction) * playerScrollSpeed /
                    Mathf.Max(
                        0.01f,
                        playerScrollSpeed * playerBoostMultiplier))
                : Mathf.Clamp01(horizontalRideSpeed / 2f);

            float waterMotion = Mathf.Clamp01(
                Mathf.Abs(
                    currentWave.GetGameplayWaveVelocity(localRideX).y));

            float bobPhase =
                Time.time *
                idleBobFrequency *
                Mathf.PI *
                2f +
                waveIndex * 0.73f;

            float bob =
                Mathf.Sin(bobPhase) *
                idleBobHeight *
                (0.35f + waterMotion * 0.65f);

            Vector3 target = new Vector3(
                localRideX,
                surfaceY + surfaceOffset + bob,
                renderDepth);

            if (playerControlled && state == RiderState.Riding)
            {
                transform.position = target;
            }
            else
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    target,
                    1f - Mathf.Exp(-surfaceFollow * dt));
            }

            if (playerControlled && specialCharging)
            {
                float charge01 = Mathf.Clamp01(specialChargeTime / Mathf.Max(0.01f, maximumSkidChargeTime));
                float shake = chargeShakeAmount * charge01;
                float shakePhase = Time.time * chargeShakeFrequency;
                transform.position += new Vector3(
                    Mathf.Sin(shakePhase * 1.37f) * shake,
                    Mathf.Cos(shakePhase * 1.91f) * shake * 0.55f,
                    0f);
            }

            float compression =
                Mathf.Sin(bobPhase + Mathf.PI * 0.5f) *
                stanceSquash *
                (0.4f + speedRatio * 0.6f);

            float xScale =
                spriteWorldScale *
                (1f + compression * 0.35f);

            float yScale =
                spriteWorldScale *
                (1f - compression);

            ApplyFacing(xScale, yScale);

            float balanceLean =
                -direction *
                directionalLean *
                speedRatio;

            float microPitch =
                Mathf.Cos(bobPhase) *
                1.2f *
                waterMotion;

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.Euler(
                    0f,
                    0f,
                    slope + balanceLean + microPitch +
                    (specialSkidding ? -direction * skidLeanDegrees : 0f)),
                1f - Mathf.Exp(-surfaceFollow * 0.7f * dt));
        }
        private void BeginTurnTrick()
        {
            if (speechBubble != null) speechBubble.HideImmediate();
            obstacleJumpActive = false;
            airTrickActive = false;
            airTrickTimer = 0f;
            currentAirTrickStateHash = 0;
            queuedAirTrickStateHash = 0;
            obstacleJumpProgress = 0f;
            state = RiderState.TurningTrick;
            stateTimer = 0f;
            airStartY = currentWave.GetGameplaySurfaceHeight(localRideX) + surfaceOffset;
            flipTrick = Random.value < flipChance;
        }

        private void BeginJumpCharge(float horizontalInput)
        {
            jumpCharging = true;
            jumpChargeTime = 0f;
            jumpChargeHorizontalInput = Mathf.Abs(horizontalInput) >= obstacleJumpInputThreshold
                ? horizontalInput
                : direction;
        }

        private void CancelJumpCharge()
        {
            jumpCharging = false;
            jumpChargeTime = 0f;
            jumpChargeHorizontalInput = 0f;
        }

        private void ReleaseChargedForwardJump()
        {
            float charge01 = Mathf.Clamp01(
                jumpChargeTime / Mathf.Max(0.05f, fullJumpChargeTime));
            float shapedCharge = Mathf.Clamp01(jumpChargeCurve.Evaluate(charge01));
            float jumpHeight = Mathf.Lerp(
                minimumObstacleJumpHeight,
                Mathf.Max(minimumObstacleJumpHeight, maximumObstacleJumpHeight),
                shapedCharge);
            float horizontalInput = Mathf.Abs(jumpChargeHorizontalInput) >= obstacleJumpInputThreshold
                ? jumpChargeHorizontalInput
                : direction;

            CancelJumpCharge();
            BeginForwardSurfJump(horizontalInput, jumpHeight);
        }

        private void BeginForwardSurfJump(float horizontalInput, float chargedJumpHeight)
        {
            if (speechBubble != null) speechBubble.HideImmediate();

            // Convert the active charged-skid push into a one-time launch impulse.
            // The skid itself stops immediately; only its current momentum affects
            // the jump distance and takeoff velocity.
            float carriedSkidSpeed = specialSkidding
                ? Mathf.Max(specialSkidCurrentSpeed, Mathf.Abs(playerHorizontalVelocity))
                : 0f;

            specialSkidding = false;
            specialSkidTimer = 0f;
            specialSkidCurrentSpeed = 0f;

            obstacleJumpActive = true;
            airTrickActive = false;
            airTrickTimer = 0f;
            currentAirTrickStateHash = 0;
            queuedAirTrickStateHash = 0;
            obstacleJumpProgress = 0f;
            float flowJumpMultiplier = AirTrickScoreSystem.Instance != null
                ? AirTrickScoreSystem.Instance.OnFireJumpMultiplier
                : 1f;
            activeObstacleJumpHeight = Mathf.Clamp(
                chargedJumpHeight * flowJumpMultiplier,
                minimumObstacleJumpHeight,
                Mathf.Max(minimumObstacleJumpHeight,
                    maximumObstacleJumpHeight * flowJumpMultiplier));
            state = RiderState.TurningTrick;
            stateTimer = 0f;

            float inputDirection = Mathf.Abs(horizontalInput) > obstacleJumpInputThreshold
                ? Mathf.Sign(horizontalInput)
                : direction;
            direction = inputDirection == 0f ? 1f : inputDirection;

            playerHorizontalVelocity = direction * carriedSkidSpeed;
            float carriedDistance = carriedSkidSpeed * 0.22f;

            obstacleJumpStartX = localRideX;
            scoredJumpStartX = localRideX;
            obstacleJumpTargetX = ClampPlayerXToSandbox(
                obstacleJumpStartX + direction * (obstacleJumpDistance + carriedDistance));
            airStartY = currentWave.GetGameplaySurfaceHeight(localRideX) + surfaceOffset;
            scoredJumpPeakY = airStartY;
            scoredHandstand = false;
            scoredRotation = false;
            scoredFlip = false;
            flipTrick = false;
            aerialTrickChainCount = 0;
            aerialTrickAirtimeBonus = 0f;
            obstacleAirElapsed = 0f;

            // Use continuous velocity instead of recalculating a normalized arc.
            // Extending a combo can therefore never move the surfer back to an
            // earlier point in the jump.
            float launchDuration = Mathf.Max(0.15f, obstacleJumpDuration);
            obstacleAirGravity = (8f * activeObstacleJumpHeight) /
                (launchDuration * launchDuration);
            obstacleAirTakeoffVelocity = (4f * activeObstacleJumpHeight) / launchDuration;
            obstacleAirVerticalVelocity = obstacleAirTakeoffVelocity;
            obstacleAirHorizontalVelocity =
                (obstacleJumpTargetX - obstacleJumpStartX) / launchDuration;

            // Start the dedicated surf-jump clip immediately. UpdateAnimation()
            // keeps this state active for the full obstacle jump and will not let
            // the ordinary move/idle animation replace it while airborne.
            UpdateAnimation(true, true);
        }

        private void TriggerAirTrick(int animationStateHash)
        {
            if (!obstacleJumpActive || state != RiderState.TurningTrick)
                return;

            // Input is read before the movement update each frame. Reject a
            // late combo press immediately when the descending surfer has
            // already reached the live water surface, rather than allowing one
            // final chained launch before the landing state is processed.
            if (currentWave != null && obstacleAirVerticalVelocity <= 0f)
            {
                float liveSurfaceY =
                    currentWave.GetGameplaySurfaceHeight(localRideX) + surfaceOffset;
                if (transform.position.y <= liveSurfaceY + 0.01f)
                {
                    queuedAirTrickStateHash = 0;
                    return;
                }
            }

            bool alreadyPerformed =
                (animationStateHash == HandstandStateHash && scoredHandstand) ||
                (animationStateHash == RotationStateHash && scoredRotation) ||
                (animationStateHash == FlipStateHash && scoredFlip);
            if (alreadyPerformed || animationStateHash == queuedAirTrickStateHash)
                return;

            int chainLimit = Mathf.Clamp(maximumTricksPerChain, 1, 3);
            if (aerialTrickChainCount >= chainLimit)
                return;

            // A different trick pressed while the current clip is playing is buffered.
            // It starts only after the current trick has played for its full duration.
            if (airTrickActive)
            {
                if (enableAerialTrickChain && aerialTrickChainCount < chainLimit)
                    queuedAirTrickStateHash = animationStateHash;
                return;
            }

            StartAirTrick(animationStateHash);
        }

        private void StartAirTrick(int animationStateHash)
        {
            airTrickActive = true;
            airTrickTimer = 0f;
            currentAirTrickStateHash = animationStateHash;
            queuedAirTrickStateHash = 0;

            if (animationStateHash == HandstandStateHash) scoredHandstand = true;
            else if (animationStateHash == RotationStateHash) scoredRotation = true;
            else if (animationStateHash == FlipStateHash) scoredFlip = true;

            aerialTrickChainCount++;

            if (enableAerialTrickChain && aerialTrickChainCount >= 2)
            {
                // The second and third tricks behave like genuine extra jumps.
                // They act on the surfer's CURRENT velocity and position instead
                // of stretching the original arc, so no rewind or old-location
                // snap can occur during descent.
                float jumpStrength = aerialTrickChainCount == 2
                    ? secondTrickJumpStrength
                    : thirdTrickJumpStrength;
                float flowJumpMultiplier = AirTrickScoreSystem.Instance != null
                    ? AirTrickScoreSystem.Instance.OnFireJumpMultiplier
                    : 1f;
                float renewedUpwardVelocity =
                    obstacleAirTakeoffVelocity * jumpStrength * flowJumpMultiplier;
                obstacleAirVerticalVelocity = Mathf.Max(
                    obstacleAirVerticalVelocity,
                    renewedUpwardVelocity);

                // Do not add or scale horizontal speed here. The complete combo
                // carries only the momentum established by the initial jump.
                // Chained tricks contribute vertical lift and animation timing only.
            }

            if (speechBubble != null)
                speechBubble.HideImmediate();

            UpdateAnimation(true, true);
        }

        private void UpdateTurnTrick()
        {
            if (obstacleJumpActive)
            {
                UpdateForwardComboJump();
                return;
            }

            float t = Mathf.Clamp01(
                stateTimer /
                Mathf.Max(0.01f, turnTrickDuration));
            obstacleJumpProgress = 0f;

            float surfaceY = currentWave.GetGameplaySurfaceHeight(localRideX);
            float baseArc = Mathf.Sin(t * Mathf.PI);
            float arc = Mathf.Pow(Mathf.Max(0f, baseArc), playerJumpArcPower) * turnJumpHeight;

            transform.position = new Vector3(
                localRideX,
                Mathf.Max(surfaceY + surfaceOffset, airStartY + arc),
                renderDepth);

            bool basicPlayerJump = playerControlled;
            if (basicPlayerJump)
            {
                transform.rotation = Quaternion.identity;
                ApplyFacing(spriteWorldScale, spriteWorldScale);
            }
            else
            {
                float spinDirection = direction >= 0f ? -1f : 1f;
                float automaticSpin = turnSpinDegrees * spinDirection * t;
                float controlledSpin = playerControlled
                    ? playerTrickInput * playerAirTrickDegrees * t
                    : 0f;
                transform.rotation = Quaternion.Euler(0f, 0f, automaticSpin + controlledSpin);

                float flipAmount = flipTrick ? Mathf.Cos(t * Mathf.PI * 2f) : 1f;
                float trickScale = spriteWorldScale * Mathf.Max(0.18f, Mathf.Abs(flipAmount));
                ApplyFacing(trickScale, spriteWorldScale);
                if (flipTrick && spriteRenderer != null && flipAmount < 0f)
                    spriteRenderer.flipX = !spriteRenderer.flipX;
            }

            if (t < 1f)
                return;

            if (!playerControlled)
                direction *= -1f;

            state = RiderState.Riding;
            stateTimer = 0f;
            transform.rotation = Quaternion.identity;
            ApplyFacing(spriteWorldScale, spriteWorldScale);
            UpdateAnimation(Mathf.Abs(playerHorizontalVelocity) > 0.03f, true);
        }

        private void UpdateForwardComboJump()
        {
            float dt = Time.deltaTime;
            obstacleAirElapsed += dt;

            if (airTrickActive)
            {
                airTrickTimer += dt;
                if (airTrickTimer >= airTrickDuration)
                {
                    airTrickActive = false;
                    airTrickTimer = 0f;
                    currentAirTrickStateHash = 0;

                    int nextTrick = queuedAirTrickStateHash;
                    queuedAirTrickStateHash = 0;
                    if (nextTrick != 0)
                        StartAirTrick(nextTrick);
                    else
                        UpdateAnimation(true, true);
                }
            }

            // Preserve momentum continuously. A chained trick only changes the
            // current velocities; it never recomputes an old point on the arc.
            localRideX += obstacleAirHorizontalVelocity * dt;
            localRideX = ClampPlayerXToSandbox(localRideX);
            RebindToNearestHorizontalSection();

            float currentY = transform.position.y;

            // Keep gravity active throughout the combo so the surfer never
            // freezes in mid-air. While a trick clip is playing gravity is
            // softened rather than disabled, producing a gentle natural drift
            // through the peak and into descent. Full gravity resumes between
            // clips and after the final animation.
            float gravityScale = airTrickActive
                ? Mathf.Clamp(activeTrickGravityMultiplier, 0.1f, 1f)
                : 1f;
            obstacleAirVerticalVelocity -= obstacleAirGravity * gravityScale * dt;

            float nextY = currentY + obstacleAirVerticalVelocity * dt;

            float surfaceY = currentWave.GetGameplaySurfaceHeight(localRideX) + surfaceOffset;

            // Water contact always ends the aerial chain. Trick animations and
            // buffered inputs may soften gravity, but they are never allowed to
            // hold the surfer above the surface or preserve an airborne state
            // after the board has reached the water.
            bool touchedWaterThisFrame = obstacleAirVerticalVelocity <= 0f &&
                nextY <= surfaceY + 0.001f;

            transform.position = new Vector3(
                localRideX,
                touchedWaterThisFrame ? surfaceY : nextY,
                renderDepth);
            scoredJumpPeakY = Mathf.Max(scoredJumpPeakY, transform.position.y);

            float expectedDuration = Mathf.Max(0.15f, obstacleJumpDuration) +
                Mathf.Max(0, aerialTrickChainCount - 1) * airTrickDuration;
            obstacleJumpProgress = Mathf.Clamp01(obstacleAirElapsed / expectedDuration);

            // Animation sheets provide the trick rotation. Keep the controller
            // root stable and facing forward so visual frames remain attached to
            // the surfer's current world position.
            transform.rotation = Quaternion.identity;
            ApplyFacing(spriteWorldScale, spriteWorldScale);

            bool descendingToWater = touchedWaterThisFrame ||
                (obstacleAirVerticalVelocity <= 0f &&
                 transform.position.y <= surfaceY + 0.001f);
            if (!descendingToWater)
                return;

            // Close the combo before reporting the landing. This prevents an
            // input read on the next frame from starting or buffering another
            // trick after the surfer has already touched the water.
            airTrickActive = false;
            airTrickTimer = 0f;
            currentAirTrickStateHash = 0;
            queuedAirTrickStateHash = 0;

            playerHorizontalVelocity = obstacleAirHorizontalVelocity;

            if (playerControlled && AirTrickScoreSystem.Instance != null)
            {
                float achievedHeight = Mathf.Max(0f, scoredJumpPeakY - airStartY);
                float travelledDistance = Mathf.Abs(localRideX - scoredJumpStartX);
                AirTrickScoreSystem.Instance.AwardJump(
                    transform.position,
                    achievedHeight,
                    scoredHandstand,
                    scoredRotation,
                    scoredFlip,
                    aerialTrickChainCount,
                    obstacleAirElapsed,
                    travelledDistance,
                    true);
            }

            obstacleJumpActive = false;
            airTrickActive = false;
            airTrickTimer = 0f;
            currentAirTrickStateHash = 0;
            queuedAirTrickStateHash = 0;
            obstacleJumpProgress = 0f;
            aerialTrickChainCount = 0;
            aerialTrickAirtimeBonus = 0f;
            obstacleAirVerticalVelocity = 0f;
            obstacleAirHorizontalVelocity = 0f;
            obstacleAirElapsed = 0f;

            state = RiderState.Riding;
            stateTimer = 0f;
            transform.position = new Vector3(localRideX, surfaceY, renderDepth);
            transform.rotation = Quaternion.identity;
            ApplyFacing(spriteWorldScale, spriteWorldScale);
            UpdateAnimation(Mathf.Abs(playerHorizontalVelocity) > 0.03f, true);
        }

        [ContextMenu("Ride Next Wave")]
        public void BeginNextWave()
        {
            if (speechBubble != null) speechBubble.HideImmediate();
            if (simulations.Count <= 1)
            {
                waveTimer = 0f;
                return;
            }

            int next;
            if (jumpToRandomWaveLayer && simulations.Count > 2)
            {
                next = waveIndex;
                int safety = 0;
                while (next == waveIndex && safety++ < 12)
                    next = Random.Range(0, simulations.Count);
            }
            else
            {
                next = (waveIndex + 1) % simulations.Count;
            }

            currentWave = simulations[next];
            waveIndex = next;
            ApplyCurrentWaveSorting(true);
            waveTimer = 0f;
            ScheduleNextLayerJump(0f);
            stateTimer = 0f;
            state = RiderState.SwitchingWave;
            renderDepth = currentWave.transform.position.z - 0.02f;

            switchStart = transform.position;
            switchTarget = GetStartingPosition(currentWave);
            switchTarget.z = renderDepth;

            transform.rotation = Quaternion.identity;
            UpdateAnimation(false, true);
        }

        private void UpdateWaveSwitch()
        {
            float t = Mathf.Clamp01(
                stateTimer /
                Mathf.Max(0.01f, switchDuration));

            float eased =
                t * t * (3f - 2f * t);

            Vector3 p = Vector3.Lerp(
                switchStart,
                switchTarget,
                eased);

            float layerDistance =
                Mathf.Abs(
                    switchTarget.y -
                    switchStart.y);

            // Normal changes use a light hop. Glide transfers stay much
            // flatter, reading as a carve carried by the active water push.
            float jump = glideWaveSwitchActive
                ? layerJumpHeight * 0.10f + layerDistance * 0.035f
                : layerJumpHeight * 0.45f + layerDistance * 0.12f;

            p.y += Mathf.Sin(t * Mathf.PI) * jump;

            if (glideWaveSwitchActive && specialSkidding)
            {
                specialSkidTimer = Mathf.Max(0f, specialSkidTimer - Time.deltaTime);
                float skid01 = Mathf.Clamp01(
                    specialSkidTimer / Mathf.Max(0.01f, specialSkidDuration));
                specialSkidCurrentSpeed = specialSkidSpeed *
                    Mathf.SmoothStep(0.15f, 1f, skid01);
            }

            transform.position = p;
            transform.rotation = Quaternion.identity;

            ApplyFacing(
                spriteWorldScale,
                spriteWorldScale);

            if (t < 1f)
                return;

            localRideX =
                playerControlled && lockPlayerToScreenX
                    ? playerScreenX
                    : switchTarget.x;

            if (playerControlled && lockPlayerToScreenX)
                switchTarget.x = playerScreenX;

            transform.position = switchTarget;
            transform.rotation = Quaternion.identity;

            state = RiderState.Riding;
            stateTimer = 0f;

            if (glideWaveSwitchActive)
            {
                if (specialSkidTimer <= 0f)
                {
                    specialSkidding = false;
                    specialSkidCurrentSpeed = 0f;
                    playerHorizontalVelocity = direction *
                        Mathf.Min(playerScrollSpeed, glideWaveSwitchSpeed * 0.2f);
                }
                else
                {
                    // Keep the same push direction and remaining force on the
                    // destination wave.
                    playerHorizontalVelocity = direction * glideWaveSwitchSpeed;
                }
            }

            glideWaveSwitchActive = false;
            glideWaveSwitchSpeed = 0f;

            ApplyFacing(
                spriteWorldScale,
                spriteWorldScale);

            UpdateAnimation(
                Mathf.Abs(playerHorizontalVelocity) > 0.03f,
                true);
        }

        private void ScheduleNextLayerJump(float initialDelay)
        {
            float variation = Random.Range(
                -simulationTimeVariation,
                simulationTimeVariation);

            currentSimulationDuration = Mathf.Max(
                1f,
                secondsPerSimulation + variation);

            // A negative timer creates a unique initial delay without forcing
            // every surfer to jump at scene start.
            waveTimer = -Mathf.Max(0f, initialDelay);
        }

        [ContextMenu("Spawn Randomly In Ocean")]
        public void SpawnAtRandomOceanPosition()
        {
            EndlessWaveSections endless = EndlessWaveSections.Instance;

            float minimumX;
            float maximumX;

            if (endless != null && endless.IsReady)
            {
                minimumX = endless.MinimumWorldX;
                maximumX = endless.MaximumWorldX;
            }
            else
            {
                PixelWaterGPU fallback = Object.FindFirstObjectByType<PixelWaterGPU>();
                if (fallback == null)
                    return;

                minimumX = fallback.TankMinimum.x;
                maximumX = fallback.TankMaximum.x;
            }

            float oceanWidth = Mathf.Max(0.01f, maximumX - minimumX);
            float padding = oceanWidth * Mathf.Clamp(randomSpawnEdgePadding, 0f, 0.45f);
            float left = minimumX + padding;
            float right = maximumX - padding;

            if (left > right)
            {
                float centre = (minimumX + maximumX) * 0.5f;
                left = centre;
                right = centre;
            }

            List<Transform> enemies = CollectSpawnEnemies();
            int attempts = Mathf.Max(1, safeSpawnAttempts);

            PixelWaterGPU bestWave = null;
            Vector3 bestPosition = transform.position;
            float bestX = left;
            float bestClearance = float.NegativeInfinity;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float candidateX = Random.Range(left, right);
                List<PixelWaterGPU> candidateLayers = EndlessWaveSections.LayersNearest(candidateX);
                candidateLayers.RemoveAll(w => w == null || !w.isActiveAndEnabled);
                candidateLayers.Sort((a, b) =>
                    a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));

                if (candidateLayers.Count == 0)
                    continue;

                PixelWaterGPU candidateWave = candidateLayers[Random.Range(0, candidateLayers.Count)];
                candidateX = Mathf.Clamp(
                    candidateX,
                    candidateWave.TankMinimum.x + 0.02f,
                    candidateWave.TankMaximum.x - 0.02f);

                Vector3 candidatePosition = GetStartingPosition(candidateWave);
                candidatePosition.x = candidateX;
                candidatePosition.y = candidateWave.GetGameplaySurfaceHeight(candidateX) + surfaceOffset;
                candidatePosition.z = candidateWave.transform.position.z - 0.02f;

                float clearance = GetNearestEnemyDistance(candidatePosition, enemies);
                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    bestWave = candidateWave;
                    bestPosition = candidatePosition;
                    bestX = candidateX;
                }

                if (clearance >= enemySafeSpawnRadius)
                    break;
            }

            if (bestWave == null)
                return;

            List<PixelWaterGPU> nearbyLayers = EndlessWaveSections.LayersNearest(bestX);
            nearbyLayers.RemoveAll(w => w == null || !w.isActiveAndEnabled);
            nearbyLayers.Sort((a, b) =>
                a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));

            simulations.Clear();
            simulations.AddRange(nearbyLayers);

            waveIndex = simulations.IndexOf(bestWave);
            if (waveIndex < 0)
            {
                simulations.Add(bestWave);
                simulations.Sort((a, b) =>
                    a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
                waveIndex = simulations.IndexOf(bestWave);
            }

            currentWave = bestWave;
            direction = Random.value < 0.5f ? -1f : 1f;
            startMovingRight = direction > 0f;
            localRideX = bestX;
            renderDepth = bestPosition.z;
            state = RiderState.Riding;
            stateTimer = 0f;
            waveTimer = 0f;

            transform.position = bestPosition;
            transform.rotation = Quaternion.identity;
            ApplyCurrentWaveSorting(true);
            UpdateAnimation(false, true);
        }

        private static List<Transform> CollectSpawnEnemies()
        {
            List<Transform> enemies = new();

            foreach (SharkLaneSwimmer shark in
                     Object.FindObjectsByType<SharkLaneSwimmer>(FindObjectsSortMode.None))
            {
                if (shark != null && shark.isActiveAndEnabled)
                    enemies.Add(shark.transform);
            }

            foreach (GiantSquidLaneSwimmer squid in
                     Object.FindObjectsByType<GiantSquidLaneSwimmer>(FindObjectsSortMode.None))
            {
                if (squid != null && squid.isActiveAndEnabled)
                    enemies.Add(squid.transform);
            }

            return enemies;
        }

        private static float GetNearestEnemyDistance(
            Vector3 candidatePosition,
            List<Transform> enemies)
        {
            if (enemies == null || enemies.Count == 0)
                return float.PositiveInfinity;

            float nearestSquared = float.PositiveInfinity;
            Vector2 candidate = candidatePosition;

            foreach (Transform enemy in enemies)
            {
                if (enemy == null)
                    continue;

                float squaredDistance =
                    ((Vector2)enemy.position - candidate).sqrMagnitude;

                if (squaredDistance < nearestSquared)
                    nearestSquared = squaredDistance;
            }

            return Mathf.Sqrt(nearestSquared);
        }

        private void PickWave(int index, bool snap)
        {
            if (simulations.Count == 0) return;

            waveIndex = Mathf.Abs(index) % simulations.Count;
            currentWave = simulations[waveIndex];
            ApplyCurrentWaveSorting(true);
            stateTimer = 0f;
            state = RiderState.Riding;
            renderDepth = currentWave.transform.position.z - 0.02f;

            Vector2 min = currentWave.TankMinimum;
            Vector2 max = currentWave.TankMaximum;
            float width = max.x - min.x;
            localRideX = direction > 0f
                ? min.x + width * (edgePadding + 0.08f)
                : max.x - width * (edgePadding + 0.08f);

            Vector3 start = GetStartingPosition(currentWave);
            start.x = localRideX;
            start.z = renderDepth;
            if (snap) transform.position = start;
        }

        private Vector3 GetStartingPosition(PixelWaterGPU wave)
        {
            Vector2 min = wave.TankMinimum;
            Vector2 max = wave.TankMaximum;
            float x = Mathf.Lerp(min.x, max.x, 0.5f);
            float y = wave.GetGameplaySurfaceHeight(x) + surfaceOffset;
            return new Vector3(x, y, wave.transform.position.z - 0.02f);
        }

        private void EnsurePixelSprite()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            Sprite imported = Resources.Load<Sprite>(surferSpriteResource);

            if (imported == null)
            {
                Sprite[] importedSprites = Resources.LoadAll<Sprite>(surferSpriteResource);
                if (importedSprites != null && importedSprites.Length > 0)
                    imported = importedSprites[0];
            }

            if (imported != null)
            {
                spriteRenderer.sprite = imported;
                spriteRenderer.sortingOrder = surferWithinWaveSortingOrder;
                spriteRenderer.color = Color.white;

                ApplyFacing(
                    spriteWorldScale,
                    spriteWorldScale);

                return;
            }

            Debug.LogWarning(
                $"TinyWaveSurfer could not load Resources/{surferSpriteResource}. " +
                "Falling back to a generated marker.",
                this);

            runtimeTexture = new Texture2D(
                8,
                8,
                TextureFormat.RGBA32,
                false)
            {
                name = "Fallback Tiny Surfer",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[64];

            void Set(int x, int y, Color color)
            {
                if (x < 0 ||
                    x >= 8 ||
                    y < 0 ||
                    y >= 8)
                {
                    return;
                }

                pixels[y * 8 + x] = color;
            }

            for (int x = 1; x <= 6; x++)
                Set(x, 1, boardColor);

            Set(3, 2, bodyColor);
            Set(5, 2, bodyColor);

            Set(3, 3, bodyColor);
            Set(4, 3, bodyColor);

            Set(4, 4, shirtColor);
            Set(4, 5, shirtColor);
            Set(3, 5, shirtColor);
            Set(5, 5, shirtColor);

            Set(2, 5, bodyColor);
            Set(6, 5, bodyColor);

            Set(4, 6, bodyColor);
            Set(4, 7, bodyColor);
            Set(3, 7, bodyColor);

            runtimeTexture.SetPixels(pixels);
            runtimeTexture.Apply(false, false);

            runtimeSprite = Sprite.Create(
                runtimeTexture,
                new Rect(
                    0f,
                    0f,
                    8f,
                    8f),
                new Vector2(
                    0.5f,
                    0.18f),
                8f,
                0,
                SpriteMeshType.FullRect);

            spriteRenderer.sprite =
                runtimeSprite;

            spriteRenderer.sortingOrder =
                surferWithinWaveSortingOrder;

            spriteRenderer.color =
                Color.white;

            ApplyFacing(
                spriteWorldScale,
                spriteWorldScale);
        }
    }

    public static class TinyWaveSurferBootstrap
    {
        private static bool surferSpawned;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PrepareSurferSpawn()
        {
            surferSpawned = false;

            if (Object.FindFirstObjectByType<PixelWaterGPU>() == null)
                return;

            if (Object.FindObjectsByType<TinyWaveSurfer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length > 0)
            {
                return;
            }

            PixelWaterGPU master = Object.FindFirstObjectByType<PixelWaterGPU>();
            if (master != null && !master.SinglePlayerModeEnabled)
            {
                SpawnAIPlayerSurfer();
                return;
            }

            GameObject listenerObject = new GameObject("Player Surfer Spawn Listener");
            listenerObject.AddComponent<TinyWaveSurferSpawnListener>();
        }

        public static void SpawnAIPlayerSurfer()
        {
            if (surferSpawned) return;
            PixelWaterGPU master = Object.FindFirstObjectByType<PixelWaterGPU>();
            if (master == null || master.SinglePlayerModeEnabled) return;
            surferSpawned = true;
            GameObject go = new GameObject("AI Player Surfer");
            TinyWaveSurfer surfer = go.AddComponent<TinyWaveSurfer>();
            surfer.ConfigureGeneratedSurfer(0, true, 0.95f,
                new Color(0.95f, 0.30f, 0.12f, 1f),
                new Color(1f, 0.88f, 0.24f, 1f), 100, 0.35f, 0f);
            surfer.ConfigureAIPlayer(master.SinglePlayerScrollSpeed, master.SinglePlayerBoostMultiplier);
        }

        public static void SpawnPlayerSurfer()
        {
            if (surferSpawned)
                return;

            PixelWaterGPU master =
                Object.FindFirstObjectByType<PixelWaterGPU>();

            if (master == null || !master.SinglePlayerModeEnabled)
                return;

            if (Object.FindObjectsByType<TinyWaveSurfer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length > 0)
            {
                surferSpawned = true;
                return;
            }

            surferSpawned = true;

            GameObject go =
                new GameObject("Tiny 8x8 Surfer 1");

            TinyWaveSurfer surfer =
                go.AddComponent<TinyWaveSurfer>();

            surfer.ConfigureGeneratedSurfer(
                0,
                true,
                0.95f,
                new Color(0.95f, 0.30f, 0.12f, 1f),
                new Color(1f, 0.88f, 0.24f, 1f),
                100,
                1.25f,
                0f);

            surfer.ConfigureSinglePlayer(
                master.SinglePlayerScrollSpeed,
                master.SinglePlayerBoostMultiplier);
        }
    }

    public sealed class TinyWaveSurferSpawnListener : MonoBehaviour
    {
        private bool inputReleasedOnce;

        private void Update()
        {
            if (!inputReleasedOnce)
            {
                if (!AnyControllerButtonHeld())
                    inputReleasedOnce = true;

                return;
            }

            if (!AnyControllerButtonPressed())
                return;

            TinyWaveSurferBootstrap.SpawnPlayerSurfer();
            Destroy(gameObject);
        }

        private static bool AnyControllerButtonPressed()
        {
    #if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.fKey.wasPressedThisFrame ||
                 keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame))
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;

            if (gamepad == null)
                return false;

            return
                gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.buttonNorth.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.buttonWest.wasPressedThisFrame ||
                gamepad.leftShoulder.wasPressedThisFrame ||
                gamepad.rightShoulder.wasPressedThisFrame ||
                gamepad.leftStickButton.wasPressedThisFrame ||
                gamepad.rightStickButton.wasPressedThisFrame ||
                gamepad.selectButton.wasPressedThisFrame ||
                gamepad.dpad.up.wasPressedThisFrame ||
                gamepad.dpad.down.wasPressedThisFrame ||
                gamepad.dpad.left.wasPressedThisFrame ||
                gamepad.dpad.right.wasPressedThisFrame;
    #elif ENABLE_LEGACY_INPUT_MANAGER
            return
                Input.GetKeyDown(KeyCode.F) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.JoystickButton0) ||
                Input.GetKeyDown(KeyCode.JoystickButton1) ||
                Input.GetKeyDown(KeyCode.JoystickButton2) ||
                Input.GetKeyDown(KeyCode.JoystickButton3) ||
                Input.GetKeyDown(KeyCode.JoystickButton4) ||
                Input.GetKeyDown(KeyCode.JoystickButton5) ||
                Input.GetKeyDown(KeyCode.JoystickButton6) ||
                Input.GetKeyDown(KeyCode.JoystickButton7);
    #else
            return false;
    #endif
        }

        private static bool AnyControllerButtonHeld()
        {
    #if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.fKey.isPressed ||
                 keyboard.enterKey.isPressed ||
                 keyboard.numpadEnterKey.isPressed ||
                 keyboard.spaceKey.isPressed))
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;

            if (gamepad == null)
                return false;

            return
                gamepad.buttonSouth.isPressed ||
                gamepad.buttonNorth.isPressed ||
                gamepad.buttonEast.isPressed ||
                gamepad.buttonWest.isPressed ||
                gamepad.leftShoulder.isPressed ||
                gamepad.rightShoulder.isPressed ||
                gamepad.leftStickButton.isPressed ||
                gamepad.rightStickButton.isPressed ||
                gamepad.selectButton.isPressed ||
                gamepad.dpad.up.isPressed ||
                gamepad.dpad.down.isPressed ||
                gamepad.dpad.left.isPressed ||
                gamepad.dpad.right.isPressed;
    #elif ENABLE_LEGACY_INPUT_MANAGER
            return
                Input.GetKey(KeyCode.F) ||
                Input.GetKey(KeyCode.Return) ||
                Input.GetKey(KeyCode.KeypadEnter) ||
                Input.GetKey(KeyCode.Space) ||
                Input.GetKey(KeyCode.JoystickButton0) ||
                Input.GetKey(KeyCode.JoystickButton1) ||
                Input.GetKey(KeyCode.JoystickButton2) ||
                Input.GetKey(KeyCode.JoystickButton3) ||
                Input.GetKey(KeyCode.JoystickButton4) ||
                Input.GetKey(KeyCode.JoystickButton5) ||
                Input.GetKey(KeyCode.JoystickButton6) ||
                Input.GetKey(KeyCode.JoystickButton7);
    #else
            return false;
    #endif
        }
    }    
}
