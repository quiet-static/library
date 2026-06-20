using System;
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
        [SerializeField] private string startingState = "Starting";

        [Header("Unity Events")]
        [Tooltip("Invoked whenever the current game state changes.")]
        [SerializeField] private StringUnityEvent onGameStateChanged;

        /// <summary>
        /// Raised whenever the global state changes.
        /// The first parameter is the previous state and the second is the new state.
        /// </summary>
        public static event Action<string, string> OnGameStateChanged;

        /// <summary>
        /// Gets the current high-level game state.
        /// </summary>
        public string CurrentState { get; private set; }

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
        }

        /// <summary>
        /// Changes the current game state.
        /// </summary>
        /// <param name="newState">New non-empty state identifier.</param>
        /// <returns>True when the state actually changed.</returns>
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

            if (CurrentState == newState)
            {
                return false;
            }

            string previousState = CurrentState;
            CurrentState = newState;

            OnGameStateChanged?.Invoke(previousState, CurrentState);
            onGameStateChanged?.Invoke(CurrentState);

            return true;
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