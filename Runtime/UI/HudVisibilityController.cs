using QuietStatic.Toolkit.State;
using UnityEngine;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>
    /// Shows or hides a HUD element based on the current global game state.
    /// </summary>
    /// <remarks>
    /// This component is generic and can control a crosshair, health bar,
    /// objective panel, interaction prompt, stamina display, or any other HUD object.
    /// </remarks>
    public class HudVisibilityController : MonoBehaviour
    {
        [Header("HUD Target")]
        [Tooltip("GameObject shown or hidden based on the configured visible states.")]
        [SerializeField] private GameObject hudRoot;

        [Header("Visibility Rules")]
        [Tooltip("Game states in which this HUD element should be visible.")]
        [SerializeField] private string[] visibleStates = { "Playing" };

        [Tooltip("Whether the HUD should be visible when no GameStateManager exists.")]
        [SerializeField] private bool visibleWithoutStateManager;

        /// <summary>
        /// Gets whether the HUD target is currently visible.
        /// </summary>
        public bool IsVisible =>
            hudRoot != null && hudRoot.activeSelf;

        private void Reset()
        {
            hudRoot = gameObject;
        }

        private void OnEnable()
        {
            GameStateManager.OnGameStateChanged += HandleGameStateChanged;
            RefreshVisibility();
        }

        private void OnDisable()
        {
            GameStateManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Refreshes the HUD based on the current global game state.
        /// </summary>
        public void RefreshVisibility()
        {
            if (GameStateManager.Instance == null)
            {
                SetVisible(visibleWithoutStateManager);
                return;
            }

            SetVisibleForState(GameStateManager.Instance.CurrentState);
        }

        /// <summary>
        /// Forces the HUD visible or hidden regardless of game state.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (hudRoot != null)
            {
                hudRoot.SetActive(visible);
            }
        }

        /// <summary>
        /// Handles a global game-state transition.
        /// </summary>
        private void HandleGameStateChanged(
            string previousState,
            string newState
        )
        {
            SetVisibleForState(newState);
        }

        /// <summary>
        /// Applies visibility rules for one state identifier.
        /// </summary>
        private void SetVisibleForState(string state)
        {
            SetVisible(IsVisibleInState(state));
        }

        /// <summary>
        /// Checks whether a state is listed as HUD-visible.
        /// </summary>
        private bool IsVisibleInState(string state)
        {
            if (string.IsNullOrWhiteSpace(state) ||
                visibleStates == null)
            {
                return false;
            }

            foreach (string visibleState in visibleStates)
            {
                if (state == visibleState?.Trim())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
