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

        [Tooltip("Maximum activations when Only Once is disabled. Zero allows unlimited activations.")]
        [Min(0)] [SerializeField] private int maximumActivations;

        [Tooltip("Minimum seconds between accepted activations when reuse is enabled.")]
        [Min(0f)] [SerializeField] private float cooldown;

        [Range(0f, 1f)]
        [Tooltip("Chance that an otherwise valid activation plays the scare.")]
        [SerializeField] private float activationChance = 1f;

        /// <summary>
        /// Tracks whether this trigger has already fired.
        /// </summary>
        private bool triggered;
        private int activationCount;
        private float lastActivationTime = float.NegativeInfinity;

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

            TryPlay();
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

            if (!onlyOnce && maximumActivations > 0 && activationCount >= maximumActivations) return false;
            if (Time.unscaledTime - lastActivationTime < cooldown) return false;

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

        /// <summary>UnityEvent entry point that attempts activation without a collider/tag check.</summary>
        public void Trigger()
        {
            if ((onlyOnce && triggered) ||
                (!onlyOnce && maximumActivations > 0 && activationCount >= maximumActivations) ||
                Time.unscaledTime - lastActivationTime < cooldown) return;
            TryPlay();
        }

        /// <summary>Clears reuse state so the trigger can activate again.</summary>
        public void ResetTrigger()
        {
            triggered = false;
            activationCount = 0;
            lastActivationTime = float.NegativeInfinity;
        }

        private void TryPlay()
        {
            if (UnityEngine.Random.value > activationChance) return;
            triggered = true;
            activationCount++;
            lastActivationTime = Time.unscaledTime;
            PlayJumpscare();
        }
    }
}
