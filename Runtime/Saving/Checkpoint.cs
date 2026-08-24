using UnityEngine;

namespace QuietStatic.Toolkit.Saving
{
    /// <summary>
    /// UnityEvent- and trigger-friendly adapter for saving at a named arrival point.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Saving/Checkpoint")]
    public sealed class Checkpoint : MonoBehaviour
    {
        [Tooltip("Required channel shared with the persistent Save Manager.")]
        [RequiredCommandChannel]
        [SerializeField] private SaveRequestChannel requestChannel;

        [Tooltip("Save slot written when this checkpoint activates.")]
        [Min(0)]
        [SerializeField] private int slot;

        [Tooltip("Spawn point used when this save is loaded.")]
        [SerializeField] private string arrivalSpawnId;

        [Header("Trigger Activation")]
        [Tooltip("Save when a matching collider enters this trigger.")]
        [SerializeField] private bool saveOnTriggerEnter;

        [Tooltip("Only colliders with this tag activate trigger saving.")]
        [SerializeField] private string activatorTag = "Player";

        [Tooltip("Prevent repeated trigger saves until ResetCheckpoint is called.")]
        [SerializeField] private bool triggerOnce = true;

        private bool hasTriggered;

        /// <summary>Saves this checkpoint. Suitable for a UnityEvent.</summary>
        public void Save()
        {
            if (requestChannel == null)
            {
                GameLogger.Warning(
                    nameof(Save),
                    this,
                    $"{nameof(Checkpoint)} requires a save request channel.");
                return;
            }

            requestChannel.RequestSave(slot, arrivalSpawnId);
        }

        /// <summary>Allows this checkpoint's trigger to save again.</summary>
        public void ResetCheckpoint() => hasTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (!saveOnTriggerEnter ||
                (triggerOnce && hasTriggered) ||
                !other.CompareTag(activatorTag))
            {
                return;
            }

            hasTriggered = true;
            Save();
        }
    }
}
