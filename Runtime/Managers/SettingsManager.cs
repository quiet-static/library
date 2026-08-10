using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietStatic
{
    public enum SubtitleTextSize { Small, Medium, Large, ExtraLarge }
    public enum SpeakerLabelMode { Off, DialogueOnly, Always }
    public enum InteractionInputMode { Hold, Toggle }

    /// <summary>Stable identifiers published when an individual preference changes.</summary>
    public enum GameSettingId
    {
        MasterVolume, MusicVolume, SfxVolume, AmbienceVolume, DialogueVolume,
        MouseSensitivity, VSync, SubtitleTextSize, SpeakerLabels, ClosedCaptions,
        ReducedFlashing, ReducedCameraMotion, InteractionInputMode,
        HighContrastPrompts, ContentWarning
    }

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

        /// <summary>Raised after any setting is applied, including during initial loading.</summary>
        public static event Action<GameSettingId> OnSettingChanged;

        /// <summary>Raised once all saved preferences have been applied.</summary>
        public static event Action OnSettingsLoaded;

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
        private const string AmbienceVolumeKey = "Settings_AmbienceVolume";
        private const string DialogueVolumeKey = "Settings_DialogueVolume";
        private const string SubtitleTextSizeKey = "Settings_SubtitleTextSize";
        private const string SpeakerLabelsKey = "Settings_SpeakerLabels";
        private const string ClosedCaptionsKey = "Settings_ClosedCaptions";
        private const string ReducedFlashingKey = "Settings_ReducedFlashing";
        private const string ReducedCameraMotionKey = "Settings_ReducedCameraMotion";
        private const string InteractionInputModeKey = "Settings_InteractionInputMode";
        private const string HighContrastPromptsKey = "Settings_HighContrastPrompts";
        private const string ContentWarningKey = "Settings_ContentWarning";

        private const string MasterVolumeParam = "MasterVolume";
        private const string MusicVolumeParam = "MusicVolume";
        private const string SfxVolumeParam = "SfxVolume";
        private const string AmbienceVolumeParam = "AmbienceVolume";
        private const string DialogueVolumeParam = "DialogueVolume";

        /// <summary>Gets the currently applied mouse sensitivity.</summary>
        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public bool VSyncEnabled { get; private set; } = true;
        public float MouseSensitivity { get; private set; } = 1f;
        public float AmbienceVolume { get; private set; } = 1f;
        public float DialogueVolume { get; private set; } = 1f;
        public SubtitleTextSize SubtitleSize { get; private set; } = SubtitleTextSize.Medium;
        public SpeakerLabelMode SpeakerLabels { get; private set; } = SpeakerLabelMode.DialogueOnly;
        public bool ClosedCaptionsEnabled { get; private set; } = true;
        public bool ReducedFlashingEnabled { get; private set; }
        public bool ReducedCameraMotionEnabled { get; private set; }
        public InteractionInputMode InteractionMode { get; private set; } = InteractionInputMode.Hold;
        public bool HighContrastPromptsEnabled { get; private set; }
        public bool ContentWarningEnabled { get; private set; } = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);

            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);

            if (brightnessSlider != null)
                brightnessSlider.onValueChanged.AddListener(SetBrightness);

            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(SetResolution);

            if (vSyncToggle != null)
                vSyncToggle.onValueChanged.AddListener(SetVSync);

            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
        }

        private void SetupResolutionDropdown()
        {
            if (resolutionDropdown == null ||
                resolutionOptions == null ||
                resolutionOptions.Length == 0)
            {
                return;
            }

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
            float ambienceVolume = PlayerPrefs.GetFloat(AmbienceVolumeKey, 1f);
            float dialogueVolume = PlayerPrefs.GetFloat(DialogueVolumeKey, 1f);

            if (masterVolumeSlider != null)
                masterVolumeSlider.SetValueWithoutNotify(masterVolume);

            if (musicVolumeSlider != null)
                musicVolumeSlider.SetValueWithoutNotify(musicVolume);

            if (sfxVolumeSlider != null)
                sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);

            if (brightnessSlider != null)
                brightnessSlider.SetValueWithoutNotify(brightness);

            if (resolutionDropdown != null)
                resolutionDropdown.SetValueWithoutNotify(resolutionIndex);

            if (vSyncToggle != null)
                vSyncToggle.SetIsOnWithoutNotify(vSync);

            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.SetValueWithoutNotify(mouseSensitivity);

            ApplyMasterVolume(masterVolume);
            ApplyMusicVolume(musicVolume);
            ApplySfxVolume(sfxVolume);
            ApplyResolution(resolutionIndex);
            ApplyVSync(vSync);
            ApplyMouseSensitivity(mouseSensitivity);
            ApplyBrightness(brightness);
            ApplyAmbienceVolume(ambienceVolume);
            ApplyDialogueVolume(dialogueVolume);
            ApplySubtitleTextSize((SubtitleTextSize)PlayerPrefs.GetInt(SubtitleTextSizeKey, (int)SubtitleTextSize.Medium));
            ApplySpeakerLabels((SpeakerLabelMode)PlayerPrefs.GetInt(SpeakerLabelsKey, (int)SpeakerLabelMode.DialogueOnly));
            ApplyClosedCaptions(PlayerPrefs.GetInt(ClosedCaptionsKey, 1) == 1);
            ApplyReducedFlashing(PlayerPrefs.GetInt(ReducedFlashingKey, 0) == 1);
            ApplyReducedCameraMotion(PlayerPrefs.GetInt(ReducedCameraMotionKey, 0) == 1);
            ApplyInteractionInputMode((InteractionInputMode)PlayerPrefs.GetInt(InteractionInputModeKey, 0));
            ApplyHighContrastPrompts(PlayerPrefs.GetInt(HighContrastPromptsKey, 0) == 1);
            ApplyContentWarning(PlayerPrefs.GetInt(ContentWarningKey, 1) == 1);
            OnSettingsLoaded?.Invoke();
        }

        public void SetAmbienceVolume(float value) => SaveFloat(AmbienceVolumeKey, value, ApplyAmbienceVolume);
        public void SetDialogueVolume(float value) => SaveFloat(DialogueVolumeKey, value, ApplyDialogueVolume);
        public void SetSubtitleTextSize(int value) => SaveInt(SubtitleTextSizeKey, value, v => ApplySubtitleTextSize((SubtitleTextSize)v));
        public void SetSpeakerLabels(int value) => SaveInt(SpeakerLabelsKey, value, v => ApplySpeakerLabels((SpeakerLabelMode)v));
        public void SetClosedCaptions(bool value) => SaveBool(ClosedCaptionsKey, value, ApplyClosedCaptions);
        public void SetReducedFlashing(bool value) => SaveBool(ReducedFlashingKey, value, ApplyReducedFlashing);
        public void SetReducedCameraMotion(bool value) => SaveBool(ReducedCameraMotionKey, value, ApplyReducedCameraMotion);
        public void SetInteractionInputMode(int value) => SaveInt(InteractionInputModeKey, value, v => ApplyInteractionInputMode((InteractionInputMode)v));
        public void SetHighContrastPrompts(bool value) => SaveBool(HighContrastPromptsKey, value, ApplyHighContrastPrompts);
        public void SetContentWarning(bool value) => SaveBool(ContentWarningKey, value, ApplyContentWarning);

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
                GameLogger.Warning(nameof(SettingsManager), this,
                    "Cannot apply brightness because ColorAdjustments is null.");
                return;
            }

            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = value;

            GameLogger.Log(nameof(SettingsManager), this, $"Brightness applied: {value}");
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
                GameLogger.Warning(nameof(SettingsManager), this,
                    "Could not find any Volume in the scene.");
                return;
            }

            if (globalVolume.profile == null)
            {
                GameLogger.Warning(nameof(SettingsManager), this,
                    "Found a Volume, but it has no profile assigned.");
                return;
            }

            if (!globalVolume.profile.TryGet(out colorAdjustments))
            {
                GameLogger.Warning(nameof(SettingsManager), this,
                    "Could not find Color Adjustments on the Volume profile.");
                return;
            }

            colorAdjustments.postExposure.overrideState = true;

            GameLogger.Log(nameof(SettingsManager), this,
                "Found Color Adjustments successfully.");
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
            MasterVolume = Mathf.Clamp01(value);
            SetMixerVolume(MasterVolumeParam, value);
            Notify(GameSettingId.MasterVolume);
        }

        private void ApplyMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            SetMixerVolume(MusicVolumeParam, value);
            Notify(GameSettingId.MusicVolume);
        }

        private void ApplySfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            SetMixerVolume(SfxVolumeParam, value);
            Notify(GameSettingId.SfxVolume);
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
            VSyncEnabled = isOn;
            QualitySettings.vSyncCount = isOn ? 1 : 0;
            Notify(GameSettingId.VSync);
        }

        private void ApplyMouseSensitivity(float value)
        {
            MouseSensitivity = value;
            OnMouseSensitivityChanged?.Invoke(value);
            Notify(GameSettingId.MouseSensitivity);
        }

        private void ApplyAmbienceVolume(float value)
        {
            AmbienceVolume = Mathf.Clamp01(value);
            SetMixerVolume(AmbienceVolumeParam, AmbienceVolume);
            Notify(GameSettingId.AmbienceVolume);
        }

        private void ApplyDialogueVolume(float value)
        {
            DialogueVolume = Mathf.Clamp01(value);
            SetMixerVolume(DialogueVolumeParam, DialogueVolume);
            Notify(GameSettingId.DialogueVolume);
        }

        private void ApplySubtitleTextSize(SubtitleTextSize value)
        {
            SubtitleSize = ClampEnum(value, SubtitleTextSize.Medium);
            Notify(GameSettingId.SubtitleTextSize);
        }

        private void ApplySpeakerLabels(SpeakerLabelMode value)
        {
            SpeakerLabels = ClampEnum(value, SpeakerLabelMode.DialogueOnly);
            Notify(GameSettingId.SpeakerLabels);
        }

        private void ApplyClosedCaptions(bool value) { ClosedCaptionsEnabled = value; Notify(GameSettingId.ClosedCaptions); }
        private void ApplyReducedFlashing(bool value) { ReducedFlashingEnabled = value; Notify(GameSettingId.ReducedFlashing); }
        private void ApplyReducedCameraMotion(bool value) { ReducedCameraMotionEnabled = value; Notify(GameSettingId.ReducedCameraMotion); }
        private void ApplyInteractionInputMode(InteractionInputMode value)
        {
            InteractionMode = ClampEnum(value, InteractionInputMode.Hold);
            Notify(GameSettingId.InteractionInputMode);
        }
        private void ApplyHighContrastPrompts(bool value) { HighContrastPromptsEnabled = value; Notify(GameSettingId.HighContrastPrompts); }
        private void ApplyContentWarning(bool value) { ContentWarningEnabled = value; Notify(GameSettingId.ContentWarning); }

        private static T ClampEnum<T>(T value, T fallback) where T : struct, Enum =>
            Enum.IsDefined(typeof(T), value) ? value : fallback;

        private static void Notify(GameSettingId id) => OnSettingChanged?.Invoke(id);

        private static void SaveFloat(string key, float value, Action<float> apply)
        {
            value = Mathf.Clamp01(value);
            apply(value);
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        private static void SaveInt(string key, int value, Action<int> apply)
        {
            apply(value);
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        private static void SaveBool(string key, bool value, Action<bool> apply) =>
            SaveInt(key, value ? 1 : 0, raw => apply(raw == 1));

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
            if (resolutionOptions == null || resolutionOptions.Length == 0)
            {
                return 0;
            }

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
            /// <summary>Resolution width in pixels.</summary>
            public int width;

            /// <summary>Resolution height in pixels.</summary>
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
