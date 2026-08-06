using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class OceanSpawnFadeIn : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float duration = 0.8f;

        private readonly List<SpriteRenderer> renderers = new();
        private readonly List<Color> targetColors = new();
        private bool prepared;

        public void Configure(float fadeDuration)
        {
            duration = Mathf.Max(0.05f, fadeDuration);

            // AddComponent invokes Awake immediately. Awake may already have
            // captured the authored renderer colours and set their alpha to zero.
            // Re-capturing here would store zero as the target alpha and leave the
            // creature permanently invisible.
            if (!prepared)
                PrepareExistingRenderersAtZeroAlpha();
        }

        private void Awake()
        {
            // If the spawner has already created its children, hide them before
            // Unity gets a chance to render the first visible frame.
            PrepareExistingRenderersAtZeroAlpha();
        }

        private void PrepareExistingRenderersAtZeroAlpha()
        {
            renderers.Clear();
            targetColors.Clear();
            GetComponentsInChildren(true, renderers);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Color target = renderer.color;
                targetColors.Add(target);
                renderer.color = new Color(target.r, target.g, target.b, 0f);
            }

            prepared = renderers.Count > 0;
        }

        private IEnumerator Start()
        {
            // Some spawners create child renderers during their own Start method.
            // Capture those late additions before beginning the visible fade.
            yield return null;

            if (!prepared)
            {
                PrepareExistingRenderersAtZeroAlpha();
            }
            else
            {
                List<SpriteRenderer> latest = new();
                GetComponentsInChildren(true, latest);
                foreach (SpriteRenderer renderer in latest)
                {
                    if (renderer == null || renderers.Contains(renderer))
                        continue;

                    Color target = renderer.color;
                    renderers.Add(renderer);
                    targetColors.Add(target);
                    renderer.color = new Color(target.r, target.g, target.b, 0f);
                }
            }

            if (renderers.Count == 0)
            {
                Destroy(this);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                for (int i = 0; i < renderers.Count; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    Color target = targetColors[i];
                    renderer.color = new Color(target.r, target.g, target.b, target.a * t);
                }

                yield return null;
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = targetColors[i];
            }

            Destroy(this);
        }
    }

    [DefaultExecutionOrder(30000)]
    public sealed class OceanSpawnFadeInstaller : MonoBehaviour
    {
        private readonly HashSet<GameObject> seen = new();
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<OceanSpawnFadeInstaller>() != null)
                return;

            new GameObject("Ocean Spawn Fade Installer")
                .AddComponent<OceanSpawnFadeInstaller>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan)
                return;

            nextScan = Time.unscaledTime + 0.1f;

            FadeNew<OceanItemBehaviour>();
            FadeNew<SodaCanPickup>();
            FadeNew<HeartLaneDrifter>();
            FadeNew<StrugglingSwimmerDrifter>();
            FadeNew<SharkLaneSwimmer>();
            FadeNew<GiantSquidLaneSwimmer>();
            FadeNew<WhaleLaneSwimmer>();
            FadeNew<JellyfishSwimmer>();
            FadeNew<BloodSharkLaneSwimmer>();
            FadeNew<TransparentSquidLaneSwimmer>();
            FadeNew<StingrayLaneSwimmer>();
            FadeNew<BloodfishSwimmer>();
            FadeNew<SeaTurtleSwimmer>();
            FadeNew<GiantTurtleSwimmer>();
            FadeNew<RubberDucklingSwimmer>();
            FadeNew<GodzillaSkullSwimmer>();
            FadeNew<AlienUfoController>();
            FadeNew<DayTwoHelicopterController>();
            FadeNew<BoomboxSurferSwimmer>();
        }

        private void FadeNew<T>() where T : Component
        {
            T[] components = FindObjectsByType<T>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (T component in components)
            {
                if (component == null)
                    continue;

                GameObject target = component.gameObject;
                if (!seen.Add(target))
                    continue;

                // Race Mode may fade an entire school/group from its root.
                // Do not layer another fade onto each child.
                if (target.GetComponentInParent<OceanSpawnFadeIn>() != null)
                    continue;

                target.AddComponent<OceanSpawnFadeIn>();
            }
        }
    }
}
