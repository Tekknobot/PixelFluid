using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Compact world-space boss health bar. It follows either supported boss and
    /// reads its public health values without changing boss combat behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossHealthBar : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Vector3 localOffset = new(0f, 2.3125f, 0f);
        [SerializeField] private Vector2 barSize = new(1.35f, 0.11f);

        [Header("Appearance")]
        [SerializeField] private Color backgroundColor = new(0.045f, 0.025f, 0.035f, 0.94f);
        [SerializeField] private Color healthColor = new(0.9f, 0.10f, 0.12f, 1f);
        [SerializeField] private Color borderColor = new(1f, 1f, 1f, 0.96f);

        private MonoBehaviour boss;
        private Transform border;
        private Transform fill;
        private SpriteRenderer borderRenderer;
        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer fillRenderer;
        private static Sprite whiteSprite;

        public void Bind(MonoBehaviour bossBehaviour)
        {
            boss = bossBehaviour;
            Build();
            Refresh();
        }

        private void Awake()
        {
            Build();
        }

        private void LateUpdate()
        {
            if (boss == null)
                boss = GetComponent<GodzillaLaneSwimmer>() as MonoBehaviour
                    ?? GetComponent<RubberDuckBossSwimmer>();

            if (boss == null)
            {
                gameObject.SetActive(false);
                return;
            }

            Refresh();
            MatchBossSorting();

            if (border != null)
            {
                border.localPosition = localOffset;
                border.rotation = Quaternion.identity;
            }
        }

        private void Build()
        {
            if (border != null)
                return;

            GameObject borderObject = CreatePart("Boss Health Border", borderColor);
            borderObject.transform.SetParent(transform, false);
            border = borderObject.transform;
            border.localPosition = localOffset;
            border.localScale = new Vector3(barSize.x + 0.06f, barSize.y + 0.06f, 1f);
            borderRenderer = borderObject.GetComponent<SpriteRenderer>();

            GameObject backgroundObject = CreatePart("Boss Health Background", backgroundColor);
            backgroundObject.transform.SetParent(border, false);
            backgroundObject.transform.localScale = new Vector3(
                barSize.x / (barSize.x + 0.06f),
                barSize.y / (barSize.y + 0.06f),
                1f);
            backgroundRenderer = backgroundObject.GetComponent<SpriteRenderer>();

            GameObject fillObject = CreatePart("Boss Health Fill", healthColor);
            fillObject.transform.SetParent(border, false);
            fill = fillObject.transform;
            fillRenderer = fillObject.GetComponent<SpriteRenderer>();
        }

        private void Refresh()
        {
            if (fill == null)
                return;

            int current = 1;
            int maximum = 1;
            bool defeated = false;

            if (boss is GodzillaLaneSwimmer reaper)
            {
                current = reaper.CurrentHealth;
                maximum = reaper.MaximumHealth;
                defeated = reaper.IsDefeated;
            }
            else if (boss is RubberDuckBossSwimmer duck)
            {
                current = duck.CurrentHealth;
                maximum = duck.MaximumHealth;
                defeated = duck.IsDefeated;
            }

            float ratio = defeated ? 0f : Mathf.Clamp01((float)current / Mathf.Max(1, maximum));
            float borderWidth = barSize.x + 0.06f;
            float borderHeight = barSize.y + 0.06f;

            fill.localScale = new Vector3(
                (barSize.x * ratio) / borderWidth,
                barSize.y / borderHeight,
                1f);
            fill.localPosition = new Vector3(
                -0.5f + (0.03f + barSize.x * ratio * 0.5f) / borderWidth,
                0f,
                -0.01f);

            bool visible = !defeated && BossArenaPrison.IsActive;
            if (borderRenderer != null) borderRenderer.enabled = visible;
            if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
            if (fillRenderer != null) fillRenderer.enabled = visible;
        }

        private void MatchBossSorting()
        {
            SpriteRenderer bossRenderer = GetComponent<SpriteRenderer>();
            if (bossRenderer == null || borderRenderer == null)
                return;

            int baseOrder = bossRenderer.sortingOrder + 30;
            int layer = bossRenderer.sortingLayerID;

            borderRenderer.sortingLayerID = layer;
            backgroundRenderer.sortingLayerID = layer;
            fillRenderer.sortingLayerID = layer;
            borderRenderer.sortingOrder = baseOrder;
            backgroundRenderer.sortingOrder = baseOrder + 1;
            fillRenderer.sortingOrder = baseOrder + 2;
        }

        private static GameObject CreatePart(string objectName, Color color)
        {
            GameObject go = new(objectName);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
            renderer.color = color;
            return go;
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Runtime Boss Health Bar Pixel"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            whiteSprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);
            whiteSprite.name = "Runtime Boss Health Bar Sprite";
            return whiteSprite;
        }
    }
}
