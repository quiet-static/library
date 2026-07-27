/*
 * DialogueEventPlayer.cs
 *
 * Scene-local wrapper for starting, advancing, choosing, and stopping dialogue.
 */

using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Scene-local wrapper for communicating with the persistent DialogueManager.
    /// Scene objects and NPCs may supply dialogue context dynamically without holding
    /// direct references to the manager in the Systems scene.
    /// </summary>
    public class DialogueEventPlayer : MonoBehaviour
    {
        [Header("Default Dialogue")]
        [Tooltip("Optional tree used by the parameterless StartDialogue method.")]
        [SerializeField] private DialogueTree dialogueTree;

        [Header("Optional Focus")]
        [Tooltip("Optional transform that camera or other systems may focus during dialogue.")]
        [SerializeField] private Transform focusTarget;

        [Tooltip("Optional transform representing the speaker or source of the dialogue.")]
        [SerializeField] private Transform speaker;

        /// <summary>
        /// Raised after this wrapper successfully starts dialogue.
        /// Arguments are the tree, focus target, and speaker.
        /// </summary>
        public event Action<DialogueTree, Transform, Transform> DialogueStarted;

        /// <summary>
        /// Raised when dialogue started by this wrapper ends.
        /// Arguments are the ended tree and its speaker.
        /// </summary>
        public event Action<DialogueTree, Transform> DialogueEnded;

        public DialogueTree ActiveTree { get; private set; }
        public Transform ActiveFocusTarget { get; private set; }
        public Transform ActiveSpeaker { get; private set; }
        public bool IsPlaying => ActiveTree != null;

        private void OnEnable()
        {
            DialogueManager.OnDialogueEnded += HandleDialogueEnded;
        }

        private void OnDisable()
        {
            DialogueManager.OnDialogueEnded -= HandleDialogueEnded;
        }

        /// <summary>
        /// Starts the dialogue configured in this component's Inspector.
        /// </summary>
        public void StartDialogue()
        {
            TryStartDialogue(dialogueTree, focusTarget, speaker);
        }

        /// <summary>
        /// Starts dialogue using context supplied by a scene object or NPC.
        /// </summary>
        public bool TryStartDialogue(
            DialogueTree tree,
            Transform requestedFocusTarget,
            Transform requestedSpeaker)
        {
            if (tree == null || DialogueManager.Instance == null || IsPlaying)
            {
                return false;
            }

            bool started = DialogueManager.Instance.StartDialogue(
                tree,
                requestedFocusTarget,
                requestedSpeaker
            );

            if (!started)
            {
                return false;
            }

            ActiveTree = tree;
            ActiveFocusTarget = requestedFocusTarget;
            ActiveSpeaker = requestedSpeaker;

            if (ActiveFocusTarget != null && CameraManager.Instance != null)
            {
                CameraManager.Instance.BeginFocus(ActiveFocusTarget);
            }

            DialogueStarted?.Invoke(ActiveTree, ActiveFocusTarget, ActiveSpeaker);
            return true;
        }

        public void StopDialogue()
        {
            DialogueManager.Instance?.StopDialogue();
        }

        public void AdvanceDialogue()
        {
            DialogueManager.Instance?.AdvanceDialogue();
        }

        public void ChooseDialogueOption(int choiceIndex)
        {
            DialogueManager.Instance?.ChooseDialogueOption(choiceIndex);
        }

        public void SetDefaultDialogue(DialogueTree tree)
        {
            dialogueTree = tree;
        }

        public void SetDefaultFocusTarget(Transform target)
        {
            focusTarget = target;
        }

        public void SetDefaultSpeaker(Transform source)
        {
            speaker = source;
        }

        private void HandleDialogueEnded(UnityEngine.Object endedDialogue)
        {
            if (ActiveTree == null || endedDialogue != ActiveTree)
            {
                return;
            }

            DialogueTree endedTree = ActiveTree;
            Transform endedSpeaker = ActiveSpeaker;

            ActiveTree = null;
            ActiveFocusTarget = null;
            ActiveSpeaker = null;

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.EndFocus();
            }

            DialogueEnded?.Invoke(endedTree, endedSpeaker);
        }
    }
}
