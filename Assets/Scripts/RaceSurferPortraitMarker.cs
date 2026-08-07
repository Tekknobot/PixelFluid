using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class RaceSurferPortraitMarker : MonoBehaviour
    {
        private const float PortraitWidth = 0.72f;
        private static Sprite pointerSprite;
        private static readonly Dictionary<Transform, RaceSurferPortraitMarker> Active = new();

        private Transform target;
        private SpriteRenderer portraitRenderer;
        private SpriteRenderer pointerRenderer;
        private float visibleUntil;
        private float fadeDuration;

        public static void Show(Transform target, Sprite portrait, float duration)
        {
            if (target == null || portrait == null)
                return;

            Active.TryGetValue(target, out RaceSurferPortraitMarker marker);

            if (marker == null)
            {
                GameObject root = new GameObject("Race Portrait Marker");
                marker = root.AddComponent<RaceSurferPortraitMarker>();
                Active[target] = marker;
            }

            marker.Configure(target, portrait, duration);
        }

        private void Configure(Transform followTarget, Sprite portrait, float duration)
        {
            target = followTarget;
            visibleUntil = Time.unscaledTime + Mathf.Max(0.25f, duration);
            fadeDuration = Mathf.Min(0.65f, Mathf.Max(0.2f, duration * 0.28f));

            if (portraitRenderer == null)
            {
                GameObject portraitObject = new GameObject("Portrait");
                portraitObject.transform.SetParent(transform, false);
                portraitRenderer = portraitObject.AddComponent<SpriteRenderer>();
                portraitRenderer.sortingOrder = 32760;

                GameObject pointerObject = new GameObject("Pointer");
                pointerObject.transform.SetParent(transform, false);
                pointerRenderer = pointerObject.AddComponent<SpriteRenderer>();
                pointerRenderer.sprite = GetPointerSprite();
                pointerRenderer.sortingOrder = 32760;
                pointerObject.transform.localPosition = new Vector3(0f, -0.43f, 0f);
                pointerObject.transform.localScale = new Vector3(0.22f, 0.18f, 1f);
            }

            portraitRenderer.sprite = portrait;
            float width = Mathf.Max(0.01f, portrait.bounds.size.x);
            float scale = PortraitWidth / width;
            portraitRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            portraitRenderer.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            SetAlpha(1f);
            gameObject.SetActive(true);
            FollowTarget();
        }

        private void LateUpdate()
        {
            if (target == null || !RaceModeManager.RaceActive)
            {
                Destroy(gameObject);
                return;
            }

            FollowTarget();

            float remaining = visibleUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            SetAlpha(Mathf.Clamp01(remaining / fadeDuration));
        }

        private void OnDestroy()
        {
            if (target != null && Active.TryGetValue(target, out RaceSurferPortraitMarker marker) && marker == this)
                Active.Remove(target);
        }

        private void FollowTarget()
        {
            Vector3 position = target.position;
            position.y += 1.28f;
            position.z -= 0.8f;
            transform.position = position;
            transform.rotation = Quaternion.identity;
        }

        private void SetAlpha(float alpha)
        {
            if (portraitRenderer != null)
            {
                Color colour = portraitRenderer.color;
                colour.a = alpha;
                portraitRenderer.color = colour;
            }

            if (pointerRenderer != null)
            {
                Color colour = pointerRenderer.color;
                colour.a = alpha;
                pointerRenderer.color = colour;
            }
        }

        private static Sprite GetPointerSprite()
        {
            if (pointerSprite != null)
                return pointerSprite;

            Texture2D texture = new Texture2D(7, 5, TextureFormat.RGBA32, false)
            {
                name = "Race Portrait Down Pointer",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color white = Color.white;
            for (int y = 0; y < texture.height; y++)
            {
                int inset = texture.height - 1 - y;
                for (int x = 0; x < texture.width; x++)
                    texture.SetPixel(x, y, x >= inset && x < texture.width - inset ? white : clear);
            }
            texture.Apply(false, true);

            pointerSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            pointerSprite.name = "Race Portrait Down Pointer";
            return pointerSprite;
        }
    }
}
