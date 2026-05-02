using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Handles character movement using a <see cref="CharacterController"/>,
    /// including camera-relative movement, turning, jumping, gravity, and optional movement state updates.
    /// </summary>
    /// <remarks>
    /// This component is intended to be reusable across third-person character-based projects.
    /// It does not read input directly; instead, it expects movement data to be provided through
    /// <see cref="Tick(Vector2, bool, bool)"/>.
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour
    {
        [Header("References")]

        /// <summary>
        /// Optional transform used to convert movement input into camera-relative movement.
        /// If not assigned, movement is applied in world-relative local X/Z space.
        /// </summary>
        [SerializeField] private Transform cameraTransform;

        /// <summary>
        /// Optional movement state controller that will be updated after motor processing.
        /// </summary>
        [SerializeField] private MovementStateController movementStateController;

        [Header("Movement")]

        /// <summary>
        /// Maximum movement speed while walking.
        /// </summary>
        [SerializeField] private float walkSpeed = 5f;

        /// <summary>
        /// Maximum movement speed while sprinting.
        /// </summary>
        [SerializeField] private float sprintSpeed = 8f;

        /// <summary>
        /// Speed used when rotating the character toward its movement direction.
        /// </summary>
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Jump / Gravity")]

        /// <summary>
        /// Desired jump height in world units.
        /// </summary>
        [SerializeField] private float jumpHeight = 1.2f;

        /// <summary>
        /// Gravity acceleration applied while airborne.
        /// Should typically be negative.
        /// </summary>
        [SerializeField] private float gravity = -9.81f;

        /// <summary>
        /// Small downward velocity applied while grounded to keep the controller anchored.
        /// </summary>
        [SerializeField] private float groundedGravity = -2f;

        [Header("Debug")]

        /// <summary>
        /// Enables or disables logger output for this component.
        /// </summary>
        [SerializeField] private bool enableDebug = true;

        /// <summary>
        /// Cached character controller used for physical movement.
        /// </summary>
        private CharacterController characterController;

        /// <summary>
        /// Current horizontal movement velocity derived from controller motion.
        /// </summary>
        private Vector3 move;

        /// <summary>
        /// Current velocity, including vertical motion from jumping and gravity.
        /// </summary>
        private Vector3 velocity;

        /// <summary>
        /// Whether the character is currently grounded.
        /// </summary>
        private bool isGrounded;

        /// <summary>
        /// Gets the current vertical velocity.
        /// Useful for animation state evaluation.
        /// </summary>
        public float VerticalVelocity => velocity.y;

        /// <summary>
        /// Gets the current horizontal speed normalized against sprint speed.
        /// </summary>
        public float NormalizedSpeed => sprintSpeed > 0f ? Mathf.Clamp01(move.magnitude / sprintSpeed) : 0f;

        /// <summary>
        /// Gets whether the character is currently grounded.
        /// </summary>
        public bool IsGrounded => isGrounded;

        /// <summary>
        /// Caches required references and applies debug settings.
        /// </summary>
        private void Awake()
        {
            if (!enableDebug)
            {
                GameLogger.DisableFor(this);
            }

            characterController = GetComponent<CharacterController>();

            if (characterController == null)
            {
                GameLogger.Error(nameof(CharacterMotor), this, "Missing CharacterController reference.");
                enabled = false;
                return;
            }
        }

        /// <summary>
        /// Processes one frame of character movement using externally supplied input data.
        /// </summary>
        /// <param name="input">
        /// Raw movement input where X is horizontal movement and Y is forward/back movement.
        /// </param>
        /// <param name="sprint">
        /// Whether sprint movement speed should be used.
        /// </param>
        /// <param name="jumpPressed">
        /// Whether jump was requested this frame.
        /// </param>
        public void Tick(Vector2 input, bool sprint, bool jumpPressed)
        {
            CheckGrounded();
            HandleJump(jumpPressed);
            HandleMove(input, sprint);
            HandleGravity();
            UpdateMovementState();
        }

        /// <summary>
        /// Updates grounded state and applies a small downward force while grounded
        /// to prevent floatiness and improve controller contact with the ground.
        /// </summary>
        private void CheckGrounded()
        {
            isGrounded = characterController.isGrounded;

            if (isGrounded && velocity.y < 0f)
            {
                velocity.y = groundedGravity;
            }
        }

        /// <summary>
        /// Applies jump velocity if a jump is requested while grounded.
        /// </summary>
        /// <param name="jumpPressed">Whether jump was requested this frame.</param>
        private void HandleJump(bool jumpPressed)
        {
            if (!jumpPressed || !isGrounded)
                return;

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isGrounded = false;

            GameLogger.Log(nameof(CharacterMotor), this, "Jump triggered.");
        }

        /// <summary>
        /// Applies horizontal movement and rotates the character toward its movement direction.
        /// </summary>
        /// <param name="input">The current movement input.</param>
        /// <param name="sprint">Whether sprint speed should be used.</param>
        private void HandleMove(Vector2 input, bool sprint)
        {
            Vector3 moveDirection = GetMoveDirection(input);
            float currentSpeed = sprint ? sprintSpeed : walkSpeed;

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                RotateTowards(moveDirection);
            }

            characterController.Move(moveDirection * currentSpeed * Time.deltaTime);
            move = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        }

        /// <summary>
        /// Converts raw 2D input into a normalized 3D movement direction.
        /// If a camera transform is assigned, movement is calculated relative to the camera's facing direction.
        /// </summary>
        /// <param name="input">Movement input on the X/Y plane.</param>
        /// <returns>A normalized world-space movement direction.</returns>
        private Vector3 GetMoveDirection(Vector2 input)
        {
            Vector3 moveDirection;

            if (cameraTransform != null)
            {
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;

                forward.y = 0f;
                right.y = 0f;

                forward.Normalize();
                right.Normalize();

                moveDirection = (forward * input.y) + (right * input.x);
            }
            else
            {
                moveDirection = new Vector3(input.x, 0f, input.y);
            }

            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            return moveDirection;
        }

        /// <summary>
        /// Smoothly rotates the character toward the supplied movement direction.
        /// </summary>
        /// <param name="moveDirection">The direction the character should face.</param>
        private void RotateTowards(Vector3 moveDirection)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Applies gravity to the vertical velocity and moves the character vertically.
        /// </summary>
        private void HandleGravity()
        {
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// Updates the optional movement state controller using the motor's current movement values.
        /// </summary>
        private void UpdateMovementState()
        {
            if (movementStateController == null)
                return;

            movementStateController.UpdateState(IsGrounded, VerticalVelocity, NormalizedSpeed);

            GameLogger.Log(
                nameof(CharacterMotor),
                this,
                $"Current State: {movementStateController.CurrentState}"
            );
        }
    }
}
