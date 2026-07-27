using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietStatic
{
    /// <summary>
    /// Connects settings UI controls to audio, display, brightness, and mouse preferences.
    /// </summary>
    /// <remarks>
    /// Values are stored in <see cref="PlayerPrefs"/> and applied on startup. Place one instance
    /// in the persistent UI or System scene. Mixer parameters must match the documented names,
    /// and brightness requires a Volume profile containing a Color Adjustments override.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Managers/Settings Manager UI")]
    public class SettingsManager : MonoBehaviour
    {
        /// <summary>Gets the active settings UI manager.</summary>
        public static SettingsManager Instance { get; private set; }

        /// <summary>Raised after the normalized mouse sensitivity changes.</summary>
        public static event Action<float> OnMouseSensitivityChanged;

        [Header("Audio Mixer")]
        [Tooltip("Mixer containing exposed MasterVolume, MusicVolume, and SfxVolume parameters.")]
        [SerializeField] private AudioMixer audioMixer;

        [Header("Audio Sliders")]
        [Tooltip("Normalized zero-to-one master volume slider.")]
        [SerializeField] private Slider masterVolumeSlider;
        [Tooltip("Normalized zero-to-one music volume slider.")]
        [SerializeField] private Slider musicVolumeSlider;
        [Tooltip("Normalized zero-to-one sound-effects volume slider.")]
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Video")]
        [Tooltip("Dropdown populated from Resolution Options at startup.")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [Tooltip("Toggle that enables one VSync interval when selected.")]
        [SerializeField] private Toggle vSyncToggle;

        [Header("Brightness")]
        [Tooltip("Post-exposure slider. Requires Color Adjustments on a loaded Volume profile.")]
        [SerializeField] private Slider brightnessSlider;

        private Volume globalVolume;
        private ColorAdjustments colorAdjustments;

        [Header("Gameplay")]
        [Tooltip("Normalized look sensitivity control broadcast to player look systems.")]
        [SerializeField] private Slider mouseSensitivitySlider;

        [Header("Resolution Options")]
        [Tooltip("Ordered resolutions displayed by the resolution dropdown.")]
        [SerializeField]
        private ResolutionOption[] resolutionOptions =
        {
            new ResolutionOption(1280, 720),
            new ResolutionOption(1366, 768),
            new ResolutionOption(1600, 900),
            new ResolutionOption(1920, 1080),
            new ResolutionOption(2560, 1440)
        };

        private const string MasterVolumeKey = "Settings_MasterVolume";
        private const string MusicVolumeKey = "Settings_MusicVolume";
        private const string SfxVolumeKey = "Settings_SfxVolume";
        private const string ResolutionIndexKey = "Settings_ResolutionIndex";
        private const string VSyncKey = "Settings_VSync";
        private const string MouseSensitivityKey = "Settings_MouseSensitivity";
        private const string BrightnessKey = "Settings_Brightness";

        private const string MasterVolumeParam = "MasterVolume";
        private const string MusicVolumeParam = "MusicVolume";
        private const string SfxVolumeParam = "SfxVolume";

        /// <summary>Gets the currently applied mouse sensitivity.</summary>
        public float MouseSensitivity { get; private set; } = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            SetupResolutionDropdown();
            SetupBrightnessVolume();
            LoadSettings();
            HookupUIEvents();
        }

        private void HookupUIEvents()
        {
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);

            if (brightnessSlider != null)
                brightnessSlider.onValueChanged.AddListener(SetBrightness);

            resolutionDropdown.onValueChanged.AddListener(SetResolution);
            vSyncToggle.onValueChanged.AddListener(SetVSync);

            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
        }

        private void SetupResolutionDropdown()
        {
            resolutionDropdown.ClearOptions();

            for (int i = 0; i < resolutionOptions.Length; i++)
            {
                ResolutionOption option = resolutionOptions[i];
                resolutionDropdown.options.Add(new TMP_Dropdown.OptionData(option.GetLabel()));
            }

            resolutionDropdown.RefreshShownValue();
        }

        private void LoadSettings()
        {
            float masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            float musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            float sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);

            int resolutionIndex = PlayerPrefs.GetInt(ResolutionIndexKey, GetDefaultResolutionIndex());
            bool vSync = PlayerPrefs.GetInt(VSyncKey, 1) == 1;
            float brightness = PlayerPrefs.GetFloat(BrightnessKey, 0f);

            float mouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, 1f);

            masterVolumeSlider.SetValueWithoutNotify(masterVolume);
            musicVolumeSlider.SetValueWithoutNotify(musicVolume);
            sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);

            if (brightnessSlider != null)
                brightnessSlider.SetValueWithoutNotify(brightness);

            resolutionDropdown.SetValueWithoutNotify(resolutionIndex);
            vSyncToggle.SetIsOnWithoutNotify(vSync);

            mouseSensitivitySlider.SetValueWithoutNotify(mouseSensitivity);

            ApplyMasterVolume(masterVolume);
            ApplyMusicVolume(musicVolume);
            ApplySfxVolume(sfxVolume);
            ApplyResolution(resolutionIndex);
            ApplyVSync(vSync);
            ApplyMouseSensitivity(mouseSensitivity);
            ApplyBrightness(brightness);
        }

        /// <summary>Applies and saves the post-exposure brightness value.</summary>
        public void SetBrightness(float value)
        {
            ApplyBrightness(value);
            PlayerPrefs.SetFloat(BrightnessKey, value);
            PlayerPrefs.Save();
        }

        private void ApplyBrightness(float value)
        {
            if (colorAdjustments == null)
            {
                Debug.LogWarning($"{nameof(SettingsManager)} cannot apply brightness because ColorAdjustments is null.");
                return;
            }

            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = value;

            Debug.Log($"Brightness applied: {value}");
        }

        private void SetupBrightnessVolume()
        {
            globalVolume = GetComponent<Volume>();

            if (globalVolume == null)
            {
                globalVolume = FindFirstObjectByType<Volume>();
            }

            if (globalVolume == null)
            {
                Debug.LogWarning($"{nameof(SettingsManager)} could not find any Volume in the scene.");
                return;
            }

            if (globalVolume.profile == null)
            {
                Debug.LogWarning($"{nameof(SettingsManager)} found a Volume, but it has no profile assigned.");
                return;
            }

            if (!globalVolume.profile.TryGet(out colorAdjustments))
            {
                Debug.LogWarning($"{nameof(SettingsManager)} could not find Color Adjustments on the Volume profile.");
                return;
            }

            colorAdjustments.postExposure.overrideState = true;

            Debug.Log($"{nameof(SettingsManager)} found Color Adjustments successfully.");
        }

        /// <summary>Applies and saves normalized master volume.</summary>
        public void SetMasterVolume(float value)
        {
            ApplyMasterVolume(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            PlayerPrefs.Save();
        }

        /// <summary>Applies and saves normalized music volume.</summary>
        public void SetMusicVolume(float value)
        {
            ApplyMusicVolume(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, value);
            PlayerPrefs.Save();
        }

        /// <summary>Applies and saves normalized sound-effects volume.</summary>
        public void SetSfxVolume(float value)
        {
            ApplySfxVolume(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, value);
            PlayerPrefs.Save();
        }

        /// <summary>Applies and saves a configured resolution option by index.</summary>
        public void SetResolution(int index)
        {
            ApplyResolution(index);
            PlayerPrefs.SetInt(ResolutionIndexKey, index);
            PlayerPrefs.Save();
        }

        /// <summary>Applies and saves VSync state.</summary>
        public void SetVSync(bool isOn)
        {
            ApplyVSync(isOn);
            PlayerPrefs.SetInt(VSyncKey, isOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Applies, broadcasts, and saves mouse sensitivity.</summary>
        public void SetMouseSensitivity(float value)
        {
            ApplyMouseSensitivity(value);
            PlayerPrefs.SetFloat(MouseSensitivityKey, value);
            PlayerPrefs.Save();
        }

        private void ApplyMasterVolume(float value)
        {
            SetMixerVolume(MasterVolumeParam, value);
        }

        private void ApplyMusicVolume(float value)
        {
            SetMixerVolume(MusicVolumeParam, value);
        }

        private void ApplySfxVolume(float value)
        {
            SetMixerVolume(SfxVolumeParam, value);
        }

        private void ApplyResolution(int index)
        {
            if (resolutionOptions == null || resolutionOptions.Length == 0)
                return;

            index = Mathf.Clamp(index, 0, resolutionOptions.Length - 1);

            ResolutionOption option = resolutionOptions[index];

            Screen.SetResolution(
                option.width,
                option.height,
                Screen.fullScreen
            );
        }

        private void ApplyVSync(bool isOn)
        {
            QualitySettings.vSyncCount = isOn ? 1 : 0;
        }

        private void ApplyMouseSensitivity(float value)
        {
            MouseSensitivity = value;
            OnMouseSensitivityChanged?.Invoke(value);
        }

        private void SetMixerVolume(string parameterName, float sliderValue)
        {
            if (audioMixer == null)
                return;

            sliderValue = Mathf.Clamp(sliderValue, 0.0001f, 1f);

            float volumeDb = Mathf.Log10(sliderValue) * 20f;
            audioMixer.SetFloat(parameterName, volumeDb);
        }

        private int GetDefaultResolutionIndex()
        {
            for (int i = 0; i < resolutionOptions.Length; i++)
            {
                if (resolutionOptions[i].width == Screen.currentResolution.width &&
                    resolutionOptions[i].height == Screen.currentResolution.height)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>Serializable width and height pair displayed in the resolution dropdown.</summary>
        [Serializable]
        public class ResolutionOption
        {
            public int width;
            public int height;

            public ResolutionOption(int width, int height)
            {
                this.width = width;
                this.height = height;
            }

            /// <summary>Returns a human-readable resolution label.</summary>
            public string GetLabel()
            {
                return $"{width} x {height}";
            }
        }
    }
}
