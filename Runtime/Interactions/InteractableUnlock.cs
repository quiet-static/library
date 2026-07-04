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
        [SerializeField] private string animationOnTrigger;

        [Header("Binary States")]
        [Tooltip("Whether this game object should act on interactions in 2 states (ON with interaction 1, OFF with interaction 2)")]
        [SerializeField] private bool isBinary;

        [Tooltip("Trigger for returning to an OFF state")]
        [SerializeField] private string animationOffTrigger;

        private bool currentState = false; // FALSE = OFF | TRUE = ON

        private void OnEnable()
        {
            FlagManager.OnFlagSet += HandleFlagSet;
        }

        private void OnDisable()
        {
            FlagManager.OnFlagSet -= HandleFlagSet;
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
            GameLogger.Log(
                "UnlockInteraction",
                this,
                $"Called on {gameObject.name}. State before toggle: {currentState}"
            );

            string triggerToUse = "";

            if (isBinary)
            {
                currentState = !currentState;
                triggerToUse = currentState ? animationOnTrigger : animationOffTrigger;
            }
            else
            {
                triggerToUse = animationOnTrigger;
            }

            animator.SetTrigger(triggerToUse);
        }
    }
}
