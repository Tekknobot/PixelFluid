using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Periodically saves the active run and flushes it when the app is paused,
    /// backgrounded, loses focus, or closes.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    [DisallowMultipleComponent]
    public sealed class SurfPersistentSaveManager : MonoBehaviour
    {
        [SerializeField, Min(10f)] private float autoSaveInterval = 60f;
        private float nextAutoSaveAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurfPersistentSaveManager>() != null)
                return;

            GameObject host = new("Surf Persistent Save Manager");
            DontDestroyOnLoad(host);
            host.AddComponent<SurfPersistentSaveManager>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            nextAutoSaveAt = Time.unscaledTime + autoSaveInterval;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextAutoSaveAt)
                return;

            nextAutoSaveAt = Time.unscaledTime + Mathf.Max(10f, autoSaveInterval);
            SaveActiveRun();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveActiveRun();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                SaveActiveRun();
        }

        private void OnApplicationQuit()
        {
            SaveActiveRun();
        }

        public static void SaveActiveRun()
        {
            TinyWaveSurfer[] surfers = FindObjectsByType<TinyWaveSurfer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            bool activePlayerRun = false;
            foreach (TinyWaveSurfer surfer in surfers)
            {
                if (surfer != null && surfer.IsPlayerControlled)
                {
                    activePlayerRun = true;
                    break;
                }
            }

            // The title/menu scene also owns a persistent day director. Do not let
            // a minute spent at the menu overwrite the player's real Continue save.
            if (!activePlayerRun)
                return;

            SurfDayProgressionDirector director = FindFirstObjectByType<SurfDayProgressionDirector>();
            if (director != null)
                SurfStageSaveSystem.Save(director);
        }
    }
}
