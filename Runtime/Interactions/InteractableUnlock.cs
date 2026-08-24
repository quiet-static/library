using System;
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

        [Tooltip("Logical binary state at scene load. Match this to the Animator's authored starting pose.")]
        [SerializeField] private bool initialState;

        /// <summary>Current binary state. False is OFF and true is ON.</summary>
        private bool currentState;

        /// <summary>Previous result used to detect a false-to-true requirement transition.</summary>
        private bool requirementWasMet;
        private FlagManager observedFlags;

        /// <summary>Raised after the logical activated state changes successfully.</summary>
        public event Action<bool> StateChanged;

        /// <summary>Gets the logical activated state owned by this animation adapter.</summary>
        public bool IsActivated => currentState;

        /// <summary>Gets whether this adapter has distinct ON and OFF triggers.</summary>
        public bool IsBinary => isBinary;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            currentState = initialState;
        }

        private void OnEnable()
        {
            observedFlags = FlagManager.Instance;
            if (observedFlags != null)
            {
                observedFlags.FlagsChanged += HandleFlagsChanged;
            }
            requirementWasMet = false;

            if (evaluateRequirementOnEnable)
            {
                EvaluateRequirement();
            }
        }

        private void OnDisable()
        {
            if (observedFlags != null)
            {
                observedFlags.FlagsChanged -= HandleFlagsChanged;
                observedFlags = null;
            }
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

            if (isBinary)
            {
                SetActivated(!currentState);
                return;
            }

            TrySetTrigger(animationOnTrigger, nameof(UnlockInteraction));
        }

        /// <summary>Idempotently requests the logical ON state.</summary>
        /// <returns>True when the component is already ON or the ON trigger was fired.</returns>
        public bool Activate()
        {
            return SetActivated(true);
        }

        /// <summary>Idempotently requests the logical OFF state.</summary>
        /// <returns>True when the component is already OFF or the OFF trigger was fired.</returns>
        public bool Deactivate()
        {
            return SetActivated(false);
        }

        /// <summary>Idempotently applies an activated state without toggling it.</summary>
        /// <param name="activated">True for the ON state; false for the OFF state.</param>
        /// <returns>True when the requested state is active; otherwise false.</returns>
        public bool SetActivated(bool activated)
        {
            if (currentState == activated)
            {
                return true;
            }

            if (!isBinary && !activated)
            {
                GameLogger.Warning(
                    nameof(SetActivated),
                    this,
                    "Cannot deactivate a non-binary animation adapter because it has no OFF trigger."
                );
                return false;
            }

            string triggerToUse = activated ? animationOnTrigger : animationOffTrigger;
            if (!TrySetTrigger(triggerToUse, nameof(SetActivated)))
            {
                return false;
            }

            currentState = activated;
            StateChanged?.Invoke(currentState);
            return true;
        }

        private bool TrySetTrigger(string triggerToUse, string operation)
        {
            if (animator == null)
            {
                GameLogger.Warning(
                    operation,
                    this,
                    "Cannot animate because no Animator is assigned."
                );
                return false;
            }

            if (string.IsNullOrWhiteSpace(triggerToUse))
            {
                GameLogger.Warning(
                    operation,
                    this,
                    "Cannot animate because the selected trigger name is empty."
                );
                return false;
            }

            animator.SetTrigger(triggerToUse);
            return true;
        }
    }
}
