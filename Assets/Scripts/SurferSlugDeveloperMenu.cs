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
        private const int MenuItemCount = 8;
        private const string DeveloperUnlockedKey = "SurferSlug.DeveloperUnlocked";

        private static SurferSlugDeveloperMenu instance;
        public static bool IsOpen => instance != null && instance.visible;
        public static bool IsUnlocked => PlayerPrefs.GetInt(DeveloperUnlockedKey, 0) == 1;

        public static void UnlockAndOpen()
        {
            PlayerPrefs.SetInt(DeveloperUnlockedKey, 1);
            PlayerPrefs.Save();

            if (instance == null)
                return;

            // The pause menu temporarily disables gameplay behaviours. Re-enable
            // this persistent overlay so the completed secret code can open it.
            instance.enabled = true;
            instance.unlockNoticeUntil = Time.unscaledTime + 2.25f;
            instance.SetVisible(true);
        }

        private bool visible;
        private bool godMode;
        private bool infiniteLives;
        private int selectedIndex;
        private float nextNavigationTime;
        private float previousTimeScale = 1f;
        private float unlockNoticeUntil;

        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle infoStyle;

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
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (visible)
                Time.timeScale = previousTimeScale;

            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            // F10 becomes a convenient shortcut only after the controller code
            // has been discovered once on this installation.
            if (IsUnlocked && TogglePressed())
                SetVisible(!visible);

            if (visible)
                UpdateControllerNavigation();

            if (infiniteLives && SurfRunLifeManager.Instance != null)
                SurfRunLifeManager.Instance.RestoreLives(SurfRunLifeManager.Instance.StartingLives);

            if (godMode)
            {
                foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
                {
                    if (surfer != null && surfer.IsPlayerControlled)
                        surfer.HealFromHeart(99);
                }
            }
        }

        private void SetVisible(bool shouldShow)
        {
            if (visible == shouldShow)
                return;

            visible = shouldShow;

            if (visible)
            {
                selectedIndex = 0;
                nextNavigationTime = Time.unscaledTime + 0.15f;
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                SurferSlugPauseMenu.Instance?.SetDeveloperOverlayPresentation(true);
            }
            else
            {
                Time.timeScale = previousTimeScale;
                SurferSlugPauseMenu.Instance?.SetDeveloperOverlayPresentation(false);
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

        private void UpdateControllerNavigation()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return;

            float vertical = gamepad.leftStick.y.ReadValue();
            bool upPressed = gamepad.dpad.up.wasPressedThisFrame;
            bool downPressed = gamepad.dpad.down.wasPressedThisFrame;

            if (Time.unscaledTime >= nextNavigationTime)
            {
                if (upPressed || vertical > 0.55f)
                {
                    selectedIndex = (selectedIndex - 1 + MenuItemCount) % MenuItemCount;
                    nextNavigationTime = Time.unscaledTime + 0.18f;
                }
                else if (downPressed || vertical < -0.55f)
                {
                    selectedIndex = (selectedIndex + 1) % MenuItemCount;
                    nextNavigationTime = Time.unscaledTime + 0.18f;
                }
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
                ActivateSelectedItem();

            if (gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)
                SetVisible(false);
#endif
        }

        private void ActivateSelectedItem()
        {
            switch (selectedIndex)
            {
                case 0:
                    godMode = !godMode;
                    break;
                case 1:
                    infiniteLives = !infiniteLives;
                    break;
                case 2:
                    SurfAbilityProgression.Instance?.DebugUnlockAll();
                    break;
                case 3:
                    AirTrickScoreSystem.Instance?.DebugMaxFlow();
                    break;
                case 4:
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugNextChapter();
                    break;
                case 5:
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugSpawnBoss();
                    break;
                case 6:
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugNextDay();
                    break;
                case 7:
                    SetVisible(false);
                    FindFirstObjectByType<SurfDayProgressionDirector>()?.DebugResetCurrentDay();
                    break;
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = PixelFontLibrary.Medium,
                fontSize = 18,
                fixedHeight = 38,
                alignment = TextAnchor.MiddleCenter
            };

            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.textColor = new Color(1f, 0.88f, 0.18f, 1f);
            selectedButtonStyle.hover.textColor = selectedButtonStyle.normal.textColor;
            selectedButtonStyle.focused.textColor = selectedButtonStyle.normal.textColor;

            infoStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Medium,
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private bool DrawMenuButton(int index, string label)
        {
            GUIStyle style = selectedIndex == index ? selectedButtonStyle : buttonStyle;
            bool clicked = GUILayout.Button(selectedIndex == index ? ">  " + label + "  <" : label, style);
            if (clicked)
            {
                selectedIndex = index;
                ActivateSelectedItem();
                return true;
            }
            return false;
        }

        private void OnGUI()
        {
            if (!visible) return;

            EnsureStyles();

            if (Time.unscaledTime < unlockNoticeUntil)
            {
                GUIStyle noticeStyle = new GUIStyle(titleStyle)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(new Rect(0f, Screen.height - 82f, Screen.width, 44f),
                    "DEVELOPER MODE ENABLED", noticeStyle);
            }

            const float panelWidth = 430f;
            const float panelHeight = 610f;

            Rect panel = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0.04f, 0.075f, 0.96f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = oldColor;

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, panel.height - 28f));
            GUILayout.Label("DEVELOPER MENU  •  F10", titleStyle);
            GUILayout.Label("D-PAD / STICK: SELECT   A: USE   B: CLOSE", infoStyle);
            GUILayout.Space(10f);

            DrawMenuButton(0, "God Mode  [" + (godMode ? "ON" : "OFF") + "]");
            DrawMenuButton(1, "Infinite Lives  [" + (infiniteLives ? "ON" : "OFF") + "]");
            GUILayout.Space(8f);
            DrawMenuButton(2, "Unlock All Mechanics");
            DrawMenuButton(3, "Max Flow / ON FIRE");
            DrawMenuButton(4, "Next Chapter");
            DrawMenuButton(5, "Spawn Current Day Boss");
            DrawMenuButton(6, "Next Day");
            DrawMenuButton(7, "Reset Current Day");

            GUILayout.Space(10f);
            SurfDayProgressionDirector director = FindFirstObjectByType<SurfDayProgressionDirector>();
            if (director != null)
            {
                GUILayout.Label(
                    $"DAY {director.CurrentDay}  •  {director.CurrentChapter}\nTIME {director.RunTime:0.0}s",
                    infoStyle);
            }

            GUILayout.EndArea();
        }
    }
}
