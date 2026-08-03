using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelOcean
{
    /// <summary>
    /// Runtime storyboard overlay. It freezes gameplay, dims the existing scene,
    /// ducks all audio, and presents square artwork with unscaled-time typewriter text.
    /// </summary>
    [DefaultExecutionOrder(-11750)]
    [DisallowMultipleComponent]
    public sealed class StoryboardCutsceneSystem : MonoBehaviour
    {
        private static StoryboardCutsceneSystem instance;

        [Header("Presentation")]
        [SerializeField, Range(0f, 1f)] private float dimOpacity = 1f;
        [SerializeField, Range(0f, 1f)] private float duckedAudioVolume = 0f;
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.30f;
        [SerializeField, Min(0.05f)] private float boardTransitionDuration = 0.24f;
        [SerializeField, Min(1f)] private float charactersPerSecond = 34f;
        [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
        [SerializeField, Min(128f)] private float boardDisplaySize = 560f;

        private Canvas canvas;
        private CanvasGroup rootGroup;
        private Image dimImage;
        private RectTransform boardRoot;
        private Image boardBorder;
        private Image boardImage;
        private TMP_Text dialogueText;
        private TMP_Text continueText;
        private TMP_FontAsset silverFont;
        private bool playing;
        private bool typing;
        private bool skipTyping;
        private float previousTimeScale = 1f;
        private float previousAudioVolume = 1f;

        public static bool IsPlaying => instance != null && instance.playing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            EnsureInstance();
        }

        public static StoryboardCutsceneSystem EnsureInstance()
        {
            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<StoryboardCutsceneSystem>();
            if (instance != null)
                return instance;

            GameObject host = new("Surfer Slug Storyboard Cutscene System");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<StoryboardCutsceneSystem>();
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
            BuildUi();
        }

        private void Update()
        {
            if (!playing || !AdvancePressed())
                return;

            if (typing)
                skipTyping = true;
        }

        public static IEnumerator PlayDayOneOpening()
        {
            StoryboardCutsceneSystem system = EnsureInstance();
            yield return system.PlaySequence(
                new[]
                {
                    "Storyboards/Day1/board_1",
                    "Storyboards/Day1/board_2",
                    "Storyboards/Day1/board_3"
                },
                new[]
                {
                    "The beach used to be full.",
                    "Nobody comes here anymore.",
                    "I should turn back.\n\nNot today."
                });
        }

        /// <summary>
        /// Plays the six-board Day 2 transition while the game remains frozen
        /// behind a full black fade. Artwork is loaded from Resources/Storyboards/Day2.
        /// </summary>
        public static IEnumerator PlayDayTwoOpening()
        {
            StoryboardCutsceneSystem system = EnsureInstance();
            yield return system.PlaySequence(
                new[]
                {
                    "Storyboards/Day2/board_1",
                    "Storyboards/Day2/board_2",
                    "Storyboards/Day2/board_3",
                    "Storyboards/Day2/board_4",
                    "Storyboards/Day2/board_5",
                    "Storyboards/Day2/board_6"
                },
                new[]
                {
                    "...IT'S TOO QUIET.",
                    "WHERE DID EVERYBODY GO?",
                    "WAIT... WHAT WAS THAT?",
                    "SOMETHING'S MOVING BENEATH ME.",
                    "THOSE AREN'T YESTERDAY'S SHARKS.",
                    "AND NOW THERE'S SOMETHING ABOVE ME."
                });
        }

        /// <summary>
        /// Plays the three-board Day 3 introduction before the Shadow Surfer and
        /// corrupted ocean systems become active. Artwork is loaded from
        /// Resources/Storyboards/Day3.
        /// </summary>
        public static IEnumerator PlayDayThreeOpening()
        {
            StoryboardCutsceneSystem system = EnsureInstance();
            yield return system.PlaySequence(
                new[]
                {
                    "Storyboards/Day3/board_1",
                    "Storyboards/Day3/board_2",
                    "Storyboards/Day3/board_3"
                },
                new[]
                {
                    "I'M SURE I SAW SOMEONE.",
                    "WHY ARE YOU FOLLOWING ME?",
                    "WHO ARE YOU?"
                });
        }

        private static Sprite LoadStoryboardSprite(string resourcePath)
        {
            Sprite board = Resources.Load<Sprite>(resourcePath);
            if (board != null)
                return board;

            // Some Pixel Lab boards import as Multiple sprites. In that case,
            // Resources.Load<Sprite>() may not return the first sub-sprite.
            Sprite[] boards = Resources.LoadAll<Sprite>(resourcePath);
            return boards != null && boards.Length > 0 ? boards[0] : null;
        }

        public IEnumerator PlaySequence(
            string[] resourcePaths,
            string[] lines)
        {
            if (playing || resourcePaths == null || lines == null)
                yield break;

            int pageCount = Mathf.Min(
                resourcePaths.Length,
                lines.Length);

            if (pageCount <= 0)
                yield break;

            BuildUi();

            playing = true;
            previousTimeScale = Time.timeScale;
            previousAudioVolume = AudioListener.volume;
            Time.timeScale = 0f;

            // Keep all board graphics hidden before the canvas becomes active.
            boardImage.sprite = null;
            boardImage.enabled = false;
            boardImage.color = new Color(1f, 1f, 1f, 0f);

            if (boardBorder != null)
            {
                boardBorder.enabled = false;

                Color borderColour = boardBorder.color;
                borderColour.a = 0f;
                boardBorder.color = borderColour;
            }

            boardRoot.anchoredPosition = new Vector2(0f, 56f);

            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = 0;
            continueText.text = string.Empty;

            dimImage.color = new Color(
                0f,
                0f,
                0f,
                dimOpacity);

            rootGroup.alpha = 0f;
            rootGroup.gameObject.SetActive(true);

            yield return FadeRoot(0f, 1f, fadeDuration);
            yield return FadeAudio(
                previousAudioVolume,
                duckedAudioVolume,
                fadeDuration);

            for (int i = 0; i < pageCount; i++)
            {
                Sprite board = LoadStoryboardSprite(resourcePaths[i]);

                if (board == null)
                {
                    Debug.LogWarning(
                        $"Storyboard board was not found at Resources/{resourcePaths[i]}.",
                        this);

                    continue;
                }

                // Assign the sprite before enabling the Image.
                boardImage.enabled = false;
                boardImage.sprite = board;
                boardImage.color = new Color(1f, 1f, 1f, 0f);
                boardImage.enabled = true;

                if (boardBorder != null)
                {
                    Color borderColour = boardBorder.color;
                    borderColour.a = 0f;
                    boardBorder.color = borderColour;
                    boardBorder.enabled = true;
                }

                dialogueText.text = string.Empty;
                dialogueText.maxVisibleCharacters = 0;
                continueText.text = string.Empty;

                yield return AnimateBoardIn(i == 0 ? 0 : 1);
                yield return TypeLine(lines[i]);

                continueText.text =
                    "A / SPACE / ENTER  •  CONTINUE";

                yield return WaitForAdvance();

                continueText.text = string.Empty;

                if (i < pageCount - 1)
                    yield return AnimateBoardOut();
            }

            // Hide the board before fading the overall canvas.
            boardImage.sprite = null;
            boardImage.enabled = false;

            if (boardBorder != null)
                boardBorder.enabled = false;

            yield return FadeRoot(1f, 0f, fadeDuration);

            yield return FadeAudio(
                AudioListener.volume,
                previousAudioVolume,
                fadeDuration);

            rootGroup.gameObject.SetActive(false);

            Time.timeScale = previousTimeScale;
            playing = false;
            typing = false;
            skipTyping = false;
        }

        private IEnumerator TypeLine(string line)
        {
            typing = true;
            skipTyping = false;
            dialogueText.text = line ?? string.Empty;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int count = dialogueText.textInfo.characterCount;
            float visible = 0f;
            while (dialogueText.maxVisibleCharacters < count)
            {
                if (skipTyping)
                {
                    dialogueText.maxVisibleCharacters = count;
                    break;
                }

                visible += Time.unscaledDeltaTime * charactersPerSecond;
                dialogueText.maxVisibleCharacters = Mathf.Min(count, Mathf.FloorToInt(visible));
                yield return null;
            }

            dialogueText.maxVisibleCharacters = count;
            typing = false;
            skipTyping = false;

            // Prevent the press used to finish typing from instantly advancing.
            yield return null;
        }

        private IEnumerator WaitForAdvance()
        {
            while (AdvanceHeld())
                yield return null;

            float pulse = 0f;
            while (!AdvancePressed())
            {
                pulse += Time.unscaledDeltaTime;
                Color colour = continueText.color;
                colour.a = Mathf.Lerp(0.45f, 1f, 0.5f + 0.5f * Mathf.Sin(pulse * 5f));
                continueText.color = colour;
                yield return null;
            }
        }

        private IEnumerator AnimateBoardIn(int direction)
        {
            float startX = direction == 0
                ? 0f
                : boardDisplaySize * 0.32f;

            boardRoot.anchoredPosition =
                new Vector2(startX, 56f);

            Color artworkColour = boardImage.color;
            artworkColour.a = 0f;
            boardImage.color = artworkColour;

            Color borderColour = boardBorder != null
                ? boardBorder.color
                : Color.clear;

            borderColour.a = 0f;

            if (boardBorder != null)
                boardBorder.color = borderColour;

            float elapsed = 0f;

            while (elapsed < boardTransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(
                    elapsed / boardTransitionDuration);

                float eased =
                    1f - Mathf.Pow(1f - t, 3f);

                boardRoot.anchoredPosition = new Vector2(
                    Mathf.Lerp(startX, 0f, eased),
                    56f);

                artworkColour.a = t;
                boardImage.color = artworkColour;

                if (boardBorder != null)
                {
                    borderColour.a = t;
                    boardBorder.color = borderColour;
                }

                yield return null;
            }

            boardRoot.anchoredPosition =
                new Vector2(0f, 56f);

            artworkColour.a = 1f;
            boardImage.color = artworkColour;

            if (boardBorder != null)
            {
                borderColour.a = 1f;
                boardBorder.color = borderColour;
            }
        }

        private IEnumerator AnimateBoardOut()
        {
            float elapsed = 0f;

            Color artworkColour = boardImage.color;

            Color borderColour = boardBorder != null
                ? boardBorder.color
                : Color.clear;

            while (elapsed < boardTransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(
                    elapsed / boardTransitionDuration);

                float eased = t * t;

                boardRoot.anchoredPosition = new Vector2(
                    Mathf.Lerp(
                        0f,
                        -boardDisplaySize * 0.25f,
                        eased),
                    56f);

                artworkColour.a = 1f - t;
                boardImage.color = artworkColour;

                if (boardBorder != null)
                {
                    borderColour.a = 1f - t;
                    boardBorder.color = borderColour;
                }

                yield return null;
            }

            boardImage.enabled = false;

            if (boardBorder != null)
                boardBorder.enabled = false;
        }

        private IEnumerator FadeRoot(float from, float to, float duration)
        {
            float elapsed = 0f;
            rootGroup.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                rootGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            rootGroup.alpha = to;
        }

        private static IEnumerator FadeAudio(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                AudioListener.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            AudioListener.volume = to;
        }

        private void BuildUi()
        {
            if (canvas != null)
                return;

            silverFont = Resources.Load<TMP_FontAsset>("Fonts/Silver SDF");
            if (silverFont == null)
                silverFont = PixelFontLibrary.TmpMedium;

            GameObject canvasObject = new(
                "Storyboard Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            rootGroup = canvasObject.GetComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = true;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

            dimImage = CreateImage("Dimmed Gameplay", canvasRect, Color.black);
            Stretch(
                dimImage.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            boardRoot = CreateRect("Storyboard Board", canvasRect);
            boardRoot.anchorMin = boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.sizeDelta = new Vector2(boardDisplaySize, boardDisplaySize);
            boardRoot.anchoredPosition = new Vector2(0f, 56f);

            boardBorder = CreateImage(
                "Board Border",
                boardRoot,
                new Color32(28, 36, 48, 255));

            Stretch(
                boardBorder.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(-6f, -6f),
                new Vector2(6f, 6f));

            // Prevent the empty Image component from producing a startup square.
            boardBorder.enabled = false;

            boardImage = CreateImage(
                "Board Artwork",
                boardRoot,
                new Color(1f, 1f, 1f, 0f));

            boardImage.preserveAspect = true;

            Stretch(
                boardImage.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            // It will only be enabled after a valid storyboard sprite is assigned.
            boardImage.sprite = null;
            boardImage.enabled = false;

            RectTransform dialoguePanel = CreateRect("Dialogue Panel", canvasRect);
            dialoguePanel.anchorMin = dialoguePanel.anchorMax =
                new Vector2(0.5f, 0.5f);
            dialoguePanel.pivot = new Vector2(0.5f, 1f);
            dialoguePanel.sizeDelta = new Vector2(920f, 190f);
            dialoguePanel.anchoredPosition = new Vector2(0f, -282f);

            AddBorder(dialoguePanel, 3f);

            dialogueText = CreateText(
                "Dialogue",
                dialoguePanel,
                32f,
                TextAlignmentOptions.Center);

            dialogueText.enableWordWrapping = true;
            dialogueText.overflowMode = TextOverflowModes.Overflow;

            Stretch(
                dialogueText.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(36f, 42f),
                new Vector2(-36f, -28f));

            continueText = CreateText(
                "Continue",
                dialoguePanel,
                16f,
                TextAlignmentOptions.BottomRight);

            Stretch(
                continueText.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(20f, 10f),
                new Vector2(-24f, -12f));

            rootGroup.gameObject.SetActive(false);
        }

        private TMP_Text CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = silverFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreateImage(string name, Transform parent, Color colour)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void AddBorder(RectTransform target, float thickness)
        {
            Color colour = new(1f, 1f, 1f, 0.88f);
            AddEdge(target, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, colour);
            AddEdge(target, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), colour);
            AddEdge(target, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), colour);
            AddEdge(target, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, colour);
        }

        private static void AddEdge(RectTransform target, string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, Color colour)
        {
            Image image = CreateImage("Border " + name, target, colour);
            Stretch(image.rectTransform, min, max, offsetMin, offsetMax);
        }

        private static bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboard = Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame);
            bool gamepad = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (keyboard || gamepad)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
            return false;
#endif
        }

        private static bool AdvanceHeld()
        {
#if ENABLE_INPUT_SYSTEM
            bool keyboard = Keyboard.current != null &&
                (Keyboard.current.spaceKey.isPressed ||
                 Keyboard.current.enterKey.isPressed ||
                 Keyboard.current.numpadEnterKey.isPressed);
            bool gamepad = Gamepad.current != null && Gamepad.current.buttonSouth.isPressed;
            if (keyboard || gamepad)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            if (playing)
            {
                Time.timeScale = previousTimeScale;
                AudioListener.volume = previousAudioVolume;
            }
        }
    }
}
