using System;
using System.Collections;
using System.Collections.Generic;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Runs a small, reusable cutscene sequence made from ordered steps.
    /// </summary>
    /// <remarks>
    /// Each step can invoke UnityEvents, select a director-owned camera shot, optionally fade in or out,
    /// start a dialogue runner, wait for dialogue to finish, wait an additional amount of time,
    /// and then invoke completion events.
    ///
    /// This component is intentionally generic so it can be reused across games and scenes.
    /// Game-specific behavior should usually be placed in the step UnityEvents rather than
    /// hardcoded into this runner.
    /// </remarks>
    public class CutsceneSequenceRunner : MonoBehaviour, ICinematicWaitSource
    {
        /// <summary>Raised by this runner whenever its sequence begins.</summary>
        public event Action SequenceStarted;

        /// <summary>Raised by this runner whenever its sequence completes or is stopped.</summary>
        public event Action SequenceEnded;

        /// <summary>
        /// One ordered unit of work inside a cutscene sequence.
        /// </summary>
        /// <remarks>
        /// Steps are executed from top to bottom in the Inspector array. A step can be used for
        /// a camera shot, a dialogue beat, a character action, a timed pause, or any combination
        /// of those pieces.
        /// </remarks>
        [Serializable]
        public class Step
        {
            [Header("Step Identity")]
            [Tooltip("Optional label used only to make this step easier to identify in the Inspector.")]
            /// <summary>Optional designer-facing step label.</summary>
            public string name;

            [Header("Events")]
            [Tooltip("Events invoked at the beginning of this step, before the camera is moved or dialogue starts.")]
            /// <summary>Callbacks invoked when this step begins.</summary>
            public UnityEvent onStepStarted;

            [Tooltip("Events invoked after this step's dialogue and wait time have completed.")]
            /// <summary>Callbacks invoked when this step finishes.</summary>
            public UnityEvent onStepFinished;

            [Header("Camera")]
            [Tooltip("Optional camera director that owns a dropdown-selected shot for this step.")]
            /// <summary>Optional director used to apply a named camera shot.</summary>
            public CinematicCutsceneCameraDirector cameraDirector;

            [Tooltip("Stable camera shot selected from the assigned director.")]
            [CinematicShotId(nameof(cameraDirector))]
            /// <summary>Stable ID of the director-owned shot applied by this step.</summary>
            public string cameraShotId;

            [Header("Dialogue")]
            [Tooltip("Optional dialogue runner to start during this step. The sequence waits until this runner is finished before continuing.")]
            /// <summary>Optional dialogue runner started and awaited by this step.</summary>
            public DialogueRunner dialogueRunner;

            [Tooltip("Optional project-specific activity to start and await. The assigned MonoBehaviour must implement ICinematicWaitSource. This takes precedence over Dialogue Runner.")]
            /// <summary>Optional component implementing ICinematicWaitSource.</summary>
            public MonoBehaviour waitSource;

            [Header("Timing")]
            [Tooltip("Time, in seconds, to wait after step-start events and camera placement but before starting dialogue or another wait source.")]
            [Min(0f)]
            /// <summary>Delay before the step's activity begins, in seconds.</summary>
            public float waitBeforeActivity;

            [Tooltip("Extra time, in seconds, to wait after dialogue completes and before the step-finished events run.")]
            [Min(0f)]
            /// <summary>Delay after the step's activity finishes, in seconds.</summary>
            public float waitAfterStep;
        }

        [Header("Steps")]
        [Tooltip("Ordered list of cutscene steps. Steps run from first to last.")]
        [SerializeField] private Step[] steps;

        [Header("Startup")]
        [Tooltip("If true, this sequence begins automatically when Start is called.")]
        [SerializeField] private bool playOnStart;

        [Tooltip("Whether step waits continue while Unity time is paused.")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Fading")]
        [Tooltip("Optional screen fader used for fade-out and fade-in transitions between steps.")]
        [SerializeField] private ScreenFader fader;

        [Tooltip("Optional cross-scene fade channel. Preferred over the direct fader when an active handler is available.")]
        [SerializeField] private ScreenFadeChannel fadeChannel;

        [Tooltip("Duration, in seconds, of the fade to black before each step begins. Set to 0 to skip this fade.")]
        [Min(0f)]
        [SerializeField] private float fadeOutBeforeStep = 0f;

        [Tooltip("Duration, in seconds, of the fade back to clear after the camera is moved. Set to 0 to skip this fade.")]
        [Min(0f)]
        [SerializeField] private float fadeInAfterCameraMove = 0f;

        [Header("Sequence Events")]
        [Tooltip("Events invoked once when the sequence begins, before the first step runs.")]
        [SerializeField] private UnityEvent onSequenceStarted;

        [Tooltip("Events invoked once after the final step completes.")]
        [SerializeField] private UnityEvent onSequenceFinished;

        private Coroutine activeRoutine;

        /// <summary>
        /// Gets whether this sequence is currently playing.
        /// </summary>
        /// <remarks>
        /// This is useful for other systems that need to avoid starting duplicate cutscenes,
        /// disabling player input while a sequence is active, or waiting until a sequence ends.
        /// </remarks>
        public bool IsRunning { get; private set; }

        /// <summary>Gets the configured steps for browsers and debug tooling.</summary>
        public IReadOnlyList<Step> Steps => steps ?? Array.Empty<Step>();

        /// <summary>Gets the zero-based step currently being played, or -1 while idle.</summary>
        public int CurrentStepIndex { get; private set; } = -1;

        /// <summary>Gets a stable designer-facing name for this cutscene.</summary>
        public string DisplayName => gameObject.name;

        /// <summary>
        /// Starts the sequence automatically if <see cref="playOnStart"/> is enabled.
        /// </summary>
        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        /// <summary>
        /// Starts the cutscene sequence if it is not already running.
        /// </summary>
        /// <remarks>
        /// Calling this while the sequence is already active does nothing. This prevents duplicate
        /// sequence coroutines from running at the same time.
        /// </remarks>
        public void Play()
        {
            if (!IsRunning)
            {
                activeRoutine = StartCoroutine(PlayRoutine());
            }
        }

        /// <summary>Stops the active sequence without invoking its completion event.</summary>
        public void Stop()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = null;
            CurrentStepIndex = -1;
            if (IsRunning)
            {
                IsRunning = false;
                SequenceEnded?.Invoke();
            }
        }

        /// <summary>Plays one step in isolation for debugging and iteration.</summary>
        public void PlayStep(int stepIndex)
        {
            if (IsRunning || steps == null || stepIndex < 0 || stepIndex >= steps.Length ||
                steps[stepIndex] == null)
            {
                return;
            }

            activeRoutine = StartCoroutine(PlaySingleStepRoutine(stepIndex));
        }

        /// <summary>
        /// Plays the full cutscene sequence from beginning to end.
        /// </summary>
        /// <returns>
        /// An enumerator used by Unity's coroutine system. Other scripts may also yield this
        /// routine directly if they need to wait for the entire sequence to finish.
        /// </returns>
        public IEnumerator PlayRoutine()
        {
            IsRunning = true;
            onSequenceStarted?.Invoke();
            SequenceStarted?.Invoke();

            if (steps != null)
            {
                for (int index = 0; index < steps.Length; index++)
                {
                    Step step = steps[index];
                    if (step == null)
                    {
                        continue;
                    }

                    CurrentStepIndex = index;
                    yield return PlayStepRoutine(step);
                }
            }

            onSequenceFinished?.Invoke();
            SequenceEnded?.Invoke();
            IsRunning = false;
            CurrentStepIndex = -1;
            activeRoutine = null;
        }

        private IEnumerator PlaySingleStepRoutine(int stepIndex)
        {
            IsRunning = true;
            CurrentStepIndex = stepIndex;
            SequenceStarted?.Invoke();
            yield return PlayStepRoutine(steps[stepIndex]);
            SequenceEnded?.Invoke();
            CurrentStepIndex = -1;
            IsRunning = false;
            activeRoutine = null;
        }

        private void OnDisable()
        {
            Stop();
        }

        /// <summary>
        /// Runs one configured cutscene step.
        /// </summary>
        /// <param name="step">The step to execute.</param>
        /// <returns>An enumerator used by Unity's coroutine system.</returns>
        private IEnumerator PlayStepRoutine(Step step)
        {
            if (fadeOutBeforeStep > 0f && HasFadeReceiver())
            {
                yield return FadeRoutine(ScreenFadeTarget.Black, fadeOutBeforeStep);
            }

            step.onStepStarted?.Invoke();
            ApplyCameraPose(step);

            if (fadeInAfterCameraMove > 0f && HasFadeReceiver())
            {
                yield return FadeRoutine(ScreenFadeTarget.Clear, fadeInAfterCameraMove);
            }

            if (step.waitBeforeActivity > 0f)
            {
                yield return WaitForDuration(step.waitBeforeActivity);
            }

            ICinematicWaitSource waitSource =
                step.waitSource as ICinematicWaitSource;

            if (step.waitSource != null && waitSource == null)
            {
                GameLogger.Warning(
                    nameof(PlayStepRoutine),
                    this,
                    $"Wait source '{step.waitSource.name}' does not implement " +
                    $"{nameof(ICinematicWaitSource)}."
                );
            }
            else if (waitSource != null)
            {
                waitSource.Play();
                yield return new WaitUntil(
                    () => step.waitSource == null || !waitSource.IsRunning
                );
            }
            else if (step.dialogueRunner != null)
            {
                ICinematicWaitSource dialogueSource = step.dialogueRunner;
                dialogueSource.Play();
                yield return new WaitUntil(() => !dialogueSource.IsRunning);
            }

            if (step.waitAfterStep > 0f)
            {
                yield return WaitForDuration(step.waitAfterStep);
            }

            step.onStepFinished?.Invoke();
        }

        private bool HasFadeReceiver()
        {
            return (fadeChannel != null && fadeChannel.HasReceiver) || fader != null;
        }

        private IEnumerator FadeRoutine(ScreenFadeTarget target, float duration)
        {
            if (fadeChannel != null && fadeChannel.HasReceiver)
            {
                yield return fadeChannel.FadeRoutine(target, duration);
                yield break;
            }

            if (fader == null) yield break;
            fader.StopActiveFade();
            yield return target == ScreenFadeTarget.Black
                ? fader.FadeToBlackRoutine(duration)
                : fader.FadeToClearRoutine(duration);
        }

        private IEnumerator WaitForDuration(float duration)
        {
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(duration);
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }
        }

        /// <summary>
        /// Applies the configured camera shot for a step.
        /// </summary>
        /// <param name="step">The step containing an optional director-owned shot.</param>
        private static void ApplyCameraPose(Step step)
        {
            if (step.cameraDirector != null &&
                !string.IsNullOrWhiteSpace(step.cameraShotId))
            {
                step.cameraDirector.CutToShot(step.cameraShotId);
            }
        }
    }

    /// <summary>Transitions to a content scene and starts a cutscene after it becomes active.</summary>
    /// <remarks>Place this coordinator in the persistent Systems scene.</remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Cinematics/Cutscene Transition Player")]
    public sealed class CutsceneTransitionPlayer : MonoBehaviour
    {
        [Header("Scene Flow")]
        [Tooltip("Required scene-flow channel used to submit the transition.")]
        [RequiredCommandChannel]
        [SerializeField] private SceneFlowRequestChannel requestChannel;

        [Header("Configured Launch")]
        [Tooltip("Scene that contains the cutscene runner.")]
        [SerializeField] private string targetScene;

        [Tooltip("GameObject name of the CutsceneSequenceRunner to play. Matching is case-insensitive.")]
        [SerializeField] private string cutsceneName;

        [Header("Events")]
        [Tooltip("Invoked after the destination runner begins playing.")]
        [SerializeField] private UnityEvent onCutsceneStarted;

        [Tooltip("Invoked if the transition cannot start or the named cutscene cannot be found.")]
        [SerializeField] private UnityEvent onLaunchFailed;

        private string pendingScene;
        private string pendingCutscene;

        /// <summary>Gets whether this coordinator is awaiting a destination scene.</summary>
        public bool IsPending => !string.IsNullOrEmpty(pendingScene);

        private void OnEnable()
        {
            if (requestChannel != null)
            {
                requestChannel.TransitionCompleted += HandleTransitionCompleted;
            }
        }

        private void OnDisable()
        {
            if (requestChannel != null)
            {
                requestChannel.TransitionCompleted -= HandleTransitionCompleted;
            }
            ClearPending();
        }

        /// <summary>Transitions using the Inspector-configured destination and cutscene.</summary>
        public void PlayConfigured() => TransitionAndPlay(targetScene, cutsceneName);

        /// <summary>Transitions to a scene and plays its named cutscene after activation.</summary>
        public bool TransitionAndPlay(string sceneName, string runnerName)
        {
            if (IsPending || string.IsNullOrWhiteSpace(sceneName) ||
                string.IsNullOrWhiteSpace(runnerName))
            {
                onLaunchFailed?.Invoke();
                return false;
            }

            pendingScene = sceneName.Trim();
            pendingCutscene = runnerName.Trim();
            SceneTransitionRequest request = new(pendingScene);
            bool accepted = requestChannel != null &&
                            requestChannel.RequestTransition(request);

            if (!accepted)
            {
                ClearPending();
                onLaunchFailed?.Invoke();
            }
            return accepted;
        }

        private void HandleTransitionCompleted(string sceneName)
        {
            if (!IsPending || !string.Equals(sceneName, pendingScene, StringComparison.Ordinal)) return;
            string requestedCutscene = pendingCutscene;
            ClearPending();
            CutsceneSequenceRunner runner = FindRunner(sceneName, requestedCutscene);
            if (runner == null)
            {
                GameLogger.Warning(nameof(CutsceneSequenceRunner), this,
                    $"No cutscene named '{requestedCutscene}' was found in scene '{sceneName}'.");
                onLaunchFailed?.Invoke();
                return;
            }

            runner.Play();
            onCutsceneStarted?.Invoke();
        }

        private static CutsceneSequenceRunner FindRunner(string sceneName, string runnerName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (CutsceneSequenceRunner runner in root.GetComponentsInChildren<CutsceneSequenceRunner>(true))
                {
                    if (string.Equals(runner.DisplayName, runnerName, StringComparison.OrdinalIgnoreCase))
                        return runner;
                }
            }
            return null;
        }

        private void ClearPending()
        {
            pendingScene = string.Empty;
            pendingCutscene = string.Empty;
        }
    }
}
