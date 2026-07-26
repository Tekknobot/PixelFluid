using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    /// <summary>
    /// A real 3D surfboard constrained to the game's 2D X/Y plane.
    /// The visual mesh is fully procedural, while a compound 3D collider and
    /// three-point buoyancy system interact with the GPU particle ocean.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SurfboardController : MonoBehaviour
    {
        private BeachGameController game;
        private Rigidbody body;
        private Transform visualRoot;
        private MeshFilter hullFilter;
        private MeshRenderer hullRenderer;
        private MeshFilter stripeFilter;
        private MeshRenderer stripeRenderer;

        private readonly List<GameObject> generatedFins = new();
        private readonly List<Collider> generatedColliders = new();

        private float rawHorizontal;
        private float smoothedHorizontal;
        private float currentRide;
        private int facingDirection = 1;

        private float visualYaw;
        private float visualYawVelocity;
        private float visualRoll;
        private float visualRollVelocity;
        private float visualPitch;
        private float visualPitchVelocity;

        [Header("3D Board Shape")]
        [SerializeField, Range(1.1f, 3.2f)] private float boardLength = 1.85f;
        [SerializeField, Range(0.28f, 0.85f)] private float boardWidth = 0.52f;
        [SerializeField, Range(0.06f, 0.24f)] private float boardThickness = 0.115f;
        [SerializeField, Range(0.05f, 0.95f)] private float noseWidth = 0.68f;
        [SerializeField, Range(0.05f, 0.95f)] private float tailWidth = 0.44f;
        [SerializeField, Range(0f, 0.32f)] private float rocker = 0.095f;
        [SerializeField, Range(0f, 1f)] private float railRoundness = 0.72f;
        [SerializeField, Range(0f, 0.10f)] private float bottomConcave = 0.025f;
        [SerializeField, Range(0f, 0.18f)] private float deckDome = 0.035f;
        [SerializeField, Range(12, 48)] private int lengthSegments = 30;
        [SerializeField, Range(8, 24)] private int radialSegments = 14;

        [Header("Presentation")]
        [SerializeField] private Color boardColour = new(0.93f, 0.96f, 0.98f, 1f);
        [SerializeField] private Color railColour = new(0.72f, 0.84f, 0.90f, 1f);
        [SerializeField] private Color stripeColour = new(0.08f, 0.34f, 0.48f, 1f);
        [SerializeField, Range(0.01f, 0.18f)] private float stripeWidth = 0.055f;
        [SerializeField, Range(0f, 25f)] private float cameraFacingTilt = 11f;
        [SerializeField, Range(0.05f, 1.2f)] private float turnDuration = 0.38f;
        [SerializeField, Range(0f, 28f)] private float steeringRoll = 9f;
        [SerializeField, Range(0f, 22f)] private float velocityPitch = 7f;
        [SerializeField, Range(0f, 18f)] private float wavePitch = 5f;
        [SerializeField, Range(0f, 0.5f)] private float visualRotationSmoothing = 0.16f;
        [SerializeField, Range(0f, 0.35f)] private float wetDarkening = 0.12f;
        [SerializeField, Range(0f, 1f)] private float materialSmoothness = 0.82f;

        [Header("Fins")]
        [SerializeField, Range(0, 4)] private int finCount = 3;
        [SerializeField, Range(0.03f, 0.24f)] private float finHeight = 0.105f;
        [SerializeField, Range(0.04f, 0.30f)] private float finLength = 0.16f;
        [SerializeField, Range(0.01f, 0.08f)] private float finThickness = 0.025f;
        [SerializeField] private Color finColour = new(0.08f, 0.11f, 0.13f, 1f);

        [Header("Board Physics")]
        [SerializeField] private float boardMass = 1.15f;
        [SerializeField] private float buoyancy = 28f;
        [SerializeField] private float waterDrag = 4.2f;
        [SerializeField] private float angularWaterDrag = 6.6f;
        [SerializeField] private float steeringForce = 1.15f;
        [SerializeField] private float waveCarry = 0.30f;
        [SerializeField] private float maxLinearSpeed = 6.2f;
        [SerializeField] private float maxAngularSpeedDegrees = 48f;

        [Header("Stability")]
        [SerializeField] private float inputResponsiveness = 2.8f;
        [SerializeField] private float uprightTorque = 5.8f;
        [SerializeField] private float maximumPitch = 30f;
        [SerializeField] private float verticalVelocityDamping = 0.86f;
        [SerializeField] private float directionFlipSpeed = 0.55f;

        [Header("Water Contact")]
        [SerializeField, Range(0.35f, 0.88f)] private float buoyancySampleSpread = 0.61f;
        [SerializeField, Range(0.01f, 0.12f)] private float targetSubmersion = 0.045f;
        [SerializeField] private float maximumBuoyancyPerPoint = 18f;
        [SerializeField, Range(0.03f, 0.30f)] private float waterSampleSmoothing = 0.12f;

        [Header("Natural Float")]
        [SerializeField, Range(0.01f, 0.20f)] private float neutralFloatDepth = 0.055f;
        [SerializeField, Range(0f, 20f)] private float heaveDamping = 6.5f;
        [SerializeField, Range(0f, 12f)] private float surfaceFollowStrength = 3.8f;
        [SerializeField, Range(3, 7)] private int buoyancyPointCount = 5;
        [SerializeField, Range(0.35f, 0.92f)] private float buoyancyLengthCoverage = 0.78f;

        [Header("Particle Water Contact")]
        [SerializeField, Range(0f, 0.22f)] private float particleContactDepth = 0.012f;
        [SerializeField, Range(0f, 0.16f)] private float particleContactPadding = 0.002f;
        [SerializeField, Range(-0.15f, 0.10f)] private float visualWaterlineOffset = -0.060f;

        [Header("Sand Collision")]
        [SerializeField] private bool collideWithSand = true;
        [SerializeField, Range(0f, 0.15f)] private float sandClearance = 0.018f;
        [SerializeField, Range(1f, 80f)] private float sandCollisionStiffness = 32f;
        [SerializeField, Range(0f, 20f)] private float sandCollisionDamping = 7.5f;
        [SerializeField, Range(0f, 10f)] private float sandFriction = 3.2f;
        [SerializeField, Range(0.3f, 0.9f)] private float sandSampleSpread = 0.68f;
        [SerializeField, Range(0.01f, 0.30f)] private float maximumSandCorrection = 0.10f;

        private float HalfLength => boardLength * 0.5f;
        private float HalfThickness => boardThickness * 0.5f;

        public float BoardLength => boardLength;
        public float BoardWidth => boardWidth;
        public float BoardThickness => boardThickness;

        /// <summary>
        /// Applies new board dimensions and immediately rebuilds the 3D mesh,
        /// fins, colliders, buoyancy spacing and GPU-water contact extents.
        /// </summary>
        public void ApplyShape(
            float length,
            float width,
            float thickness,
            float noseWidthValue,
            float tailWidthValue,
            float rockerAmount,
            float railRoundnessValue,
            float concave,
            float dome,
            int fins,
            float finSize,
            float viewTilt,
            Color baseColour,
            Color accentColour,
            float accentWidth)
        {
            ConfigureShape(
                length,
                width,
                thickness,
                noseWidthValue,
                tailWidthValue,
                rockerAmount,
                railRoundnessValue,
                concave,
                dome,
                fins,
                finSize,
                viewTilt,
                baseColour,
                accentColour,
                accentWidth);

            if (visualRoot == null)
                return;

            visualRoot.localRotation = Quaternion.Euler(
                visualPitch,
                visualYaw,
                visualRoll);

            RebuildBoardMesh();
            RebuildCompoundColliders();
            RebuildFins();

            if (game != null && game.Water != null && body != null)
            {
                RegisterWaterParticleContact();
            }
        }

        public void ConfigureShape(
            float length,
            float width,
            float thickness,
            float noseWidthValue,
            float tailWidthValue,
            float rockerAmount,
            float railRoundnessValue,
            float concave,
            float dome,
            int fins,
            float finSize,
            float viewTilt,
            Color baseColour,
            Color accentColour,
            float accentWidth)
        {
            boardLength = Mathf.Clamp(length, 1.1f, 3.2f);
            boardWidth = Mathf.Clamp(width, 0.28f, 0.85f);
            boardThickness = Mathf.Clamp(thickness, 0.06f, 0.24f);
            noseWidth = Mathf.Clamp01(noseWidthValue);
            tailWidth = Mathf.Clamp01(tailWidthValue);
            rocker = Mathf.Clamp(rockerAmount, 0f, 0.32f);
            railRoundness = Mathf.Clamp01(railRoundnessValue);
            bottomConcave = Mathf.Clamp(concave, 0f, 0.10f);
            deckDome = Mathf.Clamp(dome, 0f, 0.18f);
            finCount = Mathf.Clamp(fins, 0, 4);
            finHeight = Mathf.Clamp(finSize, 0.03f, 0.24f);
            cameraFacingTilt = Mathf.Clamp(viewTilt, 0f, 25f);
            boardColour = baseColour;
            stripeColour = accentColour;
            stripeWidth = Mathf.Clamp(accentWidth, 0.01f, 0.18f);
        }

        public void Initialise(BeachGameController controller)
        {
            game = controller;
            body = GetComponent<Rigidbody>();

            body.mass = boardMass;
            body.useGravity = true;
            body.linearDamping = 1.05f;
            body.angularDamping = 2.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints =
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY;
            body.maxAngularVelocity = maxAngularSpeedDegrees * Mathf.Deg2Rad;

            BuildVisualHierarchy();
            RebuildBoardMesh();
            RebuildCompoundColliders();
            RebuildFins();

            RegisterWaterParticleContact();

            float surface = game.Water.GetGameplaySurfaceHeight(transform.position.x);
            transform.position = new Vector3(
                transform.position.x,
                surface + HalfThickness - neutralFloatDepth + visualWaterlineOffset,
                -0.55f);
        }

        private void BuildVisualHierarchy()
        {
            visualRoot = new GameObject("Realistic 3D Board Visual").transform;
            visualRoot.SetParent(transform, false);

            visualYaw = facingDirection < 0 ? 180f : 0f;
            visualPitch = cameraFacingTilt;
            visualRoll = 0f;
            visualRoot.localRotation = Quaternion.Euler(visualPitch, visualYaw, visualRoll);

            GameObject hull = new("Fibreglass Hull");
            hull.transform.SetParent(visualRoot, false);
            hullFilter = hull.AddComponent<MeshFilter>();
            hullRenderer = hull.AddComponent<MeshRenderer>();
            hullRenderer.sharedMaterial = CreateLitMaterial("Board Fibreglass", boardColour, materialSmoothness);

            GameObject stripe = new("Deck Stringer");
            stripe.transform.SetParent(visualRoot, false);
            stripeFilter = stripe.AddComponent<MeshFilter>();
            stripeRenderer = stripe.AddComponent<MeshRenderer>();
            stripeRenderer.sharedMaterial = CreateLitMaterial("Board Stringer", stripeColour, 0.72f);
        }

        private Material CreateLitMaterial(string materialName, Color colour, float smoothness)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");

            Material material = new(shader) { name = materialName };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", colour);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0.02f);

            return material;
        }

        public void RebuildBoardMesh()
        {
            if (hullFilter == null)
                return;

            if (hullFilter.sharedMesh != null)
                Destroy(hullFilter.sharedMesh);
            if (stripeFilter.sharedMesh != null)
                Destroy(stripeFilter.sharedMesh);

            hullFilter.sharedMesh = GenerateHullMesh();
            stripeFilter.sharedMesh = GenerateStripeMesh();
        }

        private Mesh GenerateHullMesh()
        {
            int rings = lengthSegments + 1;
            int ringSize = radialSegments;
            Vector3[] vertices = new Vector3[rings * ringSize];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[lengthSegments * radialSegments * 6];

            for (int i = 0; i < rings; i++)
            {
                float u = i / (float)lengthSegments;
                float longitudinal = u * 2f - 1f;
                float x = longitudinal * HalfLength;

                float fullness = longitudinal >= 0f ? noseWidth : tailWidth;
                float exponent = Mathf.Lerp(2.8f, 0.58f, fullness);
                float outline = Mathf.Pow(Mathf.Max(0.001f, Mathf.Sin(Mathf.PI * u)), exponent);
                float localHalfWidth = Mathf.Max(0.008f, boardWidth * 0.5f * outline);

                float endLift = rocker * longitudinal * longitudinal;
                float noseExtra = longitudinal > 0f
                    ? rocker * 0.55f * Mathf.Pow(longitudinal, 4f)
                    : rocker * 0.20f * Mathf.Pow(-longitudinal, 4f);
                float centreY = endLift + noseExtra;

                for (int r = 0; r < ringSize; r++)
                {
                    float angle = r / (float)ringSize * Mathf.PI * 2f;
                    float side = Mathf.Cos(angle);
                    float vertical = Mathf.Sin(angle);

                    float roundedVertical = Mathf.Sign(vertical) *
                        Mathf.Pow(Mathf.Abs(vertical), Mathf.Lerp(0.72f, 1.25f, railRoundness));

                    float z = side * localHalfWidth;
                    float y = roundedVertical * HalfThickness;

                    if (vertical > 0f)
                    {
                        float centreMask = 1f - Mathf.Clamp01(Mathf.Abs(z) / Mathf.Max(0.001f, localHalfWidth));
                        y += deckDome * centreMask * vertical;
                    }
                    else
                    {
                        float centreMask = 1f - Mathf.Clamp01(Mathf.Abs(z) / Mathf.Max(0.001f, localHalfWidth));
                        y -= bottomConcave * centreMask * -vertical;
                    }

                    int index = i * ringSize + r;
                    vertices[index] = new Vector3(x, centreY + y, z);
                    uv[index] = new Vector2(u, r / (float)ringSize);
                }
            }

            int tri = 0;
            for (int i = 0; i < lengthSegments; i++)
            {
                for (int r = 0; r < radialSegments; r++)
                {
                    int nextR = (r + 1) % radialSegments;
                    int a = i * radialSegments + r;
                    int b = i * radialSegments + nextR;
                    int c = (i + 1) * radialSegments + r;
                    int d = (i + 1) * radialSegments + nextR;

                    triangles[tri++] = a;
                    triangles[tri++] = c;
                    triangles[tri++] = b;
                    triangles[tri++] = b;
                    triangles[tri++] = c;
                    triangles[tri++] = d;
                }
            }

            Mesh mesh = new() { name = "Procedural Realistic Surfboard Hull" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private Mesh GenerateStripeMesh()
        {
            int segments = lengthSegments;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float longitudinal = u * 2f - 1f;
                float x = longitudinal * HalfLength * 0.94f;
                float centreY =
                    rocker * longitudinal * longitudinal +
                    (longitudinal > 0f
                        ? rocker * 0.55f * Mathf.Pow(longitudinal, 4f)
                        : rocker * 0.20f * Mathf.Pow(-longitudinal, 4f));

                float y = centreY + HalfThickness + deckDome + 0.004f;
                int index = i * 2;
                vertices[index] = new Vector3(x, y, -stripeWidth * 0.5f);
                vertices[index + 1] = new Vector3(x, y, stripeWidth * 0.5f);
                uv[index] = new Vector2(u, 0f);
                uv[index + 1] = new Vector2(u, 1f);
            }

            int tri = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles[tri++] = a;
                triangles[tri++] = b;
                triangles[tri++] = c;
                triangles[tri++] = c;
                triangles[tri++] = b;
                triangles[tri++] = d;
            }

            Mesh mesh = new() { name = "Procedural Board Stringer" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void RebuildCompoundColliders()
        {
            foreach (Collider collider in generatedColliders)
                if (collider != null) Destroy(collider);
            generatedColliders.Clear();

            AddHullCollider(-boardLength * 0.30f, boardLength * 0.30f, tailWidth);
            AddHullCollider(0f, boardLength * 0.42f, 1f);
            AddHullCollider(boardLength * 0.30f, boardLength * 0.30f, noseWidth);
        }

        private void AddHullCollider(float x, float length, float widthScale)
        {
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(x, rocker * Mathf.Pow(x / Mathf.Max(0.01f, HalfLength), 2f), 0f);
            collider.size = new Vector3(
                Mathf.Max(0.12f, length),
                boardThickness * 0.72f,
                Mathf.Max(0.10f, boardWidth * Mathf.Lerp(0.45f, 0.92f, widthScale)));
            generatedColliders.Add(collider);
        }

        private void RebuildFins()
        {
            foreach (GameObject fin in generatedFins)
                if (fin != null) Destroy(fin);
            generatedFins.Clear();

            if (finCount <= 0 || visualRoot == null)
                return;

            float tailX = -boardLength * 0.28f;
            float spacing = boardWidth * 0.23f;

            for (int i = 0; i < finCount; i++)
            {
                float z;
                if (finCount == 1) z = 0f;
                else z = Mathf.Lerp(-spacing, spacing, i / (float)(finCount - 1));

                GameObject fin = new($"Fin {i + 1}");
                fin.transform.SetParent(visualRoot, false);
                fin.transform.localPosition = new Vector3(tailX, -HalfThickness - finHeight * 0.45f, z);
                fin.transform.localRotation = Quaternion.Euler(0f, i == 0 ? -4f : i == finCount - 1 ? 4f : 0f, -8f);

                MeshFilter filter = fin.AddComponent<MeshFilter>();
                filter.sharedMesh = GenerateFinMesh();
                MeshRenderer renderer = fin.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = CreateLitMaterial("Fin Material", finColour, 0.55f);
                generatedFins.Add(fin);
            }
        }

        private Mesh GenerateFinMesh()
        {
            float l = finLength;
            float h = finHeight;
            float t = finThickness * 0.5f;

            Vector3[] vertices =
            {
                new(-l * 0.5f, 0f, -t), new(l * 0.5f, 0f, -t), new(-l * 0.15f, -h, -t),
                new(-l * 0.5f, 0f, t),  new(l * 0.5f, 0f, t),  new(-l * 0.15f, -h, t)
            };
            int[] triangles =
            {
                0,2,1, 3,4,5,
                0,1,4, 0,4,3,
                1,2,5, 1,5,4,
                2,0,3, 2,3,5
            };

            Mesh mesh = new() { name = "Procedural Surfboard Fin" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void RegisterWaterParticleContact()
        {
            if (game == null || game.Water == null || body == null)
                return;

            // The particle collider is deliberately deeper than the visible hull.
            // This lets visible water particles wrap against the rails/underside
            // rather than stopping below the board.
            // Keep the particle envelope very close to the visible hull.
            // Only extend slightly below the underside so particles visually
            // meet the rails instead of forming a large empty halo.
            Vector2 localCentreOffset = new(
                0f,
                -particleContactDepth * 0.5f);

            Vector2 particleHalfExtents = new(
                Mathf.Max(0.01f, HalfLength),
                Mathf.Max(
                    0.01f,
                    HalfThickness + particleContactDepth * 0.5f));

            game.Water.RegisterSurfboard(
                transform,
                body,
                particleHalfExtents,
                localCentreOffset);
        }

        private void OnDestroy()
        {
            if (game != null && game.Water != null)
                game.Water.UnregisterSurfboard(transform);
        }

        private void Update()
        {
            ReadInput();

            smoothedHorizontal = Mathf.MoveTowards(
                smoothedHorizontal,
                rawHorizontal,
                inputResponsiveness * Time.deltaTime);

            UpdateFacingDirection();

            if (game == null || body == null)
                return;

            float rideSpeed = Mathf.Abs(body.linearVelocity.x);
            Vector2 waveVelocity = game.Water.GetGameplayWaveVelocity(transform.position.x);
            bool riding = rideSpeed > 1.15f && Mathf.Abs(waveVelocity.x) > 1.55f;

            if (riding)
            {
                currentRide += Time.deltaTime * Mathf.Max(1f, rideSpeed);
                game.SetRideScore(currentRide);
            }
            else if (currentRide > 0f)
            {
                game.EndRide(currentRide);
                currentRide = 0f;
            }
        }

        private void UpdateFacingDirection()
        {
            float intent = Mathf.Abs(smoothedHorizontal) > 0.15f
                ? smoothedHorizontal
                : body != null ? body.linearVelocity.x : 0f;

            if (Mathf.Abs(intent) >= directionFlipSpeed)
                facingDirection = intent >= 0f ? 1 : -1;

            UpdateVisualTurn();
        }

        private void UpdateVisualTurn()
        {
            if (visualRoot == null)
                return;

            float targetYaw = facingDirection < 0 ? 180f : 0f;

            // Smoothly rotate through the turn instead of snapping or mirroring.
            visualYaw = Mathf.SmoothDampAngle(
                visualYaw,
                targetYaw,
                ref visualYawVelocity,
                Mathf.Max(0.05f, turnDuration));

            float speedX = body != null ? body.linearVelocity.x : 0f;
            float speedY = body != null ? body.linearVelocity.y : 0f;
            Vector2 waveVelocity = game != null && game.Water != null
                ? game.Water.GetGameplayWaveVelocity(transform.position.x)
                : Vector2.zero;

            float directionSign = facingDirection < 0 ? -1f : 1f;

            // Bank toward the steering input. Reverse the sign when the board faces left,
            // so the near rail still dips naturally from the camera's perspective.
            float targetRoll =
                -smoothedHorizontal * steeringRoll * directionSign;

            // Pitch is presentation-only: vertical board speed and the local wave motion
            // reveal the deck, nose rocker and fins without changing the 2D physics plane.
            float speedPitch = Mathf.Clamp(
                -speedY * velocityPitch,
                -velocityPitch,
                velocityPitch);

            float localWavePitch = Mathf.Clamp(
                waveVelocity.y * wavePitch,
                -wavePitch,
                wavePitch);

            float targetPitch = cameraFacingTilt + speedPitch + localWavePitch;

            visualRoll = Mathf.SmoothDampAngle(
                visualRoll,
                targetRoll,
                ref visualRollVelocity,
                Mathf.Max(0.04f, visualRotationSmoothing));

            visualPitch = Mathf.SmoothDampAngle(
                visualPitch,
                targetPitch,
                ref visualPitchVelocity,
                Mathf.Max(0.04f, visualRotationSmoothing));

            visualRoot.localRotation = Quaternion.Euler(
                visualPitch,
                visualYaw,
                visualRoll);
        }

        private void FixedUpdate()
        {
            if (game == null || game.Water == null || body == null)
                return;

            int sampleCount = Mathf.Clamp(buoyancyPointCount, 3, 7);
            float coverage = HalfLength * buoyancyLengthCoverage;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount == 1 ? 0.5f : i / (float)(sampleCount - 1);
                float localX = Mathf.Lerp(-coverage, coverage, t);
                ApplyBuoyancyAtLocalPoint(
                    new Vector3(localX, -HalfThickness, 0f),
                    sampleCount);
            }

            if (collideWithSand)
                ApplySandCollision();

            Vector2 waveVelocity = game.Water.GetGameplayWaveVelocity(body.position.x);

            body.AddForce(
                new Vector3(smoothedHorizontal * steeringForce, 0f, 0f),
                ForceMode.Force);

            body.AddForce(
                new Vector3(waveVelocity.x * waveCarry, 0f, 0f),
                ForceMode.Force);

            ApplyStability();

            Vector3 velocity = body.linearVelocity;
            velocity.y *= verticalVelocityDamping;
            velocity.z = 0f;
            if (velocity.magnitude > maxLinearSpeed)
                velocity = velocity.normalized * maxLinearSpeed;
            body.linearVelocity = velocity;

            Vector3 angular = body.angularVelocity;
            angular.x = 0f;
            angular.y = 0f;
            angular.z = Mathf.Clamp(
                angular.z,
                -maxAngularSpeedDegrees * Mathf.Deg2Rad,
                maxAngularSpeedDegrees * Mathf.Deg2Rad);
            body.angularVelocity = angular;

            Vector3 position = body.position;
            position.x = Mathf.Clamp(
                position.x,
                game.Water.TankMinimum.x + HalfLength,
                game.Water.TankMaximum.x - HalfLength * 0.4f);
            position.z = -0.55f;
            body.position = position;
        }

        private void ApplyStability()
        {
            float angle = Mathf.DeltaAngle(transform.eulerAngles.z, 0f);
            float correctiveTorque =
                -angle * uprightTorque * Mathf.Deg2Rad -
                body.angularVelocity.z * angularWaterDrag;

            body.AddTorque(new Vector3(0f, 0f, correctiveTorque), ForceMode.Force);

            if (Mathf.Abs(angle) > maximumPitch)
            {
                float clamped = Mathf.Clamp(angle, -maximumPitch, maximumPitch);
                Quaternion target = Quaternion.Euler(0f, 0f, clamped);
                body.MoveRotation(Quaternion.Slerp(
                    body.rotation,
                    target,
                    7f * Time.fixedDeltaTime));
                body.angularVelocity *= 0.55f;
            }
        }

        private void ApplyBuoyancyAtLocalPoint(
            Vector3 localPoint,
            int totalSampleCount)
        {
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            float waterSurface = game.Water.GetGameplaySurfaceHeight(worldPoint.x);
            float depth = waterSurface - worldPoint.y;

            if (depth <= 0f)
                return;

            Vector2 sampledWater = game.Water.GetGameplayWaveVelocity(worldPoint.x);
            Vector3 waterVelocity = new(sampledWater.x, sampledWater.y, 0f);
            Vector3 pointVelocity = body.GetPointVelocity(worldPoint);
            Vector3 relativeVelocity = waterVelocity - pointVelocity;

            float desiredDepth = Mathf.Max(0.01f, neutralFloatDepth);
            float depthRatio = Mathf.Clamp01(depth / desiredDepth);

            // At the desired waterline, all sample points together support the
            // board's full weight. Extra depth adds restoring force.
            float boardWeight = body.mass * Mathf.Abs(Physics.gravity.y);
            float neutralSupportPerPoint =
                boardWeight / Mathf.Max(1, totalSampleCount);

            float springLift =
                Mathf.Max(0f, depth - desiredDepth) * buoyancy;

            float verticalDamping =
                Mathf.Max(0f, -relativeVelocity.y) * heaveDamping;

            float waveFollow =
                Mathf.Max(0f, sampledWater.y) * surfaceFollowStrength;

            float lift =
                neutralSupportPerPoint * depthRatio +
                springLift +
                verticalDamping +
                waveFollow;

            lift = Mathf.Min(lift, maximumBuoyancyPerPoint);

            Vector3 horizontalDrag = new(
                relativeVelocity.x * waterDrag * depthRatio,
                0f,
                0f);

            Vector3 force =
                Vector3.up * lift +
                horizontalDrag;

            force.z = 0f;
            body.AddForceAtPosition(force, worldPoint, ForceMode.Force);
        }

        private void ApplySandCollision()
        {
            float inset = HalfLength * sandSampleSpread;
            float deepestPenetration = 0f;

            deepestPenetration = Mathf.Max(
                deepestPenetration,
                ApplySandContactAtLocalPoint(new Vector3(-inset, -HalfThickness, 0f)));
            deepestPenetration = Mathf.Max(
                deepestPenetration,
                ApplySandContactAtLocalPoint(new Vector3(0f, -HalfThickness, 0f)));
            deepestPenetration = Mathf.Max(
                deepestPenetration,
                ApplySandContactAtLocalPoint(new Vector3(inset, -HalfThickness, 0f)));

            // Prevent a fast-moving board from tunnelling through the procedural beach.
            if (deepestPenetration > 0f)
            {
                float correction = Mathf.Min(
                    deepestPenetration,
                    maximumSandCorrection);

                Vector3 corrected = body.position;
                corrected.y += correction;
                body.position = corrected;

                Vector3 velocity = body.linearVelocity;
                if (velocity.y < 0f)
                    velocity.y *= 0.18f;
                body.linearVelocity = velocity;
            }
        }

        private float ApplySandContactAtLocalPoint(Vector3 localPoint)
        {
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            float seabed = game.Water.GetSeabedHeightAtWorldX(worldPoint.x);
            float penetration = seabed + sandClearance - worldPoint.y;

            if (penetration <= 0f)
                return 0f;

            Vector3 pointVelocity = body.GetPointVelocity(worldPoint);

            float upwardForce =
                penetration * sandCollisionStiffness -
                pointVelocity.y * sandCollisionDamping;

            upwardForce = Mathf.Max(0f, upwardForce);

            Vector3 contactForce = new(
                -pointVelocity.x * sandFriction,
                upwardForce,
                0f);

            body.AddForceAtPosition(
                contactForce,
                worldPoint,
                ForceMode.Force);

            return penetration;
        }

        private void ReadInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                rawHorizontal = 0f;
                return;
            }

            rawHorizontal =
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
#else
            rawHorizontal = Input.GetAxisRaw("Horizontal");
#endif
        }
    }
}
