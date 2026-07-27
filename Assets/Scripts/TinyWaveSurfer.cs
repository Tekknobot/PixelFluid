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
        [SerializeField, Min(0.25f)] private float playerScrollSpeed = 2.4f;
        [SerializeField, Range(1f, 4f)] private float playerBoostMultiplier = 1.75f;
        [Tooltip("World-space padding that keeps the surfer clear of the camera's left and right screen edges.")]
        [SerializeField, Range(0f, 1f)] private float playerCameraEdgePadding = 0.12f;
        [SerializeField] private bool lockPlayerToScreenX = false;
        [SerializeField] private float playerScreenX = 0f;

        [Header("Shark Death Response")]
        [SerializeField, Min(0.25f)] private float deathDuration = 1.6f;
        [SerializeField, Min(0f)] private float deathKnockUp = 0.7f;
        [SerializeField, Min(0f)] private float deathSinkSpeed = 0.65f;
        [SerializeField, Range(90f, 1080f)] private float deathSpinSpeed = 520f;
        [SerializeField] private bool respawnAfterDeath = true;
        [SerializeField, Min(0f)] private float respawnDelay = 0.7f;

        [Header("Death Blood Particles")]
        [SerializeField] private bool emitBloodOnDeath = true;
        [SerializeField, Range(4, 80)] private int deathBloodParticleCount = 64;
        [SerializeField, Min(0.05f)] private float deathBloodLifetime = 2.35f;
        [SerializeField, Min(0f)] private float deathBloodMinSpeed = 0.12f;
        [SerializeField, Min(0f)] private float deathBloodMaxSpeed = 0.48f;
        [SerializeField, Range(0.005f, 0.2f)] private float deathBloodMinSize = 0.018f;
        [SerializeField, Range(0.005f, 0.25f)] private float deathBloodMaxSize = 0.018f;
        [SerializeField] private float deathBloodGravity = 0.01f;
        [SerializeField] private Vector2 deathBloodOffset = new(0f, 0.08f);
        [SerializeField, ColorUsage(true, true)] private Color deathBloodColor = new(1f, 0f, 0f, 1f);

        [Header("Surfer Sprite")]
        [SerializeField] private string surferSpriteResource = "Surfers/chuck";
        [SerializeField, Min(0.05f)] private float spriteWorldScale = 0.65f;
        [SerializeField] private int sortingOrder = 1;
        [Tooltip("Local SpriteRenderer order inside the surfer render queue. Kept below lane creatures so a shark in front can cover this surfer.")]
        [SerializeField] private int surferWithinWaveSortingOrder = 0;

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
        private static readonly int DeathStateHash =
            Animator.StringToHash("chuck_death");

        private bool? animationMoving;

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
        [SerializeField] private AudioClip humanDeathClip;
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
        private float deathTimer;
        private float respawnTimer;
        private Vector2 deathVelocity;
        private Vector3 livingScale;
        private Color livingColor = Color.white;

        public int CurrentWaveIndex => waveIndex;
        public PixelWaterGPU CurrentWave => currentWave;
        public float TravelDirection => direction;
        public bool IsDead => state == RiderState.Dead;
        public bool IsPlayerControlled => playerControlled;

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
            EnsurePixelSprite();
            EnsureSurferAnimator();
            EnsureSharkHitCollider();

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

        private void LateUpdate()
        {
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

            animationMoving = null;
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

            if (!force && animationMoving.HasValue && animationMoving.Value == moving)
                return;

            animationMoving = moving;
            surferAnimator.Play(moving ? MoveStateHash : IdleStateHash, 0, 0f);
        }

        private void PlayDeathAnimation()
        {
            if (surferAnimator == null ||
                !surferAnimator.enabled ||
                surferAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            animationMoving = null;
            surferAnimator.Play(DeathStateHash, 0, 0f);
        }

        public bool DieFromShark(Vector2 sharkPosition)
        {
            if (state == RiderState.Dead)
                return false;

            state = RiderState.Dead;
            PlayDeathAnimation();
            EmitDeathBlood();
            if (humanDeathClip != null && deathAudioSource != null)
                deathAudioSource.PlayOneShot(humanDeathClip);
            deathTimer = 0f;
            respawnTimer = 0f;
            float away = transform.position.x >= sharkPosition.x ? 1f : -1f;
            deathVelocity = new Vector2(away * 0.8f, deathKnockUp);
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
            Color bloodRed = new Color(1f, 0f, 0f, 1f);

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
                new Color(1f, 0f, 0f, 1f);

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

            if (!respawnAfterDeath)
            {
                gameObject.SetActive(false);
                return;
            }

            respawnTimer += dt;
            if (respawnTimer >= respawnDelay)
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

        private void Update()
        {
            if (state == RiderState.Dead)
            {
                UpdateDeathResponse(Time.deltaTime);
                return;
            }

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

            simulations.Clear();
            simulations.AddRange(FindObjectsByType<PixelWaterGPU>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None));

            simulations.RemoveAll(w =>
                w == null ||
                !w.isActiveAndEnabled ||
                w.gameObject == gameObject);

            // The independent simulations keep their GameObject transforms at
            // zero. Their actual row position is stored internally, so sorting
            // by transform Y/Z produces an unstable order. The layer index is
            // the authoritative full wave-stack order:
            // 0 = lowest/foreground, larger = higher/toward the horizon.
            simulations.Sort((a, b) =>
                a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));

            // Preserve the currently ridden wave after rebuilding the list.
            if (previouslySelectedWave != null)
            {
                int selectedIndex = simulations.IndexOf(previouslySelectedWave);
                if (selectedIndex >= 0)
                {
                    waveIndex = selectedIndex;
                    currentWave = previouslySelectedWave;
                }
            }
        }

        private void UpdatePlayerControl(float dt)
        {
            ReadPlayerInput(out float horizontal, out bool jumpHeld,
                out bool layerUpHeld, out bool layerDownHeld, out bool boostHeld);

            if (Mathf.Abs(horizontal) > 0.01f && state == RiderState.Riding)
            {
                direction = Mathf.Sign(horizontal);
                float speed = playerScrollSpeed * (boostHeld ? playerBoostMultiplier : 1f);
                localRideX += horizontal * speed * dt;
            }

            bool moving =
                Mathf.Abs(horizontal) > 0.01f &&
                state == RiderState.Riding;

            UpdateAnimation(moving);

            // This is a finite sandbox: the simulations stay in place and the
            // surfer is confined to the overlap between the current wave and
            // the visible camera viewport.
            localRideX = ClampPlayerXToSandbox(localRideX);

            if (jumpHeld && !previousJumpHeld && state == RiderState.Riding)
                BeginTurnTrick();

            // Depth controls: Up/W moves one row toward the horizon,
            // while Down/S moves one row toward the foreground. Interior presses
            // move exactly one row. Only an outward press on an edge wraps.
            // A completed release is required between layer changes. This prevents
            // a held key, overlapping W/arrow input, or a press during the jump
            // from immediately starting a second wave transition.
            if (!layerUpHeld && !layerDownHeld)
                layerSwitchInputLocked = false;

            if (!layerSwitchInputLocked && state == RiderState.Riding)
            {
                bool upPressed = layerUpHeld && !previousLayerUpHeld;
                bool downPressed = layerDownHeld && !previousLayerDownHeld;

                // Ignore contradictory simultaneous input rather than guessing.
                if (upPressed != downPressed)
                {
                    BeginAdjacentWave(upPressed ? +1 : -1);
                    layerSwitchInputLocked = true;
                }
            }

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

        private void BeginAdjacentWave(int step)
        {
            if (state != RiderState.Riding)
                return;

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
            switchTarget.x = ClampPlayerXToSandbox(localRideX);
            switchTarget.z = renderDepth;
        }

        private float ClampPlayerXToSandbox(float desiredX)
        {
            if (currentWave == null)
                return desiredX;

            Vector2 waveMin = currentWave.TankMinimum;
            Vector2 waveMax = currentWave.TankMaximum;
            float waveWidth = Mathf.Max(0.01f, waveMax.x - waveMin.x);
            float wavePadding = waveWidth * edgePadding;
            float minimumX = waveMin.x + wavePadding;
            float maximumX = waveMax.x - wavePadding;

            Camera camera = Camera.main;
            if (camera == null)
                camera = FindFirstObjectByType<Camera>();

            if (camera != null)
            {
                float halfWidth;
                if (camera.orthographic)
                {
                    halfWidth = camera.orthographicSize * camera.aspect;
                }
                else
                {
                    float distance = Mathf.Abs(camera.transform.position.z - transform.position.z);
                    halfWidth = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                        * distance * camera.aspect;
                }

                float cameraLeft = camera.transform.position.x - halfWidth
                    + playerCameraEdgePadding;
                float cameraRight = camera.transform.position.x + halfWidth
                    - playerCameraEdgePadding;

                minimumX = Mathf.Max(minimumX, cameraLeft);
                maximumX = Mathf.Min(maximumX, cameraRight);
            }

            if (minimumX > maximumX)
                return (minimumX + maximumX) * 0.5f;

            return Mathf.Clamp(desiredX, minimumX, maximumX);
        }

        private static void ReadPlayerInput(out float horizontal, out bool jump,
            out bool layerUp, out bool layerDown, out bool boost)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                jump = keyboard.spaceKey.isPressed;
                layerUp = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                layerDown = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                boost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            jump = Input.GetKey(KeyCode.Space);
            layerUp = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            layerDown = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            horizontal = 0f;
            jump = layerUp = layerDown = boost = false;
#endif
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
                    slope + balanceLean + microPitch),
                1f - Mathf.Exp(-surfaceFollow * 0.7f * dt));
        }
        private void BeginTurnTrick()
        {
            state = RiderState.TurningTrick;
            stateTimer = 0f;
            airStartY = currentWave.GetGameplaySurfaceHeight(localRideX) + surfaceOffset;
            flipTrick = Random.value < flipChance;
        }

        private void UpdateTurnTrick()
        {
            float t = Mathf.Clamp01(
                stateTimer /
                Mathf.Max(0.01f, turnTrickDuration));

            float surfaceY =
                currentWave.GetGameplaySurfaceHeight(localRideX);

            float arc =
                Mathf.Sin(t * Mathf.PI) *
                turnJumpHeight;

            transform.position = new Vector3(
                localRideX,
                Mathf.Max(
                    surfaceY + surfaceOffset,
                    airStartY + arc),
                renderDepth);

            float spinDirection =
                direction >= 0f ? -1f : 1f;

            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                turnSpinDegrees * spinDirection * t);

            float flipAmount = flipTrick
                ? Mathf.Cos(t * Mathf.PI * 2f)
                : 1f;

            float trickScale =
                spriteWorldScale *
                Mathf.Max(
                    0.18f,
                    Mathf.Abs(flipAmount));

            ApplyFacing(
                trickScale,
                spriteWorldScale);

            // Temporarily reverse the visual during the middle of a flip trick.
            if (flipTrick &&
                spriteRenderer != null &&
                flipAmount < 0f)
            {
                spriteRenderer.flipX =
                    !spriteRenderer.flipX;
            }

            if (t < 1f)
                return;

            if (!playerControlled)
                direction *= -1f;

            state = RiderState.Riding;
            stateTimer = 0f;

            transform.rotation = Quaternion.identity;

            ApplyFacing(
                spriteWorldScale,
                spriteWorldScale);
        }

        [ContextMenu("Ride Next Wave")]
        public void BeginNextWave()
        {
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

            float jump =
                layerJumpHeight +
                layerDistance * 0.35f;

            p.y +=
                Mathf.Sin(t * Mathf.PI) *
                jump;

            transform.position = p;

            float spinDirection =
                direction >= 0f ? -1f : 1f;

            transform.rotation = Quaternion.Euler(
                0f,
                0f,
                540f * spinDirection * eased);

            float tuck =
                1f -
                Mathf.Sin(t * Mathf.PI) *
                0.35f;

            ApplyFacing(
                spriteWorldScale * tuck,
                spriteWorldScale * tuck);

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

            ApplyFacing(
                spriteWorldScale,
                spriteWorldScale);
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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateTinySurfers()
        {
            if (Object.FindFirstObjectByType<PixelWaterGPU>() == null)
                return;

            if (Object.FindObjectsByType<TinyWaveSurfer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length > 0)
                return;

            Color[] shirts =
            {
                new(0.95f, 0.30f, 0.12f, 1f),
                new(0.12f, 0.68f, 0.95f, 1f),
                new(0.66f, 0.20f, 0.90f, 1f),
                new(0.15f, 0.85f, 0.42f, 1f),
                new(0.95f, 0.75f, 0.12f, 1f),
                new(0.95f, 0.25f, 0.62f, 1f)
            };

            Color[] boards =
            {
                new(1f, 0.88f, 0.24f, 1f),
                new(0.95f, 0.95f, 1f, 1f),
                new(0.20f, 0.95f, 0.85f, 1f),
                new(1f, 0.42f, 0.18f, 1f),
                new(0.45f, 0.85f, 1f, 1f),
                new(0.85f, 0.95f, 0.28f, 1f)
            };

            PixelWaterGPU master = Object.FindFirstObjectByType<PixelWaterGPU>();
            bool singlePlayer = master != null && master.SinglePlayerModeEnabled;
            int surferCount = singlePlayer ? 1 : 6;
            for (int i = 0; i < surferCount; i++)
            {
                GameObject go = new($"Tiny 8x8 Surfer {i + 1}");
                TinyWaveSurfer surfer = go.AddComponent<TinyWaveSurfer>();
                surfer.ConfigureGeneratedSurfer(
                    i,
                    (i & 1) == 0,
                    0.95f + i * 0.11f,
                    shirts[i],
                    boards[i],
                    100 + i,
                    1.25f + i * 1.85f,
                    (i - 2.5f) * 0.55f);

                if (singlePlayer)
                    surfer.ConfigureSinglePlayer(
                        master.SinglePlayerScrollSpeed,
                        master.SinglePlayerBoostMultiplier);
            }
        }
    }
}
