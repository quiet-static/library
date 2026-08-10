using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Settings
{
    /// <summary>Reusable UI row that interactively rebinds one Input System binding.</summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/Input Rebind Control")]
    public sealed class InputRebindControl : MonoBehaviour
    {
        private const string OverridesKey = "Settings_InputBindingOverrides";

        [Tooltip("Input System action whose binding will be displayed and rebound.")]
        [SerializeField] private InputActionReference action;
        [Tooltip("Index within the action bindings collection. Composite parts count as bindings.")]
        [Min(0)] [SerializeField] private int bindingIndex;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Text bindingLabel;
        [Tooltip("Button that begins interactive rebinding.")]
        [SerializeField] private Button rebindButton;
        [Tooltip("Text displayed while waiting for the player's replacement input.")]
        [SerializeField] private string waitingText = "Press a control...";

        private InputActionRebindingExtensions.RebindingOperation operation;

        private void Awake()
        {
            rebindButton?.onClick.AddListener(BeginRebind);
            RefreshLabel();
        }

        private void OnDestroy() => DisposeOperation();

        public void BeginRebind()
        {
            InputAction inputAction = action?.action;
            if (inputAction == null || bindingIndex >= inputAction.bindings.Count) return;
            DisposeOperation();
            bool wasEnabled = inputAction.enabled;
            inputAction.Disable();
            if (bindingLabel != null) bindingLabel.text = waitingText;
            operation = inputAction.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => Complete(wasEnabled))
                .OnComplete(_ => Complete(wasEnabled));
            operation.Start();
        }

        public void RefreshLabel()
        {
            InputAction inputAction = action?.action;
            if (actionLabel != null) actionLabel.text = inputAction?.name ?? "Unassigned Action";
            if (bindingLabel != null)
                bindingLabel.text = inputAction != null && bindingIndex < inputAction.bindings.Count
                    ? inputAction.GetBindingDisplayString(bindingIndex)
                    : "Unassigned";
        }

        private void Complete(bool enableAfterward)
        {
            InputAction inputAction = action?.action;
            DisposeOperation();
            if (enableAfterward) inputAction?.Enable();
            SaveOverrides(inputAction?.actionMap?.asset);
            RefreshLabel();
        }

        private void DisposeOperation()
        {
            operation?.Dispose();
            operation = null;
        }

        public static void LoadOverrides(InputActionAsset asset)
        {
            string json = PlayerPrefs.GetString(OverridesKey, string.Empty);
            if (asset != null && !string.IsNullOrWhiteSpace(json)) asset.LoadBindingOverridesFromJson(json);
        }

        public static void SaveOverrides(InputActionAsset asset)
        {
            if (asset == null) return;
            PlayerPrefs.SetString(OverridesKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetOverrides(InputActionAsset asset)
        {
            asset?.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverridesKey);
            PlayerPrefs.Save();
        }
    }
}
