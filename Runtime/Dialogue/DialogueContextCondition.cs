using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>A reusable condition that reads live player state for dialogue selection.</summary>
    public abstract class DialogueContextCondition : ScriptableObject
    {
        /// <summary>Checks a condition for the supplied player root.</summary>
        /// <remarks>
        /// Read gameplay state without modifying it. The resolver may query the condition while
        /// displaying an interaction prompt and again when interaction starts.
        /// </remarks>
        /// <param name="player">The active player root supplied by the dialogue resolver.</param>
        /// <param name="interactor">The source of the interaction, or null for other dialogue starts.</param>
        /// <returns>True if the condition is met. Return false when the required state is unavailable.</returns>
        public abstract bool IsMet(GameObject player, Interactor interactor);
    }
}
