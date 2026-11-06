using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Settings
{
    /// <summary>Binds a small, presentation-neutral settings menu to the persistent settings manager.</summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/Settings Menu View")]
    public sealed class SettingsMenuView : MonoBehaviour
    {
        [Header("Controls")]
        [Tooltip("Normalized master volume from zero to one.")]
        [SerializeField] private Slider masterVolume;
        [Tooltip("Normalized music volume from zero to one.")]
        [SerializeField] private Slider musicVolume;
        [Tooltip("Normalized sound-effects volume from zero to one.")]
        [SerializeField] private Slider sfxVolume;
        [Tooltip("Normalized player look sensitivity.")]
        [SerializeField] private Slider lookSensitivity;
        [Tooltip("Enables one vertical-sync interval when selected.")]
        [SerializeField] private Toggle vSync;

        [Tooltip("Display size selected from the settings manager's configured options.")]
        [SerializeField] private TMP_Dropdown resolution;

        [Tooltip("Commits the staged control values and saves them.")]
        [SerializeField] private Button applyChanges;

        private bool hasPendingChanges;
        private bool isApplying;

        [Header("Navigation")]
        [Tooltip("Raised when the menu's Back button is pressed. Parent menus decide what to show next.")]
        [SerializeField] private UnityEvent backRequested = new();

        /// <summary>Event raised by the neutral Back button for its containing menu.</summary>
        public UnityEvent BackRequested => backRequested;

        [Header("Value readouts")]
        [Tooltip("Optional readouts. Missing readouts are created beside the corresponding slider.")]
        [SerializeField] private TMP_Text masterVolumeValue;
        [SerializeField] private TMP_Text musicVolumeValue;
        [SerializeField] private TMP_Text sfxVolumeValue;
        [SerializeField] private TMP_Text lookSensitivityValue;

        private void Awake()
        {
            masterVolumeValue = EnsureReadout(masterVolume, masterVolumeValue);
            musicVolumeValue = EnsureReadout(musicVolume, musicVolumeValue);
            sfxVolumeValue = EnsureReadout(sfxVolume, sfxVolumeValue);
            lookSensitivityValue = EnsureReadout(lookSensitivity, lookSensitivityValue);
            masterVolume?.onValueChanged.AddListener(value => SetReadout(masterVolumeValue, value, true));
            musicVolume?.onValueChanged.AddListener(value => SetReadout(musicVolumeValue, value, true));
            sfxVolume?.onValueChanged.AddListener(value => SetReadout(sfxVolumeValue, value, true));
            lookSensitivity?.onValueChanged.AddListener(value => SetReadout(lookSensitivityValue, value, false));
            masterVolume?.onValueChanged.AddListener(_ => MarkPending());
            musicVolume?.onValueChanged.AddListener(_ => MarkPending());
            sfxVolume?.onValueChanged.AddListener(_ => MarkPending());
            lookSensitivity?.onValueChanged.AddListener(_ => MarkPending());
            vSync?.onValueChanged.AddListener(_ => MarkPending());
            resolution?.onValueChanged.AddListener(_ => MarkPending());
            applyChanges?.onClick.AddListener(ApplyChanges);
        }

        private SettingsManager observedManager;

        private void Start() => BindManager();

        private void Update()
        {
            if (observedManager != SettingsManager.Instance) BindManager();
        }

        private void BindManager()
        {
            OnDisable();
            observedManager = SettingsManager.Instance;
            if (observedManager != null)
            {
                observedManager.SettingsLoaded += Refresh;
                observedManager.SettingChanged += HandleSettingChanged;
            }
            Refresh();
            UpdateApplyButton();
        }

        private void OnEnable()
        {
            hasPendingChanges = false;
            BindManager();
        }

        private void OnDisable()
        {
            if (observedManager != null)
            {
                observedManager.SettingsLoaded -= Refresh;
                observedManager.SettingChanged -= HandleSettingChanged;
                observedManager = null;
            }
        }

        /// <summary>Refreshes every control without invoking its change callback.</summary>
        public void Refresh()
        {
            if (isApplying || hasPendingChanges) return;
            SettingsManager manager = SettingsManager.Instance;
            UpdateApplyButton();
            // A menu can become visible before the additive System scene has loaded.
            // Show saved preferences until its manager becomes available.
            RefreshSlider(masterVolume, masterVolumeValue, manager != null ? manager.MasterVolume : PlayerPrefs.GetFloat("Settings_MasterVolume", 1f), true);
            RefreshSlider(musicVolume, musicVolumeValue, manager != null ? manager.MusicVolume : PlayerPrefs.GetFloat("Settings_MusicVolume", 1f), true);
            RefreshSlider(sfxVolume, sfxVolumeValue, manager != null ? manager.SfxVolume : PlayerPrefs.GetFloat("Settings_SfxVolume", 1f), true);
            RefreshSlider(lookSensitivity, lookSensitivityValue, manager != null ? manager.MouseSensitivity : PlayerPrefs.GetFloat("Settings_MouseSensitivity", 1f), false);
            if (manager == null) return;
            vSync?.SetIsOnWithoutNotify(manager.VSyncEnabled);
            if (resolution != null)
            {
                var labels = manager.ResolutionOptions?.Select(option => $"Resolution: {option.width} x {option.height}").ToList();
                resolution.ClearOptions();
                if (labels != null) resolution.AddOptions(labels);
                resolution.SetValueWithoutNotify(manager.ResolutionIndex);
                resolution.RefreshShownValue();
            }
        }

        private void MarkPending()
        {
            hasPendingChanges = true;
            UpdateApplyButton();
        }

        private void UpdateApplyButton(string message = null)
        {
            if (applyChanges == null) return;
            applyChanges.interactable = SettingsManager.Instance != null && hasPendingChanges && !isApplying;
            TMP_Text label = applyChanges.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = message ?? (SettingsManager.Instance == null ? "Settings unavailable" : "Apply Changes");
        }

        /// <summary>Applies and saves all staged values together. Back discards uncommitted edits.</summary>
        public void ApplyChanges()
        {
            SettingsManager manager = SettingsManager.Instance;
            if (manager == null || !hasPendingChanges || isApplying)
            {
                UpdateApplyButton();
                return;
            }

            // Manager setters broadcast synchronously. Keep those events from resetting
            // the remaining controls before all staged values have been committed.
            isApplying = true;
            try
            {
                if (masterVolume != null) manager.SetMasterVolume(masterVolume.value);
                if (musicVolume != null) manager.SetMusicVolume(musicVolume.value);
                if (sfxVolume != null) manager.SetSfxVolume(sfxVolume.value);
                if (lookSensitivity != null) manager.SetMouseSensitivity(lookSensitivity.value);
                if (vSync != null) manager.SetVSync(vSync.isOn);
                if (resolution != null && resolution.options.Count > 0) manager.SetResolution(resolution.value);
                hasPendingChanges = false;
            }
            finally { isApplying = false; }
            Refresh();
            UpdateApplyButton("Changes Applied");
        }

        private static void RefreshSlider(Slider slider, TMP_Text readout, float value, bool percentage)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value);
            SetReadout(readout, slider.value, percentage);
        }

        private static void SetReadout(TMP_Text readout, float value, bool percentage)
        {
            if (readout != null)
                readout.text = percentage ? $"{Mathf.RoundToInt(value * 100f)}%" : value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static TMP_Text EnsureReadout(Slider slider, TMP_Text readout)
        {
            if (slider == null || readout != null) return readout;
            Transform existing = slider.transform.parent.Find("Value");
            if (existing != null) return existing.GetComponent<TMP_Text>();
            GameObject item = new("Value", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            item.transform.SetParent(slider.transform.parent, false);
            item.transform.SetSiblingIndex(slider.transform.GetSiblingIndex() + 1);
            LayoutElement layout = item.GetComponent<LayoutElement>();
            layout.minWidth = 64f;
            layout.preferredWidth = 64f;
            layout.flexibleWidth = 0f;
            TMP_Text text = item.GetComponent<TMP_Text>();
            TMP_Text label = slider.transform.parent.GetComponentInChildren<TMP_Text>();
            if (label != null && label != text) text.font = label.font;
            text.fontSize = 16f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Requests that the parent menu return to its previous page.</summary>
        public void RequestBack()
        {
            hasPendingChanges = false;
            Refresh();
            backRequested?.Invoke();
        }

        private void HandleSettingChanged(GameSettingId _) => Refresh();
    }
}
