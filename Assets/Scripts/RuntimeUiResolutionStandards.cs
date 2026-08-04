using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelOcean
{
    /// <summary>
    /// Applies one resolution-independent UI standard to every runtime screen-space
    /// canvas. Layouts are authored at 1920x1080 and scale consistently at HD, QHD,
    /// 4K, ultrawide, and other high resolutions without changing pixel dimensions.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class RuntimeUiResolutionStandards : MonoBehaviour
    {
        private static RuntimeUiResolutionStandards instance;
        private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null)
                return;

            GameObject host = new("Runtime UI Resolution Standards");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<RuntimeUiResolutionStandards>();
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
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyToAllCanvases();
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToAllCanvases();
        }

        private void LateUpdate()
        {
            // Runtime-created HUDs and storyboards can appear after scene load.
            if (Time.frameCount % 120 == 0)
                ApplyToAllCanvases();
        }

        private static void ApplyToAllCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                    scaler = canvas.gameObject.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
                scaler.dynamicPixelsPerUnit = 100f;
            }
        }
    }
}
