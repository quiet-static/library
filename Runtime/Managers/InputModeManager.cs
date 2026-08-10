using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Input;
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
        private sealed class InputBlockClaim
        {
            public InputBlockClaim(InputBlockGroups groups, string ownerName)
            {
                Groups = groups;
                OwnerName = string.IsNullOrWhiteSpace(ownerName)
                    ? "Unnamed"
                    : ownerName.Trim();
            }

            /// <summary>Gets the input groups blocked by this claim.</summary>
            public InputBlockGroups Groups { get; }

            /// <summary>Gets the diagnostic name of the claim owner.</summary>
            public string OwnerName { get; }
        }

        [Header("State Rules")]
        [Tooltip("States that should enable registered gameplay input behaviours.")]
        [GameStateId]
        [SerializeField] private string[] gameplayStates = { "Playing" };

        [Tooltip("States that should enable registered UI input behaviours.")]
        [GameStateId]
        [SerializeField]
        private string[] uiStates =
        {
            "Paused",
            "Dialogue",
            "Title",
            "GameOver"
        };

        [Tooltip("States that should enable registered cutscene input behaviours.")]
        [GameStateId]
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

        private readonly Dictionary<int, InputBlockClaim> inputBlocks = new();
        private int nextBlockToken = 1;
        private string desiredMode = "None";

        /// <summary>
        /// Gets the currently active input mode.
        /// </summary>
        public string CurrentMode { get; private set; } = "None";

        /// <summary>Combined input groups currently suppressed by temporary owners.</summary>
        public InputBlockGroups BlockedGroups { get; private set; }

        /// <summary>Number of independently owned blocks currently active.</summary>
        public int ActiveBlockCount => inputBlocks.Count;

        /// <summary>Raised whenever the combined temporary block mask changes.</summary>
        public static event Action<InputBlockGroups> OnInputBlocksChanged;

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
            desiredMode = "Gameplay";
            ApplyDesiredMode();
        }

        /// <summary>
        /// Enables UI input and disables gameplay and cutscene input.
        /// </summary>
        public void EnableUIInput()
        {
            desiredMode = "UI";
            ApplyDesiredMode();
        }

        /// <summary>
        /// Enables cutscene input and disables gameplay and UI input.
        /// </summary>
        public void EnableCutsceneInput()
        {
            desiredMode = "Cutscene";
            ApplyDesiredMode();
        }

        /// <summary>
        /// Disables every registered input behaviour.
        /// </summary>
        public void DisableAllInput()
        {
            desiredMode = "None";
            ApplyDesiredMode();
        }

        /// <summary>
        /// Acquires a temporary block for one or more input groups.
        /// </summary>
        /// <param name="groups">Groups suppressed until the returned handle is disposed.</param>
        /// <param name="ownerName">Optional diagnostic name for the claimant.</param>
        public InputBlockHandle AcquireInputBlock(
            InputBlockGroups groups,
            string ownerName = null)
        {
            groups &= InputBlockGroups.All;
            if (groups == InputBlockGroups.None)
            {
                return new InputBlockHandle(null, 0);
            }

            int token = nextBlockToken++;
            inputBlocks[token] = new InputBlockClaim(groups, ownerName);
            RefreshBlockedGroups();
            return new InputBlockHandle(this, token);
        }

        /// <summary>Returns whether any temporary owner blocks the supplied groups.</summary>
        public bool IsInputBlocked(InputBlockGroups groups)
        {
            return (BlockedGroups & groups) != 0;
        }

        /// <summary>Returns diagnostic names for owners blocking the supplied groups.</summary>
        public IReadOnlyList<string> GetInputBlockOwners(InputBlockGroups groups)
        {
            var owners = new List<string>();
            foreach (InputBlockClaim claim in inputBlocks.Values)
            {
                if ((claim.Groups & groups) != 0)
                {
                    owners.Add(claim.OwnerName);
                }
            }

            return owners;
        }

        internal void ReleaseInputBlock(int token)
        {
            if (!inputBlocks.Remove(token))
            {
                return;
            }

            RefreshBlockedGroups();
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

        private void RefreshBlockedGroups()
        {
            InputBlockGroups combined = InputBlockGroups.None;
            foreach (InputBlockClaim claim in inputBlocks.Values)
            {
                combined |= claim.Groups;
            }

            bool changed = combined != BlockedGroups;
            BlockedGroups = combined;
            ApplyDesiredMode();

            if (changed)
            {
                OnInputBlocksChanged?.Invoke(BlockedGroups);
            }
        }

        private void ApplyDesiredMode()
        {
            bool gameplayEnabled =
                desiredMode == "Gameplay" &&
                !IsInputBlocked(InputBlockGroups.Gameplay);
            bool uiEnabled =
                desiredMode == "UI" &&
                !IsInputBlocked(InputBlockGroups.UI);
            bool cutsceneEnabled =
                desiredMode == "Cutscene" &&
                !IsInputBlocked(InputBlockGroups.Cutscene);

            SetInputGroupEnabled(gameplayInputs, gameplayEnabled);
            SetInputGroupEnabled(uiInputs, uiEnabled);
            SetInputGroupEnabled(cutsceneInputs, cutsceneEnabled);
            CurrentMode = desiredMode;
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
