using System;
using QuietStatic.Input;
using UnityEngine;

namespace QuietStatic.Toolkit.Characters.Player
{
    /// <summary>
    /// Connects shared gameplay input to a character movement motor.
    /// </summary>
    /// <remarks>
    /// This component does not read Unity input actions directly and does not contain
    /// movement physics. It receives processed gameplay input from
    /// an <see cref="IMoveInputSource"/> and sends it to <see cref="CharacterMotor"/>.
    /// </remarks>
    [RequireComponent(typeof(CharacterMotor))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Movement component that receives processed player input.")]
        [SerializeField] private CharacterMotor motor;

        [Tooltip("Component that supplies movement, sprint, and jump input. Leave empty to use the persistent GameInputManager.")]
        [SerializeField] private MonoBehaviour inputSourceBehaviour;

        [Header("Footsteps")]
        [Tooltip("Seconds between footsteps while walking.")]
        [Min(0.01f)]
        [SerializeField] private float walkStepInterval = 0.55f;

        [Tooltip("Seconds between footsteps while sprinting.")]
        [Min(0.01f)]
        [SerializeField] private float sprintStepInterval = 0.35f;

        [Tooltip("Minimum normalized movement speed required to emit footsteps.")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumSpeedForFootsteps = 0.1f;

        /// <summary>
        /// Raised whenever this character should play a footstep sound.
        /// </summary>
        public event Action<PlayerController> Footstep;

        private float footstepTimer;

        private bool movementEnabled = true;
        private IMoveInputSource inputSource;

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
                return;
            }

            ResolveInputSource();
        }

        private void Update()
        {
            if (motor == null)
            {
                return;
            }

            if (!movementEnabled)
            {
                return;
            }

            if (inputSource == null)
            {
                ResolveInputSource();
            }

            if (inputSource == null)
            {
                return;
            }

            motor.Tick(
                inputSource.Move,
                inputSource.Sprint,
                inputSource.ConsumeJump()
            );

            UpdateFootsteps(inputSource.Sprint);
        }

        /// <summary>Resolves the configured input provider or the persistent manager fallback.</summary>
        private void ResolveInputSource()
        {
            inputSource = inputSourceBehaviour as IMoveInputSource;

            if (inputSource == null && GameInputManager.Instance != null)
            {
                inputSource = GameInputManager.Instance;
            }
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
            Footstep?.Invoke(this);
        }

        private void StopMovement()
        {
            motor.Tick(Vector2.zero, false, false);

            if (inputSource is GameInputManager gameInput)
            {
                gameInput.ClearGameplayInput();
            }
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
