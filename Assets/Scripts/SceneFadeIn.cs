using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class SceneFadeIn : MonoBehaviour
    {
        [Header("Fade")]
        [SerializeField, Min(0f)] private float startDelay = 0.15f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 1.5f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool destroyAfterFade = true;

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;
        }

        private void Start()
        {
            StartCoroutine(FadeFromBlack());
        }

        private IEnumerator FadeFromBlack()
        {
            if (startDelay > 0f)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(startDelay);
                else
                    yield return new WaitForSeconds(startDelay);
            }

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += useUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

                float progress = Mathf.Clamp01(elapsed / fadeDuration);

                // Smooth rather than perfectly linear.
                progress = progress * progress * (3f - 2f * progress);

                canvasGroup.alpha = 1f - progress;
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            if (destroyAfterFade)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}