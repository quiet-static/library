using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.State
{
    /// <summary>
    /// Stores the current high-level mode of the game.
    /// </summary>
    /// <remarks>
    /// This manager does not decide what each mode means. Other systems can react
    /// to mode changes to enable input, pause gameplay, show UI, begin dialogue,
    /// or control scene transitions.
    /// </remarks>
    public class GameStateManager : ToolkitSingleton<GameStateManager>
    {
        [Serializable]
        public class StringUnityEvent : UnityEvent<string>
        {
        }

        [Header("Startup State")]
        [Tooltip("State assigned when this manager initializes.")]
        [GameStateId]
        [SerializeField] private string startingState = "Starting";

        [Header("Unity Events")]
        [Tooltip("Invoked whenever the current game state changes.")]
        [SerializeField] private StringUnityEvent onGameStateChanged;

        private readonly Queue<string> pendingStates = new Queue<string>();
        private bool isPublishingStateChange;
        private string lastScheduledState;

        /// <summary>
        /// Raised whenever the global state changes.
        /// The first parameter is the previous state and the second is the new state.
        /// </summary>
        public static event Action<string, string> OnGameStateChanged;

        /// <summary>
        /// Gets the current high-level game state.
        /// </summary>
        public string CurrentState { get; private set; } = "Starting";

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            CurrentState = string.IsNullOrWhiteSpace(startingState)
                ? "Starting"
                : startingState.Trim();
            lastScheduledState = CurrentState;
        }

        /// <summary>
        /// Changes the current game state.
        /// </summary>
        /// <param name="newState">New non-empty state identifier.</param>
        /// <returns>True when the state actually changed.</returns>
        /// <remarks>
        /// State requests made by a change listener are queued until the current
        /// notification finishes, preserving a consistent event order.
        /// </remarks>
        public bool SetState(string newState)
        {
            if (string.IsNullOrWhiteSpace(newState))
            {
                GameLogger.Warning(
                    "SetState",
                    this,
                    $"{nameof(GameStateManager)} cannot switch to an empty state."
                );
                return false;
            }

            newState = newState.Trim();

            string effectiveState = isPublishingStateChange
                ? lastScheduledState
                : CurrentState;

            if (effectiveState == newState)
            {
                return false;
            }

            if (isPublishingStateChange)
            {
                pendingStates.Enqueue(newState);
                lastScheduledState = newState;
                return true;
            }

            isPublishingStateChange = true;
            lastScheduledState = newState;

            try
            {
                PublishStateChange(newState);

                while (pendingStates.Count > 0)
                {
                    PublishStateChange(pendingStates.Dequeue());
                }
            }
            finally
            {
                pendingStates.Clear();
                lastScheduledState = CurrentState;
                isPublishingStateChange = false;
            }

            return true;
        }

        private void PublishStateChange(string newState)
        {
            string previousState = CurrentState;
            CurrentState = newState;

            OnGameStateChanged?.Invoke(previousState, newState);
            onGameStateChanged?.Invoke(newState);
            ToolkitEvents.RaiseStateChanged(newState);
        }

        /// <summary>
        /// Checks whether the manager is currently in a requested state.
        /// </summary>
        public bool IsInState(string state)
        {
            return !string.IsNullOrWhiteSpace(state) &&
                   CurrentState == state.Trim();
        }

        /// <summary>
        /// Changes state only if the current state matches an expected one.
        /// </summary>
        public bool TrySetState(string expectedCurrentState, string newState)
        {
            if (!IsInState(expectedCurrentState))
            {
                return false;
            }

            return SetState(newState);
        }
    }
}
