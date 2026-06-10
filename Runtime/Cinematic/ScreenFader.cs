using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Handles full-screen fade transitions using a <see cref="CanvasGroup"/>.
    /// </summary>
    /// <remarks>
    /// This component is intended for simple cinematic, scene transition, and UI fade effects.
    /// It should usually be placed on a full-screen UI object that contains a black Image and
    /// a CanvasGroup. The CanvasGroup alpha controls visibility, while the optional Image controls
    /// the fade color.
    ///
    /// Public methods are provided for both fire-and-forget fades and coroutine-based fades.
    /// Coroutine methods are useful when another script needs to wait until the fade finishes
    /// before continuing, such as before loading a new scene or changing a camera shot.
    /// </remarks>
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

        [Header("Startup State")]
        [Tooltip("If true, the screen starts clear. If false, the screen starts fully faded.")]
        [SerializeField] private bool startClear = true;

        /// <summary>
        /// Gets whether a fade routine is currently running.
        /// </summary>
        public bool IsFading { get; private set; }

        /// <summary>
        /// Currently running fade coroutine, if one was started through <see cref="StartFade"/>.
        /// </summary>
        private Coroutine fadeRoutine;

        /// <summary>
        /// Auto-fills component references when this component is added or reset in the Inspector.
        /// </summary>
        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            fadeImage = GetComponentInChildren<Image>();
        }

        /// <summary>
        /// Ensures required references are assigned, applies the configured fade color,
        /// and initializes the overlay to either clear or fully faded.
        /// </summary>
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
        /// Starts a fire-and-forget fade to a fully opaque overlay.
        /// </summary>
        /// <param name="duration">How long the fade should take, in unscaled seconds.</param>
        public void FadeToBlack(float duration)
        {
            StartFade(1f, duration);
        }

        /// <summary>
        /// Starts a fire-and-forget fade to a fully transparent overlay.
        /// </summary>
        /// <param name="duration">How long the fade should take, in unscaled seconds.</param>
        public void FadeToClear(float duration)
        {
            StartFade(0f, duration);
        }

        /// <summary>
        /// Returns a coroutine that fades to a fully opaque overlay.
        /// </summary>
        /// <param name="duration">How long the fade should take, in unscaled seconds.</param>
        /// <returns>An IEnumerator that can be yielded by another coroutine.</returns>
        public IEnumerator FadeToBlackRoutine(float duration)
        {
            return FadeRoutine(1f, duration);
        }

        /// <summary>
        /// Returns a coroutine that fades to a fully transparent overlay.
        /// </summary>
        /// <param name="duration">How long the fade should take, in unscaled seconds.</param>
        /// <returns>An IEnumerator that can be yielded by another coroutine.</returns>
        public IEnumerator FadeToClearRoutine(float duration)
        {
            return FadeRoutine(0f, duration);
        }

        /// <summary>
        /// Immediately sets the fade overlay alpha.
        /// </summary>
        /// <param name="alpha">
        /// Desired alpha value. Values are clamped between 0 and 1.
        /// 0 is fully clear, and 1 is fully opaque.
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
        /// Starts a managed fade coroutine and stops any previous managed fade first.
        /// </summary>
        /// <param name="targetAlpha">The alpha value to fade toward.</param>
        /// <param name="duration">How long the fade should take, in unscaled seconds.</param>
        private void StartFade(float targetAlpha, float duration)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
        }

        /// <summary>
        /// Gradually fades the overlay from its current alpha to the requested target alpha.
        /// </summary>
        /// <param name="targetAlpha">The alpha value to fade toward.</param>
        /// <param name="duration">How long the fade should take, in unscaled seconds.</param>
        /// <returns>An IEnumerator used by Unity's coroutine system.</returns>
        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            IsFading = true;

            float start = canvasGroup != null ? canvasGroup.alpha : 0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = duration <= 0f ? 1f : elapsed / duration;
                SetAlpha(Mathf.Lerp(start, targetAlpha, t));

                yield return null;
            }

            SetAlpha(targetAlpha);

            IsFading = false;
            fadeRoutine = null;
        }
    }
}
