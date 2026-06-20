using System;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Core
{
    /// <summary>
    /// Defines the high-level states that a toolkit-based game can be in.
    /// </summary>
    /// <remarks>
    /// These states are intentionally generic so the toolkit can be reused across projects
    /// without depending on a specific scene layout, game flow, or gameplay genre.
    /// </remarks>
    public enum GameState
    {
        /// <summary>
        /// Initial startup state used while core systems are being initialized.
        /// </summary>
        Boot,

        /// <summary>
        /// Main menu or title screen state.
        /// </summary>
        Title,

        /// <summary>
        /// Normal active gameplay state.
        /// </summary>
        Playing,

        /// <summary>
        /// Paused gameplay state where gameplay input is usually blocked or reduced.
        /// </summary>
        Paused,

        /// <summary>
        /// Cinematic or scripted sequence state.
        /// </summary>
        Cutscene,

        /// <summary>
        /// Dialogue-focused state where conversation UI or story text is active.
        /// </summary>
        Dialogue,

        /// <summary>
        /// Loading state used while scenes, assets, or other game content are changing.
        /// </summary>
        Loading
    }

    /// <summary>
    /// Stores and broadcasts the current high-level game state for toolkit-based projects.
    /// </summary>
    /// <remarks>
    /// This controller is intentionally scene-agnostic. It does not load scenes, pause time,
    /// enable UI, or directly control player input. Instead, other systems can listen for
    /// state changes and decide how to react.
    ///
    /// State changes are broadcast through both a static C# event and a serialized UnityEvent:
    /// <list type="bullet">
    /// <item><description><see cref="OnStateChanged"/> is useful for code-driven systems.</description></item>
    /// <item><description><see cref="onStateChanged"/> is useful for Inspector-assigned reactions.</description></item>
    /// </list>
    /// </remarks>
    public class GameStateController : ToolkitSingleton<GameStateController>
    {
        /// <summary>
        /// UnityEvent wrapper that exposes a <see cref="GameState"/> parameter in the Inspector.
        /// </summary>
        /// <remarks>
        /// Unity cannot directly serialize generic UnityEvent declarations in the Inspector,
        /// so this named subclass makes state-change callbacks assignable from UnityEvents.
        /// </remarks>
        [Serializable]
        public class StateUnityEvent : UnityEvent<GameState> { }

        [Header("Initial State")]
        [Tooltip("State assigned when this controller awakens. Use Boot for startup flows, Title for menu-only scenes, or Playing for direct gameplay test scenes.")]
        [SerializeField] private GameState startingState = GameState.Boot;

        [Header("Events")]
        [Tooltip("Inspector-assigned callbacks invoked whenever the game state changes. The new state is passed as the event argument.")]
        [SerializeField] private StateUnityEvent onStateChanged;

        /// <summary>
        /// Gets the current high-level game state.
        /// </summary>
        public GameState CurrentState { get; private set; }

        /// <summary>
        /// Gets the state that was active immediately before <see cref="CurrentState"/>.
        /// </summary>
        /// <remarks>
        /// This is primarily used by <see cref="ReturnToPreviousState"/> and by systems that
        /// need to know what state was interrupted, such as returning from pause or dialogue.
        /// </remarks>
        public GameState PreviousState { get; private set; }

        /// <summary>
        /// Code-facing event raised whenever the game state changes.
        /// </summary>
        /// <remarks>
        /// The event argument is the new current state. This event is static so other systems
        /// can subscribe without needing a direct reference to the singleton instance.
        /// </remarks>
        public static event Action<GameState> OnStateChanged;

        /// <summary>
        /// Initializes the singleton and applies the configured starting state.
        /// </summary>
        /// <remarks>
        /// Both current and previous state are initialized to the same value so returning to
        /// the previous state is safe before any explicit state transitions have occurred.
        /// </remarks>
        protected override void Awake()
        {
            base.Awake();

            CurrentState = startingState;
            PreviousState = startingState;
        }

        /// <summary>
        /// Changes the active game state and notifies listeners.
        /// </summary>
        /// <param name="newState">The state to make current.</param>
        /// <remarks>
        /// If the requested state is already active, this method exits without raising events.
        /// On a successful transition, this method updates <see cref="PreviousState"/>, updates
        /// <see cref="CurrentState"/>, raises the static C# event, invokes the serialized
        /// UnityEvent, and forwards the change through <see cref="ToolkitEvents"/>.
        /// </remarks>
        public void SetState(GameState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            PreviousState = CurrentState;
            CurrentState = newState;

            OnStateChanged?.Invoke(CurrentState);
            onStateChanged?.Invoke(CurrentState);
            ToolkitEvents.RaiseStateChanged(CurrentState.ToString());
        }

        /// <summary>
        /// Checks whether the controller is currently in a specific state.
        /// </summary>
        /// <param name="state">The state to compare against <see cref="CurrentState"/>.</param>
        /// <returns>True if <see cref="CurrentState"/> matches <paramref name="state"/>; otherwise, false.</returns>
        public bool IsState(GameState state)
        {
            return CurrentState == state;
        }

        /// <summary>
        /// Returns to the state that was active before the current state.
        /// </summary>
        /// <remarks>
        /// This is useful for temporary states such as pause, dialogue, or cutscenes.
        /// Because <see cref="SetState"/> updates <see cref="PreviousState"/>, repeatedly calling
        /// this method can toggle between the current and previous states.
        /// </remarks>
        public void ReturnToPreviousState()
        {
            SetState(PreviousState);
        }
    }
}
