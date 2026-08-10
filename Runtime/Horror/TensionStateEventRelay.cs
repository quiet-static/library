using System;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Horror
{
    /// <summary>Invokes scene-configured events for specific tension state changes.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Horror/Tension State Event Relay")]
    public sealed class TensionStateEventRelay : MonoBehaviour
    {
        [Serializable]
        private sealed class Binding
        {
            [Tooltip("Stable tension state ID matched exactly.")]
            [SerializeField] private string stateId;
            [Tooltip("Scene-local effects started when this state is entered.")]
            [SerializeField] private UnityEvent onEntered;
            [Tooltip("Scene-local effects stopped when this state is exited.")]
            [SerializeField] private UnityEvent onExited;
            public string StateId => stateId?.Trim() ?? string.Empty;
            public void Enter() => onEntered?.Invoke();
            public void Exit() => onExited?.Invoke();
        }

        [Tooltip("Optional controller filter. Leave empty to hear every tension controller.")]
        [SerializeField] private HorrorTensionController controller;
        [Tooltip("State IDs mapped to scene-specific enter and exit responses.")]
        [SerializeField] private Binding[] bindings;

        private void OnEnable() => HorrorTensionController.StateChanged += HandleChanged;
        private void OnDisable() => HorrorTensionController.StateChanged -= HandleChanged;

        private void HandleChanged(
            HorrorTensionController changedController,
            string previous,
            string current)
        {
            if (controller != null && changedController != controller) return;
            if (bindings == null) return;
            foreach (Binding binding in bindings)
            {
                if (binding == null) continue;
                if (!string.IsNullOrEmpty(previous) && binding.StateId == previous) binding.Exit();
                if (!string.IsNullOrEmpty(current) && binding.StateId == current) binding.Enter();
            }
        }
    }
}
