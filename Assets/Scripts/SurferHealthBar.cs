using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class SurferHealthBar : MonoBehaviour
    {
        [SerializeField] private Vector3 localOffset = new(0f, 0.62f, 0f);
        [SerializeField] private Vector2 barSize = new(0.62f, 0.075f);
        [SerializeField] private Color backgroundColor = new(0.08f, 0.04f, 0.04f, 0.92f);
        [SerializeField] private Color healthColor = new(0.86f, 0.08f, 0.10f, 1f);
        [SerializeField] private Color borderColor = new(0.02f, 0.02f, 0.02f, 1f);

        private Transform root, fill;
        private SpriteRenderer rootRenderer, fillRenderer, borderRenderer;
        private int health = 1, maximum = 1;
        private static Sprite whiteSprite;

        private void Awake() { Build(); Refresh(); }
        public void SetHealth(int current, int max) { health = Mathf.Max(0, current); maximum = Mathf.Max(1, max); Build(); Refresh(); }

        private void Build()
        {
            if (root != null) return;
            GameObject border = CreatePart("Health Bar Border", borderColor, 0); border.transform.SetParent(transform, false); border.transform.localPosition = localOffset; border.transform.localScale = new Vector3(barSize.x + 0.04f, barSize.y + 0.04f, 1f); borderRenderer = border.GetComponent<SpriteRenderer>();
            GameObject background = CreatePart("Health Bar Background", backgroundColor, 1); background.transform.SetParent(border.transform, false); background.transform.localScale = new Vector3(barSize.x / (barSize.x + 0.04f), barSize.y / (barSize.y + 0.04f), 1f); root = background.transform; rootRenderer = background.GetComponent<SpriteRenderer>();
            GameObject healthFill = CreatePart("Health Bar Fill", healthColor, 2); healthFill.transform.SetParent(border.transform, false); fill = healthFill.transform; fillRenderer = healthFill.GetComponent<SpriteRenderer>();
        }

        private GameObject CreatePart(string name, Color color, int order)
        {
            GameObject go = new(name); SpriteRenderer sr = go.AddComponent<SpriteRenderer>(); sr.sprite = GetWhiteSprite(); sr.color = color; sr.sortingOrder = order; return go;
        }

        private void Refresh()
        {
            if (fill == null) return;
            float ratio = Mathf.Clamp01((float)health / maximum);
            float borderW = barSize.x + 0.04f, borderH = barSize.y + 0.04f;
            fill.localScale = new Vector3((barSize.x * ratio) / borderW, barSize.y / borderH, 1f);
            fill.localPosition = new Vector3(-0.5f + (0.02f + barSize.x * ratio * 0.5f) / borderW, 0f, -0.01f);
            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (borderRenderer == null) return;
            SpriteRenderer surfer = GetComponent<SpriteRenderer>();
            if (surfer == null) return;
            int baseOrder = surfer.sortingOrder + 20;
            borderRenderer.sortingOrder = baseOrder; rootRenderer.sortingOrder = baseOrder + 1; fillRenderer.sortingOrder = baseOrder + 2;
            borderRenderer.transform.rotation = Quaternion.identity;
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Runtime Health Bar Pixel" };
            texture.SetPixel(0, 0, Color.white); texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            whiteSprite.name = "Runtime Health Bar Sprite"; return whiteSprite;
        }
    }
}
