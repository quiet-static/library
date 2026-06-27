using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietStatic.Toolkit.Input
{
    /// <summary>
    /// Reads common gameplay actions from Unity's Input System and forwards them
    /// to a shared input state manager.
    /// </summary>
    /// <remarks>
    /// This component does not decide when gameplay input is allowed.
    /// Register it with <see cref="InputModeManager"/> so that manager can enable
    /// or disable it based on the current game state.
    /// </remarks>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Actions")]
        [Tooltip("Input Action Asset containing the configured gameplay action map.")]
        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("Name of the gameplay action map inside the Input Action Asset.")]
        [SerializeField] private string actionMapName = "Player";

        [Header("Action Names")]
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string sprintActionName = "Sprint";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField] private string pauseActionName = "Pause";

        /// <summary>
        /// Optional C# event raised when pause input is pressed.
        /// </summary>
        public static event Action OnPausePressed;

        /// <summary>
        /// Cached gameplay input state manager.
        /// </summary>
        private GameInputManager inputManager;

        /// <summary>
        /// Gameplay action map resolved from the configured input asset.
        /// </summary>
        private InputActionMap playerActionMap;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction interactAction;
        private InputAction pauseAction;

        /// <summary>
        /// Resolves required input references.
        /// </summary>
        private void Awake()
        {
            if (inputActions == null)
            {
                GameLogger.Error(
                    "Awake",
                    this,
                    $"{nameof(PlayerInputReader)} is missing an InputActionAsset reference."
                );

                enabled = false;
                return;
            }

            try
            {
                playerActionMap = inputActions.FindActionMap(actionMapName, true);

                moveAction = playerActionMap.FindAction(moveActionName, true);
                lookAction = playerActionMap.FindAction(lookActionName, true);
                jumpAction = playerActionMap.FindAction(jumpActionName, true);
                sprintAction = playerActionMap.FindAction(sprintActionName, true);
                interactAction = playerActionMap.FindAction(interactActionName, true);
                pauseAction = playerActionMap.FindAction(pauseActionName, true);
            }
            catch (Exception exception)
            {
                GameLogger.Error(
                    "Awake",
                    this,
                    $"{nameof(PlayerInputReader)} could not resolve configured input actions.\n{exception}"
                );

                enabled = false;
            }
        }

        /// <summary>
        /// Registers this reader as gameplay input when its scene becomes active.
        /// </summary>
        private void OnEnable()
        {
            if (InputModeManager.Instance != null)
            {
                InputModeManager.Instance.RegisterGameplayInput(this);
            }

            if (pauseAction != null)
            {
                pauseAction.performed += HandlePausePressed;
            }

            if (playerActionMap != null)
            {
                playerActionMap.Enable();
            }
        }

        /// <summary>
        /// Stops reading input and unregisters this reader when its scene unloads.
        /// </summary>
        private void OnDisable()
        {
            if (InputModeManager.Instance != null)
            {
                InputModeManager.Instance.UnregisterInput(this);
            }

            if (pauseAction != null)
            {
                pauseAction.performed -= HandlePausePressed;
            }

            if (playerActionMap != null)
            {
                playerActionMap.Disable();
            }

            inputManager?.ClearGameplayInput();
        }

        /// <summary>
        /// Reads continuous gameplay input while this component is enabled.
        /// </summary>
        private void Update()
        {
            if (!TryResolveInputManager() ||
                playerActionMap == null ||
                !playerActionMap.enabled)
            {
                return;
            }

            CaptureMovementInput();
            CaptureActionInput();
        }

        /// <summary>
        /// Clears one-frame gameplay state after other gameplay components have read it.
        /// </summary>
        private void LateUpdate()
        {
            if (TryResolveInputManager())
            {
                inputManager.ClearFrameInput();
            }
        }

        private bool TryResolveInputManager()
        {
            if (inputManager != null)
            {
                return true;
            }

            inputManager = GameInputManager.Instance;
            return inputManager != null;
        }

        /// <summary>
        /// Captures movement, look, sprint, and jump input.
        /// </summary>
        private void CaptureMovementInput()
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            Vector2 look = lookAction.ReadValue<Vector2>();
            bool sprint = sprintAction.IsPressed();

            inputManager.SetMovementInput(move, look, sprint);

            if (jumpAction.WasPressedThisFrame())
            {
                inputManager.QueueJump();
            }
        }

        /// <summary>
        /// Captures one-shot gameplay actions.
        /// </summary>
        private void CaptureActionInput()
        {
            if (interactAction.WasPressedThisFrame())
            {
                inputManager.QueueInteract();
            }
        }

        /// <summary>
        /// Handles pause input independently so pause can be raised immediately.
        /// </summary>
        private void HandlePausePressed(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            inputManager?.RaisePause();
            OnPausePressed?.Invoke();
        }
    }
}