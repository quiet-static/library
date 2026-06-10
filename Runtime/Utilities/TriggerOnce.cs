using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Utilities
{
    /// <summary>
    /// Invokes a UnityEvent the first time a valid collider enters this trigger.
    /// </summary>
    /// <remarks>
    /// This component is useful for one-shot gameplay events such as starting a cutscene,
    /// playing a scare, enabling an object, updating an objective, or setting off any other
    /// Inspector-configured response when the player enters a trigger volume.
    ///
    /// The trigger can optionally filter entering colliders by tag and can disable itself
    /// after firing so it does not continue receiving trigger callbacks.
    /// </remarks>
    public class TriggerOnce : MonoBehaviour
    {
        [Header("Trigger Filter")]
        [Tooltip("Optional tag required for an entering collider to activate this trigger. Leave blank to allow any collider.")]
        [SerializeField] private string requiredTag = "Player";

        [Header("Trigger Behavior")]
        [Tooltip("If true, this component disables itself after the trigger fires. The internal triggered flag still prevents repeated use either way.")]
        [SerializeField] private bool disableAfterTrigger = true;

        [Header("Events")]
        [Tooltip("Events invoked once when a valid collider enters this trigger.")]
        [SerializeField] private UnityEvent onTriggered;

        /// <summary>
        /// Tracks whether this trigger has already fired.
        /// </summary>
        private bool triggered;

        /// <summary>
        /// Checks entering colliders and fires the trigger event when the collider passes validation.
        /// </summary>
        /// <param name="other">The collider that entered this trigger volume.</param>
        private void OnTriggerEnter(Collider other)
        {
            if (!CanTrigger(other))
            {
                return;
            }

            Trigger();
        }

        /// <summary>
        /// Determines whether the incoming collider is allowed to activate this trigger.
        /// </summary>
        /// <param name="other">The collider being tested.</param>
        /// <returns>
        /// <c>true</c> if the trigger has not fired yet and the collider matches the tag filter;
        /// otherwise, <c>false</c>.
        /// </returns>
        private bool CanTrigger(Collider other)
        {
            if (triggered)
            {
                return false;
            }

            if (other == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requiredTag) && !other.CompareTag(requiredTag))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Marks the trigger as fired, invokes configured events, and optionally disables this component.
        /// </summary>
        private void Trigger()
        {
            triggered = true;
            onTriggered?.Invoke();

            if (disableAfterTrigger)
            {
                enabled = false;
            }
        }
    }
}
