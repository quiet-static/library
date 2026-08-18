using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Handles full-screen fade transitions using a <see cref="CanvasGroup"/>.
    /// </summary>
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
            canvasGroup.interactable = canvasGroup.alpha > 0.01f;
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
            try
            {
                if (canvasGroup == null)
                {
                    yield break;
                }

                float startAlpha = canvasGroup.alpha;

                if (duration <= 0f)
                {
                    SetAlpha(targetAlpha);
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
            }
            finally
            {
                // Direct callers can cancel this iterator by stopping their parent
                // coroutine, so lifecycle state must not depend on reaching the end.
                IsFading = false;
                fadeRoutine = null;
            }
        }
    }

    /// <summary>Target state requested through a <see cref="ScreenFadeChannel"/>.</summary>
    public enum ScreenFadeTarget
    {
        Clear,
        Black
    }

    /// <summary>One completion-aware cross-scene fade request.</summary>
    public sealed class ScreenFadeRequest
    {
        internal ScreenFadeRequest(ScreenFadeTarget target, float duration)
        {
            Target = target;
            Duration = Mathf.Max(0f, duration);
        }

        /// <summary>Requested final screen state.</summary>
        public ScreenFadeTarget Target { get; }

        /// <summary>Requested fade duration in unscaled seconds.</summary>
        public float Duration { get; }

        /// <summary>Gets whether the receiving handler completed the fade.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>Gets whether a newer request or disabled handler canceled this fade.</summary>
        public bool WasCancelled { get; private set; }

        /// <summary>Marks this request complete. Intended for a screen-fade handler.</summary>
        public void Complete() => IsComplete = true;

        /// <summary>Releases callers waiting on a fade that can no longer complete.</summary>
        internal void Cancel()
        {
            WasCancelled = true;
            IsComplete = true;
        }
    }

    /// <summary>Routes completion-aware fade requests between separately loaded scenes.</summary>
    [CreateAssetMenu(
        fileName = "ScreenFadeChannel",
        menuName = "Quiet Static Toolkit/Cinematics/Screen Fade Channel")]
    public sealed class ScreenFadeChannel : ScriptableObject
    {
        /// <summary>Raised when a fade is requested.</summary>
        public event Action<ScreenFadeRequest> FadeRequested;

        /// <summary>Gets whether an enabled handler is subscribed.</summary>
        public bool HasReceiver => FadeRequested != null;

        /// <summary>Requests a fade and waits until its handler reports completion.</summary>
        public IEnumerator FadeRoutine(ScreenFadeTarget target, float duration)
        {
            ScreenFadeRequest request = new(target, duration);
            Action<ScreenFadeRequest> receivers = FadeRequested;
            if (receivers == null)
            {
                GameLogger.Warning(nameof(ScreenFadeChannel), this,
                    $"{nameof(ScreenFadeChannel)} has no active handler.");
                yield break;
            }

            receivers.Invoke(request);
            yield return new WaitUntil(() => request.IsComplete);
        }
    }

    /// <summary>Connects a scene-local <see cref="ScreenFader"/> to a cross-scene channel.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScreenFader))]
    [AddComponentMenu("Quiet Static Toolkit/Cinematics/Screen Fade Channel Handler")]
    public sealed class ScreenFadeChannelHandler : MonoBehaviour
    {
        [Tooltip("Channel shared with systems that request screen fades.")]
        [SerializeField] private ScreenFadeChannel channel;

        [Tooltip("Scene-local fader that performs requests. Auto-filled from this object.")]
        [SerializeField] private ScreenFader screenFader;

        private Coroutine activeRequestRoutine;
        private ScreenFadeRequest activeRequest;

        private void Reset() => screenFader = GetComponent<ScreenFader>();

        private void OnEnable()
        {
            if (screenFader == null) screenFader = GetComponent<ScreenFader>();
            if (channel != null) channel.FadeRequested += HandleFadeRequested;
        }

        private void OnDisable()
        {
            if (channel != null) channel.FadeRequested -= HandleFadeRequested;
            CancelActiveRequest();
        }

        private void HandleFadeRequested(ScreenFadeRequest request)
        {
            CancelActiveRequest();
            activeRequest = request;
            Coroutine startedRoutine = StartCoroutine(PerformFade(request));
            if (!request.IsComplete)
            {
                activeRequestRoutine = startedRoutine;
            }
        }

        private IEnumerator PerformFade(ScreenFadeRequest request)
        {
            if (screenFader != null)
            {
                screenFader.StopActiveFade();
                yield return request.Target == ScreenFadeTarget.Black
                    ? screenFader.FadeToBlackRoutine(request.Duration)
                    : screenFader.FadeToClearRoutine(request.Duration);
            }
            request.Complete();
            if (ReferenceEquals(activeRequest, request))
            {
                activeRequest = null;
                activeRequestRoutine = null;
            }
        }

        private void CancelActiveRequest()
        {
            if (activeRequestRoutine != null)
            {
                StopCoroutine(activeRequestRoutine);
                screenFader?.StopActiveFade();
            }

            activeRequestRoutine = null;
            activeRequest?.Cancel();
            activeRequest = null;
        }
    }
}
