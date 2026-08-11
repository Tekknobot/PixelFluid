using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PixelOcean
{
    /// <summary>
    /// Introduces the Sleeping Eye as a distant camera-following landmark.
    /// It first becomes visible through Day 5 travel, finishes centering during
    /// the Day 5 completion transition, and then watches the horizon in Day 6.
    /// </summary>
    [DefaultExecutionOrder(10025)]
    [DisallowMultipleComponent]
    public sealed class DayFiveSleepingEyeHorizon : MonoBehaviour
    {
        [Header("Sleeping Eye Resource")]
        [SerializeField] private string resourcePath =
            "Day6/SleepingEye/eye_opening";
        [SerializeField] private string generatedObjectName =
            "Sleeping Eye Horizon";

        [Header("Day 5 Distance Reveal")]
        [Tooltip("Fraction of Day 5's journey where the closed eye first appears.")]
        [SerializeField, Range(0f, 1f)] private float fadeBeginsAtDistance = 0.45f;
        [Tooltip("Fraction of Day 5's journey where the eye reaches full opacity.")]
        [SerializeField, Range(0f, 1f)] private float fullyVisibleAtDistance = 0.86f;
        [Tooltip("Fraction of Day 5's journey where it begins moving toward centre.")]
        [SerializeField, Range(0f, 1f)] private float centeringBeginsAtDistance = 0.50f;
        [Tooltip("Fraction of Day 5's journey where it would naturally reach centre.")]
        [SerializeField, Range(0f, 1f)] private float centeredAtDistance = 0.96f;
        [SerializeField, Min(0.05f)] private float fadeSmoothSeconds = 1.8f;
        [SerializeField, Min(0.05f)] private float movementSmoothSeconds = 2.8f;

        [Header("Background Placement")]
        [Tooltip("Uses the old tropical island's local position and renderer setup when it still exists.")]
        [SerializeField] private bool copyTropicalIslandPlacement = true;
        [SerializeField] private string tropicalIslandObjectName =
            "tropical_island_0";
        [SerializeField] private Vector3 centeredLocalPosition =
            new(0f, -0.85f, 0f);
        [SerializeField, Min(0.01f)] private float eyeScale = 1f;
        [SerializeField] private int sortingOrder = 0;
        [SerializeField] private float approachHorizontalOffset = 8.5f;
        [SerializeField, Range(0f, 0.03f)] private float parallaxFactor = 0.004f;
        [SerializeField, Min(0f)] private float maximumParallaxOffset = 0.8f;

        [Header("Opening Animation")]
        [Tooltip("Editable playback speed after the eye reaches centre.")]
        [SerializeField, Min(0.1f)] private float animationFramesPerSecond = 12f;
        [Tooltip("When enabled, the opening frames reverse to close the eye.")]
        [SerializeField] private bool reverseAtEnd = true;
        [SerializeField, Min(0f)] private float openFramePause = 0.25f;
        [SerializeField, Min(0f)] private float closedFramePause = 3.25f;
        [SerializeField, Range(0.8f, 1f)] private float animationCenterThreshold = 0.985f;

        [Header("Open Eye Idle Branch")]
        [SerializeField] private string idleResourcePath =
            "Day6/SleepingEye/eye_idle";
        [Tooltip("Chance to branch into eye_idle once during each opening.")]
        [SerializeField, Range(0f, 1f)] private float idlePlayChance = 0.55f;
        [Tooltip("Opening-sheet position where the idle branch may begin.")]
        [SerializeField, Range(0.15f, 0.85f)] private float idleTriggerNormalizedFrame = 0.50f;
        [SerializeField, Min(0.1f)] private float idleFramesPerSecond = 12f;
        [Tooltip("Complete forward-and-back idle cycles before resuming the opening sheet.")]
        [SerializeField] private Vector2Int idlePingPongCountRange = new(1, 9);

        private SurfDayProgressionDirector director;
        private TinyWaveSurfer surfer;
        private ProceduralStarryNight starryNight;
        private GameObject eyeObject;
        private SpriteRenderer eyeRenderer;
        private Color authoredEyeColour = Color.white;
        private Sprite[] frames = Array.Empty<Sprite>();
        private Sprite[] idleFrames = Array.Empty<Sprite>();
        private float visibility;
        private float visibilityVelocity;
        private float centeredProgress;
        private float centeredVelocity;
        private float playerAnchorX;
        private int animationFrame;
        private int animationDirection = 1;
        private float animationClock;
        private float endpointPauseRemaining;
        private bool restartAfterOpenPause;
        private bool playingIdle;
        private bool idleAttemptedThisOpening;
        private int idleFrame;
        private int idleDirection = 1;
        private int idlePingPongsRemaining;
        private float idleClock;
        private float nextResolveAt;
        private bool presentationCopied;

        private void Awake()
        {
            ResolveSceneReferences();
            LoadFrames();
            EnsureEyeObject();
            InitialiseForCurrentStage();
        }

        private void Update()
        {
            if (director == null || starryNight == null || eyeObject == null)
            {
                if (Time.unscaledTime >= nextResolveAt)
                {
                    nextResolveAt = Time.unscaledTime + 0.5f;
                    ResolveSceneReferences();
                    EnsureEyeObject();
                    InitialiseForCurrentStage();
                }

                if (eyeObject == null)
                    return;
            }

            if (surfer == null || surfer.IsDead)
                surfer = FindPlayer();

            CalculateStageTargets(out float targetVisibility, out float targetCenter);
            float dt = Time.unscaledDeltaTime;
            visibility = Mathf.SmoothDamp(
                visibility,
                targetVisibility,
                ref visibilityVelocity,
                fadeSmoothSeconds,
                Mathf.Infinity,
                dt);
            centeredProgress = Mathf.SmoothDamp(
                centeredProgress,
                targetCenter,
                ref centeredVelocity,
                movementSmoothSeconds,
                Mathf.Infinity,
                dt);

            ApplyPresentation();
            UpdateAnimation(dt);
        }

        private void CalculateStageTargets(
            out float targetVisibility,
            out float targetCenter)
        {
            int day = director != null ? director.CurrentDay : 1;
            targetVisibility = 0f;
            targetCenter = 0f;

            if (day < 5)
                return;

            if (day >= 6)
            {
                targetVisibility = 1f;
                targetCenter = 1f;
                return;
            }

            float distanceProgress = director.DayDistance > 0f
                ? Mathf.Clamp01(director.DistanceTravelled / director.DayDistance)
                : 0f;
            targetVisibility = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    fadeBeginsAtDistance,
                    Mathf.Max(fadeBeginsAtDistance + 0.001f, fullyVisibleAtDistance),
                    distanceProgress));
            targetCenter = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    centeringBeginsAtDistance,
                    Mathf.Max(centeringBeginsAtDistance + 0.001f, centeredAtDistance),
                    distanceProgress));

            // Day 5's locked final arena can pause journey distance. Once the
            // Warden is defeated, use the long day-transition pause to finish
            // the approach smoothly so no position snap occurs on Day 6.
            if (director.CurrentChapter == SurfDayProgressionDirector.Chapter.Complete)
            {
                targetVisibility = 1f;
                targetCenter = 1f;
            }
        }

        private void ApplyPresentation()
        {
            if (eyeRenderer == null || eyeObject == null)
                return;

            float playerParallax = 0f;
            if (surfer != null)
            {
                playerParallax = Mathf.Clamp(
                    -(surfer.transform.position.x - playerAnchorX) * parallaxFactor,
                    -maximumParallaxOffset,
                    maximumParallaxOffset);
            }

            float approachOffset = approachHorizontalOffset *
                (1f - Mathf.SmoothStep(0f, 1f, centeredProgress));
            eyeObject.transform.localPosition = centeredLocalPosition +
                new Vector3(approachOffset + playerParallax, 0f, 0f);
            eyeObject.transform.localScale = Vector3.one * eyeScale;

            Color lightingTint = starryNight != null
                ? starryNight.HorizonLandmarkTint
                : Color.white;
            Color colour = new(
                authoredEyeColour.r * lightingTint.r,
                authoredEyeColour.g * lightingTint.g,
                authoredEyeColour.b * lightingTint.b,
                authoredEyeColour.a * Mathf.Clamp01(visibility));
            eyeRenderer.color = colour;
            eyeRenderer.enabled = visibility > 0.001f;
        }

        private void UpdateAnimation(float dt)
        {
            if (eyeRenderer == null || frames.Length == 0)
                return;

            bool canAnimate = visibility >= 0.98f &&
                centeredProgress >= animationCenterThreshold;
            if (!canAnimate)
            {
                animationFrame = 0;
                animationDirection = 1;
                animationClock = 0f;
                endpointPauseRemaining = 0f;
                restartAfterOpenPause = false;
                ResetIdleBranch();
                eyeRenderer.sprite = frames[0];
                return;
            }

            if (playingIdle)
            {
                UpdateIdleAnimation(dt);
                return;
            }

            if (endpointPauseRemaining > 0f)
            {
                endpointPauseRemaining = Mathf.Max(
                    0f,
                    endpointPauseRemaining - dt);
                if (endpointPauseRemaining <= 0f && restartAfterOpenPause)
                {
                    restartAfterOpenPause = false;
                    animationFrame = 0;
                    animationDirection = 1;
                    idleAttemptedThisOpening = false;
                    eyeRenderer.sprite = frames[0];
                }
                return;
            }

            animationClock += dt * Mathf.Max(0.1f, animationFramesPerSecond);
            while (animationClock >= 1f)
            {
                animationClock -= 1f;
                AdvanceAnimationFrame();
                if (playingIdle)
                {
                    eyeRenderer.sprite = idleFrames[idleFrame];
                    return;
                }
                if (endpointPauseRemaining > 0f)
                    break;
            }

            animationFrame = Mathf.Clamp(animationFrame, 0, frames.Length - 1);
            eyeRenderer.sprite = frames[animationFrame];
        }

        private void AdvanceAnimationFrame()
        {
            if (frames.Length <= 1)
                return;

            animationFrame += animationDirection;
            int last = frames.Length - 1;
            int idleTriggerFrame = Mathf.Clamp(
                Mathf.RoundToInt(last * idleTriggerNormalizedFrame),
                1,
                Mathf.Max(1, last - 1));
            if (animationDirection > 0 &&
                animationFrame == idleTriggerFrame &&
                !idleAttemptedThisOpening)
            {
                idleAttemptedThisOpening = true;
                if (idleFrames.Length > 0 && Random.value <= idlePlayChance)
                {
                    BeginIdleAnimation();
                    return;
                }
            }

            if (animationFrame >= last)
            {
                animationFrame = last;
                endpointPauseRemaining = openFramePause;
                if (reverseAtEnd)
                    animationDirection = -1;
                else
                    restartAfterOpenPause = true;
                return;
            }

            if (animationFrame <= 0 && animationDirection < 0)
            {
                animationFrame = 0;
                animationDirection = 1;
                idleAttemptedThisOpening = false;
                endpointPauseRemaining = closedFramePause;
            }
        }

        private void BeginIdleAnimation()
        {
            playingIdle = true;
            idleFrame = 0;
            idleDirection = 1;
            idleClock = 0f;
            int minimumCycles = Mathf.Max(1,
                Mathf.Min(idlePingPongCountRange.x, idlePingPongCountRange.y));
            int maximumCycles = Mathf.Max(minimumCycles,
                Mathf.Max(idlePingPongCountRange.x, idlePingPongCountRange.y));
            idlePingPongsRemaining = Random.Range(minimumCycles, maximumCycles + 1);
            eyeRenderer.sprite = idleFrames[0];
        }

        private void UpdateIdleAnimation(float dt)
        {
            if (!playingIdle || idleFrames.Length == 0)
            {
                playingIdle = false;
                return;
            }

            idleClock += dt * Mathf.Max(0.1f, idleFramesPerSecond);
            while (idleClock >= 1f)
            {
                idleClock -= 1f;

                if (idleFrames.Length == 1)
                {
                    idlePingPongsRemaining--;
                    if (idlePingPongsRemaining <= 0)
                    {
                        FinishIdleAnimation();
                        return;
                    }
                    continue;
                }

                idleFrame += idleDirection;
                int lastIdleFrame = idleFrames.Length - 1;
                if (idleFrame >= lastIdleFrame)
                {
                    idleFrame = lastIdleFrame;
                    idleDirection = -1;
                }
                else if (idleFrame <= 0 && idleDirection < 0)
                {
                    idleFrame = 0;
                    idleDirection = 1;
                    idlePingPongsRemaining--;
                    if (idlePingPongsRemaining <= 0)
                    {
                        FinishIdleAnimation();
                        return;
                    }
                }
            }

            eyeRenderer.sprite = idleFrames[Mathf.Clamp(
                idleFrame,
                0,
                idleFrames.Length - 1)];
        }

        private void FinishIdleAnimation()
        {
            playingIdle = false;
            idleClock = 0f;
            animationClock = 0f;
            eyeRenderer.sprite = frames[Mathf.Clamp(
                animationFrame,
                0,
                frames.Length - 1)];
        }

        private void ResetIdleBranch()
        {
            playingIdle = false;
            idleAttemptedThisOpening = false;
            idleFrame = 0;
            idleDirection = 1;
            idlePingPongsRemaining = 0;
            idleClock = 0f;
        }

        private void ResolveSceneReferences()
        {
            director ??= FindFirstObjectByType<SurfDayProgressionDirector>();
            starryNight ??= FindFirstObjectByType<ProceduralStarryNight>();
            surfer = FindPlayer();
        }

        private void LoadFrames()
        {
            frames = LoadOrderedFrames(resourcePath);
            idleFrames = LoadOrderedFrames(idleResourcePath);

            if (frames.Length == 0)
                Debug.LogWarning(
                    $"Sleeping Eye could not load Resources/{resourcePath}.",
                    this);
        }

        private static Sprite[] LoadOrderedFrames(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Array.Empty<Sprite>();

            Sprite[] loaded = Resources.LoadAll<Sprite>(path);
            Array.Sort(loaded, (a, b) =>
                ExtractFrameNumber(a != null ? a.name : string.Empty)
                    .CompareTo(ExtractFrameNumber(b != null ? b.name : string.Empty)));
            return loaded;
        }

        private void EnsureEyeObject()
        {
            if (eyeObject != null || frames.Length == 0 || starryNight == null)
                return;

            Transform existing = FindTransformByName(generatedObjectName);
            if (existing != null)
            {
                eyeObject = existing.gameObject;
                eyeRenderer = eyeObject.GetComponent<SpriteRenderer>();
            }
            else
            {
                eyeObject = new GameObject(generatedObjectName);
                eyeObject.transform.SetParent(starryNight.transform, false);
                eyeRenderer = eyeObject.AddComponent<SpriteRenderer>();
            }

            if (eyeRenderer == null)
                eyeRenderer = eyeObject.AddComponent<SpriteRenderer>();
            authoredEyeColour = eyeRenderer.color;
            if (authoredEyeColour.a <= 0.001f)
                authoredEyeColour.a = 1f;
            eyeRenderer.sprite = frames[0];
            eyeRenderer.sortingOrder = sortingOrder;
            eyeRenderer.color = new Color(
                authoredEyeColour.r,
                authoredEyeColour.g,
                authoredEyeColour.b,
                0f);
            eyeRenderer.enabled = false;

            CopyIslandPresentationIfAvailable();
            eyeObject.transform.localPosition = centeredLocalPosition +
                Vector3.right * approachHorizontalOffset;
            eyeObject.transform.localScale = Vector3.one * eyeScale;
            playerAnchorX = surfer != null ? surfer.transform.position.x : 0f;
        }

        private void CopyIslandPresentationIfAvailable()
        {
            if (!copyTropicalIslandPlacement || presentationCopied || eyeRenderer == null)
                return;

            Transform island = FindTransformByName(tropicalIslandObjectName);
            if (island == null)
                return;

            centeredLocalPosition = island.localPosition;
            SpriteRenderer islandRenderer = island.GetComponent<SpriteRenderer>();
            if (islandRenderer != null)
            {
                eyeRenderer.sortingLayerID = islandRenderer.sortingLayerID;
                eyeRenderer.sortingOrder = islandRenderer.sortingOrder;
                if (islandRenderer.sharedMaterial != null)
                    eyeRenderer.sharedMaterial = islandRenderer.sharedMaterial;
            }
            presentationCopied = true;
        }

        private void InitialiseForCurrentStage()
        {
            if (director == null || eyeObject == null)
                return;

            CalculateStageTargets(out float targetVisibility, out float targetCenter);
            // Loading directly into Day 6 must already show a centred landmark.
            // A live Day 5-to-6 transition remains smoothed by Update().
            if (director.CurrentDay >= 6)
            {
                visibility = targetVisibility;
                centeredProgress = targetCenter;
                visibilityVelocity = 0f;
                centeredVelocity = 0f;
            }

            ApplyPresentation();
        }

        private static TinyWaveSurfer FindPlayer()
        {
            foreach (TinyWaveSurfer candidate in GameplayTargetCache.Surfers)
                if (candidate != null && candidate.IsPlayerControlled && !candidate.IsDead)
                    return candidate;
            return FindFirstObjectByType<TinyWaveSurfer>();
        }

        private static int ExtractFrameNumber(string value)
        {
            int underscore = value.LastIndexOf('_');
            return underscore >= 0 &&
                int.TryParse(value.Substring(underscore + 1), out int frame)
                    ? frame
                    : int.MaxValue;
        }

        private static Transform FindTransformByName(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
                return null;

            foreach (Transform candidate in FindObjectsByType<Transform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate != null &&
                    string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }
    }

    public static class DayFiveSleepingEyeHorizonBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<DayFiveSleepingEyeHorizon>() != null)
                return;

            GameObject host = new("Sleeping Eye Horizon Timeline");
            host.AddComponent<DayFiveSleepingEyeHorizon>();
        }
    }
}
