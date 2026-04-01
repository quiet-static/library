using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Synchronizes common movement-related animation parameters from a character motor
    /// and movement state source to a Unity Animator.
    /// </summary>
    /// <remarks>
    /// This component is intended to be more reusable across projects than a game-specific
    /// animation controller. Animator parameter names are configurable in the Inspector,
    /// and each parameter can be disabled by leaving its name blank.
    ///
    /// Supported parameter types:
    /// - float: movement speed
    /// - bool: grounded state
    /// - trigger: jump start
    ///
    /// Expected state values come from <see cref="MovementStateController"/>.
    /// </remarks>
    [RequireComponent(typeof(Animator))]
    public class AnimationController : MonoBehaviour
    {
        [Header("References")]
        /// <summary>
        /// Motor providing movement metrics such as normalized speed and grounded state.
        /// </summary>
        [SerializeField] private ThirdPersonMotor motor;

        /// <summary>
        /// State source providing the current movement state.
        /// </summary>
        [SerializeField] private MovementStateController stateController;

        [Header("Animator Parameters")]
        /// <summary>
        /// Name of the float Animator parameter used for movement speed.
        /// Leave blank to disable speed updates.
        /// </summary>
        [SerializeField] private string speedParameter = "Speed";

        /// <summary>
        /// Name of the bool Animator parameter used for grounded state.
        /// Leave blank to disable grounded updates.
        /// </summary>
        [SerializeField] private string groundedParameter = "IsGrounded";

        /// <summary>
        /// Name of the trigger Animator parameter used when entering the jump state.
        /// Leave blank to disable jump triggering.
        /// </summary>
        [SerializeField] private string jumpTriggerParameter = "Jump";

        [Header("Settings")]
        /// <summary>
        /// Damp time used when updating the speed parameter.
        /// </summary>
        [SerializeField] private float speedDampTime = 0.1f;

        /// <summary>
        /// Enables logger output for this component.
        /// </summary>
        [SerializeField] private bool enableDebug = true;

        private Animator animator;

        private int speedHash;
        private int groundedHash;
        private int jumpHash;

        private bool hasSpeedParameter;
        private bool hasGroundedParameter;
        private bool hasJumpTriggerParameter;

        /// <summary>
        /// Tracks whether the jump trigger has already been fired for the current airborne sequence.
        /// </summary>
        private bool hasTriggeredJump;

        private void Awake()
        {
            if (!enableDebug)
            {
                GameLogger.DisableFor(this);
            }

            animator = GetComponent<Animator>();

            if (motor == null)
            {
                motor = GetComponent<ThirdPersonMotor>();
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

        private void Update()
        {
            ApplyAnimationParameters();
        }

        /// <summary>
        /// Converts configured parameter names into Animator hashes and records which
        /// parameters are enabled.
        /// </summary>
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
        /// Sets the configured grounded bool parameter, if enabled.
        /// </summary>
        /// <param name="isGrounded">The grounded state to apply.</param>
        private void SetGrounded(bool isGrounded)
        {
            if (!hasGroundedParameter)
                return;

            animator.SetBool(groundedHash, isGrounded);
        }

        /// <summary>
        /// Sets the configured speed float parameter, if enabled.
        /// </summary>
        /// <param name="speed">The speed value to apply.</param>
        /// <param name="useDamping">
        /// True to apply damping; false to set the value immediately.
        /// </param>
        private void SetSpeed(float speed, bool useDamping)
        {
            if (!hasSpeedParameter)
                return;

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
        /// Fires the configured jump trigger parameter, if enabled.
        /// </summary>
        private void TriggerJump()
        {
            if (!hasJumpTriggerParameter)
                return;

            animator.SetTrigger(jumpHash);
        }
    }
}