/*
 * DialogueManager.cs
 * 
 * Persistent Systems-scene coordinator for dialogue sessions.
 * 
 * This manager does not store dialogue content and does not directly draw UI.
 * Dialogue content lives in DialogueTree assets.
 * Dialogue traversal lives in DialogueRunner.
 * Dialogue presentation lives in DialogueUIManager.
 * 
 * The manager coordinates those pieces so scene-local handlers and interactables
 * have one simple public API to call.
 */

using System;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Coordinates dialogue sessions between scene callers, tree traversal, and UI display.
    /// </summary>
    /// <remarks>
    /// This manager should live in the persistent Systems scene.
    /// 
    /// It is responsible for:
    /// - Starting dialogue from a DialogueTree.
    /// - Tracking whether dialogue is active.
    /// - Passing node data from DialogueRunner to DialogueUIManager.
    /// - Forwarding player advance and choice input to DialogueRunner.
    /// - Raising high-level dialogue lifecycle events.
    /// 
    /// It is not responsible for:
    /// - Storing dialogue content.
    /// - Traversing dialogue nodes directly.
    /// - Drawing text or buttons directly.
    /// - Being referenced directly by scene objects.
    /// </remarks>
    public class DialogueManager : ToolkitSingleton<DialogueManager>
    {
        /// <summary>
        /// Raised when dialogue begins.
        /// </summary>
        public static event Action<UnityEngine.Object, Transform> OnDialogueStarted;

        /// <summary>
        /// Raised when dialogue ends.
        /// </summary>
        public static event Action<UnityEngine.Object> OnDialogueEnded;

        /// <summary>
        /// Raised whenever dialogue activity changes.
        /// </summary>
        public static event Action<UnityEngine.Object, bool> OnDialogueStateChanged;

        [Serializable]
        public class DialogueStartedEvent : UnityEvent<UnityEngine.Object, Transform>
        {
        }

        [Serializable]
        public class DialogueEndedEvent : UnityEvent<UnityEngine.Object>
        {
        }

        [Header("Traversal")]
        [Tooltip("Dialogue runner used for traversing DialogueTree assets. If empty, one is searched for on this GameObject.")]
        [SerializeField] private DialogueRunner dialogueRunner;

        [Header("UI")]
        [Tooltip("Dialogue UI manager used to display speaker names, dialogue text, and choices. If empty, DialogueUIManager.Instance is used.")]
        [SerializeField] private DialogueUIManager dialogueUIManager;

        [Header("Unity Events")]
        [Tooltip("Invoked when dialogue begins.")]
        [SerializeField] private DialogueStartedEvent onDialogueStarted;

        [Tooltip("Invoked when dialogue ends.")]
        [SerializeField] private DialogueEndedEvent onDialogueEnded;

        /// <summary>
        /// Gets whether a dialogue session is currently active.
        /// </summary>
        public bool IsDialogueActive { get; private set; }

        /// <summary>
        /// Gets the dialogue tree currently being played.
        /// </summary>
        public DialogueTree CurrentDialogueTree { get; private set; }

        /// <summary>
        /// Gets the optional focus target associated with the active dialogue.
        /// </summary>
        public Transform CurrentFocusTarget { get; private set; }

        /// <summary>
        /// Gets the optional speaker transform associated with the active dialogue.
        /// </summary>
        public Transform CurrentSpeaker { get; private set; }

        /// <summary>
        /// Initializes references and subscribes to UI choice events.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            DontDestroyOnLoad(gameObject);

            if (dialogueRunner == null)
            {
                dialogueRunner = GetComponent<DialogueRunner>();
            }

            if (dialogueUIManager == null)
            {
                dialogueUIManager = DialogueUIManager.Instance;
            }
        }

        /// <summary>
        /// Subscribes to runner and UI events.
        /// </summary>
        private void OnEnable()
        {
            DialogueRunner.OnNodeChanged += HandleNodeChanged;
            DialogueRunner.OnDialogueEnded += HandleRunnerEnded;

            if (dialogueUIManager != null)
            {
                dialogueUIManager.OnChoiceSelected += ChooseDialogueOption;
            }
        }

        /// <summary>
        /// Unsubscribes from runner and UI events.
        /// </summary>
        private void OnDisable()
        {
            DialogueRunner.OnNodeChanged -= HandleNodeChanged;
            DialogueRunner.OnDialogueEnded -= HandleRunnerEnded;

            if (dialogueUIManager != null)
            {
                dialogueUIManager.OnChoiceSelected -= ChooseDialogueOption;
            }
        }

        /// <summary>
        /// Starts a new dialogue session.
        /// </summary>
        /// <param name="dialogueTree">Dialogue tree to play.</param>
        /// <param name="focusTarget">Optional world-space target for cameras or other systems.</param>
        /// <param name="speaker">Optional speaker transform associated with the session.</param>
        /// <returns>True if dialogue started successfully; otherwise, false.</returns>
        public bool StartDialogue(
            DialogueTree dialogueTree,
            Transform focusTarget = null,
            Transform speaker = null
        )
        {
            if (dialogueTree == null)
            {
                GameLogger.Warning(
                    "StartDialogue",
                    this,
                    $"{nameof(DialogueManager)} cannot start dialogue because no DialogueTree was provided."
                );
                return false;
            }

            if (dialogueRunner == null)
            {
                GameLogger.Warning(
                    "StartDialogue",
                    this,
                    $"{nameof(DialogueManager)} cannot start dialogue because no DialogueRunner is assigned."
                );
                return false;
            }

            if (IsDialogueActive)
            {
                GameLogger.Warning(
                    "StartDialogue",
                    this,
                    $"{nameof(DialogueManager)} cannot start dialogue because another dialogue is already active."
                );
                return false;
            }

            CurrentDialogueTree = dialogueTree;
            CurrentFocusTarget = focusTarget;
            CurrentSpeaker = speaker;
            IsDialogueActive = true;

            OnDialogueStateChanged?.Invoke(CurrentDialogueTree, true);
            OnDialogueStarted?.Invoke(CurrentDialogueTree, CurrentFocusTarget);
            onDialogueStarted?.Invoke(CurrentDialogueTree, CurrentFocusTarget);

            dialogueRunner.SetTree(CurrentDialogueTree);
            dialogueRunner.StartDialogue();

            return true;
        }

        /// <summary>
        /// Advances the active dialogue to the next node.
        /// </summary>
        public void AdvanceDialogue()
        {
            if (!IsDialogueActive || dialogueRunner == null)
            {
                return;
            }

            dialogueRunner.Advance();
        }

        /// <summary>
        /// Chooses a response option from the active dialogue node.
        /// </summary>
        /// <param name="choiceIndex">Index of the selected choice.</param>
        public void ChooseDialogueOption(int choiceIndex)
        {
            if (!IsDialogueActive || dialogueRunner == null)
            {
                return;
            }

            dialogueRunner.Choose(choiceIndex);
        }

        /// <summary>
        /// Stops the active dialogue session.
        /// </summary>
        /// <returns>True if a dialogue was stopped; otherwise, false.</returns>
        public bool StopDialogue()
        {
            if (!IsDialogueActive)
            {
                return false;
            }

            if (dialogueRunner != null && dialogueRunner.IsRunning)
            {
                dialogueRunner.EndDialogue();
            }
            else
            {
                FinishDialogueState();
            }

            return true;
        }

        /// <summary>
        /// Displays the current dialogue node through the DialogueUIManager.
        /// </summary>
        /// <param name="runner">Runner that changed nodes.</param>
        /// <param name="node">New active dialogue node.</param>
        private void HandleNodeChanged(DialogueRunner runner, DialogueTree.Node node)
        {
            if (runner != dialogueRunner || node == null)
            {
                return;
            }

            if (dialogueUIManager == null)
            {
                dialogueUIManager = DialogueUIManager.Instance;
            }

            if (dialogueUIManager == null)
            {
                return;
            }

            if (node.HasChoices)
            {
                dialogueUIManager.ShowChoices(
                    node.speaker,
                    node.line,
                    node.GetChoiceTexts()
                );
            }
            else
            {
                dialogueUIManager.ShowLine(
                    node.speaker,
                    node.line
                );
            }
        }

        /// <summary>
        /// Finishes manager state when the runner reports that dialogue ended.
        /// </summary>
        /// <param name="runner">Runner that ended dialogue.</param>
        private void HandleRunnerEnded(DialogueRunner runner)
        {
            if (runner != dialogueRunner)
            {
                return;
            }

            FinishDialogueState();
        }

        /// <summary>
        /// Clears dialogue state, hides UI, and raises dialogue-ended events.
        /// </summary>
        private void FinishDialogueState()
        {
            if (!IsDialogueActive)
            {
                return;
            }

            DialogueTree endedDialogue = CurrentDialogueTree;

            IsDialogueActive = false;
            CurrentDialogueTree = null;
            CurrentFocusTarget = null;
            CurrentSpeaker = null;

            if (dialogueUIManager == null)
            {
                dialogueUIManager = DialogueUIManager.Instance;
            }

            if (dialogueUIManager != null)
            {
                dialogueUIManager.HideDialogueUI();
            }

            OnDialogueStateChanged?.Invoke(endedDialogue, false);
            OnDialogueEnded?.Invoke(endedDialogue);
            onDialogueEnded?.Invoke(endedDialogue);
        }
    }
}
