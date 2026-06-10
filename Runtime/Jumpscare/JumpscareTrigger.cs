using UnityEngine;

namespace QuietStatic.Toolkit.Jumpscare
{
    /// <summary>
    /// Trigger-volume helper that plays a <see cref="JumpscareEvent" /> when an allowed collider enters.
    /// </summary>
    /// <remarks>
    /// This component is intended for simple jumpscare setups where entering a collider should
    /// start a configured jumpscare sequence. It can optionally filter by tag and optionally
    /// prevent itself from firing more than once.
    ///
    /// Typical usage:
    /// - Add this component to a GameObject with a Collider set to Is Trigger.
    /// - Assign a <see cref="JumpscareEvent" /> in the Inspector.
    /// - Leave <see cref="requiredTag" /> as "Player" if only the player should activate it.
    /// </remarks>
    public class JumpscareTrigger : MonoBehaviour
    {
        [Header("Jumpscare")]
        [Tooltip("Jumpscare event that should play when this trigger is activated.")]
        [SerializeField] private JumpscareEvent jumpscare;

        [Header("Activation Filter")]
        [Tooltip("Optional tag required to activate this trigger. Leave blank to allow any collider.")]
        [SerializeField] private string requiredTag = "Player";

        [Header("Reuse")]
        [Tooltip("If true, this trigger can only activate one time.")]
        [SerializeField] private bool onlyOnce = true;

        /// <summary>
        /// Tracks whether this trigger has already fired.
        /// </summary>
        private bool triggered;

        /// <summary>
        /// Attempts to auto-fill the jumpscare reference when the component is added or reset.
        /// </summary>
        private void Reset()
        {
            jumpscare = GetComponent<JumpscareEvent>();
        }

        /// <summary>
        /// Checks whether the entering collider is allowed to activate this trigger,
        /// then plays the configured jumpscare event.
        /// </summary>
        /// <param name="other">The collider that entered this trigger volume.</param>
        private void OnTriggerEnter(Collider other)
        {
            if (!CanTrigger(other))
            {
                return;
            }

            triggered = true;
            PlayJumpscare();
        }

        /// <summary>
        /// Determines whether the supplied collider should activate this trigger.
        /// </summary>
        /// <param name="other">The collider attempting to activate the trigger.</param>
        /// <returns>
        /// <c>true</c> if the trigger is allowed to fire; otherwise, <c>false</c>.
        /// </returns>
        private bool CanTrigger(Collider other)
        {
            if (onlyOnce && triggered)
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
        /// Plays the assigned jumpscare event if one has been configured.
        /// </summary>
        private void PlayJumpscare()
        {
            if (jumpscare == null)
            {
                return;
            }

            jumpscare.Play();
        }
    }
}
