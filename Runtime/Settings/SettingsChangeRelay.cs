using System;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Settings
{
    /// <summary>Converts settings notifications into Inspector-configured UnityEvents.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Settings/Settings Change Relay")]
    public sealed class SettingsChangeRelay : MonoBehaviour
    {
        [Serializable]
        private sealed class Binding
        {
            [Tooltip("Setting whose changes invoke this binding.")]
            [SerializeField] private GameSettingId setting;
            [Tooltip("Scene-local response invoked after the selected setting is applied.")]
            [SerializeField] private UnityEvent onChanged;
            public void InvokeIf(GameSettingId changed) { if (setting == changed) onChanged?.Invoke(); }
        }

        [Tooltip("Per-setting Inspector event bindings.")]
        [SerializeField] private Binding[] bindings;
        [Tooltip("Invoked after every settings change, before the matching binding.")]
        [SerializeField] private UnityEvent onAnySettingChanged;

        private SettingsManager observedManager;

        private void OnEnable()
        {
            observedManager = SettingsManager.Instance;
            if (observedManager != null)
            {
                observedManager.SettingChanged += HandleChanged;
            }
        }

        private void OnDisable()
        {
            if (observedManager != null)
            {
                observedManager.SettingChanged -= HandleChanged;
                observedManager = null;
            }
        }

        private void HandleChanged(GameSettingId setting)
        {
            onAnySettingChanged?.Invoke();
            if (bindings == null) return;
            foreach (Binding binding in bindings) binding?.InvokeIf(setting);
        }
    }
}
