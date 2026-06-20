using QuietStatic.Toolkit.Dialogue;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Invokes cutscene-ending logic when a dialogue runner finishes.
    /// </summary>
    /// <remarks>
    /// This component is useful for simple cutscenes where the end of dialogue should
    /// directly trigger the next cutscene action, such as returning control to the player,
    /// loading another scene, disabling a cutscene object, or notifying a sequence manager.
    ///
    /// If a specific <see cref="DialogueRunner"/> is assigned, only that runner can trigger
    /// the end event. If no runner is assigned, any dialogue runner ending will invoke the event.
    /// </remarks>
    public class EndCutsceneWhenDialogueEnds : MonoBehaviour
    {
        [Header("Dialogue Source")]
        [Tooltip("Optional dialogue runner to listen for. If left empty, this component responds when any DialogueRunner ends.")]
        /// <summary>
        /// Optional dialogue runner used to filter which dialogue ending should trigger this component.
        /// </summary>
        /// <remarks>
        /// Assign this when the scene may contain multiple dialogue runners and only one should end
        /// this cutscene. Leave it empty for broad, any-dialogue behavior.
        /// </remarks>
        [SerializeField] private DialogueRunner runner;

        [Header("Events")]
        [Tooltip("Invoked when the assigned dialogue runner ends, or when any dialogue runner ends if no runner is assigned.")]
        /// <summary>
        /// Event invoked when the relevant dialogue runner finishes.
        /// </summary>
        /// <remarks>
        /// Typical listeners include cutscene managers, scene loaders, player-control toggles,
        /// or other cleanup behavior that should run after dialogue is complete.
        /// </remarks>
        [SerializeField] private UnityEvent onEnded;

        /// <summary>
        /// Attempts to auto-fill the dialogue runner reference when this component is added or reset.
        /// </summary>
        /// <remarks>
        /// Unity calls <c>Reset</c> in the editor when the component is first added or manually reset.
        /// Searching children makes this convenient for cutscene root objects that contain their
        /// dialogue runner on a child object.
        /// </remarks>
        private void Reset()
        {
            runner = GetComponentInChildren<DialogueRunner>();
        }

        /// <summary>
        /// Subscribes to the global dialogue-ended event while this component is enabled.
        /// </summary>
        private void OnEnable()
        {
            DialogueRunner.OnDialogueEnded += HandleDialogueEnded;
        }

        /// <summary>
        /// Unsubscribes from the global dialogue-ended event when this component is disabled.
        /// </summary>
        /// <remarks>
        /// This prevents disabled or destroyed cutscene objects from responding to dialogue events later.
        /// </remarks>
        private void OnDisable()
        {
            DialogueRunner.OnDialogueEnded -= HandleDialogueEnded;
        }

        /// <summary>
        /// Handles the dialogue-ended notification and invokes the cutscene end event when appropriate.
        /// </summary>
        /// <param name="endedRunner">The dialogue runner that just finished.</param>
        private void HandleDialogueEnded(DialogueRunner endedRunner)
        {
            if (runner != null && endedRunner != runner)
            {
                return;
            }

            onEnded?.Invoke();
        }
    }
}
