using UnityEngine;
using UnityEngine.InputSystem;

namespace QuietStatic.Toolkit.Input
{
    /// <summary>
    /// Reads Inspector-configured <see cref="KeyCode"/> shortcuts through Unity's Input System.
    /// </summary>
    internal static class KeyboardShortcut
    {
        /// <summary>Returns whether the configured key was pressed during the current frame.</summary>
        public static bool WasPressedThisFrame(KeyCode keyCode)
        {
            return TryGetKeyControl(keyCode, out var keyControl) && keyControl.wasPressedThisFrame;
        }

        /// <summary>Returns whether the configured key was released during the current frame.</summary>
        public static bool WasReleasedThisFrame(KeyCode keyCode)
        {
            return TryGetKeyControl(keyCode, out var keyControl) && keyControl.wasReleasedThisFrame;
        }

        private static bool TryGetKeyControl(KeyCode keyCode, out UnityEngine.InputSystem.Controls.KeyControl keyControl)
        {
            keyControl = null;
            var keyboard = Keyboard.current;
            if (keyboard == null || !TryConvertKey(keyCode, out var key))
            {
                return false;
            }

            keyControl = keyboard[key];
            return keyControl != null;
        }

        private static bool TryConvertKey(KeyCode keyCode, out Key key)
        {
            var keyName = keyCode.ToString();
            if (keyName.StartsWith("Alpha"))
            {
                keyName = "Digit" + keyName.Substring("Alpha".Length);
            }

            return System.Enum.TryParse(keyName, true, out key) && key != Key.None;
        }
    }
}
