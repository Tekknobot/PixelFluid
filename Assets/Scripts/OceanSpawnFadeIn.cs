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

        private IEnumerator Start()
        {
            // Spawners often add renderers one frame after creating their root.
            yield return null;

            GetComponentsInChildren(true, renderers);

            if (renderers.Count == 0)
            {
                Destroy(this);
                yield break;
            }

            targetColors.Clear();

            foreach (SpriteRenderer renderer in renderers)
            {
                Color target = renderer.color;
                targetColors.Add(target);

                renderer.color = new Color(
                    target.r,
                    target.g,
                    target.b,
                    0f
                );
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration)
                );

                for (int i = 0; i < renderers.Count; i++)
                {
                    SpriteRenderer renderer = renderers[i];

                    if (renderer == null)
                        continue;

                    Color target = targetColors[i];

                    renderer.color = new Color(
                        target.r,
                        target.g,
                        target.b,
                        target.a * t
                    );
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
        // Track the actual GameObjects instead of obsolete integer IDs.
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
            // Arena bosses use BossArenaPrison's placement-aware fade. Applying the
            // generic ocean fade here can race it and capture a zero-alpha target.
            FadeNew<RubberDucklingSwimmer>();
            FadeNew<AlienUfoController>();
            FadeNew<DayTwoHelicopterController>();
            FadeNew<BoomboxSurferSwimmer>();
        }

        private void FadeNew<T>() where T : Component
        {
            T[] components = FindObjectsByType<T>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            foreach (T component in components)
            {
                if (component == null)
                    continue;

                GameObject target = component.gameObject;

                if (!seen.Add(target))
                    continue;

                if (target.GetComponent<OceanSpawnFadeIn>() != null)
                    continue;

                target.AddComponent<OceanSpawnFadeIn>();
            }
        }
    }
}