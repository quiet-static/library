using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Synchronizes common movement-related values from a character movement system
    /// to a Unity <see cref="Animator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This component is meant to sit on the same GameObject as an Animator and, usually,
    /// the character's movement components. It reads movement information from a
    /// <see cref="CharacterMotor"/> and a <see cref="MovementStateController"/>, then writes
    /// those values into Animator parameters each frame.
    /// </para>
    /// <para>
    /// Animator parameter names are configurable in the Inspector. Leave a parameter name
    /// blank to disable that specific Animator update without changing code.
    /// </para>
    /// <para>
    /// Supported Animator parameter types:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>float</c>: movement speed.</description></item>
    /// <item><description><c>bool</c>: grounded state.</description></item>
    /// <item><description><c>trigger</c>: jump start.</description></item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(Animator))]
    public class AnimationController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Movement component that provides values such as normalized movement speed. If left empty, this is searched for on the same GameObject.")]
        [SerializeField] private CharacterMotor motor;

        [Tooltip("Movement state source used to decide which Animator parameters should be updated. If left empty, this is searched for on the same GameObject.")]
        [SerializeField] private MovementStateController stateController;

        [Header("Animator Parameters")]
        [Tooltip("Name of the float Animator parameter used for movement speed. Leave blank to disable speed updates.")]
        [SerializeField] private string speedParameter = "Speed";

        [Tooltip("Name of the bool Animator parameter used for grounded state. Leave blank to disable grounded updates.")]
        [SerializeField] private string groundedParameter = "IsGrounded";

        [Tooltip("Name of the trigger Animator parameter fired when the character first enters the jump state. Leave blank to disable jump triggering.")]
        [SerializeField] private string jumpTriggerParameter = "Jump";

        [Header("Speed Smoothing")]
        [Tooltip("Damping time used when updating the speed float while the character is moving. Higher values make speed animation changes smoother but less responsive.")]
        [Min(0f)]
        [SerializeField] private float speedDampTime = 0.1f;

        [Header("Debug")]
        [Tooltip("When enabled, this component can write debug output through GameLogger. Disable this to silence logs from this component.")]
        [SerializeField] private bool enableDebug = true;

        /// <summary>
        /// Animator controlled by this component.
        /// </summary>
        private Animator animator;

        /// <summary>
        /// Cached hash for the configured speed float parameter.
        /// </summary>
        private int speedHash;

        /// <summary>
        /// Cached hash for the configured grounded bool parameter.
        /// </summary>
        private int groundedHash;

        /// <summary>
        /// Cached hash for the configured jump trigger parameter.
        /// </summary>
        private int jumpHash;

        /// <summary>
        /// True when <see cref="speedParameter"/> has a usable parameter name.
        /// </summary>
        private bool hasSpeedParameter;

        /// <summary>
        /// True when <see cref="groundedParameter"/> has a usable parameter name.
        /// </summary>
        private bool hasGroundedParameter;

        /// <summary>
        /// True when <see cref="jumpTriggerParameter"/> has a usable parameter name.
        /// </summary>
        private bool hasJumpTriggerParameter;

        /// <summary>
        /// Tracks whether the jump trigger has already been fired for the current airborne sequence.
        /// </summary>
        /// <remarks>
        /// Without this guard, the Animator trigger could be set every frame while the movement
        /// state remains <see cref="EntityState.MovementState.Jumping"/>.
        /// </remarks>
        private bool hasTriggeredJump;

        /// <summary>
        /// Automatically fills common references when the component is added or reset in the Inspector.
        /// </summary>
        private void Reset()
        {
            motor = GetComponent<CharacterMotor>();
            stateController = GetComponent<MovementStateController>();
        }

        /// <summary>
        /// Initializes component references, validates required dependencies, and caches Animator parameter hashes.
        /// </summary>
        private void Awake()
        {
            if (!enableDebug)
            {
                GameLogger.DisableFor(this);
            }

            animator = GetComponent<Animator>();

            if (motor == null)
            {
                motor = GetComponent<CharacterMotor>();
            }

            if (stateController == null)
            {
                stateController = GetComponent<MovementStateController>();
            }

            if (motor == null)
            {
                GameLogger.Error(nameof(AnimationController), this, "Missing CharacterMotor reference.");
                enabled = false;
                return;
            }

            if (stateController == null)
            {
                GameLogger.Error(nameof(AnimationController), this, "Missing MovementStateController reference.");
                enabled = false;
                return;
            }

            CacheAnimatorParameters();
            hasTriggeredJump = false;
        }

        /// <summary>
        /// Updates Animator parameters once per frame using the current movement state.
        /// </summary>
        private void Update()
        {
            ApplyAnimationParameters();
        }

        /// <summary>
        /// Converts configured Animator parameter names into integer hashes and records which
        /// parameter updates are enabled.
        /// </summary>
        /// <remarks>
        /// Hashing parameter names once avoids repeated string lookups every frame. A blank or
        /// whitespace-only parameter name disables that parameter update.
        /// </remarks>
        private void CacheAnimatorParameters()
        {
            hasSpeedParameter = !string.IsNullOrWhiteSpace(speedParameter);
            hasGroundedParameter = !string.IsNullOrWhiteSpace(groundedParameter);
            hasJumpTriggerParameter = !string.IsNullOrWhiteSpace(jumpTriggerParameter);

            if (hasSpeedParameter)
            {
                speedHash = Animator.StringToHash(speedParameter);
            }

            if (hasGroundedParameter)
            {
                groundedHash = Animator.StringToHash(groundedParameter);
            }

            if (hasJumpTriggerParameter)
            {
                jumpHash = Animator.StringToHash(jumpTriggerParameter);
            }
        }

        /// <summary>
        /// Applies Animator parameter updates based on the current movement state.
        /// </summary>
        /// <remarks>
        /// Grounded movement states reset the jump trigger guard. Jumping sets the grounded
        /// parameter to false and fires the jump trigger once. Falling only keeps the grounded
        /// parameter false.
        /// </remarks>
        private void ApplyAnimationParameters()
        {
            switch (stateController.CurrentState)
            {
                case EntityState.MovementState.Idle:
                    SetGrounded(true);
                    SetSpeed(0f, false);
                    hasTriggeredJump = false;
                    break;

                case EntityState.MovementState.Moving:
                    SetGrounded(true);
                    SetSpeed(motor.NormalizedSpeed, true);
                    hasTriggeredJump = false;

                    GameLogger.Log(
                        nameof(AnimationController),
                        this,
                        $"Normalized Speed: {motor.NormalizedSpeed:F2}"
                    );
                    break;

                case EntityState.MovementState.Jumping:
                    SetGrounded(false);

                    if (!hasTriggeredJump)
                    {
                        TriggerJump();
                        hasTriggeredJump = true;
                    }
                    break;

                case EntityState.MovementState.Falling:
                    SetGrounded(false);
                    break;
            }
        }

        /// <summary>
        /// Sets the configured grounded bool parameter, if grounded updates are enabled.
        /// </summary>
        /// <param name="isGrounded">Whether the Animator should be told the character is grounded.</param>
        private void SetGrounded(bool isGrounded)
        {
            if (!hasGroundedParameter)
            {
                return;
            }

            animator.SetBool(groundedHash, isGrounded);
        }

        /// <summary>
        /// Sets the configured speed float parameter, if speed updates are enabled.
        /// </summary>
        /// <param name="speed">The movement speed value to send to the Animator.</param>
        /// <param name="useDamping">
        /// True to apply Animator float damping; false to set the value immediately.
        /// </param>
        private void SetSpeed(float speed, bool useDamping)
        {
            if (!hasSpeedParameter)
            {
                return;
            }

            if (useDamping)
            {
                animator.SetFloat(speedHash, speed, speedDampTime, Time.deltaTime);
            }
            else
            {
                animator.SetFloat(speedHash, speed);
            }
        }

        /// <summary>
        /// Fires the configured jump trigger parameter, if jump triggering is enabled.
        /// </summary>
        private void TriggerJump()
        {
            if (!hasJumpTriggerParameter)
            {
                return;
            }

            animator.SetTrigger(jumpHash);
        }
    }
}
