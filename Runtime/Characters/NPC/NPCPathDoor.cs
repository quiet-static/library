using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Runtime passage state reported by an NPC-openable path door.</summary>
    public enum NPCPathDoorState
    {
        Closed,
        Opening,
        Open,
        Locked
    }

    /// <summary>Opts an animated interaction into idempotent NPC door opening.</summary>
    /// <remarks>
    /// This adapter deliberately checks an optional <see cref="Interactable"/> for lock
    /// requirements but does not execute the player interaction's flags or UnityEvents.
    /// <see cref="InteractableUnlock"/> remains the sole owner of animation state.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/NPC/NPC Path Door")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractableUnlock))]
    public sealed class NPCPathDoor : MonoBehaviour
    {
        [Header("Door Interaction")]
        [Tooltip("Optional interaction whose enabled state and flag requirements also govern NPC access.")]
        [SerializeField] private Interactable interactionRules;

        [Tooltip("Binary animation adapter that owns the door's open and closed state.")]
        [SerializeField] private InteractableUnlock animatedState;

        [Tooltip("Allow NPC door openers to request passage when the interaction requirements are met.")]
        [SerializeField] private bool allowNPCOpening = true;

        [Header("Passage Timing")]
        [Min(0f)]
        [Tooltip("Seconds after requesting Open before an NPC may continue. Include animation and NavMesh-obstacle carving time.")]
        [SerializeField] private float clearanceDelay = 0.25f;

        private float passableAt = float.PositiveInfinity;

        /// <summary>Gets the door's current state from its interaction, animation state, and clearance delay.</summary>
        public NPCPathDoorState CurrentState
        {
            get
            {
                if (animatedState == null || !animatedState.IsBinary)
                {
                    return NPCPathDoorState.Locked;
                }

                if (animatedState.IsActivated)
                {
                    // If the animation was activated before this adapter observed a state
                    // change, there is no clearance start time to wait from. Treat that
                    // already-open state as passable. Requests made through this adapter still
                    // establish passableAt below and honor the configured clearance delay.
                    if (float.IsPositiveInfinity(passableAt))
                    {
                        return NPCPathDoorState.Open;
                    }

                    return Time.time < passableAt
                        ? NPCPathDoorState.Opening
                        : NPCPathDoorState.Open;
                }

                if (!allowNPCOpening ||
                    (interactionRules != null && !interactionRules.CanInteract()))
                {
                    return NPCPathDoorState.Locked;
                }

                return NPCPathDoorState.Closed;
            }
        }

        /// <summary>Gets whether an NPC may currently continue through the doorway.</summary>
        public bool IsPassable => CurrentState == NPCPathDoorState.Open;

        private void Reset()
        {
            interactionRules = GetComponent<Interactable>();
            animatedState = GetComponent<InteractableUnlock>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (animatedState == null)
            {
                return;
            }

            animatedState.StateChanged += HandleStateChanged;
            passableAt = animatedState.IsActivated
                ? Time.time
                : float.PositiveInfinity;
        }

        private void OnDisable()
        {
            if (animatedState != null)
            {
                animatedState.StateChanged -= HandleStateChanged;
            }
        }

        /// <summary>Requests passage without toggling an already-open door or invoking player effects.</summary>
        /// <returns>The door state after evaluating or requesting passage.</returns>
        public NPCPathDoorState RequestPassage()
        {
            NPCPathDoorState state = CurrentState;
            if (state != NPCPathDoorState.Closed)
            {
                return state;
            }

            if (!animatedState.Activate())
            {
                return NPCPathDoorState.Locked;
            }

            // StateChanged normally establishes the delay. Keep this fallback so passage
            // remains deterministic if the adapter was enabled during an unusual callback.
            if (float.IsPositiveInfinity(passableAt))
            {
                passableAt = Time.time + clearanceDelay;
            }

            return CurrentState;
        }

        private void HandleStateChanged(bool isActivated)
        {
            passableAt = isActivated
                ? Time.time + clearanceDelay
                : float.PositiveInfinity;
        }

        private void ResolveReferences()
        {
            if (animatedState == null)
            {
                animatedState = GetComponent<InteractableUnlock>();
            }

            if (interactionRules == null)
            {
                interactionRules = GetComponent<Interactable>();
            }
        }

        private void OnValidate()
        {
            clearanceDelay = Mathf.Max(0f, clearanceDelay);
        }
    }
}
