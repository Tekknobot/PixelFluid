using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    [DefaultExecutionOrder(-11850)]
    [DisallowMultipleComponent]
    public sealed class SurferSlugDeveloperMenu : MonoBehaviour
    {
        private static SurferSlugDeveloperMenu instance;
        private bool visible;
        private bool godMode;
        private bool infiniteLives;
        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle toggleStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurferSlugDeveloperMenu>() != null) return;
            GameObject host = new("Surfer Slug Developer Menu");
            DontDestroyOnLoad(host);
            host.AddComponent<SurferSlugDeveloperMenu>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (TogglePressed()) visible = !visible;
            if (infiniteLives && SurfRunLifeManager.Instance != null)
                SurfRunLifeManager.Instance.RestoreLives(SurfRunLifeManager.Instance.StartingLives);
            if (godMode)
            {
                foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
                    if (surfer != null && surfer.IsPlayerControlled)
                        surfer.HealFromHeart(99);
            }
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F10);
#else
            return false;
#endif
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { font = PixelFontLibrary.Bold, fontSize = 26, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = PixelFontLibrary.Medium, fontSize = 18, fixedHeight = 38 };
            toggleStyle = new GUIStyle(GUI.skin.toggle) { font = PixelFontLibrary.Medium, fontSize = 18 };
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();
            float panelWidth = 390f;
            float panelHeight = 548f;

            Rect panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight
            );
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, panel.height - 24f));
            GUILayout.Label("DEVELOPER MENU  •  F10", titleStyle);
            GUILayout.Space(8f);
            godMode = GUILayout.Toggle(godMode, "God Mode", toggleStyle);
            infiniteLives = GUILayout.Toggle(infiniteLives, "Infinite Lives", toggleStyle);
            GUILayout.Space(8f);
            if (GUILayout.Button("Unlock All Mechanics", buttonStyle)) SurfAbilityProgression.Instance?.DebugUnlockAll();
            if (GUILayout.Button("Max Flow / ON FIRE", buttonStyle)) AirTrickScoreSystem.Instance?.DebugMaxFlow();
            if (GUILayout.Button("Next Chapter", buttonStyle)) FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugNextChapter();
            if (GUILayout.Button("Spawn Current Day Boss", buttonStyle)) FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugSpawnBoss();
            if (GUILayout.Button("Next Day", buttonStyle))
            {
                visible = false;
                FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugNextDay();
            }
            if (GUILayout.Button("Reset Current Day", buttonStyle)) FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugResetCurrentDay();
            GUILayout.Space(8f);
            SurfDayProgressionDirector director = FindFirstObjectByType<SurfDayProgressionDirector>();
            if (director != null)
                GUILayout.Label($"DAY {director.CurrentDay}  •  {director.CurrentChapter}\nTIME {director.RunTime:0.0}s", toggleStyle);
            GUILayout.EndArea();
        }
    }
}
