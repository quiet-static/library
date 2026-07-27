using UnityEngine;
using static QuietStatic.SettingsManager;

namespace QuietStatic
{
    /// <summary>
    /// Wrapper for communicating with the Pause and Settings Managers to send and recieve messages
    /// </summary>
    public class SystemHandler : MonoBehaviour
    {
        /// <summary>
        /// Pauses the game
        /// </summary>
        public void PauseGame()
        {
            PauseManager.Instance.PauseGame();
        }

        /// <summary>
        /// Resumes the game
        /// </summary>
        public void ResumeGame()
        {
            PauseManager.Instance.ResumeGame();
        }

        /// <summary>
        /// Pauses or unpauses the game based on the current state
        /// </summary>
        public void TogglePause()
        {
            PauseManager.Instance.TogglePause();
        }

        /// <summary>
        /// Force the time of the game to 1
        /// </summary>
        public void ForceResumeGame()
        {
            PauseManager.Instance.ForceResumeTime();
        }

        /// <summary>
        /// Sets the master volume for music in game to the given value
        /// </summary>
        /// <param name="value">Volume between 0-1 to be set</param>
        public void SetMasterVolume(float value)
        {
            SettingsManager.Instance.SetMasterVolume(value);
        }

        /// <summary>
        /// Sets volume for music in game to the given value
        /// </summary>
        /// <param name="value">Volume between 0-1 to be set</param>
        public void SetMusicVolume(float value)
        {
            SettingsManager.Instance.SetMusicVolume(value);
        }

        /// <summary>
        /// Sets volume for SFX in game to the given value
        /// </summary>
        /// <param name="value">Volume between 0-1 to be set for SFX</param>
        public void SetSfxVolume(float value)
        {
            SettingsManager.Instance.SetSfxVolume(value);
        }

        /// <summary>
        /// Sets the screen resolution to the given index from the list:
        ///    1280, 720
        ///    1366, 768
        ///    1600, 900
        ///    1920, 1080
        ///    2560, 1440
        /// </summary>
        /// <param name="index"></param>
        public void SetResolution(int index)
        {
            SettingsManager.Instance.SetResolution(index);
        }

        /// <summary>
        /// Toggles V-Sync to the given bool
        /// </summary>
        /// <param name="isOn">Whether V-Sync should be toggled on</param>
        public void SetVSync(bool isOn)
        {
            SettingsManager.Instance.SetVSync(isOn);
        }

        /// <summary>
        /// Sets mouse sensitivity to the given value
        /// </summary>
        /// <param name="value">Value from 0-1 representing mouse sensitivity</param>
        public void SetMouseSensitivity(float value)
        {
            SettingsManager.Instance.SetMouseSensitivity(value);
        }
    }
}
