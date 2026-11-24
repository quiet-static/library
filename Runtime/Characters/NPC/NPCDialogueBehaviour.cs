using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>
    /// Adds dialogue participation to an NPC without placing dialogue-manager logic on the NPC.
    /// The component forwards its request through the scene-local DialogueEventPlayer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPCController))]
    public class NPCDialogueBehaviour : MonoBehaviour
    {
        [Header("Dialogue")]
        [Tooltip("Dialogue tree played when this NPC is interacted with.")]
        [SerializeField] private DialogueTree dialogueTree;

        [Tooltip("Optional selector evaluated on every interaction. When assigned, its rules and fallback replace the fixed tree; no match starts nothing.")]
        [SerializeField] private DialogueTreeResolver dialogueResolver;

        [Tooltip("Scene-local dialogue wrapper. This should reference the DialogueEventPlayer in the NPC's gameplay scene.")]
        [SerializeField] private DialogueEventPlayer dialogueEventPlayer;

        [Header("Presentation")]
        [Tooltip("Optional camera focus point. When empty, this NPC's transform is used.")]
        [SerializeField] private Transform focusTarget;

        [Tooltip("When enabled, movement behaviours are paused for the duration of this NPC's dialogue.")]
        [SerializeField] private bool stopMovementDuringDialogue = true;

        [Tooltip("When enabled, the NPC turns toward NPCController.Target during dialogue.")]
        [SerializeField] private bool faceControllerTargetDuringDialogue = true;

        [Tooltip("Optional motor stopped while dialogue is active.")]
        [SerializeField] private NPCNavMeshMotor motor;
        [Tooltip("Optional look behavior used to face the conversation target.")]
        [SerializeField] private NPCLookAtBehaviour lookAtBehaviour;

        [Tooltip("Follow, wander, or other NPC behaviours that should be paused during dialogue.")]
        [SerializeField] private NPCBehaviour[] pauseDuringDialogue;

        [Header("Events")]
        [Tooltip("Invoked immediately before this component asks the scene dialogue handler to begin.")]
        [SerializeField] private UnityEvent onDialogueRequested;

        [Tooltip("Invoked after the persistent dialogue manager successfully accepts this dialogue.")]
        [SerializeField] private UnityEvent onDialogueBegan;

        [Tooltip("Invoked when this NPC's dialogue finishes or is stopped.")]
        [SerializeField] private UnityEvent onDialogueEnded;

        private NPCController controller;
        private bool[] previousBehaviourStates;
        private DialogueTree activeDialogueTree;

        /// <summary>Gets the dialogue tree configured for this NPC.</summary>
        public DialogueTree DialogueTree => dialogueTree;

        /// <summary>Gets whether this NPC currently owns an active dialogue.</summary>
        public bool IsInDialogue { get; private set; }

        private void Awake()
        {
            controller = GetComponent<NPCController>();

            if (motor == null)
            {
                motor = GetComponent<NPCNavMeshMotor>();
            }

            if (lookAtBehaviour == null)
            {
                lookAtBehaviour = GetComponent<NPCLookAtBehaviour>();
            }
        }

        private void OnEnable()
        {
            if (dialogueEventPlayer != null)
            {
                dialogueEventPlayer.DialogueEnded += HandleDialogueEnded;
            }
        }

        private void OnDisable()
        {
            if (dialogueEventPlayer != null)
            {
                dialogueEventPlayer.DialogueEnded -= HandleDialogueEnded;
            }

            if (IsInDialogue)
            {
                FinishDialogueState();
            }
        }

        private void Reset()
        {
            motor = GetComponent<NPCNavMeshMotor>();
            lookAtBehaviour = GetComponent<NPCLookAtBehaviour>();
        }

        /// <summary>
        /// Resolves the current conversation, or uses the fixed tree when no resolver is assigned,
        /// and requests it through the scene-local DialogueEventPlayer.
        /// This method is suitable for an Interactable UnityEvent.
        /// </summary>
        public void StartDialogue()
        {
            if (IsInDialogue)
            {
                return;
            }

            if (!TryResolveDialogue(null, out DialogueTree requestedTree))
            {
                if (dialogueResolver == null)
                {
                    GameLogger.Warning(
                        "StartDialogue",
                        this,
                        $"{name} cannot start dialogue because no DialogueTree is assigned."
                    );
                }
                return;
            }

            if (dialogueEventPlayer == null)
            {
                GameLogger.Warning(
                    "StartDialogue",
                    this,
                    $"{name} cannot start dialogue because no scene DialogueEventPlayer is assigned."
                );
                return;
            }

            RequestDialogue(requestedTree);
        }

        /// <summary>Checks whether the direct interaction has a conversation for this player.</summary>
        internal bool HasAvailableDialogue(Interactor interactor) => TryGetAvailableDialogue(interactor, out _);

        /// <summary>Starts the available conversation and reports whether the handler accepted it.</summary>
        internal bool TryStartInteractionDialogue(Interactor interactor) =>
            TryGetAvailableDialogue(interactor, out DialogueTree tree) && RequestDialogue(tree);

        private bool TryGetAvailableDialogue(Interactor interactor, out DialogueTree tree)
        {
            tree = null;
            return isActiveAndEnabled && !IsInDialogue && dialogueEventPlayer != null &&
                !dialogueEventPlayer.IsRunning && dialogueEventPlayer.CanStartDialogue &&
                TryResolveDialogue(interactor, out tree);
        }

        private bool TryResolveDialogue(Interactor interactor, out DialogueTree tree)
        {
            if (dialogueResolver != null)
            {
                return dialogueResolver.TryResolve(out tree, null, interactor != null ? interactor.Actor : null, interactor);
            }

            tree = dialogueTree;
            return tree != null;
        }

        private bool RequestDialogue(DialogueTree requestedTree)
        {
            onDialogueRequested?.Invoke();

            DialogueEventPlayer eventPlayer = dialogueEventPlayer;
            Transform resolvedFocusTarget = focusTarget != null ? focusTarget : transform;
            if (eventPlayer == null || !eventPlayer.TryStartDialogue(requestedTree, resolvedFocusTarget, transform))
            {
                return false;
            }

            if (eventPlayer == dialogueEventPlayer && eventPlayer.IsRunning && isActiveAndEnabled)
            {
                activeDialogueTree = requestedTree;
                BeginDialogueState();
            }

            return true;
        }

        /// <summary>
        /// Stops the currently active dialogue through the scene handler.
        /// </summary>
        public void StopDialogue()
        {
            if (!IsInDialogue || dialogueEventPlayer == null)
            {
                return;
            }

            dialogueEventPlayer.StopDialogue();
        }

        /// <summary>
        /// Replaces the fixed dialogue tree used when no resolver is assigned.
        /// </summary>
        public void SetDialogueTree(DialogueTree tree)
        {
            dialogueTree = tree;
        }

        /// <summary>
        /// Assigns a different scene-local dialogue wrapper at runtime.
        /// </summary>
        public void SetDialogueEventPlayer(DialogueEventPlayer eventPlayer)
        {
            if (dialogueEventPlayer == eventPlayer)
            {
                return;
            }

            if (isActiveAndEnabled && dialogueEventPlayer != null)
            {
                dialogueEventPlayer.DialogueEnded -= HandleDialogueEnded;
            }

            dialogueEventPlayer = eventPlayer;

            if (isActiveAndEnabled && dialogueEventPlayer != null)
            {
                dialogueEventPlayer.DialogueEnded += HandleDialogueEnded;
            }
        }

        private void BeginDialogueState()
        {
            IsInDialogue = true;

            if (stopMovementDuringDialogue)
            {
                PauseConfiguredBehaviours();
                motor?.Stop();
            }

            Transform participantTarget = controller != null ? controller.Target : null;

            if (faceControllerTargetDuringDialogue &&
                participantTarget != null &&
                lookAtBehaviour != null)
            {
                lookAtBehaviour.StartTemporaryLookAt(participantTarget);
            }

            onDialogueBegan?.Invoke();
        }

        private void HandleDialogueEnded(DialogueTree endedTree, Transform endedSpeaker)
        {
            if (!IsInDialogue || endedTree != activeDialogueTree || endedSpeaker != transform)
            {
                return;
            }

            FinishDialogueState();
        }

        private void FinishDialogueState()
        {
            IsInDialogue = false;
            activeDialogueTree = null;

            if (faceControllerTargetDuringDialogue && lookAtBehaviour != null)
            {
                lookAtBehaviour.StopTemporaryLook();
            }

            if (stopMovementDuringDialogue)
            {
                RestoreConfiguredBehaviours();
                motor?.Resume();
            }

            onDialogueEnded?.Invoke();
        }

        private void PauseConfiguredBehaviours()
        {
            if (pauseDuringDialogue == null)
            {
                previousBehaviourStates = null;
                return;
            }

            previousBehaviourStates = new bool[pauseDuringDialogue.Length];

            for (int i = 0; i < pauseDuringDialogue.Length; i++)
            {
                NPCBehaviour behaviour = pauseDuringDialogue[i];

                if (behaviour == null)
                {
                    continue;
                }

                previousBehaviourStates[i] = behaviour.IsBehaviourActive;
                behaviour.SetBehaviourActive(false);
            }
        }

        private void RestoreConfiguredBehaviours()
        {
            if (pauseDuringDialogue == null || previousBehaviourStates == null)
            {
                return;
            }

            int count = Mathf.Min(pauseDuringDialogue.Length, previousBehaviourStates.Length);

            for (int i = 0; i < count; i++)
            {
                NPCBehaviour behaviour = pauseDuringDialogue[i];

                if (behaviour != null)
                {
                    behaviour.SetBehaviourActive(previousBehaviourStates[i]);
                }
            }

            previousBehaviourStates = null;
        }
    }
}
