using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.UI.Menu
{
    /// <summary>Reusable navigation behavior for a title menu with a nested settings page.</summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/Title Menu View")]
    public sealed class TitleMenuView : MonoBehaviour
    {
        [Tooltip("Page containing the title and primary menu buttons.")]
        [SerializeField] private GameObject mainPage;
        [Tooltip("Nested settings page shown in place of the primary menu.")]
        [SerializeField] private GameObject settingsPage;
        [Tooltip("Quit adapter invoked by ExitGame. Editor play mode is stopped safely.")]
        [SerializeField] private GameQuitter gameQuitter;
        [Tooltip("Raised when Start Game is pressed. The containing game chooses its first scene.")]
        [SerializeField] private UnityEvent startRequested = new();

        /// <summary>Event raised when the player requests a new game.</summary>
        public UnityEvent StartRequested => startRequested;

        private void OnEnable() => ShowMainPage();

        /// <summary>Shows the primary title controls.</summary>
        public void ShowMainPage() => SetPages(true);

        /// <summary>Shows the nested settings controls.</summary>
        public void ShowSettingsPage() => SetPages(false);

        /// <summary>Raises the game-specific start request.</summary>
        public void StartGame() => startRequested?.Invoke();

        /// <summary>Exits the player through the configured adapter.</summary>
        public void ExitGame() => gameQuitter?.QuitGame();

        private void SetPages(bool showMain)
        {
            if (mainPage != null) mainPage.SetActive(showMain);
            if (settingsPage != null) settingsPage.SetActive(!showMain);
        }
    }
}
