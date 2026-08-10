using System;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Defines a continuous interaction completed by holding the normal interact action.
    /// This is deliberately separate from the one-shot <see cref="Interactable"/>.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Hold Interactable")]
    public class HoldInteractable : MonoBehaviour
    {
        [Serializable] public sealed class FloatUnityEvent : UnityEvent<float> { }

        /// <summary>Raised whenever normalized progress changes.</summary>
        public event Action<float> ProgressChanged;

        /// <summary>Raised the first time an enabled hold advances from zero.</summary>
        public event Action Started;

        /// <summary>Raised once when the meter reaches one.</summary>
        public event Action Completed;

        /// <summary>Raised whenever a new held-input interval begins.</summary>
        public event Action HoldBegan;

        /// <summary>Raised whenever held input ends, is cancelled, or completes.</summary>
        public event Action HoldEnded;

        [Header("Display")]
        [Tooltip("Prompt shown while the player looks at this object.")]
        [SerializeField] private string hoverPrompt = "Hold E to interact";
        [Tooltip("Name displayed beside the progress bar.")]
        [SerializeField] private string progressName = "Progress";

        [Header("Timing")]
        [Tooltip("Seconds the interaction action must remain held to complete.")]
        [Min(0.01f)]
        [SerializeField] private float holdDuration = 3f;
        [Tooltip("Keep partial progress when the button is released or the player looks away.")]
        [SerializeField] private bool preserveProgress;

        [Tooltip("Whether this hold interaction is available when the scene starts.")]
        [SerializeField] private bool startEnabled = true;

        [Header("Requirements")]
        [Tooltip("Optional flags required before progress can begin.")]
        [SerializeField] private FlagRequirement requirement;

        [Header("Completion")]
        [Tooltip("Prevent further use after completion.")]
        [SerializeField] private bool disableAfterCompletion = true;
        [Tooltip("Flags set when the meter completes.")]
        [FlagId]
        [SerializeField] private string[] flagsToSetOnCompletion;
        [Tooltip("Invoked every frame progress changes, with a value from zero to one.")]
        [SerializeField] private FloatUnityEvent onProgressChanged;
        [Tooltip("Invoked once when the meter reaches one. Use this to advance a game clock or start other behavior.")]
        [SerializeField] private UnityEvent onCompleted;
        [Tooltip("Invoked when an attempted hold does not meet its requirements.")]
        [SerializeField] private UnityEvent onRequirementFailed;

        [Header("Held Input Events")]
        [Tooltip("Invoked once whenever held interaction input begins.")]
        [SerializeField] private UnityEvent onHoldBegan;

        [Tooltip("Invoked once when held input is released, cancelled, disabled, or completed.")]
        [SerializeField] private UnityEvent onHoldEnded;

        /// <summary>Gets the prompt displayed while this interaction is targeted.</summary>
        public string HoverPrompt => hoverPrompt;

        /// <summary>Gets the label displayed beside the progress bar.</summary>
        public string ProgressName => progressName;

        /// <summary>Gets normalized hold progress.</summary>
        public float Progress { get; private set; }

        /// <summary>Gets whether this interaction is enabled at runtime.</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// Gets whether an <see cref="Interactor"/> may acquire this hold through
        /// its crosshair raycast. Sequence components can temporarily disable this
        /// while they own input directly.
        /// </summary>
        public bool IsInteractorTargetingEnabled { get; private set; } = true;

        /// <summary>Gets whether this interaction is currently receiving held input.</summary>
        public bool IsBeingHeld { get; private set; }

        /// <summary>Gets whether hold progress has reached completion.</summary>
        public bool IsComplete => Progress >= 1f;

        /// <summary>Checks the runtime enabled state and configured flag requirement.</summary>
        /// <returns>True when hold progress may advance.</returns>
        public bool CanInteract() =>
            IsEnabled && (requirement == null || requirement.IsMet());

        private void Awake()
        {
            IsEnabled = startEnabled;
        }

        /// <summary>Advances the meter by elapsed seconds.</summary>
        public bool Advance(float deltaTime)
        {
            if (!CanInteract())
            {
                onRequirementFailed?.Invoke();
                return false;
            }

            if (!IsBeingHeld)
            {
                IsBeingHeld = true;
                HoldBegan?.Invoke();
                onHoldBegan?.Invoke();
            }

            if (Progress <= 0f && deltaTime > 0f)
            {
                Started?.Invoke();
            }

            SetProgress(Progress + Mathf.Max(0f, deltaTime) / holdDuration);
            return true;
        }

        /// <summary>Cancels an active hold, optionally retaining its progress.</summary>
        public void Cancel()
        {
            EndHold();

            if (!preserveProgress && !IsComplete)
            {
                SetProgress(0f);
            }
        }

        /// <summary>Sets normalized progress, useful for save restoration and tests.</summary>
        public void SetProgress(float normalizedProgress)
        {
            float previous = Progress;
            Progress = Mathf.Clamp01(normalizedProgress);
            if (Mathf.Approximately(previous, Progress))
            {
                return;
            }

            ProgressChanged?.Invoke(Progress);
            onProgressChanged?.Invoke(Progress);

            if (previous < 1f && IsComplete)
            {
                Complete();
            }
        }

        /// <summary>Changes whether hold progress can advance.</summary>
        /// <param name="isEnabled">New runtime enabled state.</param>
        public void SetEnabled(bool isEnabled)
        {
            if (!isEnabled)
            {
                Cancel();
            }

            IsEnabled = isEnabled;
        }

        /// <summary>Changes whether crosshair-based interactors may target this hold.</summary>
        public void SetInteractorTargetingEnabled(bool isEnabled) =>
            IsInteractorTargetingEnabled = isEnabled;

        /// <summary>Returns hold progress to zero.</summary>
        public void ResetProgress()
        {
            EndHold();
            SetProgress(0f);
        }

        private void Complete()
        {
            EndHold();

            if (FlagManager.Instance != null && flagsToSetOnCompletion != null)
            {
                foreach (string flagId in flagsToSetOnCompletion)
                {
                    if (!string.IsNullOrWhiteSpace(flagId))
                    {
                        FlagManager.Instance.SetFlag(flagId);
                    }
                }
            }

            Completed?.Invoke();
            onCompleted?.Invoke();
            if (disableAfterCompletion)
            {
                IsEnabled = false;
            }
        }

        private void EndHold()
        {
            if (!IsBeingHeld)
            {
                return;
            }

            IsBeingHeld = false;
            HoldEnded?.Invoke();
            onHoldEnded?.Invoke();
        }
    }
}
