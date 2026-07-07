using System;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Runs a <see cref="DialogueTree"/> at runtime and exposes dialogue progress events.
    /// </summary>
    /// <remarks>
    /// This component is intentionally focused on dialogue flow only. It does not display text,
    /// read input, animate UI, or decide how dialogue should look on screen. UI scripts can listen
    /// to <see cref="OnNodeChanged"/>, <see cref="OnDialogueStarted"/>, and
    /// <see cref="OnDialogueEnded"/> to present the current node however they need.
    ///
    /// Dialogue progression supports:
    /// - Starting from the tree's configured start node.
    /// - Advancing to the next node.
    /// - Automatically choosing the first choice when <see cref="Advance"/> is used on a choice node.
    /// - Choosing a specific choice by index.
    /// - Setting flags when entering a node or selecting a choice.
    /// - Ending when a node or choice points to a negative next-node index.
    /// </remarks>
    public class DialogueRunner : MonoBehaviour
    {
        [Header("Dialogue")]
        [Tooltip("Dialogue tree asset this runner should play. The runner starts from this tree's Start Node Index.")]
        [SerializeField] private DialogueTree dialogueTree;

        [Tooltip("If true, this dialogue starts automatically when the GameObject starts.")]
        [SerializeField] private bool playOnStart;

        [Header("Unity Events")]
        [Tooltip("Invoked when this runner successfully starts dialogue.")]
        [SerializeField] private UnityEvent onDialogueStarted;

        [Tooltip("Invoked when this runner ends dialogue.")]
        [SerializeField] private UnityEvent onDialogueEnded;

        /// <summary>
        /// Gets the dialogue node currently being presented.
        /// </summary>
        /// <remarks>
        /// This value is <c>null</c> while dialogue is not running.
        /// </remarks>
        public DialogueTree.Node CurrentNode { get; private set; }

        /// <summary>
        /// Gets whether this runner is currently playing through a dialogue tree.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Raised whenever any <see cref="DialogueRunner"/> changes to a new dialogue node.
        /// </summary>
        /// <remarks>
        /// UI systems usually listen to this event so they can update speaker name, body text,
        /// portraits, choices, or other dialogue presentation elements.
        /// </remarks>
        public static event Action<DialogueRunner, DialogueTree.Node> OnNodeChanged;

        /// <summary>
        /// Raised whenever any <see cref="DialogueRunner"/> starts running dialogue.
        /// </summary>
        public static event Action<DialogueRunner> OnDialogueStarted;

        /// <summary>
        /// Raised whenever any <see cref="DialogueRunner"/> ends dialogue.
        /// </summary>
        public static event Action<DialogueRunner> OnDialogueEnded;

        /// <summary>
        /// Index of the current node inside the active <see cref="DialogueTree"/>.
        /// </summary>
        /// <remarks>
        /// This is reset to -1 while no dialogue is running. It is kept private because other
        /// systems should generally interact through <see cref="CurrentNode"/> or runner events.
        /// </remarks>
        private int currentIndex = -1;

        /// <summary>
        /// Starts dialogue automatically if <see cref="playOnStart"/> is enabled.
        /// </summary>
        private void Start()
        {
            if (playOnStart)
            {
                StartDialogue();
            }
        }

        /// <summary>
        /// Assigns the dialogue tree this runner should use.
        /// </summary>
        /// <param name="tree">The new dialogue tree to run.</param>
        /// <remarks>
        /// This does not automatically start dialogue. Call <see cref="StartDialogue"/> after
        /// assigning a tree if the new tree should begin immediately.
        /// </remarks>
        public void SetTree(DialogueTree tree)
        {
            dialogueTree = tree;
        }

        /// <summary>
        /// Starts running the assigned dialogue tree from its configured start node.
        /// </summary>
        /// <remarks>
        /// If no tree is assigned or this runner is already active, this method does nothing.
        /// Starting dialogue raises both the static dialogue-started event and this component's
        /// Inspector-configured UnityEvent, then moves to the tree's start node.
        /// </remarks>
        public void StartDialogue()
        {
            GameLogger.Log(
                "StartDialogue",
                this,
                "Started a dialogue"
            );

            if (dialogueTree == null || IsRunning)
            {
                return;
            }

            IsRunning = true;

            OnDialogueStarted?.Invoke(this);
            onDialogueStarted?.Invoke();
            ToolkitEvents.RaiseDialogueStarted();

            GoToNode(dialogueTree.StartNodeIndex);
        }

        /// <summary>
        /// Advances dialogue using the current node's default flow.
        /// </summary>
        /// <remarks>
        /// If the current node has choices, this chooses the first choice. This keeps simple
        /// dialogue input code easy, but choice-based UI should call <see cref="Choose"/> with the
        /// specific selected choice index instead.
        ///
        /// If the current node has no choices, this moves to <c>nextNodeIndex</c>. A negative
        /// next-node index ends dialogue.
        /// </remarks>
        public void Advance()
        {
            if (!IsRunning || CurrentNode == null)
            {
                return;
            }

            if (CurrentNode.choices != null && CurrentNode.choices.Length > 0)
            {
                Choose(0);
                return;
            }

            if (CurrentNode.nextNodeIndex < 0)
            {
                EndDialogue();
            }
            else
            {
                GoToNode(CurrentNode.nextNodeIndex);
            }
        }

        /// <summary>
        /// Selects a choice from the current dialogue node.
        /// </summary>
        /// <param name="choiceIndex">Index of the selected choice in the current node's choices array.</param>
        /// <remarks>
        /// Choice flags are set before moving to the next node. If the selected choice has a
        /// negative next-node index, dialogue ends instead of moving to another node.
        /// Invalid choice indexes are ignored safely.
        /// </remarks>
        public void Choose(int choiceIndex)
        {
            if (!IsValidChoiceIndex(choiceIndex))
            {
                return;
            }

            DialogueTree.Choice choice = CurrentNode.choices[choiceIndex];
            SetFlags(choice.flagsToSet);

            if (choice.nextNodeIndex < 0)
            {
                EndDialogue();
            }
            else
            {
                GoToNode(choice.nextNodeIndex);
            }
        }

        /// <summary>
        /// Ends the current dialogue session and clears the active node.
        /// </summary>
        /// <remarks>
        /// This raises the static dialogue-ended event, this component's Inspector-configured
        /// UnityEvent, and the global toolkit dialogue-ended event. Calling this while dialogue is
        /// not running does nothing.
        /// </remarks>
        public void EndDialogue()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            CurrentNode = null;
            currentIndex = -1;

            OnDialogueEnded?.Invoke(this);
            onDialogueEnded?.Invoke();
            ToolkitEvents.RaiseDialogueEnded();
        }

        /// <summary>
        /// Moves the runner to a specific node index in the active dialogue tree.
        /// </summary>
        /// <param name="index">Index of the node to load.</param>
        /// <remarks>
        /// If the requested node cannot be found, dialogue ends safely. Node enter flags are set
        /// before <see cref="OnNodeChanged"/> is raised so listeners see the updated game state.
        /// </remarks>
        private void GoToNode(int index)
        {
            if (!dialogueTree.TryGetNode(index, out DialogueTree.Node node))
            {
                EndDialogue();
                return;
            }

            currentIndex = index;
            CurrentNode = node;

            SetFlags(node.flagsToSetOnEnter);
            OnNodeChanged?.Invoke(this, CurrentNode);
        }

        /// <summary>
        /// Checks whether a requested choice index is valid for the current node.
        /// </summary>
        /// <param name="choiceIndex">Choice index being checked.</param>
        /// <returns>
        /// <c>true</c> if dialogue is running, the current node has choices, and the index is in range;
        /// otherwise, <c>false</c>.
        /// </returns>
        private bool IsValidChoiceIndex(int choiceIndex)
        {
            return IsRunning
                && CurrentNode?.choices != null
                && choiceIndex >= 0
                && choiceIndex < CurrentNode.choices.Length;
        }

        /// <summary>
        /// Sets each valid flag id through the global <see cref="FlagManager"/> singleton.
        /// </summary>
        /// <param name="flagIds">Flag ids to set. Null arrays are ignored.</param>
        /// <remarks>
        /// This method is static because setting flags does not depend on this runner's instance
        /// state. Empty or whitespace-only flag ids are skipped to avoid accidental blank flags.
        /// </remarks>
        private static void SetFlags(string[] flagIds)
        {
            if (FlagManager.Instance == null || flagIds == null)
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
