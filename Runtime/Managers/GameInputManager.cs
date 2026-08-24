using QuietStatic.Input;
using QuietStatic.Toolkit.Core;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Stores the latest captured input state for global access.
    ///
    /// This class does not read from Unity's Input System directly.
    /// Instead, input reader components update this manager every frame.
    ///
    /// Responsibilities:
    /// - Store gameplay movement input
    /// - Store gameplay look input
    /// - Store gameplay action button states
    ///
    /// This should live in the persistent System scene.
    /// </summary>
    public class GameInputManager : ToolkitSingleton<GameInputManager>,
            IMoveInputSource,
            ILookInputSource
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        //
        //                          GAMEPLAY INPUT STATE
        //
        /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Move direction in forward/backward and left/right.
        /// </summary>
        public Vector2 Move { get; private set; }

        /// <summary>
        /// Look direction in up/down and left/right.
        /// </summary>
        public Vector2 Look { get; private set; }

        /// <summary>
        /// Whether sprint is currently being held.
        /// </summary>
        public bool Sprint { get; private set; }

        /// <summary>Whether interact is currently held.</summary>
        public bool InteractHeld { get; private set; }

        /// <summary>
        /// Whether a jump press is waiting to be consumed.
        /// </summary>
        private bool jumpQueued;

        private bool interactQueued;

        [Tooltip("Seconds an interact press remains queued while an interaction consumer becomes ready.")]
        [Min(0f)]
        [SerializeField] private float interactBufferDuration = 0.15f;
        private float interactQueuedTime;

        private void Update()
        {
            if (!interactQueued)
            {
                return;
            }

            if (Time.time - interactQueuedTime > interactBufferDuration)
            {
                ClearInteractInput();
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////
        //
        //                          GAMEPLAY INPUT METHODS
        //
        /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates movement-related input values.
        /// </summary>
        /// <param name="move">
        /// Current movement input.
        /// </param>
        /// <param name="look">
        /// Current look input.
        /// </param>
        /// <param name="sprint">
        /// Whether sprint is currently held.
        /// </param>
        public void SetMovementInput(Vector2 move, Vector2 look, bool sprint)
        {
            Move = move;
            Look = look;
            Sprint = sprint;
        }

        /// <summary>Updates the continuous state of the interaction action.</summary>
        public void SetInteractHeld(bool interactHeld)
        {
            InteractHeld = interactHeld;
        }

        /// <summary>
        /// Queues a jump input so movement code can consume it once.
        /// </summary>
        public void QueueJump()
        {
            jumpQueued = true;
        }

        /// <summary>
        /// Returns whether jump was queued, then clears the queued jump.
        /// </summary>
        /// <returns>
        /// True if jump was queued. Otherwise, false.
        /// </returns>
        public bool ConsumeJump()
        {
            bool jumped = jumpQueued;
            jumpQueued = false;
            return jumped;
        }

        /// <summary>
        /// Queues an interact input so interaction code can consume it once.
        ///
        /// The input only remains valid briefly. This prevents an old interact press
        /// from firing later when the player walks into an interactable radius.
        /// </summary>
        public void QueueInteract()
        {
            interactQueued = true;
            interactQueuedTime = Time.time;
        }

        /// <summary>
        /// Returns whether interact was queued and still valid, then clears it.
        ///
        /// If the interact input is too old, this clears it and returns false.
        /// </summary>
        public bool ConsumeInteract()
        {
            if (!interactQueued)
            {
                return false;
            }

            bool isExpired = Time.time - interactQueuedTime > interactBufferDuration;

            if (isExpired)
            {
                ClearInteractInput();
                return false;
            }

            ClearInteractInput();
            return true;
        }

        /// <summary>
        /// Clears queued and one-frame interact input.
        /// </summary>
        private void ClearInteractInput()
        {
            interactQueued = false;
            interactQueuedTime = 0f;
        }

        /// <summary>
        /// Clears all continuous gameplay input.
        ///
        /// Useful when switching away from gameplay input so the player does not
        /// keep moving from stale input values.
        /// </summary>
        public void ClearGameplayInput()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            Sprint = false;
            InteractHeld = false;
            jumpQueued = false;

            ClearInteractInput();
        }

    }
}
