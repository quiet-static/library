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
    /// The sequence can optionally wait before starting, activate a scare object,
    /// play a one-shot sound, keep the scare visible for a short duration, fade the
    /// screen, disable the object afterward, and invoke UnityEvents at the start and end.
    ///
    /// This component is intentionally generic so it can be used for hallway scares,
    /// object pop-ins, enemy reveals, sudden sound events, or any other scripted scare
    /// that follows the same basic timing pattern.
    /// </remarks>
    public class JumpscareEvent : MonoBehaviour
    {
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
        /// Tracks whether the jumpscare sequence is currently running.
        /// </summary>
        /// <remarks>
        /// This prevents multiple overlapping Play calls from starting duplicate
        /// routines on the same jumpscare object.
        /// </remarks>
        private bool running;

        /// <summary>
        /// Attempts to auto-fill nearby component references when this component is
        /// first added or reset in the Unity Inspector.
        /// </summary>
        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
            fader = FindAnyObjectByType<ScreenFader>();
        }

        /// <summary>
        /// Starts the jumpscare sequence if it is not already running.
        /// </summary>
        /// <remarks>
        /// This method is safe to call from UnityEvents, animation events, triggers,
        /// interactables, or other gameplay scripts. Repeated calls while the sequence
        /// is already active are ignored.
        /// </remarks>
        public void Play()
        {
            if (running)
            {
                return;
            }

            StartCoroutine(PlayRoutine());
        }

        /// <summary>
        /// Runs the full jumpscare sequence from delay to cleanup.
        /// </summary>
        /// <returns>
        /// Coroutine enumerator used by Unity to step through the timed sequence.
        /// </returns>
        private IEnumerator PlayRoutine()
        {
            running = true;

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

        /// <summary>
        /// Performs the start-of-scare actions.
        /// </summary>
        /// <remarks>
        /// This invokes the start event, enables the scare object if assigned, and
        /// plays the scare sound if both an AudioSource and AudioClip are available.
        /// </remarks>
        private void BeginScare()
        {
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

        /// <summary>
        /// Performs cleanup after the scare has been visible long enough.
        /// </summary>
        private void CleanupScare()
        {
            if (disableObjectAfter && scareObject != null)
            {
                scareObject.SetActive(false);
            }
        }

        /// <summary>
        /// Marks the sequence as complete and invokes the finished event.
        /// </summary>
        private void FinishScare()
        {
            onFinished?.Invoke();
            running = false;
        }
    }
}
