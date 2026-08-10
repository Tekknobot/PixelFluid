using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelOcean
{
    /// <summary>
    /// Shared runtime TextMeshPro construction and staggered motion for the
    /// transparent end-of-day screens. Everything is generated in code, so no
    /// scene prefab or inspector wiring is required.
    /// </summary>
    internal static class EndDayPanelPresentation
    {
        internal sealed class MotionElement
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 TargetPosition;
            public int Order;
        }

        public static readonly Color Cyan = new(0.42f, 0.94f, 1f, 1f);
        public static readonly Color Gold = new(1f, 0.79f, 0.22f, 1f);
        public static readonly Color White = new(0.97f, 0.99f, 1f, 1f);
        public static readonly Color SoftWhite = new(0.84f, 0.93f, 0.97f, 1f);

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject canvasObject = new(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            return canvas;
        }

        public static RectTransform CreateTopLeftRect(Transform parent, string name,
            Vector2 position, Vector2 size)
        {
            GameObject item = new(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;
            return rect;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name,
            string text, TMP_FontAsset font, float fontSize,
            TextAlignmentOptions alignment, Color colour)
        {
            GameObject textObject = new(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Normal;
            label.alignment = alignment;
            label.color = colour;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.margin = Vector4.zero;

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(3f, -3f);
            shadow.useGraphicAlpha = true;
            return label;
        }

        public static Image CreateRule(Transform parent, string name, Color colour)
        {
            GameObject ruleObject = new(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            ruleObject.transform.SetParent(parent, false);
            Image image = ruleObject.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        public static MotionElement AddMotion(RectTransform rect, int order,
            List<MotionElement> elements)
        {
            CanvasGroup group = rect.gameObject.GetComponent<CanvasGroup>();
            if (group == null)
                group = rect.gameObject.AddComponent<CanvasGroup>();

            MotionElement element = new()
            {
                Rect = rect,
                Group = group,
                TargetPosition = rect.anchoredPosition,
                Order = order
            };
            group.alpha = 0f;
            rect.anchoredPosition = element.TargetPosition + Vector2.left * 76f;
            elements.Add(element);
            return element;
        }

        public static float Reveal(float shownAt, int order, float delay = 0.065f,
            float duration = 0.38f)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - shownAt - order * delay) /
                Mathf.Max(0.01f, duration));
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        public static void Animate(List<MotionElement> elements, float shownAt)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                MotionElement element = elements[i];
                if (element == null || element.Rect == null || element.Group == null)
                    continue;

                float reveal = Reveal(shownAt, element.Order);
                element.Group.alpha = reveal;
                element.Rect.anchoredPosition = element.TargetPosition +
                    Vector2.left * ((1f - reveal) * 76f);
            }
        }

        public static void ResetMotion(List<MotionElement> elements)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                MotionElement element = elements[i];
                if (element == null || element.Rect == null || element.Group == null)
                    continue;

                element.Group.alpha = 0f;
                element.Rect.anchoredPosition = element.TargetPosition + Vector2.left * 76f;
            }
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
