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

        public string NpcId => npcId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Transform Target => target;
        public bool BehavioursEnabled => behavioursEnabled;

        public event Action<Transform> TargetChanged;

        private void Awake()
        {
            behaviours = GetComponents<NPCBehaviour>();
        }

        public void SetTarget(Transform newTarget)
        {
            if (target == newTarget)
                return;

            target = newTarget;
            TargetChanged?.Invoke(target);
            onTargetChanged?.Invoke(target);
        }

        public void ClearTarget() => SetTarget(null);

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

        public void EnableBehaviours() => SetBehavioursEnabled(true);
        public void DisableBehaviours() => SetBehavioursEnabled(false);
    }
}
