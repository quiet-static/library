using QuietStatic.Toolkit.State;
using UnityEngine;

namespace QuietStatic.Toolkit.Objectives
{
    /// <summary>
    /// Shows or hides an objective UI object based on configured game states.
    /// </summary>
    public class ObjectiveVisibilityController : MonoBehaviour
    {
        [Header("UI Target")]
        [Tooltip("Root object containing the objective UI.")]
        [SerializeField] private GameObject objectiveRoot;

        [Header("Visibility Rules")]
        [Tooltip("States where the objective UI should be visible.")]
        [GameStateId]
        [SerializeField]
        private string[] visibleStates =
        {
            "Playing"
        };

        private void Reset()
        {
            objectiveRoot = gameObject;
        }

        private GameStateManager observedGameState;

        private void OnEnable()
        {
            ObserveActiveGameState();
            RefreshVisibility();
        }

        private void Start()
        {
            ObserveActiveGameState();
            RefreshVisibility();
        }

        private void OnDisable()
        {
            if (observedGameState != null)
            {
                observedGameState.StateChanged -= HandleGameStateChanged;
                observedGameState = null;
            }
        }

        private void ObserveActiveGameState()
        {
            GameStateManager activeGameState = GameStateManager.Instance;
            if (observedGameState == activeGameState)
            {
                return;
            }

            if (observedGameState != null)
            {
                observedGameState.StateChanged -= HandleGameStateChanged;
            }

            observedGameState = activeGameState;
            if (observedGameState != null)
            {
                observedGameState.StateChanged += HandleGameStateChanged;
            }
        }

        /// <summary>
        /// Rechecks objective visibility using the current game state.
        /// </summary>
        public void RefreshVisibility()
        {
            if (GameStateManager.Instance == null)
            {
                SetVisible(false);
                return;
            }

            SetVisibleForState(GameStateManager.Instance.CurrentState);
        }

        /// <summary>
        /// Forces the objective UI visible or hidden.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (objectiveRoot != null)
            {
                objectiveRoot.SetActive(visible);
            }
        }

        private void HandleGameStateChanged(
            string previousState,
            string newState
        )
        {
            SetVisibleForState(newState);
        }

        private void SetVisibleForState(string state)
        {
            if (visibleStates == null)
            {
                SetVisible(false);
                return;
            }

            foreach (string visibleState in visibleStates)
            {
                if (state == visibleState?.Trim())
                {
                    SetVisible(true);
                    return;
                }
            }

            SetVisible(false);
        }
    }
}
