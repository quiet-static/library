using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Determines and stores the current high-level movement state for a character
    /// based on grounded status, vertical velocity, and horizontal speed.
    /// </summary>
    /// <remarks>
    /// This component is intended to act as a lightweight state resolver for movement-driven
    /// systems such as animation, effects, or gameplay logic.
    /// </remarks>
    public class MovementStateController : MonoBehaviour
    {
        [Header("Settings")]
        /// <summary>
        /// Minimum horizontal speed required before the character is considered moving.
        /// Values below this threshold are treated as idle.
        /// </summary>
        [SerializeField] private float idleSpeedThreshold = 0.01f;

        [Header("Debug")]
        /// <summary>
        /// Enables or disables logger output for this component.
        /// </summary>
        [SerializeField] private bool enableDebug = true;

        /// <summary>
        /// Gets the character's current movement state.
        /// </summary>
        public EntityState.MovementState CurrentState { get; private set; } = EntityState.MovementState.Idle;

        /// <summary>
        /// Gets the movement state from the previous update.
        /// Useful for detecting state transitions.
        /// </summary>
        public EntityState.MovementState PreviousState { get; private set; } = EntityState.MovementState.Idle;

        /// <summary>
        /// Gets whether the state changed during the most recent update.
        /// </summary>
        public bool StateChangedThisFrame { get; private set; }

        /// <summary>
        /// Applies debug settings during initialization.
        /// </summary>
        private void Awake()
        {
            if (!enableDebug)
            {
                GameLogger.DisableFor(this);
            }
        }

        /// <summary>
        /// Updates the current movement state using the supplied movement data.
        /// </summary>
        /// <param name="isGrounded">
        /// Whether the character is currently grounded.
        /// </param>
        /// <param name="verticalVelocity">
        /// The character's current vertical velocity.
        /// Positive values indicate upward movement, negative values indicate downward movement.
        /// </param>
        /// <param name="speed">
        /// The character's current horizontal speed.
        /// </param>
        public void UpdateState(bool isGrounded, float verticalVelocity, float speed)
        {
            EntityState.MovementState newState = DetermineState(isGrounded, verticalVelocity, speed);
            SetState(newState);
        }

        /// <summary>
        /// Explicitly sets the movement state.
        /// </summary>
        /// <param name="newState">The new state to assign.</param>
        public void SetState(EntityState.MovementState newState)
        {
            PreviousState = CurrentState;
            CurrentState = newState;
            StateChangedThisFrame = CurrentState != PreviousState;

            if (StateChangedThisFrame)
            {
                GameLogger.Log(
                    nameof(MovementStateController),
                    this,
                    $"State changed: {PreviousState} -> {CurrentState}"
                );
            }
        }

        /// <summary>
        /// Resolves the appropriate movement state from grounded state, vertical velocity, and speed.
        /// </summary>
        /// <param name="isGrounded">Whether the character is grounded.</param>
        /// <param name="verticalVelocity">The current vertical velocity.</param>
        /// <param name="speed">The current horizontal speed.</param>
        /// <returns>The resolved movement state.</returns>
        private EntityState.MovementState DetermineState(bool isGrounded, float verticalVelocity, float speed)
        {
            if (!isGrounded)
            {
                if (verticalVelocity > 0f)
                {
                    return EntityState.MovementState.Jumping;
                }

                return EntityState.MovementState.Falling;
            }

            if (speed < idleSpeedThreshold)
            {
                return EntityState.MovementState.Idle;
            }

            return EntityState.MovementState.Moving;
        }
    }
}
