using UnityEngine;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>
    /// Controls the visibility and lock state of the Unity cursor.
    /// </summary>
    /// <remarks>
    /// This component is useful for switching between gameplay and UI states.
    /// For example, gameplay can hide and lock the cursor, while menus, pause screens,
    /// title screens, or dialogue interfaces can show and unlock it.
    /// </remarks>
    public class CursorController : MonoBehaviour
    {
        [Header("Startup")]
        [Tooltip("If true, the cursor is shown and unlocked when this component starts. If false, the cursor is hidden and locked.")]
        [SerializeField] private bool showOnStart;

        /// <summary>
        /// Applies the configured startup cursor state when the object becomes active.
        /// </summary>
        private void Start()
        {
            SetCursorVisible(showOnStart);
        }

        /// <summary>
        /// Sets whether the cursor should be visible and unlocked.
        /// </summary>
        /// <param name="visible">
        /// True to show the cursor and unlock it for UI use.
        /// False to hide the cursor and lock it for gameplay use.
        /// </param>
        public void SetCursorVisible(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        }

        /// <summary>
        /// Shows the cursor and unlocks it so the player can interact with UI.
        /// </summary>
        public void ShowCursor()
        {
            SetCursorVisible(true);
        }

        /// <summary>
        /// Hides the cursor and locks it, usually for gameplay camera control.
        /// </summary>
        public void HideCursor()
        {
            SetCursorVisible(false);
        }
    }
}
