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
                FocusNextSurfer();

            if (CancelPressed() && cinematicActive)
                DisableCinematic();

            if (!cinematicActive)
                return;

            if (surfer == null || !surfer.isActiveAndEnabled)
                RefreshSurferListAndSelect(0);
        }

        private void LateUpdate()
        {
            if (!cinematicActive || controlledCamera == null || surfer == null)
                return;

            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector3 surferPosition = surfer.transform.position;

            Vector3 estimatedVelocity = Vector3.zero;
            if (hasPreviousSurferPosition)
                estimatedVelocity =
                    (surferPosition - previousSurferPosition) / deltaTime;

            previousSurferPosition = surferPosition;
            hasPreviousSurferPosition = true;

            Vector3 desiredLookAhead = new(
                Mathf.Clamp(estimatedVelocity.x, -1f, 1f) * horizontalLookAhead,
                Mathf.Clamp(estimatedVelocity.y, -1f, 1f) * verticalLookAhead,
                0f);

            smoothedLookAhead = Vector3.SmoothDamp(
                smoothedLookAhead,
                desiredLookAhead,
                ref lookAheadVelocity,
                lookAheadSmoothTime,
                Mathf.Infinity,
                deltaTime);

            Vector3 desiredPosition = new(
                surferPosition.x + framingOffset.x + smoothedLookAhead.x,
                surferPosition.y + framingOffset.y + smoothedLookAhead.y,
                cameraDepth);

            if (clampToCurrentSimulation && surfer.CurrentWave != null)
                desiredPosition = ClampInsideSimulation(desiredPosition);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                followSmoothTime,
                maximumFollowSpeed,
                deltaTime);

            if (controlledCamera.orthographic)
            {
                controlledCamera.orthographicSize = Mathf.SmoothDamp(
                    controlledCamera.orthographicSize,
                    orthographicZoom,
                    ref zoomVelocity,
                    zoomSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }
            else
            {
                controlledCamera.fieldOfView = Mathf.SmoothDamp(
                    controlledCamera.fieldOfView,
                    perspectiveFieldOfView,
                    ref zoomVelocity,
                    zoomSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
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

        private Vector3 ClampInsideSimulation(Vector3 desired)
        {
            Vector2 min = surfer.CurrentWave.TankMinimum;
            Vector2 max = surfer.CurrentWave.TankMaximum;

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

            float minX = min.x + halfWidth + clampInset;
            float maxX = max.x - halfWidth - clampInset;
            float minY = min.y + halfHeight + clampInset;
            float maxY = max.y - halfHeight - clampInset;

            desired.x = minX <= maxX
                ? Mathf.Clamp(desired.x, minX, maxX)
                : (min.x + max.x) * 0.5f;

            desired.y = minY <= maxY
                ? Mathf.Clamp(desired.y, minY, maxY)
                : (min.y + max.y) * 0.5f;

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
            transform.position = storedPosition;
            transform.rotation = storedRotation;
            controlledCamera.orthographicSize = storedOrthographicSize;
            controlledCamera.fieldOfView = storedFieldOfView;

            RestoreCameraControllers();

            followVelocity = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            smoothedLookAhead = Vector3.zero;
            zoomVelocity = 0f;
            hasPreviousSurferPosition = false;
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
