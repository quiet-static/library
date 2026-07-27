/*
 * DialogueRunner.cs
 * 
 * Runtime traversal component for DialogueTree assets.
 * 
 * This class owns dialogue flow only:
 * - Which node is active.
 * - How to advance to the next node.
 * - How to select choices.
 * - When dialogue traversal starts and ends.
 * - Which flags are set by nodes and choices.
 * 
 * It does not display UI, read player input, lock the cursor, pause gameplay,
 * move cameras, or manage scene-specific behavior.
 */

using System;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Traverses a DialogueTree at runtime and exposes progress events.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        /// <summary>
        /// Raised whenever any DialogueRunner starts.
        /// </summary>
        public static event Action<DialogueRunner> OnDialogueStarted;

        /// <summary>
        /// Raised whenever any DialogueRunner changes to a new node.
        /// </summary>
        public static event Action<DialogueRunner, DialogueTree.Node> OnNodeChanged;

        /// <summary>
        /// Raised whenever any DialogueRunner ends.
        /// </summary>
        public static event Action<DialogueRunner> OnDialogueEnded;

        [Header("Dialogue")]
        [Tooltip("Dialogue tree this runner should play.")]
        [SerializeField] private DialogueTree dialogueTree;

        [Tooltip("If true, this runner starts the assigned dialogue tree when Start is called.")]
        [SerializeField] private bool playOnStart;

        [Header("Unity Events")]
        [Tooltip("Invoked when this runner starts dialogue.")]
        [SerializeField] private UnityEvent onDialogueStarted;

        [Tooltip("Invoked when this runner changes to a new node.")]
        [SerializeField] private UnityEvent onNodeChanged;

        [Tooltip("Invoked when this runner ends dialogue.")]
        [SerializeField] private UnityEvent onDialogueEnded;

        /// <summary>
        /// Gets the dialogue node currently being presented.
        /// </summary>
        public DialogueTree.Node CurrentNode { get; private set; }

        /// <summary>
        /// Gets whether this runner is currently traversing dialogue.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Index of the current node inside the active DialogueTree.
        /// </summary>
        private int currentNodeIndex = -1;

        /// <summary>
        /// Starts dialogue automatically when configured to do so.
        /// </summary>
        private void Start()
        {
            if (playOnStart)
            {
                StartDialogue();
            }
        }

        /// <summary>
        /// Assigns the dialogue tree this runner should traverse.
        /// </summary>
        /// <param name="tree">Dialogue tree to assign.</param>
        public void SetTree(DialogueTree tree)
        {
            dialogueTree = tree;
        }

        /// <summary>
        /// Starts the assigned dialogue tree from its configured start node.
        /// </summary>
        public void StartDialogue()
        {
            if (dialogueTree == null || IsRunning)
            {
                return;
            }

            IsRunning = true;

            OnDialogueStarted?.Invoke(this);
            onDialogueStarted?.Invoke();

            GoToNode(dialogueTree.StartNodeIndex);
        }

        /// <summary>
        /// Advances from the current node using the node's default flow.
        /// </summary>
        public void Advance()
        {
            if (!IsRunning || CurrentNode == null)
            {
                return;
            }

            if (CurrentNode.HasChoices)
            {
                return;
            }

            GoToOrEnd(CurrentNode.nextNodeIndex);
        }

        /// <summary>
        /// Selects a choice from the current node.
        /// </summary>
        /// <param name="choiceIndex">Index of the selected choice.</param>
        public void Choose(int choiceIndex)
        {
            if (!IsValidChoiceIndex(choiceIndex))
            {
                return;
            }

            DialogueTree.Choice choice = CurrentNode.choices[choiceIndex];

            SetFlags(choice.flagsToSet);
            GoToOrEnd(choice.nextNodeIndex);
        }

        /// <summary>
        /// Ends the current dialogue traversal.
        /// </summary>
        public void EndDialogue()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            CurrentNode = null;
            currentNodeIndex = -1;

            OnDialogueEnded?.Invoke(this);
            onDialogueEnded?.Invoke();
        }

        /// <summary>
        /// Moves to the requested node index, or ends dialogue if the index is negative.
        /// </summary>
        /// <param name="nodeIndex">Node index to visit, or a negative value to end dialogue.</param>
        private void GoToOrEnd(int nodeIndex)
        {
            if (nodeIndex < 0)
            {
                EndDialogue();
                return;
            }

            GoToNode(nodeIndex);
        }

        /// <summary>
        /// Moves to a specific node in the active dialogue tree.
        /// </summary>
        /// <param name="nodeIndex">Index of the node to visit.</param>
        private void GoToNode(int nodeIndex)
        {
            if (dialogueTree == null || !dialogueTree.TryGetNode(nodeIndex, out DialogueTree.Node node))
            {
                EndDialogue();
                return;
            }

            currentNodeIndex = nodeIndex;
            CurrentNode = node;

            SetFlags(CurrentNode.flagsToSetOnEnter);

            OnNodeChanged?.Invoke(this, CurrentNode);
            onNodeChanged?.Invoke();
        }

        /// <summary>
        /// Checks whether a choice index is valid for the current node.
        /// </summary>
        /// <param name="choiceIndex">Choice index to check.</param>
        /// <returns>True if the choice exists; otherwise, false.</returns>
        private bool IsValidChoiceIndex(int choiceIndex)
        {
            return IsRunning
                && CurrentNode != null
                && CurrentNode.choices != null
                && choiceIndex >= 0
                && choiceIndex < CurrentNode.choices.Length;
        }

        /// <summary>
        /// Sets all non-empty flag ids through the global FlagManager.
        /// </summary>
        /// <param name="flagIds">Flag ids to set.</param>
        private static void SetFlags(string[] flagIds)
        {
            if (flagIds == null || FlagManager.Instance == null)
            {
                return;
            }

            foreach (string flagId in flagIds)
            {
                if (string.IsNullOrWhiteSpace(flagId))
                {
                    continue;
                }

                FlagManager.Instance.SetFlag(flagId);
            }
        }
    }
}
