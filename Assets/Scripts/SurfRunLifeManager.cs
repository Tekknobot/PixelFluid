using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Keeps the persistent ocean scene alive while managing a three-life surf run.
    /// The final death restarts progression in place instead of reloading the scene.
    /// </summary>
    [DefaultExecutionOrder(-11900)]
    [DisallowMultipleComponent]
    public sealed class SurfRunLifeManager : MonoBehaviour
    {
        public static SurfRunLifeManager Instance { get; private set; }

        [SerializeField, Min(1)] private int startingLives = 3;
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.65f;
        [SerializeField, Min(0f)] private float blackHoldDuration = 0.25f;

        private int livesRemaining;
        private float fadeAlpha;
        private bool handlingDeath;
        private Texture2D fadeTexture;

        public int LivesRemaining => livesRemaining;
        public int StartingLives => Mathf.Max(1, startingLives);

        public void ResetLivesForNewRun() => livesRemaining = Mathf.Max(1, startingLives);
        public void RestoreLives(int savedLives) => livesRemaining = Mathf.Clamp(savedLives, 1, Mathf.Max(1, startingLives));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurfRunLifeManager>() != null)
                return;

            GameObject host = new("Surf Run Life Manager");
            DontDestroyOnLoad(host);
            host.AddComponent<SurfRunLifeManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            livesRemaining = Mathf.Max(1, startingLives);
            fadeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Surf Run Fade Pixel",
                hideFlags = HideFlags.HideAndDontSave
            };
            fadeTexture.SetPixel(0, 0, Color.white);
            fadeTexture.Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (fadeTexture != null)
                Destroy(fadeTexture);
        }

        public void HandleFinishedPlayerDeath(TinyWaveSurfer surfer)
        {
            if (handlingDeath || surfer == null || !surfer.IsPlayerControlled)
                return;

            StartCoroutine(ResolveDeath(surfer));
        }

        private IEnumerator ResolveDeath(TinyWaveSurfer surfer)
        {
            handlingDeath = true;
            livesRemaining = Mathf.Max(0, livesRemaining - 1);

            yield return FadeTo(1f);
            if (blackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(blackHoldDuration);

            if (livesRemaining > 0)
            {
                surfer.RespawnForManagedRun();
            }
            else
            {
                // Keep the stage checkpoint intact. The player chooses Continue
                // from the main menu to rebuild the ocean at that saved stage.
                SurferSlugPauseMenu.Instance?.ShowGameOver();
            }

            yield return FadeTo(0f);
            handlingDeath = false;
        }

        private IEnumerator FadeTo(float target)
        {
            float start = fadeAlpha;
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, fadeDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeAlpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            fadeAlpha = target;
        }

        private void OnGUI()
        {
            if (fadeAlpha > 0.001f && fadeTexture != null)
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeTexture, ScaleMode.StretchToFill);
                GUI.color = oldColor;
            }
        }
    }
}
