using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelOcean
{
    [DefaultExecutionOrder(-11850)]
    public sealed class SurfDayUpgradeScreen : MonoBehaviour
    {
        public static SurfDayUpgradeScreen Instance { get; private set; }

        private bool visible;
        private bool chosen;
        private int selectedIndex;
        private bool horizontalHeld;
        private float previousTimeScale = 1f;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle detailStyle;
        private GUIStyle hintStyle;

        private Texture2D panelTexture;
        private Texture2D buttonTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D selectedTexture;
        private Texture2D selectedPressedTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurfDayUpgradeScreen>() != null) return;
            GameObject host = new("Surf Day Upgrade Screen");
            DontDestroyOnLoad(host);
            host.AddComponent<SurfDayUpgradeScreen>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public IEnumerator ShowAndWait()
        {
            visible = true;
            chosen = false;
            selectedIndex = 0;
            horizontalHeld = true; // Require the stick/d-pad to be released once.

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            while (!chosen)
                yield return null;

            visible = false;
            Time.timeScale = previousTimeScale;
        }

        /// <summary>
        /// Releases an upgrade screen immediately when Developer Mode replaces
        /// an in-progress day transition. Without this, cancelling the owning
        /// director coroutine can leave the game permanently paused.
        /// </summary>
        public void CancelImmediate()
        {
            if (!visible)
                return;

            chosen = true;
            visible = false;
            Time.timeScale = previousTimeScale;
        }

        private void Update()
        {
            if (!visible || chosen)
                return;

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool leftPressed = keyboard != null &&
                (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame);
            bool rightPressed = keyboard != null &&
                (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame);

            float horizontal = 0f;
            if (gamepad != null)
            {
                horizontal = gamepad.leftStick.x.ReadValue();
                if (gamepad.dpad.left.isPressed) horizontal = -1f;
                if (gamepad.dpad.right.isPressed) horizontal = 1f;
            }

            bool controllerDirectionHeld = Mathf.Abs(horizontal) >= 0.55f;
            if (!controllerDirectionHeld)
                horizontalHeld = false;

            if (leftPressed || (horizontal < -0.55f && !horizontalHeld))
            {
                selectedIndex = (selectedIndex + 2) % 3;
                horizontalHeld = controllerDirectionHeld;
            }
            else if (rightPressed || (horizontal > 0.55f && !horizontalHeld))
            {
                selectedIndex = (selectedIndex + 1) % 3;
                horizontalHeld = controllerDirectionHeld;
            }

            bool confirm = keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame);

            confirm |= gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;

            if (confirm)
                Apply(selectedIndex);
        }

        private void Apply(int index)
        {
            if (chosen)
                return;

            TinyWaveSurfer player = null;
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
            {
                if (surfer != null && surfer.IsPlayerControlled)
                {
                    player = surfer;
                    break;
                }
            }

            if (SurfAbilityProgression.Instance != null)
                SurfAbilityProgression.Instance.AddUpgrade(index);
            else if (player != null)
                player.ApplyDayUpgrade(index);

            chosen = true;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            panelTexture = MakeTexture(new Color(0.015f, 0.08f, 0.13f, 0.97f));
            buttonTexture = MakeTexture(new Color(0.03f, 0.22f, 0.30f, 1f));
            buttonHoverTexture = MakeTexture(new Color(0.04f, 0.31f, 0.40f, 1f));
            selectedTexture = MakeTexture(new Color(0.05f, 0.48f, 0.58f, 1f));
            selectedPressedTexture = MakeTexture(new Color(0.95f, 0.70f, 0.16f, 1f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 48,
                fontStyle = FontStyle.Normal,
                normal = { textColor = Color.white }
            };

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Medium,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                normal = { textColor = new Color(0.55f, 0.92f, 1f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = PixelFontLibrary.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                padding = new RectOffset(18, 18, 20, 20),
                normal = { background = buttonTexture, textColor = Color.white },
                hover = { background = buttonHoverTexture, textColor = Color.white },
                active = { background = selectedPressedTexture, textColor = new Color(0.03f, 0.08f, 0.10f, 1f) },
                focused = { background = buttonHoverTexture, textColor = Color.white }
            };

            selectedButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { background = selectedTexture, textColor = Color.white },
                hover = { background = selectedTexture, textColor = Color.white },
                focused = { background = selectedTexture, textColor = Color.white },
                active = { background = selectedPressedTexture, textColor = new Color(0.03f, 0.08f, 0.10f, 1f) }
            };

            detailStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Medium,
                alignment = TextAnchor.UpperCenter,
                fontSize = 23,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.96f, 1f, 1f) }
            };

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                font = PixelFontLibrary.Medium,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                normal = { textColor = new Color(1f, 0.78f, 0.24f, 1f) }
            };
        }

        private void DrawBorder(Rect rect, Color color, float thickness)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();

            float width = Mathf.Min(1080f, Screen.width - 36f);
            float height = Mathf.Min(620f, Screen.height - 36f);
            Rect panel = new((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.DrawTexture(panel, panelTexture);
            DrawBorder(panel, new Color(0.25f, 0.88f, 1f, 1f), 4f);
            DrawBorder(new Rect(panel.x + 8f, panel.y + 8f, panel.width - 16f, panel.height - 16f),
                new Color(1f, 0.72f, 0.18f, 0.9f), 2f);

            GUI.Label(new Rect(panel.x + 24f, panel.y + 22f, panel.width - 48f, 58f),
                "CHOOSE YOUR NEXT WAVE", titleStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 77f, panel.width - 48f, 40f),
                "PICK ONE UPGRADE BEFORE THE NEXT DAY", subtitleStyle);

            string[] names = { "HIGHER LAUNCH", "FASTER WATER SLASH", "STRONGER SKID" };
            string[] details =
            {
                "Catch more air.\nJump height +10%",
                "Get back in the flow sooner.\nWater Slash cooldown -15%",
                "Hit the wave with more force.\nCharged skid speed +15%"
            };

            float gap = 20f;
            float buttonWidth = (panel.width - 96f - gap * 2f) / 3f;
            float buttonY = panel.y + 145f;
            float buttonHeight = 250f;

            for (int i = 0; i < 3; i++)
            {
                float x = panel.x + 48f + i * (buttonWidth + gap);
                Rect buttonRect = new(x, buttonY, buttonWidth, buttonHeight);
                bool selected = i == selectedIndex;

                if (selected)
                {
                    Rect glow = new(buttonRect.x - 6f, buttonRect.y - 6f, buttonRect.width + 12f, buttonRect.height + 12f);
                    DrawBorder(glow, new Color(1f, 0.76f, 0.20f, 1f), 5f);
                    GUI.Label(new Rect(buttonRect.x, buttonRect.y - 36f, buttonRect.width, 30f), "≈  SELECTED  ≈", hintStyle);
                }

                if (GUI.Button(buttonRect, names[i], selected ? selectedButtonStyle : buttonStyle))
                {
                    selectedIndex = i;
                    Apply(i);
                }

                GUI.Label(new Rect(x + 10f, panel.y + 414f, buttonWidth - 20f, 82f), details[i], detailStyle);
            }

            GUI.Label(new Rect(panel.x + 20f, panel.yMax - 74f, panel.width - 40f, 42f),
                "LEFT STICK / D-PAD TO CHOOSE     A / ENTER TO CONFIRM", hintStyle);
        }

        private void OnDestroy()
        {
            if (panelTexture != null) Destroy(panelTexture);
            if (buttonTexture != null) Destroy(buttonTexture);
            if (buttonHoverTexture != null) Destroy(buttonHoverTexture);
            if (selectedTexture != null) Destroy(selectedTexture);
            if (selectedPressedTexture != null) Destroy(selectedPressedTexture);
        }
    }
}
