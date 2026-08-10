/*
 * DialogueEventPlayer.cs
 *
 * Scene-local wrapper for starting, advancing, choosing, and stopping dialogue.
 */

using System;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Scene-local wrapper for communicating with the persistent DialogueManager.
    /// Scene objects and NPCs may supply dialogue context dynamically without holding
    /// direct references to the manager in the Systems scene.
    /// </summary>
    public class DialogueEventPlayer : MonoBehaviour
    {
        /// <summary>
        /// Gets or sets whether optional collider triggers may start dialogue.
        /// Direct calls to the dialogue start methods are unaffected.
        /// </summary>
        public static bool TriggerStartsEnabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTriggerStartsEnabled()
        {
            TriggerStartsEnabled = true;
        }

        [Header("Default Dialogue")]
        [Tooltip("Optional tree used by the parameterless StartDialogue method.")]
        [SerializeField] private DialogueTree dialogueTree;

        [Header("Start Requirement")]
        [Tooltip("Optional flag condition required before this dialogue can start. None always allows it.")]
        [SerializeField] private FlagRequirement startRequirement;

        [Header("Optional Focus")]
        [Tooltip("Optional transform that camera or other systems may focus during dialogue.")]
        [SerializeField] private Transform focusTarget;

        [Tooltip("Optional transform representing the speaker or source of the dialogue.")]
        [SerializeField] private Transform speaker;

        [Header("Optional Trigger")]
        [Tooltip("When enabled, entering a trigger collider on this GameObject starts the default dialogue.")]
        [SerializeField] private bool startOnTriggerEnter;

        [Tooltip("Optional tag required to start dialogue from the trigger. Leave blank to allow any collider.")]
        [SerializeField] private string requiredTag = "Player";

        [Tooltip("When enabled, this trigger can successfully start its default dialogue only once.")]
        [SerializeField] private bool triggerOnce = true;

        [Header("Unity Events")]
        [Tooltip("Invoked after this wrapper successfully starts dialogue.")]
        [SerializeField] private UnityEvent onDialogueStarted;

        [Tooltip("Invoked when dialogue started by this wrapper ends.")]
        [SerializeField] private UnityEvent onDialogueEnded;

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

        /// <summary>Gets the dialogue tree most recently started by this component.</summary>
        public DialogueTree ActiveTree { get; private set; }

        /// <summary>Gets the camera focus target used by the active dialogue.</summary>
        public Transform ActiveFocusTarget { get; private set; }

        /// <summary>Gets the speaker transform used by the active dialogue.</summary>
        public Transform ActiveSpeaker { get; private set; }

        /// <summary>Gets whether this component currently owns a running dialogue.</summary>
        public bool IsPlaying => ActiveTree != null;

        /// <summary>Gets whether the optional collider trigger has already fired.</summary>
        public bool HasTriggered { get; private set; }

        /// <summary>Gets whether the configured flag requirement currently permits dialogue.</summary>
        public bool CanStartDialogue => startRequirement == null || startRequirement.IsMet();

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
            TryStartDefaultDialogue();
        }

        /// <summary>
        /// Attempts to start the dialogue configured in this component's Inspector.
        /// </summary>
        /// <returns>True when the persistent dialogue manager accepts the dialogue.</returns>
        public bool TryStartDefaultDialogue()
        {
            return TryStartDialogue(dialogueTree, focusTarget, speaker);
        }

        /// <summary>
        /// Starts dialogue using context supplied by a scene object or NPC.
        /// </summary>
        public bool TryStartDialogue(
            DialogueTree tree,
            Transform requestedFocusTarget,
            Transform requestedSpeaker)
        {
            if (tree == null || DialogueManager.Instance == null || IsPlaying || !CanStartDialogue)
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
            onDialogueStarted?.Invoke();
            return true;
        }

        /// <summary>Stops the active dialogue through the persistent dialogue manager.</summary>
        public void StopDialogue()
        {
            DialogueManager.Instance?.StopDialogue();
        }

        /// <summary>Advances the active dialogue to its next linear node.</summary>
        public void AdvanceDialogue()
        {
            DialogueManager.Instance?.AdvanceDialogue();
        }

        /// <summary>Selects an option on the active dialogue node.</summary>
        /// <param name="choiceIndex">Zero-based choice index.</param>
        public void ChooseDialogueOption(int choiceIndex)
        {
            DialogueManager.Instance?.ChooseDialogueOption(choiceIndex);
        }

        /// <summary>Changes the dialogue tree used by parameterless start methods and triggers.</summary>
        /// <param name="tree">New default dialogue tree.</param>
        public void SetDefaultDialogue(DialogueTree tree)
        {
            dialogueTree = tree;
        }

        /// <summary>Changes the default camera focus target.</summary>
        /// <param name="target">New default focus target.</param>
        public void SetDefaultFocusTarget(Transform target)
        {
            focusTarget = target;
        }

        /// <summary>Changes the default speaker transform.</summary>
        /// <param name="source">New default speaker.</param>
        public void SetDefaultSpeaker(Transform source)
        {
            speaker = source;
        }

        /// <summary>
        /// Allows an opt-in one-shot collider trigger to start its dialogue again.
        /// </summary>
        public void ResetTrigger()
        {
            HasTriggered = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TriggerStartsEnabled ||
                !startOnTriggerEnter ||
                other == null ||
                (triggerOnce && HasTriggered) ||
                (!string.IsNullOrWhiteSpace(requiredTag) && !other.CompareTag(requiredTag)))
            {
                return;
            }

            if (TryStartDefaultDialogue())
            {
                HasTriggered = true;
            }
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
            onDialogueEnded?.Invoke();
        }
    }
}
