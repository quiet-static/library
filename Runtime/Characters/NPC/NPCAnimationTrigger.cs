using UnityEngine;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Exposes an NPC Animator's trigger parameters to reusable scene events.</summary>
    [DisallowMultipleComponent]
    public sealed class NPCAnimationTrigger : MonoBehaviour
    {
        [Tooltip("Animator that receives animation triggers.")]
        [SerializeField] private Animator animator;

        [Tooltip("Trigger fired by the parameterless TriggerConfigured method.")]
        [SerializeField] private string configuredTrigger;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        /// <summary>Fires the trigger configured in the Inspector.</summary>
        public void TriggerConfigured()
        {
            SetTrigger(configuredTrigger);
        }

        /// <summary>Fires a named Animator trigger. Suitable for a UnityEvent string argument.</summary>
        /// <param name="triggerName">Animator trigger parameter to fire.</param>
        public void SetTrigger(string triggerName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            string normalizedName = triggerName.Trim();
            if (!HasTrigger(normalizedName))
            {
                GameLogger.Warning(nameof(NPCAnimationTrigger), this,
                    $"Animator on {name} has no trigger parameter named '{normalizedName}'.");
                return;
            }

            animator.SetTrigger(normalizedName);
        }

        /// <summary>Clears a named Animator trigger. Suitable for a UnityEvent string argument.</summary>
        /// <param name="triggerName">Animator trigger parameter to clear.</param>
        public void ResetTrigger(string triggerName)
        {
            if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            {
                animator.ResetTrigger(triggerName.Trim());
            }
        }

        private bool HasTrigger(string triggerName)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger &&
                    parameter.name == triggerName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
