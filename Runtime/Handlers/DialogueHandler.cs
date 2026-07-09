/*
 * DialogueHandler.cs
 * 
 * Scene-local wrapper for starting, advancing, choosing, and stopping dialogue.
 * 
 * This component is intended to live in normal gameplay scenes and be referenced
 * by UnityEvents on interactables, triggers, timeline events, buttons, and other
 * scene objects.
 * 
 * It prevents scene reference mismatch issues by forwarding requests to the
 * persistent DialogueManager that lives in the Systems scene.
 */

using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Scene-local wrapper for communicating with the persistent dialogue system.
    /// </summary>
    /// <remarks>
    /// This class intentionally contains very little logic.
    /// 
    /// Its job is similar to InteractionHandler:
    /// - Scene objects reference this local handler.
    /// - This handler calls the persistent DialogueManager.
    /// - Scene objects do not need direct references to Systems-scene managers.
    /// 
    /// Add this component once per gameplay scene when you want to call dialogue
    /// behavior from Inspector UnityEvents.
    /// </remarks>
    public class DialogueHandler : MonoBehaviour
    {
        [Header("Default Dialogue")]
        [Tooltip("Optional dialogue tree used by StartDefaultDialogue.")]
        [SerializeField] private DialogueTree defaultDialogueTree;

        [Header("Optional Focus")]
        [Tooltip("Optional transform that camera or other systems may focus during dialogue.")]
        [SerializeField] private Transform defaultFocusTarget;

        [Tooltip("Optional transform representing the speaker or source of the dialogue.")]
        [SerializeField] private Transform defaultSpeaker;

        /// <summary>
        /// Starts the dialogue tree assigned in this handler's Inspector.
        /// </summary>
        public void StartDefaultDialogue()
        {
            StartDialogue(defaultDialogueTree);
        }

        /// <summary>
        /// Starts a specific dialogue tree.
        /// </summary>
        /// <param name="tree">Dialogue tree to start.</param>
        public void StartDialogue(DialogueTree tree)
        {
            if (DialogueManager.Instance == null)
            {
                GameLogger.Warning(
                    "StartDialogue",
                    this,
                    $"{nameof(DialogueHandler)} could not start dialogue because no {nameof(DialogueManager)} exists."
                );
                return;
            }

            DialogueManager.Instance.StartDialogue(
                tree,
                defaultFocusTarget,
                defaultSpeaker
            );
        }

        /// <summary>
        /// Starts a specific dialogue tree with an explicit focus target.
        /// </summary>
        /// <param name="tree">Dialogue tree to start.</param>
        /// <param name="focusTarget">Transform to associate with this dialogue session.</param>
        public void StartDialogueWithFocus(DialogueTree tree, Transform focusTarget)
        {
            if (DialogueManager.Instance == null)
            {
                GameLogger.Warning(
                    "StartDialogueWithFocus",
                    this,
                    $"{nameof(DialogueHandler)} could not start dialogue because no {nameof(DialogueManager)} exists."
                );
                return;
            }

            DialogueManager.Instance.StartDialogue(
                tree,
                focusTarget,
                defaultSpeaker
            );
        }

        /// <summary>
        /// Advances the currently active dialogue.
        /// </summary>
        public void AdvanceDialogue()
        {
            if (DialogueManager.Instance == null)
            {
                return;
            }

            DialogueManager.Instance.AdvanceDialogue();
        }

        /// <summary>
        /// Selects a response choice from the currently active dialogue.
        /// </summary>
        /// <param name="choiceIndex">Index of the selected response choice.</param>
        public void ChooseDialogueOption(int choiceIndex)
        {
            if (DialogueManager.Instance == null)
            {
                return;
            }

            DialogueManager.Instance.ChooseDialogueOption(choiceIndex);
        }

        /// <summary>
        /// Stops the currently active dialogue.
        /// </summary>
        public void StopDialogue()
        {
            if (DialogueManager.Instance == null)
            {
                return;
            }

            DialogueManager.Instance.StopDialogue();
        }
    }
}
