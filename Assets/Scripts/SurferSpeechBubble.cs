using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class SurferSpeechBubble : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Vector3 localOffset = new(0f, 0.72f, 0f);
        [SerializeField, Min(0.1f)] private float defaultDuration = 2.6f;
        [SerializeField] private int sortingOrderOffset = 40;

        [Header("Dynamic Layout")]
        [SerializeField, Range(12, 64)] private int preferredFontSize = 84;
        [SerializeField, Range(10, 48)] private int minimumFontSize = 84;
        [SerializeField, Min(4)] private int maximumCharactersPerLine = 100;
        [SerializeField, Min(4.25f)] private float maximumBubbleWorldWidth = 16.4f;
        [SerializeField, Min(1)] private int horizontalPaddingPixels = 2;
        [SerializeField, Min(1)] private int verticalPaddingPixels = 0;
        [SerializeField, Range(1, 4)] private int outlinePixels = 1;
        [SerializeField, Range(1, 16)] private int cornerRadiusPixels = 6;
        [SerializeField, Min(3)] private int tailHeightPixels = 4;
        [SerializeField, Min(0.001f)] private float textWorldScale = 0.012f;
        [SerializeField, Min(16f)] private float pixelsPerUnit = 32f;

        [Header("Colours")]
        [SerializeField] private Color bubbleColor = new(0.96f, 0.95f, 0.86f, 1f);
        [SerializeField] private Color outlineColor = new(0.08f, 0.07f, 0.06f, 1f);
        [SerializeField] private Color textColor = new(0f, 0f, 0f, 1f);

        private GameObject root;
        private SpriteRenderer bubbleRenderer;
        private TextMesh textMesh;
        private MeshRenderer textRenderer;
        private Sprite bubbleSprite;
        private Texture2D bubbleTexture;
        private float hideAt;

        public bool IsVisible => root != null && root.activeSelf;

        private void Awake()
        {
            BuildBubble();
            HideImmediate();
        }

        private void LateUpdate()
        {
            if (root != null && root.activeSelf && Time.time >= hideAt)
                HideImmediate();
        }

        public void Show(string message, float duration = -1f)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            BuildBubble();
            root.SetActive(true);
            LayoutForMessage(message.Trim().ToUpperInvariant());
            hideAt = Time.time + (duration > 0f ? duration : defaultDuration);
            RefreshSorting();
        }

        public void HideImmediate()
        {
            if (root != null)
                root.SetActive(false);
        }

        public void RefreshSorting()
        {
            if (bubbleRenderer == null || textRenderer == null)
                return;

            SpriteRenderer surferRenderer = GetComponent<SpriteRenderer>();
            int baseOrder = surferRenderer != null ? surferRenderer.sortingOrder : 0;
            string layerName = surferRenderer != null ? surferRenderer.sortingLayerName : "Default";

            bubbleRenderer.sortingLayerName = layerName;
            bubbleRenderer.sortingOrder = baseOrder + sortingOrderOffset;
            textRenderer.sortingLayerName = layerName;
            textRenderer.sortingOrder = baseOrder + sortingOrderOffset + 1;
        }

        private void BuildBubble()
        {
            if (root != null)
                return;

            root = new GameObject("Surfer Speech Bubble");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = localOffset;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            bubbleRenderer = root.AddComponent<SpriteRenderer>();

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one * textWorldScale;

            textMesh = textObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = preferredFontSize;
            textMesh.fontStyle = FontStyle.Normal;
            textMesh.characterSize = 1f;
            textMesh.lineSpacing = 0.9f;
            textMesh.color = textColor;
            textMesh.text = string.Empty;
            textMesh.richText = false;

            Font font = PixelFontLibrary.Bold;
            if (font != null)
            {
                textMesh.font = font;
                textRenderer = textMesh.GetComponent<MeshRenderer>();
                textRenderer.sharedMaterial = font.material;
            }
            else
            {
                textRenderer = textMesh.GetComponent<MeshRenderer>();
            }

            RefreshSorting();
        }

        private void LayoutForMessage(string message)
        {
            string wrapped = WrapText(message, maximumCharactersPerLine);
            textMesh.text = wrapped;
            textMesh.fontSize = Mathf.Max(minimumFontSize, preferredFontSize);

            // TextMesh bounds update synchronously after its text/font settings change.
            Vector2 textSize = MeasureTextInRootSpace();
            float allowedTextWidth = Mathf.Max(0.1f,
                maximumBubbleWorldWidth - ((horizontalPaddingPixels * 2f + outlinePixels * 2f) / pixelsPerUnit));

            while (textMesh.fontSize > minimumFontSize && textSize.x > allowedTextWidth)
            {
                textMesh.fontSize = Mathf.Max(minimumFontSize, textMesh.fontSize - 2);
                textSize = MeasureTextInRootSpace();
            }

            int textWidthPixels = Mathf.CeilToInt(textSize.x * pixelsPerUnit);
            int textHeightPixels = Mathf.CeilToInt(textSize.y * pixelsPerUnit);
            int bodyWidth = Mathf.Max(24,
                textWidthPixels + horizontalPaddingPixels * 2 + outlinePixels * 2);
            int bodyHeight = Mathf.Max(16,
                textHeightPixels + verticalPaddingPixels * 2 + outlinePixels * 2);

            int maxWidthPixels = Mathf.Max(24, Mathf.RoundToInt(maximumBubbleWorldWidth * pixelsPerUnit));
            bodyWidth = Mathf.Min(bodyWidth, maxWidthPixels);

            RebuildBubbleSprite(bodyWidth, bodyHeight);

            float bodyCentreY = (tailHeightPixels + bodyHeight * 0.5f) / pixelsPerUnit;
            textMesh.transform.localPosition = new Vector3(0f, bodyCentreY, -0.01f);
        }

        private Vector2 MeasureTextInRootSpace()
        {
            if (textRenderer == null)
                return Vector2.one * 0.1f;

            Bounds worldBounds = textRenderer.bounds;
            Vector3 localSize = root.transform.InverseTransformVector(worldBounds.size);
            return new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
        }

        private string WrapText(string text, int maxCharacters)
        {
            maxCharacters = Mathf.Max(4, maxCharacters);
            string[] paragraphs = text.Replace("\r", string.Empty).Split('\n');
            StringBuilder result = new StringBuilder();

            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                    result.Append('\n');

                string paragraph = paragraphs[p].Trim();
                if (paragraph.Length == 0)
                    continue;

                string[] words = paragraph.Split(' ');
                List<string> lines = new List<string>();
                StringBuilder line = new StringBuilder();

                foreach (string rawWord in words)
                {
                    if (string.IsNullOrEmpty(rawWord))
                        continue;

                    string word = rawWord;
                    while (word.Length > maxCharacters)
                    {
                        if (line.Length > 0)
                        {
                            lines.Add(line.ToString());
                            line.Clear();
                        }

                        lines.Add(word.Substring(0, maxCharacters));
                        word = word.Substring(maxCharacters);
                    }

                    int proposedLength = line.Length == 0 ? word.Length : line.Length + 1 + word.Length;
                    if (line.Length > 0 && proposedLength > maxCharacters)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }

                    if (line.Length > 0)
                        line.Append(' ');
                    line.Append(word);
                }

                if (line.Length > 0)
                    lines.Add(line.ToString());

                result.Append(string.Join("\n", lines));
            }

            return result.ToString();
        }

        private void RebuildBubbleSprite(int bodyWidth, int bodyHeight)
        {
            if (bubbleSprite != null)
                Destroy(bubbleSprite);
            if (bubbleTexture != null)
                Destroy(bubbleTexture);

            int width = bodyWidth;
            int height = bodyHeight + tailHeightPixels;
            bubbleTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime Dynamic Surfer Speech Bubble",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            int bodyBottom = tailHeightPixels;
            int radius = Mathf.Clamp(cornerRadiusPixels, 1, Mathf.Max(1, Mathf.Min(width, bodyHeight) / 2));
            int innerRadius = Mathf.Max(0, radius - outlinePixels);

            for (int y = bodyBottom; y < height; y++)
            {
                int bodyY = y - bodyBottom;
                for (int x = 0; x < width; x++)
                {
                    bool insideOuter = IsInsideRoundedRectangle(x, bodyY, width, bodyHeight, radius);
                    if (!insideOuter)
                        continue;

                    bool insideInner = IsInsideRoundedRectangle(
                        x - outlinePixels,
                        bodyY - outlinePixels,
                        width - outlinePixels * 2,
                        bodyHeight - outlinePixels * 2,
                        innerRadius);

                    pixels[y * width + x] = insideInner ? bubbleColor : outlineColor;
                }
            }

            int tailCentre = Mathf.Clamp(width / 2 - Mathf.Max(4, width / 10), 4, width - 5);
            int widestHalf = Mathf.Max(3, tailHeightPixels - 1);
            for (int row = 0; row < tailHeightPixels; row++)
            {
                float t = tailHeightPixels <= 1 ? 1f : row / (float)(tailHeightPixels - 1);
                int halfWidth = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(0f, widestHalf, t)));
                int y = row;

                for (int x = tailCentre - halfWidth; x <= tailCentre + halfWidth; x++)
                {
                    if (x < 0 || x >= width)
                        continue;

                    bool edge = x == tailCentre - halfWidth || x == tailCentre + halfWidth;
                    pixels[y * width + x] = edge ? outlineColor : bubbleColor;
                }
            }

            bubbleTexture.SetPixels(pixels);
            bubbleTexture.Apply(false, false);

            bubbleSprite = Sprite.Create(
                bubbleTexture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0f),
                pixelsPerUnit);
            bubbleRenderer.sprite = bubbleSprite;
        }

        private static bool IsInsideRoundedRectangle(int x, int y, int width, int height, int radius)
        {
            if (width <= 0 || height <= 0 || x < 0 || y < 0 || x >= width || y >= height)
                return false;

            radius = Mathf.Clamp(radius, 0, Mathf.Min(width, height) / 2);
            if (radius <= 0)
                return true;

            // Straight centre bands are always inside.
            if (x >= radius && x < width - radius)
                return true;
            if (y >= radius && y < height - radius)
                return true;

            int centreX = x < radius ? radius - 1 : width - radius;
            int centreY = y < radius ? radius - 1 : height - radius;
            int dx = x - centreX;
            int dy = y - centreY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private void OnDestroy()
        {
            if (bubbleSprite != null)
                Destroy(bubbleSprite);
            if (bubbleTexture != null)
                Destroy(bubbleTexture);
        }
    }
}
