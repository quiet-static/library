using System;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>
    /// Lightweight root component for an NPC. It stores shared state and provides one place
    /// for handlers or scripted events to pause and resume all attached NPC behaviours.
    /// </summary>
    [DisallowMultipleComponent]
    public class NPCController : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Stable identifier used by game-specific systems and save data.")]
        [SerializeField] private string npcId;
        [Tooltip("Player-facing name used by dialogue or interaction UI.")]
        [SerializeField] private string displayName;

        [Header("Shared Target")]
        [Tooltip("Optional runtime target. Scene handlers may assign the player after scenes load.")]
        [SerializeField] private Transform target;

        [Header("State")]
        [Tooltip("Master switch controlling all NPC behavior components.")]
        [SerializeField] private bool behavioursEnabled = true;

        [Header("Events")]
        [Tooltip("Invoked when the shared runtime target changes.")]
        [SerializeField] private UnityEvent<Transform> onTargetChanged;
        [Tooltip("Invoked when the behavior master switch is enabled.")]
        [SerializeField] private UnityEvent onBehavioursEnabled;
        [Tooltip("Invoked when the behavior master switch is disabled.")]
        [SerializeField] private UnityEvent onBehavioursDisabled;

        private NPCBehaviour[] behaviours;

        /// <summary>Gets the stable NPC identifier.</summary>
        public string NpcId => npcId;

        /// <summary>Gets the player-facing name, falling back to the GameObject name.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        /// <summary>Gets the shared target used by attached NPC behaviors.</summary>
        public Transform Target => target;

        /// <summary>Gets whether the behavior master switch is enabled.</summary>
        public bool BehavioursEnabled => behavioursEnabled;

        /// <summary>Raised when the shared target changes.</summary>
        public event Action<Transform> TargetChanged;

        private void Awake()
        {
            behaviours = GetComponents<NPCBehaviour>();
        }

        /// <summary>Changes the shared target and notifies attached systems.</summary>
        /// <param name="newTarget">New shared target, or null to clear it.</param>
        public void SetTarget(Transform newTarget)
        {
            if (target == newTarget)
                return;

            target = newTarget;
            TargetChanged?.Invoke(target);
            onTargetChanged?.Invoke(target);
        }

        /// <summary>Clears the shared target.</summary>
        public void ClearTarget() => SetTarget(null);

        /// <summary>Changes the active state of every attached NPC behavior.</summary>
        /// <param name="enabledState">Whether attached behaviors should be active.</param>
        public void SetBehavioursEnabled(bool enabledState)
        {
            behavioursEnabled = enabledState;

            foreach (NPCBehaviour behaviour in behaviours)
            {
                if (behaviour != null)
                    behaviour.SetBehaviourActive(enabledState);
            }

            if (enabledState) onBehavioursEnabled?.Invoke();
            else onBehavioursDisabled?.Invoke();
        }

        /// <summary>Enables all attached NPC behaviors.</summary>
        public void EnableBehaviours() => SetBehavioursEnabled(true);

        /// <summary>Disables all attached NPC behaviors.</summary>
        public void DisableBehaviours() => SetBehavioursEnabled(false);
    }
}
