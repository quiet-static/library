using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Drives an Animator trigger from a direct interaction callback or from a configured
    /// flag requirement becoming satisfied.
    /// </summary>
    /// <remarks>
    /// Direct callers may always use <see cref="UnlockInteraction"/>. Automatic flag reactions
    /// only occur when <see cref="FlagRequirement.IsConfigured"/> is true, preventing the
    /// default <c>None</c> requirement from reacting to every global flag change.
    /// </remarks>
    [RequireComponent(typeof(Animator))]
    public class InteractableUnlock : MonoBehaviour
    {
        [Header("Requirements")]
        [Tooltip("Optional flag condition for automatic activation. Leave Mode as None when this component is only called by interaction events.")]
        [SerializeField] private FlagRequirement requirement;

        [Tooltip("Evaluate a configured requirement when this component enables, allowing starting or restored flags to activate it.")]
        [SerializeField] private bool evaluateRequirementOnEnable = true;

        [Header("Animation")]
        [Tooltip("Animator that receives the configured trigger.")]
        [SerializeField] private Animator animator;

        [Tooltip("Animator trigger used for activation or the binary ON state.")]
        [SerializeField] private string animationOnTrigger;

        [Header("Binary States")]
        [Tooltip("Toggle between ON and OFF triggers on repeated direct calls.")]
        [SerializeField] private bool isBinary;

        [Tooltip("Animator trigger used when a binary interaction returns to its OFF state.")]
        [SerializeField] private string animationOffTrigger;

        /// <summary>Current binary state. False is OFF and true is ON.</summary>
        private bool currentState;

        /// <summary>Previous result used to detect a false-to-true requirement transition.</summary>
        private bool requirementWasMet;

        private void OnEnable()
        {
            FlagManager.OnFlagsChanged += HandleFlagsChanged;
            requirementWasMet = false;

            if (evaluateRequirementOnEnable)
            {
                EvaluateRequirement();
            }
        }

        private void OnDisable()
        {
            FlagManager.OnFlagsChanged -= HandleFlagsChanged;
        }

        /// <summary>
        /// Re-evaluates the configured condition whenever the active flag collection changes.
        /// </summary>
        private void HandleFlagsChanged()
        {
            EvaluateRequirement();
        }

        /// <summary>
        /// Activates once when a valid requirement transitions from unmet to met.
        /// </summary>
        private void EvaluateRequirement()
        {
            if (requirement == null || !requirement.IsConfigured)
            {
                requirementWasMet = false;
                return;
            }

            bool requirementIsMet = requirement.IsMet();
            bool becameMet = requirementIsMet && !requirementWasMet;
            requirementWasMet = requirementIsMet;

            if (becameMet)
            {
                UnlockInteraction();
            }
        }

        /// <summary>
        /// Fires the next configured Animator trigger immediately.
        /// This method is suitable for Interactable success events and other direct callbacks.
        /// </summary>
        public void UnlockInteraction()
        {
            GameLogger.Log(
                "UnlockInteraction",
                this,
                $"Called on {gameObject.name}. State before toggle: {currentState}"
            );

            if (animator == null)
            {
                GameLogger.Warning(
                    nameof(UnlockInteraction),
                    this,
                    "Cannot animate because no Animator is assigned."
                );
                return;
            }

            string triggerToUse;

            if (isBinary)
            {
                currentState = !currentState;
                triggerToUse = currentState ? animationOnTrigger : animationOffTrigger;
            }
            else
            {
                triggerToUse = animationOnTrigger;
            }

            if (string.IsNullOrWhiteSpace(triggerToUse))
            {
                GameLogger.Warning(
                    nameof(UnlockInteraction),
                    this,
                    "Cannot animate because the selected trigger name is empty."
                );
                return;
            }

            animator.SetTrigger(triggerToUse);
        }
    }
}
