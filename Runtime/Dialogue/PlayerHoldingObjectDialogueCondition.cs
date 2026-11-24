using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>Checks whether the player's interactor is carrying a grabbable object.</summary>
    [CreateAssetMenu(
        fileName = "PlayerHoldingObject",
        menuName = "Quiet Static Toolkit/Dialogue/Conditions/Player Holding Object")]
    public sealed class PlayerHoldingObjectDialogueCondition : DialogueContextCondition
    {
        [Tooltip("Enable to require a held object. Disable to require an empty-handed interactor.")]
        [SerializeField] private bool requireHeldObject = true;

        /// <inheritdoc />
        public override bool IsMet(GameObject player, Interactor interactor)
        {
            if (interactor == null && player != null)
            {
                interactor = player.GetComponentInChildren<Interactor>();
            }

            return interactor != null && (interactor.HeldGrabbable != null) == requireHeldObject;
        }
    }
}
