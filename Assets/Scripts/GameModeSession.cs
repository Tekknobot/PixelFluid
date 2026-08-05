using System;
using UnityEngine;

namespace PixelOcean
{
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class GameModeSession : MonoBehaviour
    {
        public enum Mode { None, Story, Race }
        public static GameModeSession Instance { get; private set; }
        public static Mode CurrentMode { get; private set; } = Mode.None;
        public static bool HasChosenMode => CurrentMode != Mode.None;
        public static bool IsStory => CurrentMode == Mode.Story;
        public static bool IsRace => CurrentMode == Mode.Race;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<GameModeSession>() == null)
            {
                GameObject host = new("Game Mode Session");
                DontDestroyOnLoad(host);
                host.AddComponent<GameModeSession>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentMode = Mode.None;
        }

        private void LateUpdate()
        {
            if (CurrentMode == Mode.None)
                RemoveGameplayPopulation();
        }

        public static void SelectStoryMode()
        {
            // Set the destination mode before Race cleanup restores spawners.
            // ExitRaceMode checks IsStory when deciding what may be re-enabled.
            CurrentMode = Mode.Story;
            RaceModeManager.EnsureInstance().ExitRaceMode(true);
            SetStoryPresentation(true);
            TinyWaveSurferBootstrap.ResetSpawnState();
        }

        public static void SelectRaceMode()
        {
            CurrentMode = Mode.Race;
            SetStoryPresentation(false);
            RemoveGameplayPopulation();
        }

        public static void ReturnToModeSelect()
        {
            RaceModeManager.EnsureInstance().ExitRaceMode(true);
            CurrentMode = Mode.None;
            SetStoryPresentation(false);
            RemoveGameplayPopulation();
        }

        private static void SetStoryPresentation(bool enabled)
        {
            foreach (SurfDayProgressionDirector x in FindObjectsByType<SurfDayProgressionDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None)) x.enabled = enabled;
            foreach (StoryboardCutsceneSystem x in FindObjectsByType<StoryboardCutsceneSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)) x.enabled = enabled;
            foreach (SurferSlugMinimalHud x in FindObjectsByType<SurferSlugMinimalHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                x.enabled = true;
                x.SetStoryHudActive(enabled);
            }
        }

        public static void RemoveGameplayPopulation()
        {
            DestroyAll<TinyWaveSurfer>();
            DestroyAll<SharkLaneSwimmer>();
            DestroyAll<GiantSquidLaneSwimmer>();
            DestroyAll<WhaleLaneSwimmer>();
            DestroyAll<JellyfishSchoolController>();
            DestroyAll<BloodSharkLaneSwimmer>();
            DestroyAll<StingrayLaneSwimmer>();
            DestroyAll<BloodfishSchoolController>();
            DestroyAll<StrugglingSwimmerDrifter>();
            DestroyAll<BoomboxSurferSwimmer>();
            DestroyAll<GodzillaLaneSwimmer>();
            DestroyAll<RubberDuckBossSwimmer>();
            DestroyAll<AlienUfoController>();
            DestroyAll<DayTwoHelicopterController>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            foreach (T item in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item != null) Destroy(item.gameObject);
        }
    }
}
