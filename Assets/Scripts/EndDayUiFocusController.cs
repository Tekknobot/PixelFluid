using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Gives the recap or upgrade panel exclusive visual focus. Every other
    /// Canvas is hidden temporarily and restored to its exact prior state.
    /// </summary>
    [DefaultExecutionOrder(32700)]
    public sealed class EndDayUiFocusController : MonoBehaviour
    {
        private static EndDayUiFocusController instance;
        private static readonly HashSet<Canvas> EndDayCanvases = new();
        private static float focusHoldUntil;

        private readonly Dictionary<Canvas, bool> previousCanvasStates = new();

        public static bool IsActive
        {
            get
            {
                // A panel can be disabled or destroyed without its closing coroutine
                // reaching End (for example when changing modes). Do not let that
                // abandoned registration permanently suppress gameplay UI/input.
                EndDayCanvases.RemoveWhere(canvas =>
                    canvas == null ||
                    !canvas.gameObject.activeInHierarchy ||
                    !canvas.enabled);
                return EndDayCanvases.Count > 0 || Time.unscaledTime < focusHoldUntil;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            EnsureInstance();
        }

        public static void Begin(Canvas endDayCanvas)
        {
            if (endDayCanvas == null)
                return;

            SurferSlugPauseMenu.Instance?.CloseForEndDayPanel();
            EndDayCanvases.Add(endDayCanvas);
            endDayCanvas.enabled = true;
            EnsureInstance().RefreshState();
        }

        public static void End(Canvas endDayCanvas)
        {
            if (endDayCanvas == null || !EndDayCanvases.Remove(endDayCanvas))
                return;

            // Prevent the ordinary HUD flashing during the recap-to-upgrade handoff.
            focusHoldUntil = Mathf.Max(focusHoldUntil, Time.unscaledTime + 0.20f);
            EnsureInstance().RefreshState();
        }

        /// <summary>
        /// Releases recap/upgrade focus before entering another game mode. This
        /// restores every canvas to its captured state and clears the short focus
        /// handoff delay so race HUD and pause input are available immediately.
        /// </summary>
        public static void ReleaseForModeTransition()
        {
            EndDayCanvases.Clear();
            focusHoldUntil = 0f;

            if (instance != null)
                instance.RestoreOtherCanvases();
        }

        private static EndDayUiFocusController EnsureInstance()
        {
            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<EndDayUiFocusController>();
            if (instance != null)
                return instance;

            GameObject host = new("End Day UI Focus Controller");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<EndDayUiFocusController>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void LateUpdate()
        {
            RefreshState();
        }

        private void RefreshState()
        {
            if (IsActive)
                SuppressOtherCanvases();
            else
                RestoreOtherCanvases();
        }

        private void SuppressOtherCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Canvas canvas in canvases)
            {
                if (canvas == null)
                    continue;

                if (EndDayCanvases.Contains(canvas))
                {
                    canvas.enabled = true;
                    continue;
                }

                if (!previousCanvasStates.ContainsKey(canvas))
                    previousCanvasStates.Add(canvas, canvas.enabled);

                canvas.enabled = false;
            }
        }

        private void RestoreOtherCanvases()
        {
            foreach (KeyValuePair<Canvas, bool> entry in previousCanvasStates)
            {
                if (entry.Key != null)
                    entry.Key.enabled = entry.Value;
            }

            previousCanvasStates.Clear();
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            RestoreOtherCanvases();
            instance = null;
        }
    }
}
