using QuietStatic.Toolkit.Pause;
using UnityEngine;

namespace QuietStatic.Toolkit.UI.Menu
{
    /// <summary>UnityEvent entry points for a pause menu with a nested settings page.</summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/Pause Menu View")]
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Tooltip("Page containing Resume, Settings, and Exit controls.")]
        [SerializeField] private GameObject mainPage;
        [Tooltip("Nested settings page shown while the game remains paused.")]
        [SerializeField] private GameObject settingsPage;
        [Tooltip("Quit adapter invoked by ExitGame. Editor play mode is stopped safely.")]
        [SerializeField] private GameQuitter gameQuitter;
        [Tooltip("Required channel used to resume gameplay from the pause overlay.")]
        [RequiredCommandChannel]
        [SerializeField] private PauseRequestChannel pauseRequestChannel;

        private void OnEnable() => ShowMainPage();

        public void ShowMainPage() => SetPages(true);
        public void ShowSettingsPage() => SetPages(false);
        public void Resume() => pauseRequestChannel?.Resume();
        public void ExitGame() => gameQuitter?.QuitGame();

        private void SetPages(bool showMain)
        {
            if (mainPage != null) mainPage.SetActive(showMain);
            if (settingsPage != null) settingsPage.SetActive(!showMain);
        }
    }
}
