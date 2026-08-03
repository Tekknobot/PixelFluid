using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Shared, throttled surfer lookup used by enemies and pickups. This prevents
    /// many independent AIs from performing a complete scene search in the same frame.
    /// </summary>
    public static class GameplayTargetCache
    {
        private static TinyWaveSurfer[] surfers = Array.Empty<TinyWaveSurfer>();
        private static float nextRefreshTime;

        public static TinyWaveSurfer[] Surfers
        {
            get
            {
                if (Time.unscaledTime >= nextRefreshTime)
                    Refresh();

                return surfers;
            }
        }

        public static void Refresh()
        {
            surfers = UnityEngine.Object.FindObjectsByType<TinyWaveSurfer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            nextRefreshTime = Time.unscaledTime + 0.35f;
        }
    }

    /// <summary>
    /// Keeps off-screen actors moving so they can enter the scene, but suspends
    /// presentation work that cannot affect gameplay while they are far outside
    /// the camera. A padded wake zone restores everything before the actor appears.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OffscreenGameplaySleeper : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float horizontalWakePadding = 1.5f;
        [SerializeField, Min(0.5f)] private float verticalWakePadding = 1.0f;
        [SerializeField, Range(0.05f, 1f)] private float visibilityCheckInterval = 0.35f;

        private Camera gameplayCamera;
        private SpriteRenderer primaryRenderer;
        private MonoBehaviour[] visualBehaviours = Array.Empty<MonoBehaviour>();
        private Collider2D[] colliders = Array.Empty<Collider2D>();
        private AudioSource[] audioSources = Array.Empty<AudioSource>();
        private float nextCheckTime;
        private bool sleeping;

        private static readonly HashSet<string> VisualBehaviourNames = new()
        {
            nameof(SharkSpriteAnimation),
            nameof(GiantSquidSpriteAnimation),
            nameof(WhaleSpriteAnimation),
            nameof(StrugglingSwimmerAnimation),
            nameof(BoomboxSurferAnimation),
            nameof(DayTwoPredatorAnimation),
            nameof(RubberDuckBossAnimation),
            nameof(HeartSpriteAnimation),
            nameof(GodzillaSpriteAnimation)
        };

        private void Awake()
        {
            gameplayCamera = Camera.main;
            primaryRenderer = GetComponent<SpriteRenderer>();
            colliders = GetComponents<Collider2D>();
            audioSources = GetComponents<AudioSource>();

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            List<MonoBehaviour> visuals = new();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != null &&
                    behaviour != this &&
                    VisualBehaviourNames.Contains(behaviour.GetType().Name))
                {
                    visuals.Add(behaviour);
                }
            }

            visualBehaviours = visuals.ToArray();
            nextCheckTime = Time.unscaledTime + UnityEngine.Random.Range(0f, visibilityCheckInterval);
        }

        private void OnDisable()
        {
            SetSleeping(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheckTime)
                return;

            nextCheckTime = Time.unscaledTime + visibilityCheckInterval;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (gameplayCamera == null)
            {
                SetSleeping(false);
                return;
            }

            Vector3 cameraPosition = gameplayCamera.transform.position;
            float halfHeight = gameplayCamera.orthographic
                ? gameplayCamera.orthographicSize
                : 6f;
            float halfWidth = halfHeight * gameplayCamera.aspect;
            Vector3 position = transform.position;

            bool farOutside =
                Mathf.Abs(position.x - cameraPosition.x) > halfWidth + horizontalWakePadding ||
                Mathf.Abs(position.y - cameraPosition.y) > halfHeight + verticalWakePadding;

            SetSleeping(farOutside);
        }

        private void SetSleeping(bool shouldSleep)
        {
            if (sleeping == shouldSleep)
                return;

            sleeping = shouldSleep;

            foreach (MonoBehaviour behaviour in visualBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = !sleeping;
            }

            foreach (Collider2D collider in colliders)
            {
                if (collider != null)
                    collider.enabled = !sleeping;
            }

            foreach (AudioSource source in audioSources)
            {
                if (source == null)
                    continue;

                source.mute = sleeping;
            }

            // Renderer disabling is mostly a small CPU saving, because Unity already
            // performs render culling. The wake padding restores it before entry.
            if (primaryRenderer != null)
                primaryRenderer.enabled = !sleeping;
        }
    }

    /// <summary>Adds sleepers to newly spawned gameplay actors at a low frequency.</summary>
    public sealed class OffscreenGameplayOptimizerInstaller : MonoBehaviour
    {
        private static readonly HashSet<string> ActorTypeNames = new()
        {
            nameof(SharkLaneSwimmer),
            nameof(GiantSquidLaneSwimmer),
            nameof(WhaleLaneSwimmer),
            nameof(JellyfishSwimmer),
            nameof(BloodSharkLaneSwimmer),
            nameof(TransparentSquidLaneSwimmer),
            nameof(StingrayLaneSwimmer),
            nameof(BloodfishSwimmer),
            nameof(RubberDucklingSwimmer),
            nameof(StrugglingSwimmerDrifter),
            nameof(BoomboxSurferSwimmer),
            nameof(HeartLaneDrifter),
            nameof(SodaCanPickup),
            nameof(OceanItemBehaviour)
        };

        private float nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<OffscreenGameplayOptimizerInstaller>() != null)
                return;

            GameObject host = new("Offscreen Gameplay Optimizer");
            DontDestroyOnLoad(host);
            host.AddComponent<OffscreenGameplayOptimizerInstaller>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
                return;

            nextScanTime = Time.unscaledTime + 2.5f;

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || !ActorTypeNames.Contains(behaviour.GetType().Name))
                    continue;

                GameObject actor = behaviour.gameObject;
                if (actor.GetComponent<OffscreenGameplaySleeper>() == null)
                    actor.AddComponent<OffscreenGameplaySleeper>();
            }
        }
    }
}
