using System;
using System.Collections;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Dialogue;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Runs a small, reusable cutscene sequence made from ordered steps.
    /// </summary>
    /// <remarks>
    /// Each step can invoke UnityEvents, move a camera to a pose, optionally fade in or out,
    /// start a dialogue runner, wait for dialogue to finish, wait an additional amount of time,
    /// and then invoke completion events.
    ///
    /// This component is intentionally generic so it can be reused across games and scenes.
    /// Game-specific behavior should usually be placed in the step UnityEvents rather than
    /// hardcoded into this runner.
    /// </remarks>
    public class CutsceneSequenceRunner : MonoBehaviour
    {
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
            public string name;

            [Header("Events")]
            [Tooltip("Events invoked at the beginning of this step, before the camera is moved or dialogue starts.")]
            public UnityEvent onStepStarted;

            [Tooltip("Events invoked after this step's dialogue and wait time have completed.")]
            public UnityEvent onStepFinished;

            [Header("Camera")]
            [Tooltip("The camera or camera rig transform that should be moved for this step. Leave empty if this step does not move a camera.")]
            public Transform cameraTransform;

            [Tooltip("The pose transform whose position and rotation should be copied onto the camera transform.")]
            public Transform cameraPose;

            [Header("Dialogue")]
            [Tooltip("Optional dialogue runner to start during this step. The sequence waits until this runner is finished before continuing.")]
            public DialogueRunner dialogueRunner;

            [Header("Timing")]
            [Tooltip("Extra time, in seconds, to wait after dialogue completes and before the step-finished events run.")]
            [Min(0f)]
            public float waitAfterStep;
        }

        [Header("Steps")]
        [Tooltip("Ordered list of cutscene steps. Steps run from first to last.")]
        [SerializeField] private Step[] steps;

        [Header("Startup")]
        [Tooltip("If true, this sequence begins automatically when Start is called.")]
        [SerializeField] private bool playOnStart;

        [Header("Fading")]
        [Tooltip("Optional screen fader used for fade-out and fade-in transitions between steps.")]
        [SerializeField] private ScreenFader fader;

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

        /// <summary>
        /// Gets whether this sequence is currently playing.
        /// </summary>
        /// <remarks>
        /// This is useful for other systems that need to avoid starting duplicate cutscenes,
        /// disabling player input while a sequence is active, or waiting until a sequence ends.
        /// </remarks>
        public bool IsRunning { get; private set; }

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
                StartCoroutine(PlayRoutine());
            }
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
            ToolkitEvents.RaiseCutsceneStarted();

            foreach (Step step in steps)
            {
                if (step == null)
                {
                    continue;
                }

                yield return PlayStepRoutine(step);
            }

            onSequenceFinished?.Invoke();
            ToolkitEvents.RaiseCutsceneEnded();
            IsRunning = false;
        }

        /// <summary>
        /// Runs one configured cutscene step.
        /// </summary>
        /// <param name="step">The step to execute.</param>
        /// <returns>An enumerator used by Unity's coroutine system.</returns>
        private IEnumerator PlayStepRoutine(Step step)
        {
            if (fader != null && fadeOutBeforeStep > 0f)
            {
                yield return fader.FadeToBlackRoutine(fadeOutBeforeStep);
            }

            step.onStepStarted?.Invoke();
            ApplyCameraPose(step);

            if (fader != null && fadeInAfterCameraMove > 0f)
            {
                yield return fader.FadeToClearRoutine(fadeInAfterCameraMove);
            }

            if (step.dialogueRunner != null)
            {
                step.dialogueRunner.StartDialogue();
                yield return new WaitUntil(() => !step.dialogueRunner.IsRunning);
            }

            if (step.waitAfterStep > 0f)
            {
                yield return new WaitForSeconds(step.waitAfterStep);
            }

            step.onStepFinished?.Invoke();
        }

        /// <summary>
        /// Moves the configured camera transform to the configured camera pose for a step.
        /// </summary>
        /// <param name="step">The step containing the camera transform and pose.</param>
        /// <remarks>
        /// If either camera reference is missing, this method safely does nothing so the same
        /// step type can be used for non-camera beats.
        /// </remarks>
        private static void ApplyCameraPose(Step step)
        {
            if (step.cameraTransform == null || step.cameraPose == null)
            {
                return;
            }

            step.cameraTransform.SetPositionAndRotation(
                step.cameraPose.position,
                step.cameraPose.rotation
            );
        }
    }
}
