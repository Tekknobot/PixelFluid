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

        [Header("Camera Stability")]
        [Tooltip("Small vertical surfer movements inside this range do not move the camera. This prevents foam and particle noise from shaking the whole screen.")]
        [SerializeField, Range(0f, 1f)] private float verticalFollowDeadZone = 0.20f;
        [Tooltip("Smooths the vertical target separately before the camera follows it.")]
        [SerializeField, Min(0.01f)] private float verticalTargetSmoothTime = 0.32f;
        [Tooltip("Maximum speed of the stabilized vertical target. Large jumps still catch up without reacting to tiny particle spikes.")]
        [SerializeField, Min(0.1f)] private float maximumVerticalTargetSpeed = 5f;
        [Tooltip("How often the camera refreshes its cached list of wave simulations.")]
        [SerializeField, Range(0.1f, 3f)] private float simulationBoundsRefreshInterval = 0.75f;

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

        [Header("Boss Death Focus")]
        [Tooltip("Orthographic size used while holding on a defeated boss.")]
        [SerializeField, Min(0.1f)] private float bossDeathFocusZoom = 2.35f;
        [Tooltip("Camera offset from the defeated boss during its death sequence.")]
        [SerializeField] private Vector2 bossDeathFocusOffset = new(0f, 0.15f);
        [Tooltip("How quickly the camera pans to and follows a defeated boss.")]
        [SerializeField, Min(0.01f)] private float bossDeathFocusSmoothTime = 0.28f;
        [SerializeField, Min(0f)] private float bossDeathFocusMaximumSpeed = 32f;

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
        private float stabilizedTargetY;
        private float stabilizedTargetYVelocity;
        private bool hasStabilizedTargetY;
        private PixelWaterGPU[] cachedSimulations;
        private float nextSimulationBoundsRefreshTime;
        private Transform bossDeathFocusTarget;

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

        /// <summary>
        /// Rebinds the camera after a Story/Race mode transition and clears every
        /// piece of smoothing and clamp state that belonged to the old surfer.
        /// </summary>
        public void SetFollowTarget(TinyWaveSurfer target, bool snapImmediately = true)
        {
            surfer = target;
            bossDeathFocusTarget = null;
            followVelocity = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            smoothedLookAhead = Vector3.zero;
            zoomVelocity = 0f;
            hasPreviousSurferPosition = false;
            ResetCameraStability();
            RefreshSimulationBoundsCache();
            nextSimulationBoundsRefreshTime = 0f;

            if (controlledCamera == null)
                controlledCamera = GetComponent<Camera>();

            if (target == null || controlledCamera == null || !snapImmediately)
                return;

            // Restore the exact zoom for the active camera state before calculating
            // clamp extents. Smooth zooming through an oversized viewport was what
            // caused the old clamp to collapse to centre/lowest-Y after transitions.
            if (controlledCamera.orthographic)
            {
                controlledCamera.orthographicSize = cinematicActive
                    ? Mathf.Clamp(orthographicZoom + cinematicZoomAdjustment, minimumOrthographicZoom, maximumOrthographicZoom)
                    : Mathf.Clamp(normalOrthographicZoom + normalZoomAdjustment, minimumOrthographicZoom, maximumOrthographicZoom);
            }

            Vector2 offset = cinematicActive ? framingOffset : normalFramingOffset;
            Vector3 desired = new Vector3(
                target.transform.position.x + offset.x,
                target.transform.position.y + offset.y,
                cameraDepth);

            if (clampToCurrentSimulation)
                desired = ClampInsideSimulation(desired);

            transform.position = ClampInsideBossArena(desired);
            previousSurferPosition = target.transform.position;
            hasPreviousSurferPosition = true;
        }

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            cameraDepth = transform.position.z;
            RefreshSimulationBoundsCache();

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

            bool focusingBoss = bossDeathFocusTarget != null;
            if (!focusingBoss && surfer == null)
                return;

            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector3 surferPosition = focusingBoss
                ? bossDeathFocusTarget.position
                : surfer.transform.position;

            Vector3 desiredPosition;
            float activeSmoothTime;
            float activeMaximumSpeed;

            if (Time.unscaledTime >= nextSimulationBoundsRefreshTime)
                RefreshSimulationBoundsCache();

            if (focusingBoss)
            {
                smoothedLookAhead = Vector3.SmoothDamp(
                    smoothedLookAhead,
                    Vector3.zero,
                    ref lookAheadVelocity,
                    bossDeathFocusSmoothTime,
                    Mathf.Infinity,
                    deltaTime);

                desiredPosition = new Vector3(
                    surferPosition.x + bossDeathFocusOffset.x,
                    StabilizeVerticalTarget(
                        surferPosition.y + bossDeathFocusOffset.y,
                        deltaTime),
                    cameraDepth);

                activeSmoothTime = bossDeathFocusSmoothTime;
                activeMaximumSpeed = bossDeathFocusMaximumSpeed;
            }
            else if (cinematicActive)
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

                float rawCinematicTargetY =
                    surferPosition.y + framingOffset.y + smoothedLookAhead.y;

                desiredPosition = new Vector3(
                    surferPosition.x +
                    framingOffset.x +
                    smoothedLookAhead.x,

                    StabilizeVerticalTarget(rawCinematicTargetY, deltaTime),

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

                float rawNormalTargetY =
                    surferPosition.y + normalFramingOffset.y;

                desiredPosition = new Vector3(
                    surferPosition.x + normalFramingOffset.x,
                    StabilizeVerticalTarget(rawNormalTargetY, deltaTime),
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
                float baseZoom = focusingBoss
                    ? bossDeathFocusZoom
                    : cinematicActive
                        ? orthographicZoom
                        : normalOrthographicZoom;
                float adjustment = focusingBoss
                    ? 0f
                    : cinematicActive
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
                float targetFov = (focusingBoss || cinematicActive)
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

        public void BeginBossDeathFocus(Transform defeatedBoss)
        {
            if (defeatedBoss == null)
                return;

            bossDeathFocusTarget = defeatedBoss;
            followVelocity = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            smoothedLookAhead = Vector3.zero;
            ResetCameraStability();
        }

        public void EndBossDeathFocus(Transform defeatedBoss = null)
        {
            if (defeatedBoss != null && bossDeathFocusTarget != defeatedBoss)
                return;

            bossDeathFocusTarget = null;
            followVelocity = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            smoothedLookAhead = Vector3.zero;
            ResetCameraStability();
        }

        private float StabilizeVerticalTarget(float rawTargetY, float deltaTime)
        {
            if (!hasStabilizedTargetY)
            {
                stabilizedTargetY = rawTargetY;
                stabilizedTargetYVelocity = 0f;
                hasStabilizedTargetY = true;
                return stabilizedTargetY;
            }

            float difference = rawTargetY - stabilizedTargetY;
            float targetOutsideDeadZone = stabilizedTargetY;

            if (Mathf.Abs(difference) > verticalFollowDeadZone)
            {
                targetOutsideDeadZone = rawTargetY -
                    Mathf.Sign(difference) * verticalFollowDeadZone;
            }

            stabilizedTargetY = Mathf.SmoothDamp(
                stabilizedTargetY,
                targetOutsideDeadZone,
                ref stabilizedTargetYVelocity,
                verticalTargetSmoothTime,
                maximumVerticalTargetSpeed,
                deltaTime);

            return stabilizedTargetY;
        }

        private void RefreshSimulationBoundsCache()
        {
            cachedSimulations = FindObjectsByType<PixelWaterGPU>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            nextSimulationBoundsRefreshTime =
                Time.unscaledTime + Mathf.Max(0.1f, simulationBoundsRefreshInterval);
        }

        private void ResetCameraStability()
        {
            hasStabilizedTargetY = false;
            stabilizedTargetYVelocity = 0f;
        }

        private void SelectPlayerSurfer()
        {
            TinyWaveSurfer[] surfers =
                FindObjectsByType<TinyWaveSurfer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            TinyWaveSurfer fallback = null;

            // The selected human racer or Story Chuck always wins camera priority.
            foreach (TinyWaveSurfer candidate in surfers)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsDead)
                    continue;

                if (candidate.IsPlayerControlled)
                {
                    SetFollowTarget(candidate, false);
                    return;
                }

                if (fallback == null)
                    fallback = candidate;
            }

            if (fallback != null)
                SetFollowTarget(fallback, false);
            else
                surfer = null;
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
            if (cachedSimulations == null || cachedSimulations.Length == 0)
                RefreshSimulationBoundsCache();

            PixelWaterGPU[] simulations = cachedSimulations;

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

            // During a mode transition the camera zoom and water cache can be
            // between configurations for a frame. Never collapse to world-centre
            // or the lowest possible Y when the viewport temporarily exceeds the
            // bounds; preserve the correctly framed target until valid bounds return.
            if (minX <= maxX)
                desired.x = Mathf.Clamp(desired.x, minX, maxX);

            if (minY <= maxY)
                desired.y = Mathf.Clamp(desired.y, minY, maxY);

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
            ResetCameraStability();
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
            ResetCameraStability();
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

            ResetCameraStability();
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
            verticalTargetSmoothTime = Mathf.Max(0.01f, verticalTargetSmoothTime);
            maximumVerticalTargetSpeed = Mathf.Max(0.1f, maximumVerticalTargetSpeed);
            simulationBoundsRefreshInterval = Mathf.Max(0.1f, simulationBoundsRefreshInterval);
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
