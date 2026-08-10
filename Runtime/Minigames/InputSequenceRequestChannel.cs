using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Minigames
{
    /// <summary>
    /// Relays minigame requests between scene content and a persistent sequence runner.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InputSequenceRequestChannel",
        menuName = "Quiet Static Toolkit/Minigames/Input Sequence Request Channel")]
    public sealed class InputSequenceRequestChannel : CrossSceneCommandChannel
    {
        /// <summary>
        /// Raised until one enabled runner accepts the supplied definition.
        /// </summary>
        public event Func<InputSequenceDefinition, bool> StartRequested;

        /// <summary>Raised when callers request cancellation of the active session.</summary>
        public event Action CancelRequested;

        /// <summary>Raised after a runner reaches a terminal outcome.</summary>
        public event Action<InputSequenceDefinition, InputSequenceOutcome> Finished;

        /// <summary>
        /// Requests a sequence and returns whether an enabled runner accepted it.
        /// </summary>
        public bool TryStart(InputSequenceDefinition definition)
        {
            if (definition == null)
            {
                GameLogger.Warning(nameof(InputSequenceRequestChannel), this,
                    "Cannot start a null sequence.");
                return false;
            }

            Delegate[] listeners = StartRequested?.GetInvocationList();
            if (listeners == null)
            {
                GameLogger.Warning(nameof(InputSequenceRequestChannel), this,
                    "No enabled runner is listening.");
                return false;
            }

            foreach (Delegate listener in listeners)
            {
                if (((Func<InputSequenceDefinition, bool>)listener).Invoke(definition))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Requests a sequence from a UnityEvent. Use <see cref="TryStart"/> when the
        /// caller needs to know whether a runner accepted the request.
        /// </summary>
        public void StartMinigame(InputSequenceDefinition definition)
        {
            TryStart(definition);
        }

        /// <summary>Cancels the active session on runners listening to this channel.</summary>
        public void CancelMinigame()
        {
            CancelRequested?.Invoke();
        }

        internal void ReportFinished(
            InputSequenceDefinition definition,
            InputSequenceOutcome outcome)
        {
            Finished?.Invoke(definition, outcome);
        }
    }
}
