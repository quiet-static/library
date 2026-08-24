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

        [Header("Navigation")]
        [Tooltip("Raised when the menu's Back button is pressed. Parent menus decide what to show next.")]
        [SerializeField] private UnityEvent backRequested = new();

        /// <summary>Event raised by the neutral Back button for its containing menu.</summary>
        public UnityEvent BackRequested => backRequested;

        private void Awake()
        {
            masterVolume?.onValueChanged.AddListener(value => SettingsManager.Instance?.SetMasterVolume(value));
            musicVolume?.onValueChanged.AddListener(value => SettingsManager.Instance?.SetMusicVolume(value));
            sfxVolume?.onValueChanged.AddListener(value => SettingsManager.Instance?.SetSfxVolume(value));
            lookSensitivity?.onValueChanged.AddListener(value => SettingsManager.Instance?.SetMouseSensitivity(value));
            vSync?.onValueChanged.AddListener(value => SettingsManager.Instance?.SetVSync(value));
        }

        private SettingsManager observedManager;

        private void OnEnable()
        {
            observedManager = SettingsManager.Instance;
            if (observedManager != null)
            {
                observedManager.SettingsLoaded += Refresh;
                observedManager.SettingChanged += HandleSettingChanged;
            }
            Refresh();
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
            SettingsManager manager = SettingsManager.Instance;
            if (manager == null) return;

            masterVolume?.SetValueWithoutNotify(manager.MasterVolume);
            musicVolume?.SetValueWithoutNotify(manager.MusicVolume);
            sfxVolume?.SetValueWithoutNotify(manager.SfxVolume);
            lookSensitivity?.SetValueWithoutNotify(manager.MouseSensitivity);
            vSync?.SetIsOnWithoutNotify(manager.VSyncEnabled);
        }

        /// <summary>Requests that the parent menu return to its previous page.</summary>
        public void RequestBack() => backRequested?.Invoke();

        private void HandleSettingChanged(GameSettingId _) => Refresh();
    }
}
