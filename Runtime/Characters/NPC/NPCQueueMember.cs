using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Lifecycle states applied to an NPC participating in an <see cref="NPCQueueController"/>.</summary>
    public enum NPCQueueMemberState
    {
        Inactive,
        Entering,
        Waiting,
        ReadyForService,
        InService,
        Leaving,
        Completed
    }

    /// <summary>Adapts one NPC for reusable queue movement, behavior modes, and presentation events.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPCController), typeof(NPCNavMeshMotor))]
    public sealed class NPCQueueMember : MonoBehaviour
    {
        [Header("NPC")]
        [Tooltip("Controller whose autonomous behaviors can be coordinated with queue modes.")]
        [SerializeField] private NPCController controller;

        [Tooltip("Motor used to navigate between queue positions.")]
        [SerializeField] private NPCNavMeshMotor motor;

        [Tooltip("Optional forward probe that opens marked doors and reports when movement must wait.")]
        [SerializeField] private NPCDoorOpener doorOpener;

        [Tooltip("Optional mode controller used to switch this NPC's behavior by queue phase.")]
        [SerializeField] private NPCModeController modeController;

        [Tooltip("Optional animation-trigger adapter used for queue phase animations.")]
        [SerializeField] private NPCAnimationTrigger animationTrigger;

        [Header("Route")]
        [Tooltip("Ordered destinations this NPC visits after entering and before reaching service.")]
        [SerializeField] private Transform[] preServiceWaypoints = Array.Empty<Transform>();

        [Header("Behavior Modes")]
        [Tooltip("Optional NPC mode applied while entering or approaching service.")]
        [SerializeField] private string enteringMode;

        [Tooltip("Optional NPC mode applied while standing in line.")]
        [SerializeField] private string waitingMode;

        [Tooltip("Optional NPC mode applied when first at the service point.")]
        [SerializeField] private string readyMode;

        [Tooltip("Optional NPC mode applied while being served.")]
        [SerializeField] private string serviceMode;

        [Tooltip("Optional NPC mode applied while exiting.")]
        [SerializeField] private string leavingMode;

        [Header("Animation Triggers")]
        [Tooltip("Optional Animator trigger fired upon reaching the service point.")]
        [SerializeField] private string readyAnimationTrigger;

        [Tooltip("Optional Animator trigger fired when service begins.")]
        [SerializeField] private string serviceAnimationTrigger;

        [Tooltip("Optional Animator trigger fired when this NPC starts leaving.")]
        [SerializeField] private string leavingAnimationTrigger;

        [Header("Phase Events")]
        [SerializeField] private UnityEvent onEntering = new UnityEvent();
        [SerializeField] private UnityEvent onWaiting = new UnityEvent();
        [SerializeField] private UnityEvent onReadyForService = new UnityEvent();
        [SerializeField] private UnityEvent onServiceStarted = new UnityEvent();
        [SerializeField] private UnityEvent onLeaving = new UnityEvent();
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        /// <summary>Gets the NPC controller coordinated by this queue member.</summary>
        public NPCController Controller => controller;

        /// <summary>Gets the movement motor coordinated by this queue member.</summary>
        public NPCNavMeshMotor Motor => motor;

        /// <summary>Gets the optional door opener coordinated by queue movement.</summary>
        public NPCDoorOpener DoorOpener => doorOpener;

        /// <summary>Gets the ordered route visited immediately before service.</summary>
        public IReadOnlyList<Transform> PreServiceWaypoints =>
            preServiceWaypoints ?? Array.Empty<Transform>();

        /// <summary>Gets the member's current queue lifecycle state.</summary>
        public NPCQueueMemberState State { get; private set; } = NPCQueueMemberState.Inactive;

        private void Reset()
        {
            controller = GetComponent<NPCController>();
            motor = GetComponent<NPCNavMeshMotor>();
            doorOpener = GetComponent<NPCDoorOpener>();
            modeController = GetComponent<NPCModeController>();
            animationTrigger = GetComponent<NPCAnimationTrigger>();
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<NPCController>();
            }

            if (motor == null)
            {
                motor = GetComponent<NPCNavMeshMotor>();
            }

            if (doorOpener == null)
            {
                doorOpener = GetComponent<NPCDoorOpener>();
            }
        }

        /// <summary>Applies a queue state and its configured behavior and presentation hooks.</summary>
        /// <param name="state">New queue lifecycle state.</param>
        /// <param name="notifyListeners">
        /// Whether this member's animation triggers and lifecycle UnityEvents should run. Restore
        /// operations can suppress them while still applying the behavior mode for the state.
        /// </param>
        public void ApplyState(NPCQueueMemberState state, bool notifyListeners = true)
        {
            State = state;
            switch (state)
            {
                case NPCQueueMemberState.Entering:
                    ApplyMode(enteringMode);
                    if (notifyListeners)
                    {
                        GameLogger.SafeInvoke("raising NPC queue entering callbacks", onEntering, this);
                    }
                    break;
                case NPCQueueMemberState.Waiting:
                    ApplyMode(waitingMode);
                    if (notifyListeners)
                    {
                        GameLogger.SafeInvoke("raising NPC queue waiting callbacks", onWaiting, this);
                    }
                    break;
                case NPCQueueMemberState.ReadyForService:
                    ApplyMode(readyMode);
                    if (notifyListeners)
                    {
                        animationTrigger?.SetTrigger(readyAnimationTrigger);
                        GameLogger.SafeInvoke("raising NPC queue ready callbacks", onReadyForService, this);
                    }
                    break;
                case NPCQueueMemberState.InService:
                    ApplyMode(serviceMode);
                    if (notifyListeners)
                    {
                        animationTrigger?.SetTrigger(serviceAnimationTrigger);
                        GameLogger.SafeInvoke("raising NPC queue service callbacks", onServiceStarted, this);
                    }
                    break;
                case NPCQueueMemberState.Leaving:
                    ApplyMode(leavingMode);
                    if (notifyListeners)
                    {
                        animationTrigger?.SetTrigger(leavingAnimationTrigger);
                        GameLogger.SafeInvoke("raising NPC queue leaving callbacks", onLeaving, this);
                    }
                    break;
                case NPCQueueMemberState.Completed:
                    if (notifyListeners)
                    {
                        GameLogger.SafeInvoke("raising NPC queue completion callbacks", onCompleted, this);
                    }
                    break;
            }
        }

        private void ApplyMode(string modeName)
        {
            if (modeController != null && !string.IsNullOrWhiteSpace(modeName))
            {
                modeController.SetModeByName(modeName.Trim());
            }
        }
    }
}
