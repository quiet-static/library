using System;
using System.Collections;
using QuietStatic.Toolkit.Cinematics;
using UnityEngine;
using UnityEngine.Events;

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

        /// <summary>
        /// Raised when a jumpscare's running state changes.
        /// The bool parameter is true while running and false when complete.
        /// </summary>
        public static event Action<JumpscareEvent, bool> OnJumpscareRunningChanged;

        [Header("Scare Target")]
        [Tooltip("Optional GameObject to enable when the jumpscare starts. This is usually the scare model, image, prop, or enemy reveal object.")]
        [SerializeField] private GameObject scareObject;

        [Header("Audio")]
        [Tooltip("Optional AudioSource used to play the scare sound. If this is not assigned, no sound will be played.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Optional sound effect played once when the scare begins.")]
        [SerializeField] private AudioClip scareClip;

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

        [Header("Cleanup")]
        [Tooltip("If true, the scare object is disabled after the visible duration and fade-to-black step.")]
        [SerializeField] private bool disableObjectAfter = true;

        [Header("Events")]
        [Tooltip("Invoked after the optional start delay, right before the scare object is enabled and the sound plays.")]
        [SerializeField] private UnityEvent onStarted;

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

            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            SetRunning(true);

            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            BeginScare();

            if (visibleDuration > 0f)
            {
                yield return new WaitForSeconds(visibleDuration);
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

            FinishScare();
        }

        private void BeginScare()
        {
            // Global listeners first, then scene-local UnityEvent listeners.
            OnJumpscareStarted?.Invoke(this);
            onStarted?.Invoke();

            if (scareObject != null)
            {
                scareObject.SetActive(true);
            }

            if (audioSource != null && scareClip != null)
            {
                audioSource.PlayOneShot(scareClip);
            }
        }

        private void CleanupScare()
        {
            if (disableObjectAfter && scareObject != null)
            {
                scareObject.SetActive(false);
            }
        }

        private void FinishScare()
        {
            // Global listeners first, then scene-local UnityEvent listeners.
            OnJumpscareFinished?.Invoke(this);
            onFinished?.Invoke();

            SetRunning(false);
        }

        /// <summary>
        /// Updates the running state and notifies global listeners when it changes.
        /// </summary>
        private void SetRunning(bool isRunning)
        {
            if (running == isRunning)
            {
                return;
            }

            running = isRunning;
            OnJumpscareRunningChanged?.Invoke(this, running);
        }
    }
}