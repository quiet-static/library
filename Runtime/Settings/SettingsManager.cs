using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Settings
{
    /// <summary>
    /// Stores, loads, and applies common player-facing Unity settings.
    /// </summary>
    /// <remarks>
    /// This manager intentionally stays independent from any specific settings menu UI.
    /// Buttons, sliders, toggles, dropdowns, or other UI components can call these public
    /// methods directly through code or UnityEvents.
    ///
    /// Supported settings include:
    /// - Master audio volume.
    /// - Fullscreen mode.
    /// - VSync.
    /// - Resolution selection by index.
    /// - Look sensitivity storage.
    /// - Brightness storage.
    ///
    /// Brightness is only saved by this class. To make brightness visible in-game,
    /// another system should read <see cref="GetBrightness"/> and apply it to post-processing,
    /// a fullscreen overlay, lighting, or a custom shader.
    /// </remarks>
    public class SettingsManager : MonoBehaviour
    {
        /// <summary>
        /// PlayerPrefs key used to store master volume.
        /// </summary>
        public const string MasterVolumeKey = "QS_MasterVolume";

        /// <summary>
        /// PlayerPrefs key used to store fullscreen preference.
        /// </summary>
        public const string FullscreenKey = "QS_Fullscreen";

        /// <summary>
        /// PlayerPrefs key used to store VSync preference.
        /// </summary>
        public const string VsyncKey = "QS_Vsync";

        /// <summary>
        /// PlayerPrefs key used to store look sensitivity.
        /// </summary>
        public const string SensitivityKey = "QS_LookSensitivity";

        /// <summary>
        /// PlayerPrefs key used to store brightness preference.
        /// </summary>
        public const string BrightnessKey = "QS_Brightness";

        /// <summary>
        /// Raised after saved settings have been applied through <see cref="ApplySavedSettings"/>.
        /// </summary>
        public static event Action OnSettingsApplied;

        [Header("Default Audio Settings")]
        [Tooltip("Default master volume used when no saved volume exists yet. 0 is silent and 1 is full volume.")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;

        [Header("Default Display Settings")]
        [Tooltip("Default fullscreen setting used when no saved fullscreen preference exists yet.")]
        [SerializeField] private bool defaultFullscreen = true;

        [Tooltip("Default VSync setting used when no saved VSync preference exists yet.")]
        [SerializeField] private bool defaultVsync = true;

        [Header("Default Input Settings")]
        [Tooltip("Default look sensitivity used when no saved sensitivity value exists yet.")]
        [SerializeField, Min(0.01f)] private float defaultLookSensitivity = 1f;

        [Header("Default Visual Settings")]
        [Tooltip("Default brightness value used when no saved brightness preference exists yet. This value is stored only; another system must apply it visually.")]
        [SerializeField, Range(0.25f, 2f)] private float defaultBrightness = 1f;

        /// <summary>
        /// Applies saved settings when this component starts.
        /// </summary>
        private void Start()
        {
            ApplySavedSettings();
        }

        /// <summary>
        /// Loads saved values from PlayerPrefs and applies them to Unity systems where possible.
        /// </summary>
        /// <remarks>
        /// Audio, fullscreen, and VSync are applied immediately. Look sensitivity and brightness
        /// are stored values that other gameplay or rendering systems should read and apply.
        /// </remarks>
        public void ApplySavedSettings()
        {
            SetMasterVolume(PlayerPrefs.GetFloat(MasterVolumeKey, defaultMasterVolume));
            SetFullscreen(PlayerPrefs.GetInt(FullscreenKey, defaultFullscreen ? 1 : 0) == 1);
            SetVsync(PlayerPrefs.GetInt(VsyncKey, defaultVsync ? 1 : 0) == 1);
            SetLookSensitivity(PlayerPrefs.GetFloat(SensitivityKey, defaultLookSensitivity));
            SetBrightness(PlayerPrefs.GetFloat(BrightnessKey, defaultBrightness));

            OnSettingsApplied?.Invoke();
        }

        /// <summary>
        /// Sets the global Unity master volume and saves it to PlayerPrefs.
        /// </summary>
        /// <param name="value">
        /// Volume value to apply. Values are clamped between 0 and 1.
        /// </param>
        public void SetMasterVolume(float value)
        {
            value = Mathf.Clamp01(value);
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
        }

        /// <summary>
        /// Sets fullscreen mode and saves the preference to PlayerPrefs.
        /// </summary>
        /// <param name="fullscreen">
        /// True to use fullscreen; false to use windowed mode.
        /// </param>
        public void SetFullscreen(bool fullscreen)
        {
            Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        }

        /// <summary>
        /// Enables or disables VSync and saves the preference to PlayerPrefs.
        /// </summary>
        /// <param name="enabled">
        /// True to enable VSync; false to disable it.
        /// </param>
        public void SetVsync(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.SetInt(VsyncKey, enabled ? 1 : 0);
        }

        /// <summary>
        /// Applies a resolution from <see cref="Screen.resolutions"/> by array index.
        /// </summary>
        /// <param name="index">
        /// Index into Unity's current list of supported display resolutions.
        /// Invalid indexes are ignored.
        /// </param>
        /// <remarks>
        /// This method does not currently save the selected resolution. It applies the resolution
        /// immediately using the current fullscreen mode and the selected resolution's refresh rate.
        /// </remarks>
        public void SetResolutionByIndex(int index)
        {
            Resolution[] resolutions = Screen.resolutions;

            if (index < 0 || index >= resolutions.Length)
            {
                return;
            }

            Resolution resolution = resolutions[index];
            Screen.SetResolution(
                resolution.width,
                resolution.height,
                Screen.fullScreenMode,
                resolution.refreshRateRatio
            );
        }

        /// <summary>
        /// Saves a look sensitivity value to PlayerPrefs.
        /// </summary>
        /// <param name="value">
        /// Sensitivity value to save. Values below 0.01 are raised to 0.01.
        /// </param>
        /// <remarks>
        /// Camera or input systems should read this value with <see cref="GetLookSensitivity"/>
        /// and apply it to their own sensitivity calculations.
        /// </remarks>
        public void SetLookSensitivity(float value)
        {
            PlayerPrefs.SetFloat(SensitivityKey, Mathf.Max(0.01f, value));
        }

        /// <summary>
        /// Gets the saved look sensitivity value.
        /// </summary>
        /// <returns>
        /// The saved look sensitivity, or <see cref="defaultLookSensitivity"/> if none has been saved.
        /// </returns>
        public float GetLookSensitivity()
        {
            return PlayerPrefs.GetFloat(SensitivityKey, defaultLookSensitivity);
        }

        /// <summary>
        /// Saves a brightness value to PlayerPrefs.
        /// </summary>
        /// <param name="value">
        /// Brightness value to save. Values are clamped between 0.25 and 2.
        /// </param>
        /// <remarks>
        /// This method only stores brightness. Hook the saved value into post-processing exposure,
        /// a fullscreen overlay, lighting, or a custom shader to make it affect the scene visually.
        /// </remarks>
        public void SetBrightness(float value)
        {
            PlayerPrefs.SetFloat(BrightnessKey, Mathf.Clamp(value, 0.25f, 2f));
        }

        /// <summary>
        /// Gets the saved brightness value.
        /// </summary>
        /// <returns>
        /// The saved brightness value, or <see cref="defaultBrightness"/> if none has been saved.
        /// </returns>
        public float GetBrightness()
        {
            return PlayerPrefs.GetFloat(BrightnessKey, defaultBrightness);
        }
    }
}
