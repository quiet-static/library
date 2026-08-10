using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Settings
{
    /// <summary>Applies common accessibility preferences to scene-specific presentation objects.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Settings/Accessibility Settings Applier")]
    public sealed class AccessibilitySettingsApplier : MonoBehaviour
    {
        [Tooltip("Effects disabled while Reduced Flashing is enabled, such as light flicker scripts.")]
        [SerializeField] private Behaviour[] flashingEffects;
        [Tooltip("Effects disabled while Reduced Camera Motion is enabled, such as head bob scripts.")]
        [SerializeField] private Behaviour[] cameraMotionEffects;
        [Tooltip("Subtitle text elements whose point size follows the selected subtitle size.")]
        [SerializeField] private TMP_Text[] subtitleTexts;
        [Tooltip("Speaker-label roots hidden when speaker labels are Off.")]
        [SerializeField] private GameObject[] speakerLabelObjects;
        [Tooltip("Apply the project's high-contrast prompt theme.")]
        [SerializeField] private UnityEvent onHighContrastEnabled;
        [Tooltip("Restore the project's normal interaction prompt theme.")]
        [SerializeField] private UnityEvent onHighContrastDisabled;

        private bool lastHighContrast;

        private void OnEnable()
        {
            SettingsManager.OnSettingChanged += HandleChanged;
            SettingsManager.OnSettingsLoaded += Apply;
            Apply();
        }

        private void OnDisable()
        {
            SettingsManager.OnSettingChanged -= HandleChanged;
            SettingsManager.OnSettingsLoaded -= Apply;
        }

        public void Apply()
        {
            SettingsManager manager = SettingsManager.Instance;
            if (manager == null) return;
            SetEnabled(flashingEffects, !manager.ReducedFlashingEnabled);
            SetEnabled(cameraMotionEffects, !manager.ReducedCameraMotionEnabled);
            float fontSize = manager.SubtitleSize switch
            {
                SubtitleTextSize.Small => 24f,
                SubtitleTextSize.Large => 40f,
                SubtitleTextSize.ExtraLarge => 48f,
                _ => 32f
            };
            if (subtitleTexts != null)
                foreach (TMP_Text text in subtitleTexts) if (text != null) text.fontSize = fontSize;
            bool labelsVisible = manager.SpeakerLabels != SpeakerLabelMode.Off;
            if (speakerLabelObjects != null)
                foreach (GameObject item in speakerLabelObjects) if (item != null) item.SetActive(labelsVisible);
            if (lastHighContrast != manager.HighContrastPromptsEnabled)
            {
                lastHighContrast = manager.HighContrastPromptsEnabled;
                if (lastHighContrast) onHighContrastEnabled?.Invoke();
                else onHighContrastDisabled?.Invoke();
            }
        }

        private void HandleChanged(GameSettingId _) => Apply();

        private static void SetEnabled(Behaviour[] targets, bool enabled)
        {
            if (targets == null) return;
            foreach (Behaviour target in targets) if (target != null) target.enabled = enabled;
        }
    }
}
