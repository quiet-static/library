using QuietStatic.Toolkit.State;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// UnityEvent-facing bridge for changing the global string-based game state.
    /// </summary>
    /// <remarks>
    /// Use this on the persistent handler object when a scene-local trigger, button, timeline,
    /// or animation event needs to request a state change.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Handlers/Gameplay Handler")]
    public class GameplayHandler : MonoBehaviour
    {
        /// <summary>Requests a new state from the active <see cref="GameStateManager"/>.</summary>
        /// <param name="newState">Database-defined state identifier.</param>
        public void SetState(string newState)
        {
            GameStateManager.Instance.SetState(newState);
        }
    }
}
