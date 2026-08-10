using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietStatic.Toolkit.Settings
{
    /// <summary>Loads and resets saved Input System binding overrides for a game's action asset.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Settings/Input Binding Overrides Loader")]
    public sealed class InputBindingOverridesLoader : MonoBehaviour
    {
        [Tooltip("Game-wide InputActionAsset that receives saved binding overrides during Awake.")]
        [SerializeField] private InputActionAsset inputActions;

        private void Awake() => InputRebindControl.LoadOverrides(inputActions);

        /// <summary>UnityEvent entry point that restores every binding in the assigned asset.</summary>
        public void ResetAllBindings() => InputRebindControl.ResetOverrides(inputActions);
    }
}
