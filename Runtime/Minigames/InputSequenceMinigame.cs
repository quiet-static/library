using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Input;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace QuietStatic.Toolkit.Minigames
{
    /// <summary>
    /// Runs a minigame in which the player performs a displayed series of inputs.
    /// </summary>
    public sealed class InputSequenceMinigame : MonoBehaviour
    {
        [Header("Sequence")]
        [Tooltip("Reusable definition containing the ordered input actions.")]
        [SerializeField] private InputSequenceDefinition sequence;

        [Tooltip("How an input from this sequence that is pressed out of order is handled.")]
        [SerializeField] private WrongInputResponse wrongInputResponse = WrongInputResponse.ResetSequence;

        [Tooltip("Allow StartMinigame to restart a minigame that is already active.")]
        [SerializeField] private bool allowRestartWhileActive;

        [Tooltip("Temporarily suppress registered gameplay input while this minigame is active.")]
        [SerializeField] private bool blockGameplayInput = true;

        [Tooltip("Optional input action used to cancel an active sequence.")]
        [SerializeField] private InputActionReference cancelAction;

        [Tooltip("Optional cross-scene channel through which this runner accepts requests.")]
        [SerializeField] private InputSequenceRequestChannel requestChannel;

        [Header("Presentation")]
        [Tooltip("Optional view that displays the sequence and its current progress.")]
        [SerializeField] private InputSequenceView view;

        [Header("Events")]
        [Tooltip("Raised whenever the minigame starts.")]
        [SerializeField] private UnityEvent onStarted = new UnityEvent();

        [Tooltip("Raised after each correct input that does not complete the sequence.")]
        [SerializeField] private UnityEvent onCorrectInput = new UnityEvent();

        [Tooltip("Raised whenever the player supplies an incorrect sequence input.")]
        [SerializeField] private UnityEvent onIncorrectInput = new UnityEvent();

        [Tooltip("Raised after the full sequence is entered correctly.")]
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        [Tooltip("Raised when an incorrect input is configured to fail the minigame.")]
        [SerializeField] private UnityEvent onFailed = new UnityEvent();

        [Tooltip("Raised when an active minigame is cancelled.")]
        [SerializeField] private UnityEvent onCancelled = new UnityEvent();

        private readonly InputSequenceProgress progress = new InputSequenceProgress();
        private readonly List<InputAction> subscribedActions = new List<InputAction>();
        private readonly HashSet<InputAction> actionsEnabledByThis = new HashSet<InputAction>();
        private InputBlockHandle inputBlockHandle;
        private InputAction subscribedCancelAction;
        private InputSequenceDefinition activeSequence;
        private InputSequenceRequestChannel activeRequestChannel;
        private InputSequenceRequestChannel pendingRequestChannel;
        private CrossSceneChannelSubscription<InputSequenceRequestChannel>
            requestSubscription;

        private CrossSceneChannelSubscription<InputSequenceRequestChannel>
            RequestSubscription =>
                requestSubscription ??=
                    new CrossSceneChannelSubscription<InputSequenceRequestChannel>(
                        SubscribeToRequests,
                        UnsubscribeFromRequests);

        /// <summary>Whether this minigame is currently accepting inputs.</summary>
        public bool IsActive => progress.IsActive;

        /// <summary>Zero-based index of the input currently expected.</summary>
        public int CurrentStepIndex => progress.CurrentIndex;

        /// <summary>The definition currently being played, or null while idle.</summary>
        public InputSequenceDefinition ActiveSequence => activeSequence;

        /// <summary>Raised after a session completes, fails, or is cancelled.</summary>
        public event Action<
            InputSequenceMinigame,
            InputSequenceDefinition,
            InputSequenceOutcome> Finished;

        private void OnEnable()
        {
            RequestSubscription.Bind(requestChannel);
        }

        private void OnDisable()
        {
            RequestSubscription.Unbind();

            if (IsActive)
            {
                CancelMinigame();
            }
            else
            {
                UnsubscribeFromActions();
                view?.Hide();
            }
        }

        /// <summary>
        /// Starts the minigame. This method can be connected directly to a UnityEvent.
        /// </summary>
        public void StartMinigame()
        {
            TryStartMinigame(sequence);
        }

        /// <summary>Starts a supplied definition. Suitable for a UnityEvent.</summary>
        public void StartMinigame(InputSequenceDefinition definition)
        {
            TryStartMinigame(definition);
        }

        /// <summary>Changes the request channel and updates its live subscription.</summary>
        public void SetRequestChannel(InputSequenceRequestChannel value)
        {
            requestChannel = value;
            if (isActiveAndEnabled)
            {
                RequestSubscription.Bind(requestChannel);
            }
        }

        /// <summary>Starts the configured definition and reports whether it was accepted.</summary>
        public bool TryStartMinigame()
        {
            return TryStartMinigame(sequence);
        }

        /// <summary>Starts a supplied definition and reports whether it was accepted.</summary>
        public bool TryStartMinigame(InputSequenceDefinition definition)
        {
            if (IsActive)
            {
                if (!allowRestartWhileActive)
                {
                    return false;
                }

                EndSession(InputSequenceOutcome.Cancelled);
            }

            if (!TryPrepareSequence(definition))
            {
                return false;
            }

            activeSequence = definition;
            activeRequestChannel = pendingRequestChannel;
            AcquireGameplayBlock();
            progress.Start();
            view?.Show(activeSequence, progress.CurrentIndex);
            onStarted.Invoke();
            return true;
        }

        /// <summary>
        /// Cancels the active minigame. This method can be connected to a UnityEvent.
        /// </summary>
        public void CancelMinigame()
        {
            if (!IsActive)
            {
                return;
            }

            EndSession(InputSequenceOutcome.Cancelled);
        }

        private bool TryPrepareSequence(InputSequenceDefinition definition)
        {
            UnsubscribeFromActions();

            if (definition == null || definition.Count == 0)
            {
                GameLogger.Warning(nameof(InputSequenceMinigame), this,
                    "A non-empty sequence is required.");
                return false;
            }

            for (int i = 0; i < definition.Count; i++)
            {
                InputAction action = definition.Steps[i].Action;
                if (action == null)
                {
                    GameLogger.Warning(nameof(InputSequenceMinigame), this,
                        $"No action is assigned at step {i + 1}.");
                    UnsubscribeFromActions();
                    return false;
                }

                if (subscribedActions.Contains(action))
                {
                    continue;
                }

                SubscribeSequenceAction(action);
            }

            subscribedCancelAction = cancelAction != null ? cancelAction.action : null;
            if (subscribedCancelAction != null)
            {
                subscribedCancelAction.performed += HandleCancelPerformed;
                EnsureActionEnabled(subscribedCancelAction);
            }

            return true;
        }

        private void HandleInputPerformed(InputAction.CallbackContext context)
        {
            if (!IsActive || activeSequence == null)
            {
                return;
            }

            InputAction expectedAction =
                activeSequence.Steps[progress.CurrentIndex].Action;
            InputSequenceResult result = progress.Submit(
                context.action == expectedAction,
                activeSequence.Count,
                wrongInputResponse);

            switch (result)
            {
                case InputSequenceResult.Correct:
                    view?.Show(activeSequence, progress.CurrentIndex);
                    onCorrectInput.Invoke();
                    break;
                case InputSequenceResult.Completed:
                    EndSession(InputSequenceOutcome.Completed);
                    break;
                case InputSequenceResult.Reset:
                    view?.Show(activeSequence, progress.CurrentIndex);
                    onIncorrectInput.Invoke();
                    break;
                case InputSequenceResult.Failed:
                    onIncorrectInput.Invoke();
                    EndSession(InputSequenceOutcome.Failed);
                    break;
                case InputSequenceResult.Incorrect:
                    onIncorrectInput.Invoke();
                    break;
            }
        }

        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            CancelMinigame();
        }

        private void EndSession(InputSequenceOutcome outcome)
        {
            InputSequenceDefinition finishedSequence = activeSequence;
            InputSequenceRequestChannel finishedChannel =
                activeRequestChannel;
            progress.Stop();
            UnsubscribeFromActions();
            ReleaseGameplayBlock();
            view?.Hide();
            activeSequence = null;
            activeRequestChannel = null;

            switch (outcome)
            {
                case InputSequenceOutcome.Completed:
                    onCompleted.Invoke();
                    break;
                case InputSequenceOutcome.Failed:
                    onFailed.Invoke();
                    break;
                case InputSequenceOutcome.Cancelled:
                    onCancelled.Invoke();
                    break;
            }

            Finished?.Invoke(this, finishedSequence, outcome);
            finishedChannel?.ReportFinished(finishedSequence, outcome);
        }

        private void SubscribeToRequests(InputSequenceRequestChannel value)
        {
            value.StartRequested += HandleStartRequested;
            value.CancelRequested += CancelMinigame;
        }

        private void UnsubscribeFromRequests(InputSequenceRequestChannel value)
        {
            value.StartRequested -= HandleStartRequested;
            value.CancelRequested -= CancelMinigame;
        }

        private bool HandleStartRequested(InputSequenceDefinition definition)
        {
            pendingRequestChannel = RequestSubscription.Channel;
            try
            {
                return TryStartMinigame(definition);
            }
            finally
            {
                pendingRequestChannel = null;
            }
        }

        private void AcquireGameplayBlock()
        {
            if (!blockGameplayInput ||
                inputBlockHandle != null ||
                InputModeManager.Instance == null)
            {
                return;
            }

            inputBlockHandle = InputModeManager.Instance.AcquireInputBlock(
                InputBlockGroups.Gameplay,
                name);
        }

        private void ReleaseGameplayBlock()
        {
            inputBlockHandle?.Dispose();
            inputBlockHandle = null;
        }

        private void UnsubscribeFromActions()
        {
            foreach (InputAction action in subscribedActions)
            {
                if (action != null)
                {
                    action.performed -= HandleInputPerformed;
                }
            }

            if (subscribedCancelAction != null)
            {
                subscribedCancelAction.performed -= HandleCancelPerformed;
            }

            foreach (InputAction action in actionsEnabledByThis)
            {
                action?.Disable();
            }

            subscribedActions.Clear();
            actionsEnabledByThis.Clear();
            subscribedCancelAction = null;
        }

        private void SubscribeSequenceAction(InputAction action)
        {
            subscribedActions.Add(action);
            action.performed += HandleInputPerformed;
            EnsureActionEnabled(action);
        }

        private void EnsureActionEnabled(InputAction action)
        {
            if (!action.enabled)
            {
                action.Enable();
                actionsEnabledByThis.Add(action);
            }
        }
    }
}
