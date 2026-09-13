using QuietStatic.Toolkit.Pause;
using QuietStatic.Toolkit.Saving;
using TMPro;
using UnityEngine;

namespace QuietStatic.Toolkit.UI.Menu
{
    /// <summary>UnityEvent entry points for a pause menu with a nested settings page.</summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/Pause Menu View")]
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Tooltip("Page containing Resume, Save Game, Settings, and Exit controls.")]
        [SerializeField] private GameObject mainPage;
        [Tooltip("Nested settings page shown while the game remains paused.")]
        [SerializeField] private GameObject settingsPage;
        [Tooltip("Quit adapter invoked by ExitGame. Editor play mode is stopped safely.")]
        [SerializeField] private GameQuitter gameQuitter;
        [Tooltip("Required channel used to resume gameplay from the pause overlay.")]
        [RequiredCommandChannel]
        [SerializeField] private PauseRequestChannel pauseRequestChannel;

        [Tooltip("Channel connected to the persistent Save Manager.")]
        [RequiredCommandChannel]
        [SerializeField] private SaveRequestChannel saveRequestChannel;
        [Tooltip("Zero-based slot replaced when Save Game is pressed.")]
        [Min(0)]
        [SerializeField] private int saveSlot;
        [Tooltip("Optional arrival spawn used on load. Empty uses the saved scene's normal entry.")]
        [SerializeField] private string arrivalSpawnId = "";
        [Tooltip("Save button label used to show the result without closing the pause menu.")]
        [SerializeField] private TMP_Text saveButtonLabel;

        private void OnEnable()
        {
            ShowMainPage();
            SetSaveLabel("Save Game");
        }

        /// <summary>Saves progress in the configured slot while keeping gameplay paused.</summary>
        public void SaveGame()
        {
            if (saveRequestChannel == null || !saveRequestChannel.HasReceivers)
            {
                SetSaveLabel("Save unavailable");
                return;
            }

            // Save requests complete synchronously, including disk writes and failure reporting.
            // Scope the subscription to this click so closed menus never retain listeners.
            SetSaveLabel("Save failed - retry");
            saveRequestChannel.SaveCompleted += HandleSaveCompleted;
            try
            {
                saveRequestChannel.RequestSave(saveSlot, arrivalSpawnId);
            }
            finally
            {
                saveRequestChannel.SaveCompleted -= HandleSaveCompleted;
            }
        }

        private void HandleSaveCompleted(int slot, bool succeeded)
        {
            if (slot == saveSlot)
                SetSaveLabel(succeeded ? "Game saved" : "Save failed - retry");
        }

        private void SetSaveLabel(string text)
        {
            if (saveButtonLabel != null) saveButtonLabel.text = text;
        }

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
