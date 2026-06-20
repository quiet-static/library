using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Handles full-screen fade transitions using a <see cref="CanvasGroup"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("CanvasGroup used to control the opacity of the full-screen fade overlay.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("Optional UI Image used as the visible fade overlay. Its color is set from Fade Color on Awake.")]
        [SerializeField] private Image fadeImage;

        [Header("Appearance")]
        [Tooltip("Color applied to the fade image when the fader initializes.")]
        [SerializeField] private Color fadeColor = Color.black;

        [Header("Default Durations")]
        [Tooltip("Default duration used by FadeToBlack() when no duration is supplied.")]
        [Min(0f)]
        [SerializeField] private float fadeToBlackDuration = 0.25f;

        [Tooltip("Default duration used by FadeToClear() when no duration is supplied.")]
        [Min(0f)]
        [SerializeField] private float fadeToClearDuration = 0.25f;

        [Header("Startup State")]
        [Tooltip("If true, the screen starts clear. If false, the screen starts fully faded.")]
        [SerializeField] private bool startClear = true;

        /// <summary>
        /// Gets whether a fade routine is currently running.
        /// </summary>
        public bool IsFading { get; private set; }

        /// <summary>
        /// Currently running fire-and-forget fade coroutine.
        /// </summary>
        private Coroutine fadeRoutine;

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            fadeImage = GetComponentInChildren<Image>();
        }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (fadeImage != null)
            {
                fadeImage.color = fadeColor;
            }

            SetAlpha(startClear ? 0f : 1f);
        }

        /// <summary>
        /// Starts a fade to black using the configured default duration.
        /// </summary>
        public void FadeToBlack()
        {
            FadeToBlack(fadeToBlackDuration);
        }

        /// <summary>
        /// Starts a fade to black.
        /// </summary>
        /// <param name="duration">Fade duration in unscaled seconds.</param>
        public void FadeToBlack(float duration)
        {
            StartFade(1f, duration);
        }

        /// <summary>
        /// Starts a fade to clear using the configured default duration.
        /// </summary>
        public void FadeToClear()
        {
            FadeToClear(fadeToClearDuration);
        }

        /// <summary>
        /// Starts a fade to clear.
        /// </summary>
        /// <param name="duration">Fade duration in unscaled seconds.</param>
        public void FadeToClear(float duration)
        {
            StartFade(0f, duration);
        }

        /// <summary>
        /// Immediately makes the screen fully black.
        /// </summary>
        public void SetBlackInstant()
        {
            StopActiveFade();
            SetAlpha(1f);
        }

        /// <summary>
        /// Immediately makes the screen fully clear.
        /// </summary>
        public void SetClearInstant()
        {
            StopActiveFade();
            SetAlpha(0f);
        }

        /// <summary>
        /// Returns a coroutine that fades to black using the configured default duration.
        /// </summary>
        public IEnumerator FadeToBlackRoutine()
        {
            yield return FadeRoutine(1f, fadeToBlackDuration);
        }

        /// <summary>
        /// Returns a coroutine that fades to black.
        /// </summary>
        /// <param name="duration">Fade duration in unscaled seconds.</param>
        public IEnumerator FadeToBlackRoutine(float duration)
        {
            yield return FadeRoutine(1f, duration);
        }

        /// <summary>
        /// Returns a coroutine that fades to clear using the configured default duration.
        /// </summary>
        public IEnumerator FadeToClearRoutine()
        {
            yield return FadeRoutine(0f, fadeToClearDuration);
        }

        /// <summary>
        /// Returns a coroutine that fades to clear.
        /// </summary>
        /// <param name="duration">Fade duration in unscaled seconds.</param>
        public IEnumerator FadeToClearRoutine(float duration)
        {
            yield return FadeRoutine(0f, duration);
        }

        /// <summary>
        /// Immediately sets the fade overlay alpha.
        /// </summary>
        /// <param name="alpha">
        /// Desired alpha value from 0 to 1.
        /// </param>
        public void SetAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.01f;
        }

        /// <summary>
        /// Stops any active fire-and-forget fade routine.
        /// </summary>
        public void StopActiveFade()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            IsFading = false;
        }

        /// <summary>
        /// Starts a managed fade coroutine and stops any earlier managed fade.
        /// </summary>
        private void StartFade(float targetAlpha, float duration)
        {
            StopActiveFade();
            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
        }

        /// <summary>
        /// Gradually fades from the current alpha to the requested alpha.
        /// </summary>
        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            IsFading = true;

            if (canvasGroup == null)
            {
                IsFading = false;
                yield break;
            }

            float startAlpha = canvasGroup.alpha;

            if (duration <= 0f)
            {
                SetAlpha(targetAlpha);
                IsFading = false;
                fadeRoutine = null;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, progress));

                yield return null;
            }

            SetAlpha(targetAlpha);

            IsFading = false;
            fadeRoutine = null;
        }
    }
}