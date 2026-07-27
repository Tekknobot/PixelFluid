using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Generates an endless procedural ocean wash from filtered noise.
    /// No recorded audio clips are required. The sound reacts to the active
    /// PixelWaterGPU simulations by sampling their surface velocity.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralWaveAudio : MonoBehaviour
    {
        [Header("Output")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.48f;
        [SerializeField, Range(-1f, 1f)] private float stereoWidth = 0.35f;
        [SerializeField] private bool reactToWaterSimulation = true;

        [Header("Ocean Body")]
        [SerializeField, Range(0f, 1f)] private float deepWash = 0.8f;
        [SerializeField, Range(0f, 1f)] private float midWash = 0.42f;
        [SerializeField, Range(0f, 1f)] private float foamHiss = 0.24f;
        [SerializeField, Range(0.02f, 1f)] private float swellRate = 0.16f;
        [SerializeField, Range(0f, 1f)] private float swellDepth = 0.55f;

        [Header("Breaking Waves")]
        [SerializeField, Range(0f, 1f)] private float crashAmount = 0.48f;
        [SerializeField, Range(0.2f, 8f)] private float crashDecay = 2.4f;
        [SerializeField, Range(0.1f, 4f)] private float activitySensitivity = 1.25f;
        [SerializeField, Range(0.05f, 3f)] private float activityRefreshRate = 0.18f;

        [Header("Simulation Sampling")]
        [SerializeField, Range(1, 12)] private int samplesPerWave = 5;
        [SerializeField, Range(0.02f, 1f)] private float minimumActivity = 0.12f;
        [SerializeField, Range(0.1f, 8f)] private float waveListRefreshSeconds = 2f;

        private readonly List<PixelWaterGPU> waves = new();
        private AudioSource audioSource;
        private AudioClip silentDriverClip;
        private float activityTimer;
        private float waveRefreshTimer;
        private int outputSampleRate = 48000;

        // Values written on Unity's main thread and read by the audio thread.
        private volatile float targetActivity = 0.35f;
        private volatile float targetPan;

        // Audio-thread-only state.
        private uint noiseState = 0xA341316Cu;
        private float lowLeft;
        private float lowRight;
        private float midLeft;
        private float midRight;
        private float previousWhiteLeft;
        private float previousWhiteRight;
        private float swellPhase;
        private float crashEnvelope;
        private float smoothedActivity = 0.35f;
        private float previousActivity = 0.35f;

        private void Awake()
        {
            outputSampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);

            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 1f;
            audioSource.pitch = 1f;

            // A tiny silent looping clip keeps the AudioSource active while
            // OnAudioFilterRead supplies the actual synthesized samples.
            silentDriverClip = AudioClip.Create(
                "Procedural Ocean Driver",
                256,
                1,
                outputSampleRate,
                false);

            silentDriverClip.SetData(new float[256], 0);
            audioSource.clip = silentDriverClip;

            RefreshWaveList();
            audioSource.Play();
        }

        private void Update()
        {
            if (!reactToWaterSimulation)
                return;

            waveRefreshTimer -= Time.unscaledDeltaTime;
            if (waveRefreshTimer <= 0f)
            {
                RefreshWaveList();
                waveRefreshTimer = Mathf.Max(0.1f, waveListRefreshSeconds);
            }

            activityTimer -= Time.unscaledDeltaTime;
            if (activityTimer <= 0f)
            {
                SampleWaterActivity();
                activityTimer = Mathf.Max(0.02f, activityRefreshRate);
            }
        }

        private void OnDisable()
        {
            if (audioSource != null)
                audioSource.Stop();
        }

        private void OnEnable()
        {
            if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
                audioSource.Play();
        }

        private void OnDestroy()
        {
            if (silentDriverClip != null)
                Destroy(silentDriverClip);
        }

        [ContextMenu("Refresh Water Simulations")]
        public void RefreshWaveList()
        {
            waves.Clear();
            waves.AddRange(FindObjectsByType<PixelWaterGPU>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None));

            waves.RemoveAll(w => w == null || !w.isActiveAndEnabled);
        }

        /// <summary>
        /// Allows other gameplay systems to drive the sound manually.
        /// </summary>
        public void SetWaveActivity(float activity, float pan = 0f)
        {
            targetActivity = Mathf.Clamp01(activity);
            targetPan = Mathf.Clamp(pan, -1f, 1f);
        }

        private void SampleWaterActivity()
        {
            if (waves.Count == 0)
            {
                targetActivity = minimumActivity;
                targetPan = 0f;
                return;
            }

            float totalActivity = 0f;
            float weightedPan = 0f;
            int validSamples = 0;

            Camera camera = Camera.main;
            float cameraX = camera != null ? camera.transform.position.x : 0f;
            float halfWidth = 1f;

            if (camera != null)
            {
                halfWidth = camera.orthographic
                    ? Mathf.Max(0.1f, camera.orthographicSize * camera.aspect)
                    : 6f;
            }

            int count = Mathf.Max(1, samplesPerWave);

            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                PixelWaterGPU wave = waves[waveIndex];
                if (wave == null || !wave.isActiveAndEnabled)
                    continue;

                Vector2 minimum = wave.TankMinimum;
                Vector2 maximum = wave.TankMaximum;

                for (int sampleIndex = 0; sampleIndex < count; sampleIndex++)
                {
                    float t = count == 1
                        ? 0.5f
                        : sampleIndex / (float)(count - 1);

                    float x = Mathf.Lerp(minimum.x, maximum.x, t);
                    Vector2 velocity = wave.GetGameplayWaveVelocity(x);

                    // Vertical movement creates the strongest breaking/foam cue,
                    // with a smaller contribution from horizontal water motion.
                    float activity = Mathf.Abs(velocity.y)
                        + Mathf.Abs(velocity.x) * 0.22f;

                    activity *= activitySensitivity;
                    totalActivity += activity;

                    float pan = Mathf.Clamp((x - cameraX) / halfWidth, -1f, 1f);
                    weightedPan += pan * Mathf.Max(0.001f, activity);
                    validSamples++;
                }
            }

            if (validSamples == 0)
            {
                targetActivity = minimumActivity;
                targetPan = 0f;
                return;
            }

            float average = totalActivity / validSamples;
            targetActivity = Mathf.Clamp01(Mathf.Max(minimumActivity, average));
            targetPan = totalActivity > 0.0001f
                ? Mathf.Clamp(weightedPan / totalActivity, -1f, 1f)
                : 0f;
        }

        private float NextNoise()
        {
            // Fast deterministic xorshift noise; safe on Unity's audio thread.
            uint x = noiseState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            noiseState = x;
            return (x / 4294967295f) * 2f - 1f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0)
                return;

            int sampleRate = outputSampleRate;
            float activityTarget = targetActivity;
            float panTarget = targetPan * stereoWidth;

            // Coefficients are intentionally conservative to avoid harsh digital noise.
            float lowCoefficient = 1f - Mathf.Exp(-2f * Mathf.PI * 95f / sampleRate);
            float midCoefficient = 1f - Mathf.Exp(-2f * Mathf.PI * 850f / sampleRate);
            float activityCoefficient = 1f - Mathf.Exp(-1f / (0.35f * sampleRate));
            float crashRelease = Mathf.Exp(-crashDecay / sampleRate);
            float phaseStep = Mathf.Max(0.005f, swellRate) / sampleRate;

            for (int i = 0; i < data.Length; i += channels)
            {
                smoothedActivity += (activityTarget - smoothedActivity) * activityCoefficient;

                float activityRise = smoothedActivity - previousActivity;
                if (activityRise > 0.00003f)
                    crashEnvelope = Mathf.Clamp01(crashEnvelope + activityRise * 95f);

                previousActivity = smoothedActivity;
                crashEnvelope *= crashRelease;

                swellPhase += phaseStep;
                if (swellPhase >= 1f)
                    swellPhase -= 1f;

                float swell = Mathf.Sin(swellPhase * Mathf.PI * 2f) * 0.5f + 0.5f;
                swell = swell * swell;
                float swellGain = Mathf.Lerp(1f - swellDepth, 1f, swell);

                float whiteLeft = NextNoise();
                float whiteRight = NextNoise();

                lowLeft += (whiteLeft - lowLeft) * lowCoefficient;
                lowRight += (whiteRight - lowRight) * lowCoefficient;
                midLeft += (whiteLeft - midLeft) * midCoefficient;
                midRight += (whiteRight - midRight) * midCoefficient;

                // A simple high-pass component becomes foam hiss.
                float hissLeft = whiteLeft - previousWhiteLeft;
                float hissRight = whiteRight - previousWhiteRight;
                previousWhiteLeft = whiteLeft;
                previousWhiteRight = whiteRight;

                float dynamicFoam = foamHiss * (0.18f + smoothedActivity * 0.82f);
                float crashGain = crashAmount * crashEnvelope;
                float overall = masterVolume
                    * Mathf.Lerp(0.35f, 1f, smoothedActivity)
                    * swellGain;

                float left = (
                    lowLeft * deepWash
                    + midLeft * midWash
                    + hissLeft * (dynamicFoam + crashGain)) * overall;

                float right = (
                    lowRight * deepWash
                    + midRight * midWash
                    + hissRight * (dynamicFoam + crashGain)) * overall;

                // Equal-power-ish balance without expensive trigonometry per sample.
                float leftGain = Mathf.Clamp01(1f - panTarget * 0.5f);
                float rightGain = Mathf.Clamp01(1f + panTarget * 0.5f);

                data[i] = Mathf.Clamp(left * leftGain, -1f, 1f);
                if (channels > 1)
                    data[i + 1] = Mathf.Clamp(right * rightGain, -1f, 1f);

                for (int channel = 2; channel < channels; channel++)
                    data[i + channel] = (data[i] + data[i + 1]) * 0.5f;
            }
        }
    }

    /// <summary>
    /// Automatically creates one procedural ocean audio generator after the scene loads.
    /// </summary>
    public static class ProceduralWaveAudioBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGenerator()
        {
            if (Object.FindFirstObjectByType<PixelWaterGPU>() == null)
                return;

            if (Object.FindFirstObjectByType<ProceduralWaveAudio>() != null)
                return;

            GameObject generator = new GameObject("Procedural Wave Audio");
            Object.DontDestroyOnLoad(generator);
            generator.AddComponent<ProceduralWaveAudio>();
        }
    }
}
