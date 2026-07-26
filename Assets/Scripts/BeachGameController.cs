using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    /// <summary>
    /// Minimal scene layer: simulated water, particle beach and an optionally spawned surfboard.
    /// </summary>
    public sealed class BeachGameController : MonoBehaviour
    {
        private PixelWaterGPU water;
        private SurfboardController board;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private float bestRide;
        private float rideScore;
        private float sceneStartTime;
        private string message = "Press B to place the surfboard on the water";

        [Header("Board Spawning")]
        [SerializeField] private bool spawnBoardOnStart = false;
        [SerializeField, Range(0f, 15f)] private float automaticSpawnDelay = 2f;
        [SerializeField, Range(0.05f, 0.95f)] private float spawnPositionAcrossOcean = 0.63f;
        [SerializeField, Range(-0.25f, 0.5f)] private float spawnHeightOffset = 0.03f;
        [SerializeField] private bool allowRespawnWithB = true;

        public enum BoardPreset
        {
            Longboard,
            Shortboard,
            Fish,
            Funboard,
            Gun,
            Custom
        }

        [Header("3D Board Preset")]
        [SerializeField] private BoardPreset boardPreset = BoardPreset.Custom;
        [SerializeField] private bool applyPresetOnSpawn = false;

        [Header("3D Board Shape")]
        [SerializeField, Range(1.1f, 3.2f)] private float boardLength = 1.85f;
        [SerializeField, Range(0.28f, 0.85f)] private float boardWidth = 0.52f;
        [SerializeField, Range(0.06f, 0.24f)] private float boardThickness = 0.115f;
        [SerializeField, Range(0.05f, 0.95f)] private float noseWidth = 0.68f;
        [SerializeField, Range(0.05f, 0.95f)] private float tailWidth = 0.44f;
        [SerializeField, Range(0f, 0.32f)] private float boardRocker = 0.095f;
        [SerializeField, Range(0f, 1f)] private float railRoundness = 0.72f;
        [SerializeField, Range(0f, 0.10f)] private float bottomConcave = 0.025f;
        [SerializeField, Range(0f, 0.18f)] private float deckDome = 0.035f;

        [Header("3D Board Fins / View")]
        [SerializeField, Range(0, 4)] private int finCount = 3;
        [SerializeField, Range(0.03f, 0.24f)] private float finSize = 0.105f;
        [SerializeField, Range(0f, 25f)] private float cameraFacingTilt = 11f;

        [Header("3D Board Material")]
        [SerializeField] private Color boardColour = new(0.93f, 0.96f, 0.98f, 1f);
        [SerializeField] private Color stripeColour = new(0.08f, 0.34f, 0.48f, 1f);
        [SerializeField, Range(0.01f, 0.18f)] private float stripeWidth = 0.055f;

        [Header("Inspector Editing")]
        [SerializeField] private bool updateSpawnedBoardLive = true;
        [SerializeField, Range(0.05f, 1f)] private float liveUpdateInterval = 0.15f;

        private float nextShapeUpdateTime;
        private int lastShapeHash;

        public PixelWaterGPU Water => water;

        private void Awake()
        {
            water = FindAnyObjectByType<PixelWaterGPU>();
            if (water == null)
            {
                Debug.LogError("Beach Game requires PixelWaterGPU in the scene.", this);
                enabled = false;
                return;
            }

            sceneStartTime = Time.time;
            RemoveOldPrototypeObjects();
            ConfigureCamera(null);
        }

        private void Update()
        {
            if (water == null)
                return;

            if (updateSpawnedBoardLive && board != null &&
                Time.unscaledTime >= nextShapeUpdateTime)
            {
                nextShapeUpdateTime = Time.unscaledTime + liveUpdateInterval;
                ApplyInspectorShapeIfChanged();
            }

            bool shouldAutoSpawn =
                spawnBoardOnStart &&
                board == null &&
                Time.time >= sceneStartTime + automaticSpawnDelay;

            bool pressedSpawn = SpawnKeyPressed();

            if (shouldAutoSpawn || pressedSpawn)
            {
                if (board != null && allowRespawnWithB)
                    Destroy(board.gameObject);

                if (board == null || allowRespawnWithB)
                    CreateBoard();
            }
        }

        private bool SpawnKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.B);
#endif
        }

        private void RemoveOldPrototypeObjects()
        {
            BeachPlayer oldPlayer = FindAnyObjectByType<BeachPlayer>();
            if (oldPlayer != null) Destroy(oldPlayer.gameObject);

            SurfboardPickup oldPickup = FindAnyObjectByType<SurfboardPickup>();
            if (oldPickup != null) Destroy(oldPickup.gameObject);

            foreach (BeachShell shell in FindObjectsByType<BeachShell>(FindObjectsInactive.Exclude))
                Destroy(shell.gameObject);

            SurfboardController existingBoard = FindAnyObjectByType<SurfboardController>();
            if (existingBoard != null)
                Destroy(existingBoard.gameObject);
        }

        [ContextMenu("Spawn Surfboard")]
        public void CreateBoard()
        {
            if (board != null)
                Destroy(board.gameObject);

            GameObject boardObject = new("Particle-Coupled Surfboard");
            boardObject.transform.SetParent(transform);

            float startX = Mathf.Lerp(
                water.TankMinimum.x,
                water.TankMaximum.x,
                spawnPositionAcrossOcean);
            float startY = water.GetGameplaySurfaceHeight(startX) + spawnHeightOffset;
            boardObject.transform.position = new Vector3(startX, startY, -0.55f);

            if (applyPresetOnSpawn)
                ApplyBoardPreset();

            boardObject.AddComponent<Rigidbody>();
            board = boardObject.AddComponent<SurfboardController>();
            board.ConfigureShape(
                boardLength,
                boardWidth,
                boardThickness,
                noseWidth,
                tailWidth,
                boardRocker,
                railRoundness,
                bottomConcave,
                deckDome,
                finCount,
                finSize,
                cameraFacingTilt,
                boardColour,
                stripeColour,
                stripeWidth);
            board.Initialise(this);
            lastShapeHash = CalculateShapeHash();

            message = "Board spawned — shape it live from BeachGameController";
            ConfigureCamera(board.transform);
        }

        private void ApplyBoardPreset()
        {
            switch (boardPreset)
            {
                case BoardPreset.Longboard:
                    boardLength = 2.75f;
                    boardWidth = 0.62f;
                    boardThickness = 0.14f;
                    noseWidth = 0.90f;
                    tailWidth = 0.72f;
                    boardRocker = 0.055f;
                    railRoundness = 0.82f;
                    bottomConcave = 0.010f;
                    deckDome = 0.040f;
                    finCount = 1;
                    finSize = 0.14f;
                    break;

                case BoardPreset.Shortboard:
                    boardLength = 1.85f;
                    boardWidth = 0.52f;
                    boardThickness = 0.115f;
                    noseWidth = 0.68f;
                    tailWidth = 0.44f;
                    boardRocker = 0.095f;
                    railRoundness = 0.72f;
                    bottomConcave = 0.025f;
                    deckDome = 0.035f;
                    finCount = 3;
                    finSize = 0.105f;
                    break;

                case BoardPreset.Fish:
                    boardLength = 1.65f;
                    boardWidth = 0.60f;
                    boardThickness = 0.13f;
                    noseWidth = 0.78f;
                    tailWidth = 0.58f;
                    boardRocker = 0.060f;
                    railRoundness = 0.78f;
                    bottomConcave = 0.018f;
                    deckDome = 0.040f;
                    finCount = 2;
                    finSize = 0.11f;
                    break;

                case BoardPreset.Funboard:
                    boardLength = 2.25f;
                    boardWidth = 0.58f;
                    boardThickness = 0.13f;
                    noseWidth = 0.84f;
                    tailWidth = 0.62f;
                    boardRocker = 0.070f;
                    railRoundness = 0.80f;
                    bottomConcave = 0.015f;
                    deckDome = 0.042f;
                    finCount = 3;
                    finSize = 0.11f;
                    break;

                case BoardPreset.Gun:
                    boardLength = 2.65f;
                    boardWidth = 0.48f;
                    boardThickness = 0.12f;
                    noseWidth = 0.42f;
                    tailWidth = 0.30f;
                    boardRocker = 0.125f;
                    railRoundness = 0.66f;
                    bottomConcave = 0.035f;
                    deckDome = 0.030f;
                    finCount = 3;
                    finSize = 0.12f;
                    break;
            }
        }

        [ContextMenu("Apply Inspector Shape To Board")]
        public void ApplyInspectorShapeToBoard()
        {
            if (board == null)
            {
                Debug.Log("Spawn the board first, then apply its Inspector shape.", this);
                return;
            }

            ApplyInspectorShape(force: true);
        }

        private void ApplyInspectorShapeIfChanged()
        {
            int currentHash = CalculateShapeHash();
            if (currentHash == lastShapeHash)
                return;

            ApplyInspectorShape(force: true);
        }

        private void ApplyInspectorShape(bool force)
        {
            if (board == null)
                return;

            lastShapeHash = CalculateShapeHash();

            board.ApplyShape(
                boardLength,
                boardWidth,
                boardThickness,
                noseWidth,
                tailWidth,
                boardRocker,
                railRoundness,
                bottomConcave,
                deckDome,
                finCount,
                finSize,
                cameraFacingTilt,
                boardColour,
                stripeColour,
                stripeWidth);
        }

        private int CalculateShapeHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + boardLength.GetHashCode();
                hash = hash * 31 + boardWidth.GetHashCode();
                hash = hash * 31 + boardThickness.GetHashCode();
                hash = hash * 31 + noseWidth.GetHashCode();
                hash = hash * 31 + tailWidth.GetHashCode();
                hash = hash * 31 + boardRocker.GetHashCode();
                hash = hash * 31 + railRoundness.GetHashCode();
                hash = hash * 31 + bottomConcave.GetHashCode();
                hash = hash * 31 + deckDome.GetHashCode();
                hash = hash * 31 + finCount;
                hash = hash * 31 + finSize.GetHashCode();
                hash = hash * 31 + cameraFacingTilt.GetHashCode();
                hash = hash * 31 + boardColour.GetHashCode();
                hash = hash * 31 + stripeColour.GetHashCode();
                hash = hash * 31 + stripeWidth.GetHashCode();
                return hash;
            }
        }

        [ContextMenu("Remove Surfboard")]
        public void RemoveBoard()
        {
            if (board != null)
                Destroy(board.gameObject);

            board = null;
            rideScore = 0f;
            message = "Press B to place the surfboard on the water";
            ConfigureCamera(null);
        }

        private void ConfigureCamera(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 3.1f;
            camera.backgroundColor = new Color(0.43f, 0.79f, 0.92f, 1f);

            float centreX = target != null
                ? target.position.x
                : Mathf.Lerp(water.TankMinimum.x, water.TankMaximum.x, 0.55f);
            camera.transform.position = new Vector3(centreX, 0f, -10f);

            BeachCameraFollow follow =
                camera.GetComponent<BeachCameraFollow>() ??
                camera.gameObject.AddComponent<BeachCameraFollow>();
            follow.Target = target;
        }

        public void SetRideScore(float score)
        {
            rideScore = score;
            bestRide = Mathf.Max(bestRide, score);
            message = "Riding — use A / D or arrow keys to trim";
        }

        public void EndRide(float score)
        {
            bestRide = Mathf.Max(bestRide, score);
            rideScore = 0f;
            message = $"Ride ended: {Mathf.RoundToInt(score * 10f)} pts";
        }

        public void AddShell(int amount = 1) { }
        public void SetMessage(string value) => message = value;

        public void SetRide(float score, bool riding)
        {
            if (riding) SetRideScore(score);
            else EndRide(score);
        }

        public void SetRide(bool riding, float score)
        {
            SetRide(score, riding);
        }

        private void OnGUI()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            GUI.Box(new Rect(16, 16, 430, 112), GUIContent.none);
            GUI.Label(new Rect(30, 25, 390, 26), "TROPICAL BREAK", titleStyle);
            GUI.Label(new Rect(30, 53, 400, 22), message, bodyStyle);

            string controls = board == null
                ? "B — spawn board"
                : $"A / D steer   B respawn   Ride: {Mathf.RoundToInt(rideScore * 10f)}   Best: {Mathf.RoundToInt(bestRide * 10f)}";
            GUI.Label(new Rect(30, 78, 400, 22), controls, bodyStyle);
        }
    }
}
