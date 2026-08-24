using QuietStatic.Toolkit.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietStatic.Toolkit.Pause
{
    /// <summary>Submits pause toggles from an Inspector-assigned input action.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Pause/Pause Input Handler")]
    public sealed class PauseInputHandler : MonoBehaviour
    {
        [Tooltip("Dedicated pause action. Keep it outside gameplay action maps that pause disables.")]
        [SerializeField] private InputActionReference pauseAction;

        [Tooltip("Required channel used to submit the pause toggle.")]
        [RequiredCommandChannel]
        [SerializeField] private PauseRequestChannel requestChannel;

        private InputAction boundAction;
        private bool enabledBoundAction;

        private void OnEnable()
        {
            BindPauseAction();
        }

        private void OnDisable()
        {
            UnbindPauseAction();
        }

        /// <summary>Assigns the dedicated pause action and refreshes the active subscription.</summary>
        public void SetPauseAction(InputActionReference value)
        {
            if (pauseAction == value)
            {
                return;
            }

            bool shouldRebind = isActiveAndEnabled;
            if (shouldRebind)
            {
                UnbindPauseAction();
            }

            pauseAction = value;
            if (shouldRebind)
            {
                BindPauseAction();
            }
        }

        /// <summary>Assigns the persistent pause request channel.</summary>
        public void SetRequestChannel(PauseRequestChannel value) => requestChannel = value;

        private void BindPauseAction()
        {
            if (pauseAction == null || pauseAction.action == null)
            {
                GameLogger.Warning(
                    $"[{nameof(PauseInputHandler)}] A pause action is required.",
                    this);
                return;
            }

            boundAction = pauseAction.action;
            enabledBoundAction = !boundAction.enabled;
            boundAction.performed += HandlePerformed;
            if (enabledBoundAction)
            {
                boundAction.Enable();
            }
        }

        private void UnbindPauseAction()
        {
            if (boundAction == null)
            {
                return;
            }

            boundAction.performed -= HandlePerformed;
            if (enabledBoundAction)
            {
                boundAction.Disable();
            }

            boundAction = null;
            enabledBoundAction = false;
        }

        private void HandlePerformed(InputAction.CallbackContext context)
        {
            if (InputModeManager.Instance != null &&
                InputModeManager.Instance.IsInputBlocked(InputBlockGroups.Pause))
            {
                return;
            }

            if (requestChannel == null)
            {
                GameLogger.Warning(
                    $"[{nameof(PauseInputHandler)}] Cannot toggle pause; no request channel is assigned.",
                    this);
                return;
            }

            requestChannel.Toggle();
        }
    }
}
