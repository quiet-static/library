using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Handles unlocking an interactable (changing its state) upon successful Interaction.
    /// 
    /// This can be done through either events, or by detecting flag changes.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class InteractableUnlock : MonoBehaviour
    {
        [Header("Requirements")]
        [Tooltip("Required flags for activation")]
        [SerializeField] private FlagRequirement requirement;

        [Header("Animation")]
        [Tooltip("Animator for this object")]
        [SerializeField] Animator animator;

        [Tooltip("Animation trigger string")]
        [SerializeField] private string animationTrigger;

        private void OnEnable()
        {
            FlagSet.OnFlagSet += HandleFlagSet;
        }

        private void OnDisable()
        {
            FlagSet.OnFlagSet -= HandleFlagSet;
        }

        /// <summary>
        /// Listens for flag changes and unlocks the interactable when requirements are met
        /// </summary>
        /// <param name="flag">Flag that was just set</param>
        private void HandleFlagSet(string flag)
        {
            if (requirement == null)
                return;

            if (requirement.IsMet())
            {
                UnlockInteraction();
            }
        }

        /// <summary>
        /// Unlocks the animator trigger immediately, can be used on Success events
        /// </summary>
        public void UnlockInteraction()
        {
            animator.SetTrigger(animationTrigger);
        }
    }
}
