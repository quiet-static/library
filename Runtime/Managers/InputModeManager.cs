using System.Collections.Generic;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.State;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Enables or disables registered input-related components based on the current game state.
    /// </summary>
    /// <remarks>
    /// Input behaviours register themselves at runtime, which makes this manager safe
    /// to use with additive scenes. For example, a player input reader can register
    /// when the Player scene loads and unregister when that scene unloads.
    ///
    /// This manager does not read input itself. It only controls whether registered
    /// input readers or other input-handling behaviours are enabled.
    /// </remarks>
    public class InputModeManager : ToolkitSingleton<InputModeManager>
    {
        [Header("State Rules")]
        [Tooltip("States that should enable registered gameplay input behaviours.")]
        [SerializeField] private string[] gameplayStates = { "Playing" };

        [Tooltip("States that should enable registered UI input behaviours.")]
        [SerializeField]
        private string[] uiStates =
        {
            "Paused",
            "Dialogue",
            "Title",
            "GameOver"
        };

        [Tooltip("States that should enable registered cutscene input behaviours.")]
        [SerializeField] private string[] cutsceneStates = { "Cutscene" };

        [Header("Startup")]
        [Tooltip("If true, refreshes input groups using the current game state when this manager enables.")]
        [SerializeField] private bool refreshOnEnable = true;

        /// <summary>
        /// Input behaviours enabled during gameplay states.
        /// </summary>
        private readonly List<Behaviour> gameplayInputs = new();

        /// <summary>
        /// Input behaviours enabled during UI-focused states.
        /// </summary>
        private readonly List<Behaviour> uiInputs = new();

        /// <summary>
        /// Input behaviours enabled during cutscene states.
        /// </summary>
        private readonly List<Behaviour> cutsceneInputs = new();

        /// <summary>
        /// Gets the currently active input mode.
        /// </summary>
        public string CurrentMode { get; private set; } = "None";

        /// <summary>
        /// Initializes the singleton and ensures no input group is active by default.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            DisableAllInput();
        }

        /// <summary>
        /// Subscribes to global state changes and optionally applies the current state.
        /// </summary>
        private void OnEnable()
        {
            GameStateManager.OnGameStateChanged += HandleGameStateChanged;

            if (refreshOnEnable)
            {
                RefreshForCurrentState();
            }
        }

        /// <summary>
        /// Unsubscribes from global state changes.
        /// </summary>
        private void OnDisable()
        {
            GameStateManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Registers an input behaviour that should be active during gameplay.
        /// </summary>
        /// <param name="inputBehaviour">Behaviour to register.</param>
        public void RegisterGameplayInput(Behaviour inputBehaviour)
        {
            RegisterInput(inputBehaviour, gameplayInputs);
            RefreshForCurrentState();
        }

        /// <summary>
        /// Registers an input behaviour that should be active during UI states.
        /// </summary>
        /// <param name="inputBehaviour">Behaviour to register.</param>
        public void RegisterUIInput(Behaviour inputBehaviour)
        {
            RegisterInput(inputBehaviour, uiInputs);
            RefreshForCurrentState();
        }

        /// <summary>
        /// Registers an input behaviour that should be active during cutscene states.
        /// </summary>
        /// <param name="inputBehaviour">Behaviour to register.</param>
        public void RegisterCutsceneInput(Behaviour inputBehaviour)
        {
            RegisterInput(inputBehaviour, cutsceneInputs);
            RefreshForCurrentState();
        }

        /// <summary>
        /// Removes an input behaviour from every registered input group.
        /// </summary>
        /// <param name="inputBehaviour">Behaviour to unregister.</param>
        public void UnregisterInput(Behaviour inputBehaviour)
        {
            if (inputBehaviour == null)
            {
                return;
            }

            gameplayInputs.Remove(inputBehaviour);
            uiInputs.Remove(inputBehaviour);
            cutsceneInputs.Remove(inputBehaviour);
        }

        /// <summary>
        /// Refreshes which input group is active using the current global game state.
        /// </summary>
        public void RefreshForCurrentState()
        {
            if (GameStateManager.Instance == null)
            {
                DisableAllInput();
                return;
            }

            ApplyState(GameStateManager.Instance.CurrentState);
        }

        /// <summary>
        /// Enables gameplay input and disables UI and cutscene input.
        /// </summary>
        public void EnableGameplayInput()
        {
            SetInputGroupEnabled(gameplayInputs, true);
            SetInputGroupEnabled(uiInputs, false);
            SetInputGroupEnabled(cutsceneInputs, false);

            CurrentMode = "Gameplay";
        }

        /// <summary>
        /// Enables UI input and disables gameplay and cutscene input.
        /// </summary>
        public void EnableUIInput()
        {
            SetInputGroupEnabled(gameplayInputs, false);
            SetInputGroupEnabled(uiInputs, true);
            SetInputGroupEnabled(cutsceneInputs, false);

            CurrentMode = "UI";
        }

        /// <summary>
        /// Enables cutscene input and disables gameplay and UI input.
        /// </summary>
        public void EnableCutsceneInput()
        {
            SetInputGroupEnabled(gameplayInputs, false);
            SetInputGroupEnabled(uiInputs, false);
            SetInputGroupEnabled(cutsceneInputs, true);

            CurrentMode = "Cutscene";
        }

        /// <summary>
        /// Disables every registered input behaviour.
        /// </summary>
        public void DisableAllInput()
        {
            SetInputGroupEnabled(gameplayInputs, false);
            SetInputGroupEnabled(uiInputs, false);
            SetInputGroupEnabled(cutsceneInputs, false);

            CurrentMode = "None";
        }

        /// <summary>
        /// Handles a global game-state change.
        /// </summary>
        private void HandleGameStateChanged(
            string previousState,
            string newState
        )
        {
            ApplyState(newState);
        }

        /// <summary>
        /// Selects an input mode based on configured game-state lists.
        /// </summary>
        private void ApplyState(string state)
        {
            if (IsStateInList(state, gameplayStates))
            {
                EnableGameplayInput();
                return;
            }

            if (IsStateInList(state, uiStates))
            {
                EnableUIInput();
                return;
            }

            if (IsStateInList(state, cutsceneStates))
            {
                EnableCutsceneInput();
                return;
            }

            DisableAllInput();
        }

        /// <summary>
        /// Adds a behaviour to one runtime input group if it is valid and not already registered.
        /// </summary>
        private static void RegisterInput(
            Behaviour inputBehaviour,
            List<Behaviour> targetList
        )
        {
            if (inputBehaviour == null || targetList.Contains(inputBehaviour))
            {
                return;
            }

            targetList.Add(inputBehaviour);
        }

        /// <summary>
        /// Enables or disables all valid behaviours in an input group.
        /// Removes destroyed scene objects from the list automatically.
        /// </summary>
        private static void SetInputGroupEnabled(
            List<Behaviour> behaviours,
            bool shouldEnable
        )
        {
            for (int i = behaviours.Count - 1; i >= 0; i--)
            {
                Behaviour behaviour = behaviours[i];

                if (behaviour == null)
                {
                    behaviours.RemoveAt(i);
                    continue;
                }

                behaviour.enabled = shouldEnable;
            }
        }

        /// <summary>
        /// Checks whether a state appears in a configured list of state identifiers.
        /// </summary>
        private static bool IsStateInList(
            string state,
            string[] states
        )
        {
            if (string.IsNullOrWhiteSpace(state) || states == null)
            {
                return false;
            }

            foreach (string configuredState in states)
            {
                if (state == configuredState?.Trim())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
