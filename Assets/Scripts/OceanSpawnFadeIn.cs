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
        private bool guaranteeVisibleTarget;
        private bool completed;

        public bool IsComplete => completed;

        public void Configure(
            float fadeDuration,
            bool ensureVisibleTarget = false)
        {
            duration = Mathf.Max(0.05f, fadeDuration);
            guaranteeVisibleTarget |= ensureVisibleTarget;

            // AddComponent invokes Awake immediately. Awake may already have
            // captured the authored renderer colours and set their alpha to zero.
            // Re-capturing here would store zero as the target alpha and leave the
            // creature permanently invisible.
            if (!prepared)
                PrepareExistingRenderersAtZeroAlpha();

            if (guaranteeVisibleTarget)
                NormalizeTargetAlpha();

            // Race Mode checks its bosses every frame. A completed fade remains
            // as a marker so it is not added and restarted over and over.
            if (completed)
                RepairZeroAlphaRenderers();
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
            List<SpriteRenderer> found = new();
            GetComponentsInChildren(true, found);

            foreach (SpriteRenderer renderer in found)
            {
                if (renderer == null)
                    continue;

                Color target = GetTargetColor(renderer.color);
                renderers.Add(renderer);
                targetColors.Add(target);
                renderer.color = new Color(target.r, target.g, target.b, 0f);
            }

            prepared = renderers.Count > 0;
        }

        private Color GetTargetColor(Color color)
        {
            if (guaranteeVisibleTarget && color.a <= 0.001f)
                color.a = 1f;

            return color;
        }

        private void NormalizeTargetAlpha()
        {
            for (int i = 0; i < targetColors.Count; i++)
            {
                Color target = targetColors[i];
                if (target.a <= 0.001f)
                {
                    target.a = 1f;
                    targetColors[i] = target;
                }
            }
        }

        private IEnumerator Start()
        {
            if (completed)
                yield break;

            // Some spawners create child renderers during their own Start method.
            // Capture those late additions before beginning the visible fade.
            yield return null;

            if (completed)
                yield break;

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

                    Color target = GetTargetColor(renderer.color);
                    renderers.Add(renderer);
                    targetColors.Add(target);
                    renderer.color = new Color(target.r, target.g, target.b, 0f);
                }
            }

            if (renderers.Count == 0)
            {
                completed = true;
                enabled = false;
                yield break;
            }

            float elapsed = 0f;
            while (!completed && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
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

            if (!completed)
                CompleteFade();
        }

        private void CompleteFade()
        {
            if (guaranteeVisibleTarget)
                NormalizeTargetAlpha();

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = targetColors[i];
            }

            completed = true;
            enabled = false;
        }

        private void RepairZeroAlphaRenderers()
        {
            int count = Mathf.Min(renderers.Count, targetColors.Count);
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer.color.a > 0.001f)
                    continue;

                renderer.color = targetColors[i];
            }
        }

        private void OnDisable()
        {
            // If pausing, deactivation, or another system interrupts the fade,
            // finalize it instead of leaving any renderer at alpha zero.
            if (!completed && prepared)
                CompleteFade();
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
                if (target.GetComponent<OceanSpawnFadeIn>() != null)
                    continue;

                OceanSpawnFadeIn parentFade = target.transform.parent != null
                    ? target.transform.parent.GetComponentInParent<OceanSpawnFadeIn>()
                    : null;
                if (parentFade != null && !parentFade.IsComplete)
                    continue;

                target.AddComponent<OceanSpawnFadeIn>();
            }
        }
    }
}
