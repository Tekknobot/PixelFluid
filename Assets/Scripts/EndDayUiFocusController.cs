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
                EndDayCanvases.RemoveWhere(canvas => canvas == null);
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
