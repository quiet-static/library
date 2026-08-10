using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Minigames
{
    /// <summary>
    /// Starts one configured sequence from UnityEvents and routes its result back to
    /// the scene object that made the request.
    /// </summary>
    public sealed class InputSequenceMinigameActivator : MonoBehaviour
    {
        [Header("Request")]
        [Tooltip("Sequence requested by Activate. Required.")]
        [SerializeField] private InputSequenceDefinition sequence;

        [Tooltip("Cross-scene channel used to reach a persistent runner.")]
        [SerializeField] private InputSequenceRequestChannel requestChannel;

        [Tooltip("Optional local runner. When assigned, it is used instead of the request channel.")]
        [SerializeField] private InputSequenceMinigame localRunner;

        [Tooltip("Cancel this request if the activator is disabled before it finishes.")]
        [SerializeField] private bool cancelOnDisable = true;

        [Header("Events")]
        [Tooltip("Raised after a runner accepts the request.")]
        [SerializeField] private UnityEvent onStarted = new UnityEvent();

        [Tooltip("Raised when this activator's requested sequence completes.")]
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        [Tooltip("Raised when this activator's requested sequence fails.")]
        [SerializeField] private UnityEvent onFailed = new UnityEvent();

        [Tooltip("Raised when this activator's requested sequence is cancelled.")]
        [SerializeField] private UnityEvent onCancelled = new UnityEvent();

        [Tooltip("Raised when no runner accepts the request or the setup is invalid.")]
        [SerializeField] private UnityEvent onRejected = new UnityEvent();

        private bool awaitingResult;
        private InputSequenceMinigame subscribedLocalRunner;
        private InputSequenceRequestChannel subscribedRequestChannel;

        /// <summary>Whether this component has an accepted request awaiting a result.</summary>
        public bool IsAwaitingResult => awaitingResult;

        private void OnEnable()
        {
            SubscribeToResults();
        }

        private void OnDisable()
        {
            UnsubscribeFromResults();

            if (!awaitingResult)
            {
                return;
            }

            if (cancelOnDisable)
            {
                Cancel();
            }
            else
            {
                awaitingResult = false;
            }
        }

        /// <summary>Requests the configured sequence. Suitable for a UnityEvent.</summary>
        public void Activate()
        {
            TryActivate();
        }

        /// <summary>Requests the configured sequence and reports whether it was accepted.</summary>
        public bool TryActivate()
        {
            if (awaitingResult || sequence == null)
            {
                onRejected.Invoke();
                return false;
            }

            awaitingResult = true;
            bool accepted = localRunner != null
                ? localRunner.TryStartMinigame(sequence)
                : requestChannel != null && requestChannel.TryStart(sequence);

            if (!accepted)
            {
                awaitingResult = false;
                onRejected.Invoke();
                return false;
            }

            if (awaitingResult)
            {
                onStarted.Invoke();
            }

            return true;
        }

        /// <summary>
        /// Changes the cross-scene request channel and updates result subscriptions.
        /// The local runner still takes precedence while assigned.
        /// </summary>
        public void SetRequestChannel(InputSequenceRequestChannel value)
        {
            UnsubscribeFromResults();
            requestChannel = value;
            if (isActiveAndEnabled)
            {
                SubscribeToResults();
            }
        }

        /// <summary>
        /// Changes the optional local runner and updates result subscriptions.
        /// </summary>
        public void SetLocalRunner(InputSequenceMinigame value)
        {
            UnsubscribeFromResults();
            localRunner = value;
            if (isActiveAndEnabled)
            {
                SubscribeToResults();
            }
        }

        /// <summary>Cancels this activator's accepted request.</summary>
        public void Cancel()
        {
            if (!awaitingResult)
            {
                return;
            }

            if (localRunner != null)
            {
                localRunner.CancelMinigame();
            }
            else if (requestChannel != null)
            {
                requestChannel.CancelMinigame();
            }

            // A disabled activator has already unsubscribed and cannot receive the
            // cancellation result.
            if (!isActiveAndEnabled)
            {
                awaitingResult = false;
            }
        }

        private void SubscribeToResults()
        {
            if (localRunner != null)
            {
                localRunner.Finished += HandleLocalFinished;
                subscribedLocalRunner = localRunner;
            }
            else if (requestChannel != null)
            {
                requestChannel.Finished += HandleChannelFinished;
                subscribedRequestChannel = requestChannel;
            }
        }

        private void UnsubscribeFromResults()
        {
            if (subscribedLocalRunner != null)
            {
                subscribedLocalRunner.Finished -= HandleLocalFinished;
            }

            if (subscribedRequestChannel != null)
            {
                subscribedRequestChannel.Finished -= HandleChannelFinished;
            }

            subscribedLocalRunner = null;
            subscribedRequestChannel = null;
        }

        private void HandleLocalFinished(
            InputSequenceMinigame runner,
            InputSequenceDefinition finishedSequence,
            InputSequenceOutcome outcome)
        {
            HandleFinished(finishedSequence, outcome);
        }

        private void HandleChannelFinished(
            InputSequenceDefinition finishedSequence,
            InputSequenceOutcome outcome)
        {
            HandleFinished(finishedSequence, outcome);
        }

        private void HandleFinished(
            InputSequenceDefinition finishedSequence,
            InputSequenceOutcome outcome)
        {
            if (!awaitingResult || finishedSequence != sequence)
            {
                return;
            }

            awaitingResult = false;
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
        }
    }
}
