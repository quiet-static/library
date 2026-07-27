using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Characters.Player
{
    /// <summary>
    /// Connects shared gameplay input to a character movement motor.
    /// </summary>
    /// <remarks>
    /// This component does not read Unity input actions directly and does not contain
    /// movement physics. It receives processed gameplay input from
    /// <see cref="GameInputManager"/> and sends it to <see cref="CharacterMotor"/>.
    /// </remarks>
    [RequireComponent(typeof(CharacterMotor))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Movement component that receives processed player input.")]
        [SerializeField] private CharacterMotor motor;

        [Header("Footsteps")]
        [Tooltip("Seconds between footsteps while walking.")]
        [Min(0.01f)]
        [SerializeField] private float walkStepInterval = 0.55f;

        [Tooltip("Seconds between footsteps while sprinting.")]
        [Min(0.01f)]
        [SerializeField] private float sprintStepInterval = 0.35f;

        [Tooltip("Minimum normalized movement speed required to emit footsteps.")]
        [Min(0f)]
        [SerializeField] private float minimumSpeedForFootsteps = 0.1f;

        /// <summary>
        /// Raised whenever this character should play a footstep sound.
        /// </summary>
        public static event Action<PlayerController> OnFootstep;

        private float footstepTimer;

        private bool movementEnabled = true;

        private void Reset()
        {
            motor = GetComponent<CharacterMotor>();
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<CharacterMotor>();
            }

            if (motor == null)
            {
                GameLogger.Error(
                    "Awake",
                    this,
                    $"{nameof(PlayerController)} requires a {nameof(CharacterMotor)}."
                );

                enabled = false;
            }
        }

        private void Update()
        {
            // Null guard check
            if (motor == null || GameInputManager.Instance == null)
            {
                return;
            }

            // Movement guard check
            if (!movementEnabled)
                return;

            GameInputManager input = GameInputManager.Instance;

            motor.Tick(
                input.Move,
                input.Sprint,
                input.ConsumeJump()
            );

            UpdateFootsteps(input.Sprint);
        }

        /// <summary>
        /// Determines whether enough movement has occurred to emit a footstep event.
        /// </summary>
        private void UpdateFootsteps(bool isSprinting)
        {
            if (motor.NormalizedSpeed <= minimumSpeedForFootsteps)
            {
                footstepTimer = 0f;
                return;
            }

            float stepInterval = isSprinting
                ? sprintStepInterval
                : walkStepInterval;

            footstepTimer += Time.deltaTime;

            if (footstepTimer < stepInterval)
            {
                return;
            }

            footstepTimer = 0f;
            OnFootstep?.Invoke(this);
        }

        private void StopMovement()
        {
            GameInputManager.Instance.ClearGameplayInput();
        }

        /// <summary>
        /// Disables player movement. Can be used for events requiring locking the player in place
        /// </summary>
        /// <param name="enabled">Whether movement should be enabled or not</param>
        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;

            if (!enabled)
            {
                StopMovement();
            }
        }
    }
}