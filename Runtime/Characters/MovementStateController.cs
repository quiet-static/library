using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Determines and stores a character's current high-level movement state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This component converts low-level movement data, such as grounded status,
    /// vertical velocity, and horizontal speed, into a simple reusable state value.
    /// Other systems can then read <see cref="CurrentState"/> instead of each system
    /// needing to duplicate movement-state logic.
    /// </para>
    /// <para>
    /// Typical consumers include animation controllers, footstep systems, landing
    /// effects, jump effects, sound triggers, and gameplay logic that needs to know
    /// whether a character is idle, moving, jumping, or falling.
    /// </para>
    /// </remarks>
    public class MovementStateController : MonoBehaviour
    {
        [Header("Movement State Settings")]
        [Tooltip("Minimum horizontal speed required before the character is treated as moving. Speeds below this value are treated as idle.")]
        [Min(0f)]
        [SerializeField] private float idleSpeedThreshold = 0.01f;

        [Header("Debug")]
        [Tooltip("When true, this component logs movement-state transitions. Disable this to reduce console noise.")]
        [SerializeField] private bool enableDebug = true;

        /// <summary>
        /// Gets the character's current resolved movement state.
        /// </summary>
        /// <remarks>
        /// This value is updated when <see cref="UpdateState"/> or <see cref="SetState"/>
        /// is called. It starts as <see cref="EntityState.MovementState.Idle"/> by default.
        /// </remarks>
        public EntityState.MovementState CurrentState { get; private set; } = EntityState.MovementState.Idle;

        /// <summary>
        /// Gets the movement state that was active immediately before the current state.
        /// </summary>
        /// <remarks>
        /// This is useful when another system needs to detect transitions, such as
        /// moving from jumping to falling or falling to idle.
        /// </remarks>
        public EntityState.MovementState PreviousState { get; private set; } = EntityState.MovementState.Idle;

        /// <summary>
        /// Gets whether the most recent state update changed the movement state.
        /// </summary>
        /// <remarks>
        /// This value is recalculated each time <see cref="SetState"/> is called.
        /// </remarks>
        public bool StateChangedThisFrame { get; private set; }

        /// <summary>
        /// Applies debug settings when the component initializes.
        /// </summary>
        /// <remarks>
        /// If debug logging is disabled in the Inspector, this component is registered
        /// with <see cref="GameLogger"/> so its log messages can be suppressed.
        /// </remarks>
        private void Awake()
        {
            if (!enableDebug)
            {
                GameLogger.DisableFor(this);
            }
        }

        /// <summary>
        /// Updates the current movement state from the latest movement data.
        /// </summary>
        /// <param name="isGrounded">
        /// Whether the character is currently touching the ground.
        /// </param>
        /// <param name="verticalVelocity">
        /// The character's current vertical velocity. Positive values indicate upward
        /// movement, while negative values indicate downward movement.
        /// </param>
        /// <param name="speed">
        /// The character's current horizontal movement speed.
        /// </param>
        /// <remarks>
        /// This is the main method other movement components should call after they
        /// calculate grounded status, vertical velocity, and speed.
        /// </remarks>
        public void UpdateState(bool isGrounded, float verticalVelocity, float speed)
        {
            EntityState.MovementState newState = DetermineState(isGrounded, verticalVelocity, speed);
            SetState(newState);
        }

        /// <summary>
        /// Explicitly assigns the current movement state.
        /// </summary>
        /// <param name="newState">The movement state that should become active.</param>
        /// <remarks>
        /// Use this when another system already knows the state that should be active,
        /// or when forcing a special movement state during scripted behavior.
        /// This method also updates <see cref="PreviousState"/> and
        /// <see cref="StateChangedThisFrame"/>.
        /// </remarks>
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
        /// Resolves the best movement state for the supplied movement values.
        /// </summary>
        /// <param name="isGrounded">Whether the character is currently grounded.</param>
        /// <param name="verticalVelocity">The character's current vertical velocity.</param>
        /// <param name="speed">The character's current horizontal movement speed.</param>
        /// <returns>
        /// The movement state that matches the supplied grounded status, vertical
        /// velocity, and horizontal speed.
        /// </returns>
        /// <remarks>
        /// Airborne states take priority over horizontal movement. If the character is
        /// not grounded, positive vertical velocity resolves to jumping and zero or
        /// negative vertical velocity resolves to falling. Grounded characters resolve
        /// to idle or moving based on <see cref="idleSpeedThreshold"/>.
        /// </remarks>
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
