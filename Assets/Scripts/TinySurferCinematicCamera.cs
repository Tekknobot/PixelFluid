using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    /// <summary>
    /// Press Z to toggle a smooth cinematic camera that zooms in and follows
    /// the autonomous TinyWaveSurfer. Press Z again to restore the previous
    /// camera position, zoom and camera-follow scripts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TinySurferCinematicCamera : MonoBehaviour
    {
        [Header("Toggle")]
        [SerializeField] private KeyCode legacyToggleKey = KeyCode.Z;
        [SerializeField] private bool startInCinematicMode;

        [Header("Normal Camera")]
        [SerializeField, Min(0.1f)] private float normalOrthographicZoom = 3.5f;
        [SerializeField] private Vector2 normalFramingOffset = new(0f, 0.15f);
        [SerializeField, Min(0.01f)] private float normalFollowSmoothTime = 0.28f;
        [SerializeField, Min(0f)] private float normalMaximumFollowSpeed = 25f;

        [Header("Cinematic Framing")]
        [SerializeField, Min(0.1f)] private float orthographicZoom = 1.75f;
        [SerializeField, Min(5f)] private float perspectiveFieldOfView = 32f;
        [SerializeField] private Vector2 framingOffset = new(0.35f, 0.18f);
        [SerializeField] private float cameraDepth = -10f;

        [Header("Follow")]
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.16f;
        [SerializeField, Min(0f)] private float maximumFollowSpeed = 45f;
        [SerializeField, Min(0f)] private float horizontalLookAhead = 0.42f;
        [SerializeField, Min(0f)] private float verticalLookAhead = 0.16f;
        [SerializeField, Min(0.01f)] private float lookAheadSmoothTime = 0.20f;

        [Header("Camera Edge Clamp")]
        [Tooltip("Keeps the viewport inside the active water simulation so empty space beyond its edges is not shown.")]
        [SerializeField] private bool clampToCurrentSimulation = true;
        [SerializeField, Min(0f)] private float clampInset = 0.05f;

        [Header("Zoom")]
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.22f;
        [SerializeField, Min(0.1f)] private float minimumOrthographicZoom = 0.85f;
        [SerializeField, Min(0.1f)] private float maximumOrthographicZoom = 3.5f;
        [SerializeField, Min(0.05f)] private float gamepadZoomSpeed = 2.2f;
        [SerializeField, Range(0f, 0.95f)] private float gamepadZoomDeadZone = 0.18f;
        [Tooltip("Y / North toggles cinematic mode. Right-stick vertical zooms. Right-stick click resets zoom.")]
        [SerializeField] private bool enableGamepadCameraControls = true;

        private Camera controlledCamera;
        private TinyWaveSurfer surfer;
        private TinyWaveSurfer[] availableSurfers;
        private int focusedSurferIndex = -1;
        private Vector3 followVelocity;
        private Vector3 lookAheadVelocity;
        private Vector3 smoothedLookAhead;
        private Vector3 previousSurferPosition;
        private bool hasPreviousSurferPosition;

        private bool cinematicActive;
        private Vector3 storedPosition;
        private Quaternion storedRotation;
        private float storedOrthographicSize;
        private float storedFieldOfView;

        private readonly List<MonoBehaviour> disabledCameraControllers = new();
        private float zoomVelocity;
        private float normalZoomAdjustment;
        private float cinematicZoomAdjustment;

        public bool CinematicActive => cinematicActive;

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            cameraDepth = transform.position.z;

            if (startInCinematicMode)
                EnableCinematic();
        }

        private void Update()
        {
            if (TogglePressed())
                ToggleCinematic();

            if (CancelPressed() && cinematicActive)
                DisableCinematic();

            UpdateManualZoomInput();

            if (surfer == null || !surfer.isActiveAndEnabled)
                SelectPlayerSurfer();
        }
        
        private void LateUpdate()
        {
            if (controlledCamera == null)
                return;

            if (surfer == null || !surfer.isActiveAndEnabled)
                SelectPlayerSurfer();

            if (surfer == null)
                return;

            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector3 surferPosition = surfer.transform.position;

            Vector3 desiredPosition;
            float activeSmoothTime;
            float activeMaximumSpeed;

            if (cinematicActive)
            {
                Vector3 estimatedVelocity = Vector3.zero;

                if (hasPreviousSurferPosition)
                {
                    estimatedVelocity =
                        (surferPosition - previousSurferPosition) / deltaTime;
                }

                previousSurferPosition = surferPosition;
                hasPreviousSurferPosition = true;

                Vector3 desiredLookAhead = new(
                    Mathf.Clamp(estimatedVelocity.x, -1f, 1f) *
                    horizontalLookAhead,

                    Mathf.Clamp(estimatedVelocity.y, -1f, 1f) *
                    verticalLookAhead,

                    0f);

                smoothedLookAhead = Vector3.SmoothDamp(
                    smoothedLookAhead,
                    desiredLookAhead,
                    ref lookAheadVelocity,
                    lookAheadSmoothTime,
                    Mathf.Infinity,
                    deltaTime);

                desiredPosition = new Vector3(
                    surferPosition.x +
                    framingOffset.x +
                    smoothedLookAhead.x,

                    surferPosition.y +
                    framingOffset.y +
                    smoothedLookAhead.y,

                    cameraDepth);

                activeSmoothTime = followSmoothTime;
                activeMaximumSpeed = maximumFollowSpeed;
            }
            else
            {
                smoothedLookAhead = Vector3.SmoothDamp(
                    smoothedLookAhead,
                    Vector3.zero,
                    ref lookAheadVelocity,
                    normalFollowSmoothTime,
                    Mathf.Infinity,
                    deltaTime);

                desiredPosition = new Vector3(
                    surferPosition.x + normalFramingOffset.x,
                    surferPosition.y + normalFramingOffset.y,
                    cameraDepth);

                activeSmoothTime = normalFollowSmoothTime;
                activeMaximumSpeed = normalMaximumFollowSpeed;
            }

            if (clampToCurrentSimulation)
                desiredPosition = ClampInsideSimulation(desiredPosition);

            desiredPosition = ClampInsideBossArena(desiredPosition);

            Vector3 smoothedPosition = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                activeSmoothTime,
                activeMaximumSpeed,
                deltaTime);

            Vector3 finalPosition = clampToCurrentSimulation
                ? ClampInsideSimulation(smoothedPosition)
                : smoothedPosition;
            transform.position = ClampInsideBossArena(finalPosition);

            if (controlledCamera.orthographic)
            {
                float baseZoom = cinematicActive
                    ? orthographicZoom
                    : normalOrthographicZoom;
                float adjustment = cinematicActive
                    ? cinematicZoomAdjustment
                    : normalZoomAdjustment;
                float targetZoom = Mathf.Clamp(
                    baseZoom + adjustment,
                    minimumOrthographicZoom,
                    maximumOrthographicZoom);

                controlledCamera.orthographicSize = Mathf.SmoothDamp(
                    controlledCamera.orthographicSize,
                    targetZoom,
                    ref zoomVelocity,
                    zoomSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }
            else
            {
                float targetFov = cinematicActive
                    ? perspectiveFieldOfView
                    : storedFieldOfView;

                controlledCamera.fieldOfView = Mathf.SmoothDamp(
                    controlledCamera.fieldOfView,
                    targetFov,
                    ref zoomVelocity,
                    zoomSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }
        }

        private void SelectPlayerSurfer()
        {
            TinyWaveSurfer[] surfers =
                FindObjectsByType<TinyWaveSurfer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            surfer = null;

            foreach (TinyWaveSurfer candidate in surfers)
            {
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                // Keep watching the AI through its death animation and respawn.
                if (candidate.IsAIControlled)
                {
                    surfer = candidate;
                    return;
                }

                if (candidate.IsDead)
                    continue;

                if (candidate.IsPlayerControlled)
                {
                    surfer = candidate;
                    return;
                }

                if (surfer == null)
                    surfer = candidate;
            }
        }

        private void RefreshSurferList()
        {
            availableSurfers = FindObjectsByType<TinyWaveSurfer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            System.Array.Sort(
                availableSurfers,
                (a, b) => string.CompareOrdinal(
                    a.gameObject.name,
                    b.gameObject.name));
        }

        private void RefreshSurferListAndSelect(int index)
        {
            RefreshSurferList();

            if (availableSurfers == null || availableSurfers.Length == 0)
            {
                surfer = null;
                focusedSurferIndex = -1;
                return;
            }

            focusedSurferIndex = Mathf.Clamp(
                index,
                0,
                availableSurfers.Length - 1);

            surfer = availableSurfers[focusedSurferIndex];
        }

        private Vector3 ClampInsideBossArena(Vector3 desired)
        {
            BossArenaPrison arena = BossArenaPrison.Active;
            if (arena == null || !BossArenaPrison.IsActive || controlledCamera == null)
                return desired;

            float halfWidth;
            if (controlledCamera.orthographic)
            {
                halfWidth = controlledCamera.orthographicSize * controlledCamera.aspect;
            }
            else
            {
                float distance = Mathf.Abs(cameraDepth - surfer.transform.position.z);
                float halfHeight = Mathf.Tan(
                    controlledCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
                halfWidth = halfHeight * controlledCamera.aspect;
            }

            desired.x = arena.ClampCameraX(desired.x, halfWidth);
            return desired;
        }

        private Vector3 ClampInsideSimulation(Vector3 desired)
        {
            // Clamp against the complete stack, not only the surfer's current row.
            // The lower viewport edge is pinned to the visible particle field of
            // the lowest active simulation, ignoring tank and seabed geometry.
            PixelWaterGPU[] simulations = FindObjectsByType<PixelWaterGPU>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            Vector2 min = surfer.CurrentWave.TankMinimum;
            Vector2 max = surfer.CurrentWave.TankMaximum;
            float lowestVisibleWaveBottom = surfer.CurrentWave.VisibleWaveBottom;

            bool foundSimulation = false;
            for (int i = 0; i < simulations.Length; i++)
            {
                PixelWaterGPU simulation = simulations[i];
                if (simulation == null || !simulation.isActiveAndEnabled)
                    continue;

                Vector2 simulationMin = simulation.TankMinimum;
                Vector2 simulationMax = simulation.TankMaximum;

                if (!foundSimulation)
                {
                    min = simulationMin;
                    max = simulationMax;
                    lowestVisibleWaveBottom = simulation.VisibleWaveBottom;
                    foundSimulation = true;
                }
                else
                {
                    min = Vector2.Min(min, simulationMin);
                    max = Vector2.Max(max, simulationMax);
                    lowestVisibleWaveBottom = Mathf.Min(
                        lowestVisibleWaveBottom,
                        simulation.VisibleWaveBottom);
                }
            }

            float halfHeight;
            float halfWidth;

            if (controlledCamera.orthographic)
            {
                halfHeight = controlledCamera.orthographicSize;
                halfWidth = halfHeight * controlledCamera.aspect;
            }
            else
            {
                float distance = Mathf.Abs(cameraDepth - surfer.transform.position.z);
                halfHeight = Mathf.Tan(
                    controlledCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
                halfWidth = halfHeight * controlledCamera.aspect;
            }

            float leftCameraInset = 2.0f;
            float rightCameraInset = 2.0f;

            float minX =
                min.x +
                halfWidth +
                clampInset +
                leftCameraInset;

            float maxX =
                max.x -
                halfWidth -
                clampInset -
                rightCameraInset;

            // Pin the viewport to the actual bottom row of particles. Do not
            // use TankMinimum or horizontalSeabedHeight here: both can sit below
            // the rendered water and reveal a strip of the star background.
            float minY = lowestVisibleWaveBottom + halfHeight + 0.16f;
            float maxY = max.y - halfHeight - clampInset;

            desired.x = minX <= maxX
                ? Mathf.Clamp(desired.x, minX, maxX)
                : (min.x + max.x) * 0.5f;

            desired.y = minY <= maxY
                ? Mathf.Clamp(desired.y, minY, maxY)
                : minY;

            return desired;
        }

        [ContextMenu("Focus Next Surfer")]
        public void FocusNextSurfer()
        {
            RefreshSurferList();

            if (availableSurfers == null || availableSurfers.Length == 0)
            {
                Debug.LogWarning(
                    "No TinyWaveSurfer was found for the cinematic camera.",
                    this);
                return;
            }

            if (!cinematicActive)
            {
                focusedSurferIndex = 0;
                surfer = availableSurfers[focusedSurferIndex];
                EnableCinematic();
                return;
            }

            focusedSurferIndex =
                (focusedSurferIndex + 1) % availableSurfers.Length;
            surfer = availableSurfers[focusedSurferIndex];

            followVelocity = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            smoothedLookAhead = Vector3.zero;
            previousSurferPosition = surfer.transform.position;
            hasPreviousSurferPosition = true;
        }

        [ContextMenu("Toggle Surfer Cinematic")]
        public void ToggleCinematic()
        {
            if (cinematicActive)
                DisableCinematic();
            else
                FocusNextSurfer();
        }

        [ContextMenu("Enable Surfer Cinematic")]
        public void EnableCinematic()
        {
            if (cinematicActive || controlledCamera == null)
                return;

            if (surfer == null)
                RefreshSurferListAndSelect(0);

            if (surfer == null)
            {
                Debug.LogWarning(
                    "No TinyWaveSurfer was found for the cinematic camera.",
                    this);
                return;
            }

            storedPosition = transform.position;
            storedRotation = transform.rotation;
            storedOrthographicSize = controlledCamera.orthographicSize;
            storedFieldOfView = controlledCamera.fieldOfView;
            cameraDepth = transform.position.z;

            DisableCompetingCameraControllers();

            cinematicActive = true;
            followVelocity = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            smoothedLookAhead = Vector3.zero;
            zoomVelocity = 0f;
            previousSurferPosition = surfer.transform.position;
            hasPreviousSurferPosition = true;
        }

        [ContextMenu("Disable Surfer Cinematic")]
        public void DisableCinematic()
        {
            if (!cinematicActive)
                return;

            cinematicActive = false;

            SelectPlayerSurfer();

            followVelocity = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            smoothedLookAhead = Vector3.zero;
            zoomVelocity = 0f;

            if (surfer != null)
            {
                previousSurferPosition = surfer.transform.position;
                hasPreviousSurferPosition = true;
            }
            else
            {
                hasPreviousSurferPosition = false;
            }
        }

        private void DisableCompetingCameraControllers()
        {
            disabledCameraControllers.Clear();

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null ||
                    behaviour == this ||
                    !behaviour.enabled)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName == "SmoothBoardCamera" ||
                    typeName == "BeachCameraFollow")
                {
                    behaviour.enabled = false;
                    disabledCameraControllers.Add(behaviour);
                }
            }
        }

        private void RestoreCameraControllers()
        {
            foreach (MonoBehaviour behaviour in disabledCameraControllers)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            disabledCameraControllers.Clear();
        }

        private void UpdateManualZoomInput()
        {
            if (!enableGamepadCameraControls || controlledCamera == null)
                return;

#if ENABLE_INPUT_SYSTEM
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return;

            if (gamepad.rightStickButton.wasPressedThisFrame)
            {
                if (cinematicActive)
                    cinematicZoomAdjustment = 0f;
                else
                    normalZoomAdjustment = 0f;

                zoomVelocity = 0f;
            }

            float zoomInput = gamepad.rightStick.ReadValue().y;
            if (Mathf.Abs(zoomInput) < gamepadZoomDeadZone)
                zoomInput = 0f;

            if (!Mathf.Approximately(zoomInput, 0f))
            {
                // Stick up zooms in (smaller orthographic size); down zooms out.
                float delta = -zoomInput * gamepadZoomSpeed * Time.unscaledDeltaTime;

                if (cinematicActive)
                {
                    cinematicZoomAdjustment = ClampZoomAdjustment(
                        orthographicZoom,
                        cinematicZoomAdjustment + delta);
                }
                else
                {
                    normalZoomAdjustment = ClampZoomAdjustment(
                        normalOrthographicZoom,
                        normalZoomAdjustment + delta);
                }
            }
#endif
        }

        private float ClampZoomAdjustment(float baseZoom, float adjustment)
        {
            float target = Mathf.Clamp(
                baseZoom + adjustment,
                minimumOrthographicZoom,
                maximumOrthographicZoom);
            return target - baseZoom;
        }

        private bool CancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                Keyboard.current.zKey.wasPressedThisFrame)
            {
                return true;
            }

            if (enableGamepadCameraControls &&
                Gamepad.current != null &&
                Gamepad.current.buttonNorth.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(legacyToggleKey);
#else
            return false;
#endif
        }

        private void OnDisable()
        {
            if (cinematicActive)
                DisableCinematic();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            orthographicZoom = Mathf.Max(0.1f, orthographicZoom);
            perspectiveFieldOfView = Mathf.Clamp(
                perspectiveFieldOfView,
                5f,
                170f);
            followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
            lookAheadSmoothTime = Mathf.Max(0.01f, lookAheadSmoothTime);
            zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
            minimumOrthographicZoom = Mathf.Max(0.1f, minimumOrthographicZoom);
            maximumOrthographicZoom = Mathf.Max(minimumOrthographicZoom, maximumOrthographicZoom);
            gamepadZoomSpeed = Mathf.Max(0.05f, gamepadZoomSpeed);
        }
#endif
    }

    /// <summary>
    /// Automatically installs the Z-key cinematic controller on the main camera.
    /// </summary>
    public static class TinySurferCinematicCameraBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Camera camera = Camera.main;

            if (camera == null)
                camera = Object.FindFirstObjectByType<Camera>();

            if (camera == null ||
                camera.GetComponent<TinySurferCinematicCamera>() != null)
            {
                return;
            }

            camera.gameObject.AddComponent<TinySurferCinematicCamera>();
        }
    }
}
