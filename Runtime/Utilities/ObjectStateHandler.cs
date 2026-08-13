using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Utilities
{
    /// <summary>
    /// Activates the scene objects mapped to one state and deactivates objects from every
    /// other configured state.
    /// </summary>
    /// <remarks>
    /// Pass an <see cref="ObjectStateDefinition"/> to <see cref="ActivateState"/> from a
    /// UnityEvent to select a state through the Inspector asset picker. A state may contain
    /// any number of objects, including zero objects for an intentionally empty state.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Utilities/Object State Handler")]
    public sealed class ObjectStateHandler : MonoBehaviour
    {
        /// <summary>Maps a reusable state definition to its scene visuals.</summary>
        [Serializable]
        public sealed class StateBinding
        {
            [Tooltip("State represented by the objects in this binding.")]
            [SerializeField] private ObjectStateDefinition state;

            [Tooltip("Objects active while this state is selected.")]
            [SerializeField] private GameObject[] objects;

            /// <summary>Gets the definition represented by this binding.</summary>
            public ObjectStateDefinition State => state;

            /// <summary>Gets the objects active for this binding.</summary>
            public IReadOnlyList<GameObject> Objects => objects;

            /// <summary>Creates a state-to-objects mapping.</summary>
            public StateBinding(ObjectStateDefinition state, params GameObject[] objects)
            {
                this.state = state;
                this.objects = objects;
            }
        }

        /// <summary>
        /// UnityEvent whose argument is the newly selected state, or null when cleared.
        /// </summary>
        [Serializable]
        public sealed class StateChangedUnityEvent : UnityEvent<ObjectStateDefinition> { }

        [Header("States")]
        [Tooltip("State definitions and the scene objects that represent each one.")]
        [SerializeField] private StateBinding[] states;

        [Tooltip("State selected when this component awakens. Leave empty to hide all state objects.")]
        [SerializeField] private ObjectStateDefinition startingState;

        [Header("Cross-Scene Requests")]
        [Tooltip("Optional channel that allows assets and objects in other scenes to select this handler's state.")]
        [SerializeField] private ObjectStateChannel channel;

        [Header("Events")]
        [Tooltip("Inspector callbacks invoked after the selected state changes.")]
        [SerializeField] private StateChangedUnityEvent onStateChanged;

        /// <summary>Gets the currently selected state, or null when all states are cleared.</summary>
        public ObjectStateDefinition CurrentState { get; private set; }

        private CrossSceneChannelSubscription<ObjectStateChannel>
            channelSubscription;

        private CrossSceneChannelSubscription<ObjectStateChannel>
            ChannelSubscription =>
                channelSubscription ??=
                    new CrossSceneChannelSubscription<ObjectStateChannel>(
                        SubscribeToChannel,
                        UnsubscribeFromChannel);

        private void Awake()
        {
            if (startingState == null)
            {
                ClearState(false);
                return;
            }

            ActivateState(startingState, false);
        }

        private void OnEnable()
        {
            ChannelSubscription.Bind(channel);
        }

        private void OnDisable()
        {
            ChannelSubscription.Unbind();
        }

        /// <summary>
        /// Changes the cross-scene request channel and updates subscriptions immediately.
        /// </summary>
        /// <param name="value">New channel, or null to stop receiving channel requests.</param>
        public void SetChannel(ObjectStateChannel value)
        {
            if (channel == value && ChannelSubscription.Channel == value)
            {
                return;
            }

            channel = value;

            if (isActiveAndEnabled)
            {
                ChannelSubscription.Bind(channel);
            }
        }

        /// <summary>
        /// Selects a configured state, enabling its objects and disabling all other state objects.
        /// </summary>
        /// <param name="state">
        /// Definition selected through code or the UnityEvent object picker.
        /// </param>
        public void ActivateState(ObjectStateDefinition state)
        {
            ActivateState(state, true);
        }

        /// <summary>Disables every configured state object and clears the current state.</summary>
        public void ClearState()
        {
            ClearState(true);
        }

        /// <summary>Checks whether the supplied definition is currently selected.</summary>
        public bool IsStateActive(ObjectStateDefinition state)
        {
            return state != null && CurrentState == state;
        }

        /// <summary>Activates the configured state with the supplied stable ID.</summary>
        /// <returns>True when a matching configured state was activated.</returns>
        public bool TryActivateStateById(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId) || states == null)
            {
                return false;
            }

            string normalizedId = stateId.Trim();
            foreach (StateBinding binding in states)
            {
                ObjectStateDefinition state = binding?.State;
                if (state != null &&
                    string.Equals(state.Id?.Trim(), normalizedId, StringComparison.Ordinal))
                {
                    ActivateState(state);
                    return true;
                }
            }

            return false;
        }

        private void ActivateState(ObjectStateDefinition state, bool notify)
        {
            StateBinding selectedBinding = FindBinding(state);
            if (state == null || selectedBinding == null)
            {
                GameLogger.Warning(
                    "ActivateState",
                    this,
                    state == null
                        ? "ObjectStateHandler cannot activate a null state. Use ClearState instead."
                        : $"ObjectStateHandler has no binding for state '{state.name}'."
                );
                return;
            }

            bool changed = CurrentState != state;
            SetAllObjectsInactive();
            SetObjectsActive(selectedBinding.Objects);
            CurrentState = state;

            if (notify && changed)
            {
                onStateChanged?.Invoke(CurrentState);
            }
        }

        private void HandleCommand(ObjectStateCommand command)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            switch (command.Type)
            {
                case ObjectStateCommandType.Activate:
                    ActivateState(command.State);
                    break;
                case ObjectStateCommandType.Clear:
                    ClearState();
                    break;
            }
        }

        private void SubscribeToChannel(ObjectStateChannel value)
        {
            value.CommandRequested += HandleCommand;
        }

        private void UnsubscribeFromChannel(ObjectStateChannel value)
        {
            value.CommandRequested -= HandleCommand;
        }

        private void ClearState(bool notify)
        {
            bool changed = CurrentState != null;
            SetAllObjectsInactive();
            CurrentState = null;

            if (notify && changed)
            {
                onStateChanged?.Invoke(null);
            }
        }

        private StateBinding FindBinding(ObjectStateDefinition state)
        {
            if (states == null)
            {
                return null;
            }

            foreach (StateBinding binding in states)
            {
                if (binding != null && binding.State == state)
                {
                    return binding;
                }
            }

            return null;
        }

        private void SetAllObjectsInactive()
        {
            if (states == null)
            {
                return;
            }

            foreach (StateBinding binding in states)
            {
                if (binding != null)
                {
                    SetObjectsActive(binding.Objects, false);
                }
            }
        }

        private static void SetObjectsActive(IReadOnlyList<GameObject> objects, bool active = true)
        {
            if (objects == null)
            {
                return;
            }

            for (int index = 0; index < objects.Count; index++)
            {
                GameObject target = objects[index];
                if (target != null && target.activeSelf != active)
                {
                    target.SetActive(active);
                }
            }
        }
    }
}
