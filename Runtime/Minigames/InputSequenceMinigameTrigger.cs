using UnityEngine;

namespace QuietStatic.Toolkit.Minigames
{
    /// <summary>
    /// Starts an input-sequence minigame when a matching collider enters this trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class InputSequenceMinigameTrigger : MonoBehaviour
    {
        [Tooltip("Minigame started when a matching collider enters.")]
        [SerializeField] private InputSequenceMinigame minigame;

        [Tooltip("Optional request activator. When assigned, it is used instead of the local minigame.")]
        [SerializeField] private InputSequenceMinigameActivator activator;

        [Tooltip("Only colliders with this tag can activate the minigame.")]
        [SerializeField] private string activatorTag = "Player";

        [Tooltip("Prevent this trigger from starting the minigame more than once.")]
        [SerializeField] private bool triggerOnce = true;

        private bool hasTriggered;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((triggerOnce && hasTriggered) ||
                !other.CompareTag(activatorTag))
            {
                return;
            }

            bool started = activator != null
                ? activator.TryActivate()
                : minigame != null && minigame.TryStartMinigame();

            if (started)
            {
                hasTriggered = true;
            }
        }

        /// <summary>Allows a trigger-once activator to be used again.</summary>
        public void ResetTrigger() => hasTriggered = false;
    }
}
