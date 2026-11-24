/*
 * DialogueEventPlayer.cs
 *
 * Scene-local wrapper for starting, advancing, choosing, and stopping dialogue.
 */

using System;
using QuietStatic.Toolkit.Cinematics;
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
    public class DialogueEventPlayer : MonoBehaviour, ICinematicWaitSource
    {
        /// <summary>
        /// Gets whether optional collider triggers may start dialogue, or sets the authored runtime gate.
        /// Checkpoint restoration also suppresses triggers. Explicit dialogue starts are unaffected.
        /// </summary>
        public static bool TriggerStartsEnabled
        {
            get => triggerStartsEnabled && !RestoreSuppressesTriggerStarts;
            set => triggerStartsEnabled = value;
        }

        private static bool triggerStartsEnabled = true;
        internal static bool RestoreSuppressesTriggerStarts { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTriggerStartsEnabled()
        {
            triggerStartsEnabled = true;
            RestoreSuppressesTriggerStarts = false;
        }

        [Header("Default Dialogue")]
        [Tooltip("Tree used by configured starts when no Dialogue Tree Resolver is assigned.")]
        [SerializeField] private DialogueTree dialogueTree;

        [Tooltip("Optional ordered rules used for configured starts, including collider triggers.")]
        [SerializeField] private DialogueTreeResolver dialogueResolver;

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

        [Tooltip("Optional tag matched on the entering collider or its actor hierarchy. Leave blank to allow any collider.")]
        [SerializeField] private string requiredTag = "Player";

        [Tooltip("When enabled, this trigger can successfully start its default dialogue only once.")]
        [SerializeField] private bool triggerOnce = true;

        [Header("Unity Events")]
        [Tooltip("Invoked after this wrapper successfully starts dialogue.")]
        [SerializeField] private UnityEvent onDialogueStarted;

        [Tooltip("Invoked when dialogue started by this wrapper ends.")]
        [SerializeField] private UnityEvent onDialogueEnded;

        /// <summary>
        /// Raised when dialogue started by this wrapper ends.
        /// Arguments are the ended tree and its speaker.
        /// </summary>
        public event Action<DialogueTree, Transform> DialogueEnded;

        /// <summary>Raised for each node presented by this player's own dialogue session.</summary>
        public event Action<DialogueTree.Node> NodeChanged;

        internal event Action<DialogueSession, GameObject> SessionStarted;
        internal event Action<DialogueSession> SessionEnded;

        /// <summary>Gets the current node of this player's active session.</summary>
        public DialogueTree.Node CurrentNode => IsRunning ? observedManager.CurrentNode : null;

        private DialogueSession activeSession;
        private Transform activeSpeaker;
        private DialogueManager observedManager;
        private bool presentedFirstNode;

        /// <summary>Gets whether dialogue started by this wrapper is still active.</summary>
        public bool IsRunning => observedManager != null && observedManager.OwnsSession(activeSession);

        /// <summary>Gets whether the optional collider trigger has already fired.</summary>
        public bool HasTriggered { get; private set; }

        /// <summary>Gets whether the configured flag requirement currently permits dialogue.</summary>
        public bool CanStartDialogue => startRequirement == null || startRequirement.IsMet();

        private void OnDestroy() => DetachSession();

        /// <summary>
        /// Starts the dialogue configured in this component's Inspector.
        /// </summary>
        public void StartDialogue()
        {
            TryStartDefaultDialogue();
        }

        /// <summary>Starts the configured dialogue through the cinematic wait-source contract.</summary>
        public void Play()
        {
            StartDialogue();
        }

        /// <summary>
        /// Attempts to start the dialogue configured in this component's Inspector.
        /// </summary>
        /// <returns>True when the persistent dialogue manager accepts the dialogue.</returns>
        public bool TryStartDefaultDialogue() => TryStartDefaultDialogue(null);

        /// <summary>Starts the configured conversation for the actor that activated a scene event.</summary>
        /// <param name="actor">The entering actor. Use null to resolve against the registered player.</param>
        /// <returns>True when the dialogue entered its first node.</returns>
        public bool TryStartDefaultDialogue(GameObject actor)
        {
            DialogueTree tree = dialogueTree;
            if (dialogueResolver != null && !dialogueResolver.TryResolve(out tree, null, actor))
                return false;

            return TryStartDialogue(tree, focusTarget, speaker, actor);
        }

        /// <summary>
        /// Starts dialogue using context supplied by a scene object or NPC.
        /// </summary>
        public bool TryStartDialogue(
            DialogueTree tree,
            Transform requestedFocusTarget,
            Transform requestedSpeaker) => TryStartDialogue(tree, requestedFocusTarget, requestedSpeaker, null);

        private bool TryStartDialogue(
            DialogueTree tree,
            Transform requestedFocusTarget,
            Transform requestedSpeaker,
            GameObject actor)
        {
            DialogueManager manager = DialogueManager.Instance;
            if (tree == null ||
                manager == null ||
                activeSession != null ||
                !CanStartDialogue)
            {
                return false;
            }

            DialogueSession session = manager.StartDialogueSession(tree, requestedFocusTarget, accepted =>
            {
                activeSession = accepted;
                activeSpeaker = requestedSpeaker;
                observedManager = manager;
                presentedFirstNode = false;
                accepted.NodePresented += HandleNodeChanged;
                accepted.Completed += HandleDialogueEnded;
            });
            if (session == null || !session.HasEnteredNode) return false;

            if (activeSession == session && IsRunning)
            {
                SessionStarted?.Invoke(session, actor);
                if (activeSession == session && IsRunning) onDialogueStarted?.Invoke();
            }
            return true;
        }

        /// <summary>Stops this player's active dialogue session.</summary>
        public void StopDialogue()
        {
            if (IsRunning) observedManager.StopDialogue();
        }

        /// <summary>Advances this player's active dialogue to its next linear node.</summary>
        public void AdvanceDialogue()
        {
            if (IsRunning) observedManager.AdvanceDialogue();
        }

        /// <summary>Selects an option on this player's active dialogue node.</summary>
        /// <param name="choiceIndex">Zero-based choice index.</param>
        public void ChooseDialogueOption(int choiceIndex)
        {
            if (IsRunning) observedManager.ChooseDialogueOption(choiceIndex);
        }

        /// <summary>Changes the fixed tree used by configured starts without a resolver.</summary>
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

        /// <summary>Restores a collider's one-shot latch without replaying its conversation.</summary>
        public void RestoreTriggered(bool value) => HasTriggered = value;

        private void OnTriggerEnter(Collider other)
        {
            // Unity sends trigger messages to disabled MonoBehaviours too. A scene
            // owner disabling this wrapper must also suppress new trigger starts.
            if (!isActiveAndEnabled || !TriggerStartsEnabled ||
                !startOnTriggerEnter ||
                (triggerOnce && HasTriggered) ||
                !TryGetTriggerActor(other, out GameObject actor))
            {
                return;
            }

            if (TryStartDefaultDialogue(actor))
            {
                HasTriggered = true;
            }
        }

        private bool TryGetTriggerActor(Collider other, out GameObject actor)
        {
            actor = null;
            if (other == null) return false;

            Transform body = other.attachedRigidbody != null ? other.attachedRigidbody.transform : null;
            Transform tagged = other.transform;
            if (!string.IsNullOrWhiteSpace(requiredTag))
            {
                while (tagged != null && !tagged.CompareTag(requiredTag))
                {
                    // An independent body under the player (such as a carried prop) has its own tag.
                    tagged = tagged == body ? null : tagged.parent;
                }
                if (tagged == null) return false;
            }

            PlayerManager playerManager = PlayerManager.Instance;
            GameObject registeredPlayer = playerManager != null ? playerManager.Player : null;
            if (registeredPlayer != null &&
                (tagged == registeredPlayer.transform || tagged.IsChildOf(registeredPlayer.transform)))
            {
                actor = registeredPlayer;
            }
            else
            {
                actor = string.IsNullOrWhiteSpace(requiredTag) && body != null ? body.gameObject : tagged.gameObject;
            }
            return true;
        }

        private void HandleDialogueEnded(DialogueSession session)
        {
            Transform endedSpeaker = activeSpeaker;
            DetachSession();
            if (!session.HasEnteredNode) return;

            SessionEnded?.Invoke(session);
            DialogueEnded?.Invoke(session.Tree, endedSpeaker);
            onDialogueEnded?.Invoke();
        }

        private void HandleNodeChanged(DialogueTree.Node node)
        {
            if (!presentedFirstNode)
            {
                presentedFirstNode = true;
                activeSession.BeginFocus();
            }
            NodeChanged?.Invoke(node);
        }

        private void DetachSession()
        {
            // An owner remains subscribed while disabled so it still receives completion.
            if (activeSession != null)
            {
                activeSession.NodePresented -= HandleNodeChanged;
                activeSession.Completed -= HandleDialogueEnded;
            }
            activeSession = null;
            activeSpeaker = null;
            observedManager = null;
        }
    }
}
