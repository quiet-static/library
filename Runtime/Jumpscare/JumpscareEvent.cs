using System;
using System.Collections;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Settings;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Jumpscare
{
    /// <summary>
    /// Runs a simple, reusable jumpscare sequence.
    /// </summary>
    /// <remarks>
    /// Supports both Inspector-assigned UnityEvents and static C# events.
    ///
    /// Use UnityEvents for local scene behavior such as animations, lights, and object activation.
    /// Use static C# events for systems that may exist in other scenes, such as objective,
    /// audio, analytics, UI, or progression managers.
    /// </remarks>
    public class JumpscareEvent : MonoBehaviour
    {
        /// <summary>
        /// Raised when any jumpscare begins, after its optional delay has completed.
        /// </summary>
        public static event Action<JumpscareEvent> OnJumpscareStarted;

        /// <summary>
        /// Raised when any jumpscare finishes its cleanup and fade sequence.
        /// </summary>
        public static event Action<JumpscareEvent> OnJumpscareFinished;

        [Header("Scare Target")]
        [Tooltip("Optional GameObject to enable when the jumpscare starts. This is usually the scare model, image, prop, or enemy reveal object.")]
        [SerializeField] private GameObject scareObject;

        [Header("Audio")]
        [Tooltip("Optional AudioSource used to play the scare sound. If this is not assigned, no sound will be played.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Optional sound effect played once when the scare begins.")]
        [SerializeField] private AudioClip scareClip;

        [Tooltip("Optional pool. When populated, one clip is selected instead of Scare Clip.")]
        [SerializeField] private AudioClip[] randomizedScareClips;

        [Tooltip("One-shot volume applied independently from the AudioSource volume.")]
        [Range(0f, 1f)]
        [SerializeField] private float scareVolume = 1f;

        [Header("Reveal Effects")]
        [Tooltip("Additional objects enabled for the reveal and cleaned up with the primary object.")]
        [SerializeField] private GameObject[] additionalScareObjects;
        [Tooltip("Animators that receive Animation Trigger when the reveal begins.")]
        [SerializeField] private Animator[] revealAnimators;
        [SerializeField] private string animationTrigger = "Scare";
        [Tooltip("Particle systems restarted when the reveal begins.")]
        [SerializeField] private ParticleSystem[] revealParticles;
        [Tooltip("Lights enabled during the reveal.")]
        [SerializeField] private Light[] revealLights;

        [Header("Overlay Flash")]
        [Tooltip("Fullscreen CanvasGroup animated for the optional reveal flash.")]
        [SerializeField] private CanvasGroup flashCanvasGroup;
        [Tooltip("Optional image tinted with Flash Color before the flash.")]
        [SerializeField] private Image flashImage;
        [Tooltip("Color assigned to the optional flash image.")]
        [SerializeField] private Color flashColor = Color.white;
        [Tooltip("Peak opacity reached by the reveal flash.")]
        [Range(0f, 1f)] [SerializeField] private float flashAlpha = 0.75f;
        [Tooltip("Seconds taken to reach peak flash opacity.")]
        [Min(0f)] [SerializeField] private float flashAttack = 0.03f;
        [Tooltip("Seconds taken for the flash to return to transparent.")]
        [Min(0f)] [SerializeField] private float flashRelease = 0.2f;

        [Header("Camera Motion")]
        [Tooltip("Optional transform shaken around its starting local position.")]
        [SerializeField] private Transform shakeTarget;
        [Min(0f)] [SerializeField] private float shakeDuration = 0.25f;
        [Min(0f)] [SerializeField] private float shakeAmplitude = 0.08f;
        [Tooltip("Oscillation frequency used by the procedural camera shake.")]
        [Min(0f)] [SerializeField] private float shakeFrequency = 28f;

        [Header("Accessibility")]
        [Tooltip("Suppress overlay flashing and camera shake when the corresponding accessibility preferences are enabled.")]
        [SerializeField] private bool respectAccessibilitySettings = true;

        [Header("Fade")]
        [Tooltip("Optional screen fader used to quickly fade to black and back after the scare is visible.")]
        [SerializeField] private ScreenFader fader;

        [Header("Timing")]
        [Tooltip("Seconds to wait after Play is called before the scare begins.")]
        [Min(0f)]
        [SerializeField] private float startDelay;

        [Tooltip("Seconds the scare remains visible before cleanup begins.")]
        [Min(0f)]
        [SerializeField] private float visibleDuration = 1f;

        [Tooltip("Additional pause after cleanup before the sequence finishes.")]
        [Min(0f)] [SerializeField] private float recoveryDuration;

        [Tooltip("Use unscaled time so a scare can continue while gameplay time is paused or slowed.")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Cleanup")]
        [Tooltip("If true, the scare object is disabled after the visible duration and fade-to-black step.")]
        [SerializeField] private bool disableObjectAfter = true;

        [Header("Events")]
        [Tooltip("Invoked after the optional start delay, right before the scare object is enabled and the sound plays.")]
        [SerializeField] private UnityEvent onStarted;

        [Tooltip("Invoked immediately when Play is accepted, before the start delay.")]
        [SerializeField] private UnityEvent onAnticipation;

        [Tooltip("Invoked when reveal objects and presentation effects activate.")]
        [SerializeField] private UnityEvent onRevealed;

        [Tooltip("Invoked when reveal objects, particles, and lights are cleaned up.")]
        [SerializeField] private UnityEvent onCleanedUp;

        [Tooltip("Invoked after the scare sequence has completed cleanup and any fade-to-clear step.")]
        [SerializeField] private UnityEvent onFinished;

        /// <summary>
        /// Gets whether this jumpscare sequence is currently active.
        /// </summary>
        public bool IsRunning => running;

        /// <summary>
        /// Tracks whether the jumpscare sequence is currently running.
        /// </summary>
        private bool running;
        private Coroutine sequenceRoutine;
        private Coroutine flashRoutine;
        private Coroutine shakeRoutine;
        private Vector3 shakeStartPosition;

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
            fader = FindAnyObjectByType<ScreenFader>();
        }

        /// <summary>
        /// Starts the jumpscare sequence if it is not already running.
        /// </summary>
        public void Play()
        {
            if (running)
            {
                return;
            }

            sequenceRoutine = StartCoroutine(PlayRoutine());
        }

        /// <summary>Cancels the active sequence and restores all temporary presentation state.</summary>
        public void Stop()
        {
            if (!running) return;
            if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
            StopPresentationRoutines();
            CleanupScare();
            FinishScare();
        }

        private IEnumerator PlayRoutine()
        {
            running = true;
            onAnticipation?.Invoke();

            if (startDelay > 0f)
            {
                yield return Wait(startDelay);
            }

            BeginScare();

            if (visibleDuration > 0f)
            {
                yield return Wait(visibleDuration);
            }

            if (fader != null)
            {
                yield return fader.FadeToBlackRoutine(0.1f);
            }

            CleanupScare();

            if (fader != null)
            {
                yield return fader.FadeToClearRoutine(0.25f);
            }

            if (recoveryDuration > 0f) yield return Wait(recoveryDuration);

            FinishScare();
        }

        private void BeginScare()
        {
            // Global listeners first, then scene-local UnityEvent listeners.
            OnJumpscareStarted?.Invoke(this);
            onStarted?.Invoke();
            onRevealed?.Invoke();

            SetObjectsActive(true);

            if (revealAnimators != null && !string.IsNullOrWhiteSpace(animationTrigger))
                foreach (Animator animator in revealAnimators)
                    if (animator != null) animator.SetTrigger(animationTrigger);

            if (revealParticles != null)
                foreach (ParticleSystem particles in revealParticles)
                    if (particles != null) { particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particles.Play(true); }

            if (revealLights != null)
                foreach (Light revealLight in revealLights) if (revealLight != null) revealLight.enabled = true;

            AudioClip clip = SelectScareClip();
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, scareVolume);
            }

            bool reducedFlashing = respectAccessibilitySettings && SettingsManager.Instance != null && SettingsManager.Instance.ReducedFlashingEnabled;
            bool reducedMotion = respectAccessibilitySettings && SettingsManager.Instance != null && SettingsManager.Instance.ReducedCameraMotionEnabled;
            if (!reducedFlashing && flashCanvasGroup != null) flashRoutine = StartCoroutine(FlashRoutine());
            if (!reducedMotion && shakeTarget != null && shakeDuration > 0f && shakeAmplitude > 0f)
            {
                shakeStartPosition = shakeTarget.localPosition;
                shakeRoutine = StartCoroutine(ShakeRoutine());
            }
        }

        private void CleanupScare()
        {
            StopPresentationRoutines();
            if (disableObjectAfter) SetObjectsActive(false);
            if (revealParticles != null)
                foreach (ParticleSystem particles in revealParticles)
                    if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (revealLights != null)
                foreach (Light revealLight in revealLights) if (revealLight != null) revealLight.enabled = false;
            onCleanedUp?.Invoke();
        }

        private void FinishScare()
        {
            // Global listeners first, then scene-local UnityEvent listeners.
            OnJumpscareFinished?.Invoke(this);
            onFinished?.Invoke();

            running = false;
            sequenceRoutine = null;
        }

        private object Wait(float duration) => useUnscaledTime
            ? new WaitForSecondsRealtime(duration)
            : new WaitForSeconds(duration);

        private AudioClip SelectScareClip()
        {
            if (randomizedScareClips != null && randomizedScareClips.Length > 0)
                return randomizedScareClips[UnityEngine.Random.Range(0, randomizedScareClips.Length)];
            return scareClip;
        }

        private void SetObjectsActive(bool active)
        {
            if (scareObject != null) scareObject.SetActive(active);
            if (additionalScareObjects == null) return;
            foreach (GameObject item in additionalScareObjects) if (item != null) item.SetActive(active);
        }

        private IEnumerator FlashRoutine()
        {
            if (flashImage != null) flashImage.color = flashColor;
            yield return FadeCanvas(flashCanvasGroup.alpha, flashAlpha, flashAttack);
            yield return FadeCanvas(flashCanvasGroup.alpha, 0f, flashRelease);
            flashRoutine = null;
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (duration <= 0f) { flashCanvasGroup.alpha = to; yield break; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                flashCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            flashCanvasGroup.alpha = to;
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;
            while (elapsed < shakeDuration && shakeTarget != null)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float strength = shakeAmplitude * (1f - Mathf.Clamp01(elapsed / shakeDuration));
                float phase = elapsed * shakeFrequency;
                shakeTarget.localPosition = shakeStartPosition + new Vector3(
                    Mathf.Sin(phase * 1.17f), Mathf.Cos(phase), 0f) * strength;
                yield return null;
            }
            RestoreShakeTarget();
            shakeRoutine = null;
        }

        private void StopPresentationRoutines()
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            flashRoutine = null;
            shakeRoutine = null;
            if (flashCanvasGroup != null) flashCanvasGroup.alpha = 0f;
            RestoreShakeTarget();
        }

        private void RestoreShakeTarget()
        {
            if (shakeTarget != null) shakeTarget.localPosition = shakeStartPosition;
        }

        private void OnDisable()
        {
            if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
            StopPresentationRoutines();
            running = false;
        }
    }
}
