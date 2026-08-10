using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PixelOcean
{
    [DefaultExecutionOrder(-11850)]
    public sealed class SurfDayUpgradeScreen : MonoBehaviour
    {
        private sealed class OptionView
        {
            public RectTransform Row;
            public Image Rail;
            public TextMeshProUGUI Number;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Detail;
            public TextMeshProUGUI SelectedTag;
        }

        public static SurfDayUpgradeScreen Instance { get; private set; }

        private bool visible;
        private bool chosen;
        private int selectedIndex;
        private bool navigationHeld;
        private float previousTimeScale = 1f;
        private float revealStartedAt;
        private float selectionChangedAt;
        private float lastClosedAt = -100f;

        private Canvas panelCanvas;
        private RectTransform contentRoot;
        private readonly List<EndDayPanelPresentation.MotionElement> motionElements = new();
        private readonly List<OptionView> optionViews = new();

        public bool IsVisible => visible;

        private static readonly string[] UpgradeNames =
        {
            "HIGHER LAUNCH",
            "FASTER WATER SLASH",
            "STRONGER SKID"
        };

        private static readonly string[] UpgradeDetails =
        {
            "Catch more air  /  Jump height +10%",
            "Return to the flow sooner  /  Cooldown -15%",
            "Drive through the wave  /  Charged speed +15%"
        };

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
            if (visible)
            {
                // A duplicate day-completion coroutine shares the current panel
                // instead of resetting its selection or creating another entry.
                while (visible)
                    yield return null;
                yield break;
            }

            if (Time.unscaledTime - lastClosedAt < 0.50f)
                yield break;

            AirTrickScoreSystem.Instance?.DismissRecapImmediate();
            EnsureUi();
            visible = true;
            chosen = false;
            selectedIndex = 0;
            navigationHeld = true;
            revealStartedAt = Time.unscaledTime;
            selectionChangedAt = revealStartedAt;
            EndDayPanelPresentation.ResetMotion(motionElements);
            UpdateSelectionVisuals();
            panelCanvas.gameObject.SetActive(true);
            EndDayUiFocusController.Begin(panelCanvas);

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            while (!chosen)
                yield return null;

            visible = false;
            if (panelCanvas != null)
            {
                panelCanvas.gameObject.SetActive(false);
                EndDayUiFocusController.End(panelCanvas);
            }
            lastClosedAt = Time.unscaledTime;
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
            if (panelCanvas != null)
            {
                panelCanvas.gameObject.SetActive(false);
                EndDayUiFocusController.End(panelCanvas);
            }
            lastClosedAt = Time.unscaledTime;
            Time.timeScale = previousTimeScale;
        }

        private void Update()
        {
            if (!visible || chosen)
                return;

            EndDayPanelPresentation.Animate(motionElements, revealStartedAt);
            UpdateSelectionVisuals();
            ReadNavigationInput();
            ReadMouseInput();
        }

        private void ReadNavigationInput()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool previousPressed = keyboard != null &&
                (keyboard.leftArrowKey.wasPressedThisFrame ||
                 keyboard.upArrowKey.wasPressedThisFrame ||
                 keyboard.aKey.wasPressedThisFrame ||
                 keyboard.wKey.wasPressedThisFrame);
            bool nextPressed = keyboard != null &&
                (keyboard.rightArrowKey.wasPressedThisFrame ||
                 keyboard.downArrowKey.wasPressedThisFrame ||
                 keyboard.dKey.wasPressedThisFrame ||
                 keyboard.sKey.wasPressedThisFrame);

            float navigation = 0f;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                navigation = Mathf.Abs(stick.y) > Mathf.Abs(stick.x) ? -stick.y : stick.x;
                if (gamepad.dpad.left.isPressed || gamepad.dpad.up.isPressed) navigation = -1f;
                if (gamepad.dpad.right.isPressed || gamepad.dpad.down.isPressed) navigation = 1f;
            }

            bool controllerDirectionHeld = Mathf.Abs(navigation) >= 0.55f;
            if (!controllerDirectionHeld)
                navigationHeld = false;

            if (previousPressed || (navigation < -0.55f && !navigationHeld))
            {
                ChangeSelection((selectedIndex + UpgradeNames.Length - 1) % UpgradeNames.Length);
                navigationHeld = controllerDirectionHeld;
            }
            else if (nextPressed || (navigation > 0.55f && !navigationHeld))
            {
                ChangeSelection((selectedIndex + 1) % UpgradeNames.Length);
                navigationHeld = controllerDirectionHeld;
            }

            bool confirm = keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame);
            confirm |= gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;

            if (confirm && Time.unscaledTime - revealStartedAt >= 0.25f)
                Apply(selectedIndex);
        }

        private void ReadMouseInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasReleasedThisFrame)
                return;

            Vector2 pointer = mouse.position.ReadValue();
            for (int i = 0; i < optionViews.Count; i++)
            {
                OptionView view = optionViews[i];
                if (view?.Row == null ||
                    !RectTransformUtility.RectangleContainsScreenPoint(view.Row, pointer, null))
                    continue;

                ChangeSelection(i);
                Apply(i);
                return;
            }
        }

        private void ChangeSelection(int index)
        {
            if (selectedIndex == index)
                return;

            selectedIndex = Mathf.Clamp(index, 0, UpgradeNames.Length - 1);
            selectionChangedAt = Time.unscaledTime;
            UpdateSelectionVisuals();
        }

        private void UpdateSelectionVisuals()
        {
            for (int i = 0; i < optionViews.Count; i++)
            {
                OptionView view = optionViews[i];
                if (view == null)
                    continue;

                bool selected = i == selectedIndex;
                Color main = selected
                    ? EndDayPanelPresentation.Gold
                    : new Color(0.83f, 0.92f, 0.96f, 0.78f);
                Color detail = selected
                    ? EndDayPanelPresentation.White
                    : new Color(0.76f, 0.86f, 0.91f, 0.66f);

                view.Rail.color = selected
                    ? EndDayPanelPresentation.Gold
                    : new Color(0.02f, 0.40f, 0.78f, 0.72f);
                view.Number.color = selected
                    ? EndDayPanelPresentation.Gold
                    : EndDayPanelPresentation.Cyan;
                view.Name.color = main;
                view.Detail.color = detail;
                view.SelectedTag.gameObject.SetActive(selected);

                float pulse = selected
                    ? 1f + Mathf.Sin(Mathf.Clamp01(
                        (Time.unscaledTime - selectionChangedAt) / 0.35f) * Mathf.PI) * 0.45f
                    : 1f;
                view.Rail.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal, (selected ? 6f : 2f) * pulse);
            }
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

        private void EnsureUi()
        {
            if (panelCanvas != null)
                return;

            panelCanvas = EndDayPanelPresentation.CreateCanvas(
                transform, "End Day Upgrade TMP Canvas", 32600);
            GraphicRaycaster raycaster = panelCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;

            GameObject content = new("Left Middle Upgrade Stack", typeof(RectTransform));
            content.transform.SetParent(panelCanvas.transform, false);
            contentRoot = content.GetComponent<RectTransform>();
            contentRoot.anchorMin = contentRoot.anchorMax = new Vector2(0f, 0.5f);
            contentRoot.pivot = new Vector2(0f, 0.5f);
            contentRoot.anchoredPosition = new Vector2(106f, 0f);
            contentRoot.sizeDelta = new Vector2(780f, 660f);

            CreateMotionText("Eyebrow", "END OF DAY  /  ONE CHOICE",
                new Vector2(0f, 0f), new Vector2(780f, 28f),
                PixelFontLibrary.TmpMedium, 19f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.Cyan, 0);
            CreateMotionText("Heading", "CHOOSE YOUR NEXT WAVE",
                new Vector2(0f, 30f), new Vector2(780f, 68f),
                PixelFontLibrary.TmpBold, 55f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.White, 1);
            CreateMotionText("Subheading", "Pick one permanent upgrade before the next day.",
                new Vector2(0f, 99f), new Vector2(780f, 34f),
                PixelFontLibrary.TmpRegular, 23f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.SoftWhite, 2);

            const float optionsTop = 160f;
            const float optionHeight = 108f;
            const float optionGap = 12f;
            for (int i = 0; i < UpgradeNames.Length; i++)
                CreateOption(i, optionsTop + i * (optionHeight + optionGap), 3 + i);

            float footerY = optionsTop + UpgradeNames.Length * (optionHeight + optionGap) + 6f;
            RectTransform ruleGroup = EndDayPanelPresentation.CreateTopLeftRect(
                contentRoot, "Footer Rule Motion", new Vector2(0f, footerY), new Vector2(530f, 2f));
            Image rule = EndDayPanelPresentation.CreateRule(
                ruleGroup, "Footer Rule", new Color(0.42f, 0.94f, 1f, 0.62f));
            EndDayPanelPresentation.Stretch(rule.rectTransform);
            EndDayPanelPresentation.AddMotion(ruleGroup, 7, motionElements);

            CreateMotionText("Input Hint", "D-PAD / STICK  CHOOSE     A / ENTER  CONFIRM",
                new Vector2(0f, footerY + 13f), new Vector2(780f, 30f),
                PixelFontLibrary.TmpMedium, 18f, TextAlignmentOptions.MidlineLeft,
                EndDayPanelPresentation.White, 8);

            panelCanvas.gameObject.SetActive(false);
        }

        private TextMeshProUGUI CreateMotionText(string name, string text,
            Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize,
            TextAlignmentOptions alignment, Color colour, int order)
        {
            RectTransform group = EndDayPanelPresentation.CreateTopLeftRect(
                contentRoot, name + " Motion", position, size);
            TextMeshProUGUI label = EndDayPanelPresentation.CreateText(
                group, name, text, font, fontSize, alignment, colour);
            EndDayPanelPresentation.Stretch(label.rectTransform);
            EndDayPanelPresentation.AddMotion(group, order, motionElements);
            return label;
        }

        private void CreateOption(int index, float y, int order)
        {
            RectTransform row = EndDayPanelPresentation.CreateTopLeftRect(
                contentRoot, "Upgrade " + (index + 1) + " Motion",
                new Vector2(0f, y), new Vector2(780f, 108f));
            EndDayPanelPresentation.AddMotion(row, order, motionElements);

            Image rail = CreatePlacedRule(row, "Selection Rail",
                new Vector2(0f, 7f), new Vector2(6f, 74f),
                EndDayPanelPresentation.Gold);
            TextMeshProUGUI number = CreatePlacedText(row, "Number", "0" + (index + 1),
                new Vector2(17f, 10f), new Vector2(42f, 31f),
                PixelFontLibrary.TmpMedium, 18f, TextAlignmentOptions.TopLeft,
                EndDayPanelPresentation.Cyan);
            TextMeshProUGUI optionName = CreatePlacedText(row, "Name", UpgradeNames[index],
                new Vector2(69f, 4f), new Vector2(702f, 47f),
                PixelFontLibrary.TmpSemiBold, 31f, TextAlignmentOptions.TopLeft,
                EndDayPanelPresentation.White);
            TextMeshProUGUI detail = CreatePlacedText(row, "Detail", UpgradeDetails[index],
                new Vector2(70f, 52f), new Vector2(698f, 35f),
                PixelFontLibrary.TmpRegular, 20f, TextAlignmentOptions.TopLeft,
                EndDayPanelPresentation.SoftWhite);
            TextMeshProUGUI selectedTag = CreatePlacedText(row, "Selected", "SELECTED",
                new Vector2(0f, 13f), new Vector2(772f, 26f),
                PixelFontLibrary.TmpSemiBold, 15f, TextAlignmentOptions.TopRight,
                EndDayPanelPresentation.Gold);

            optionViews.Add(new OptionView
            {
                Row = row,
                Rail = rail,
                Number = number,
                Name = optionName,
                Detail = detail,
                SelectedTag = selectedTag
            });
        }

        private static TextMeshProUGUI CreatePlacedText(Transform parent, string name,
            string text, Vector2 position, Vector2 size, TMP_FontAsset font,
            float fontSize, TextAlignmentOptions alignment, Color colour)
        {
            RectTransform holder = EndDayPanelPresentation.CreateTopLeftRect(
                parent, name + " Holder", position, size);
            TextMeshProUGUI label = EndDayPanelPresentation.CreateText(
                holder, name, text, font, fontSize, alignment, colour);
            EndDayPanelPresentation.Stretch(label.rectTransform);
            return label;
        }

        private static Image CreatePlacedRule(Transform parent, string name,
            Vector2 position, Vector2 size, Color colour)
        {
            RectTransform holder = EndDayPanelPresentation.CreateTopLeftRect(
                parent, name + " Holder", position, size);
            Image image = EndDayPanelPresentation.CreateRule(holder, name, colour);
            EndDayPanelPresentation.Stretch(image.rectTransform);
            return image;
        }
    }
}
