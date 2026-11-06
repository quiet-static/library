using UnityEngine;

namespace QuietStatic.Toolkit.Flags
{
    /// <summary>
    /// Inspector-friendly helper for setting one or more progression flags.
    /// </summary>
    /// <remarks>
    /// This component is useful when a flag needs to be set from a UnityEvent,
    /// animation event, trigger callback, timeline signal, button press, or other
    /// Inspector-configured interaction.
    ///
    /// The actual flag storage is handled by <see cref="FlagManager"/>. This class only
    /// forwards configured flag ids to the active <see cref="FlagManager.Instance"/>.
    /// </remarks>
    public class FlagHandler : MonoBehaviour
    {
        [Header("Flags")]
        [Tooltip("Flags that will be set when SetFlags is called. Blank entries are ignored by FlagManager.")]
        [QuietStatic.Toolkit.Flags.FlagId]
        [SerializeField] private string[] flagsToSet;

        [Tooltip("Typed assignments applied by SetValues. Values must match their catalog types.")]
        [SerializeField] private FlagValueEntry[] valuesToSet;

        [FlagId, Tooltip("Typed value targeted by the single-value UnityEvent methods below.")]
        [SerializeField] private string valueFlagId;

        /// <summary>Applies configured typed assignments through the shared manager.</summary>
        public void SetValues()
        {
            if (FlagManager.Instance == null) return;
            foreach (var entry in valuesToSet ?? System.Array.Empty<FlagValueEntry>())
                if (entry != null) FlagManager.Instance.SetValue(entry.flagId, entry.value);
        }
        /// <summary>Sets the configured Boolean value.</summary>
        public void SetBoolean(bool value) => FlagManager.Instance?.SetBoolean(valueFlagId, value);
        /// <summary>Sets the configured Integer value.</summary>
        public void SetInteger(int value) => FlagManager.Instance?.SetInteger(valueFlagId, value);
        /// <summary>Sets the configured Float value.</summary>
        public void SetFloat(float value) => FlagManager.Instance?.SetFloat(valueFlagId, value);
        /// <summary>Sets the configured String value.</summary>
        public void SetString(string value) => FlagManager.Instance?.SetString(valueFlagId, value);
        /// <summary>Adds to the configured Integer value without overflow.</summary>
        public void AddInteger(int delta) => FlagManager.Instance?.AddInteger(valueFlagId, delta);
        /// <summary>Adds to the configured Float value, rejecting nonfinite results.</summary>
        public void AddFloat(float delta) => FlagManager.Instance?.AddFloat(valueFlagId, delta);

        /// <summary>
        /// Sets every flag configured in <see cref="flagsToSet"/>.
        /// </summary>
        /// <remarks>
        /// This method is designed to be called from the Inspector, such as from
        /// a UnityEvent, animation event, trigger event, or UI button.
        ///
        /// If no <see cref="FlagManager"/> singleton exists, the method safely does nothing.
        /// </remarks>
        public void SetFlags()
        {
            if (FlagManager.Instance == null)
            {
                return;
            }

            foreach (string flag in flagsToSet ?? System.Array.Empty<string>())
            {
                FlagManager.Instance.SetFlag(flag);
            }
        }

        /// <summary>
        /// Sets a single flag by id.
        /// </summary>
        /// <param name="flagId">
        /// The flag id to set on the active <see cref="FlagManager"/>.
        /// </param>
        /// <remarks>
        /// This method is useful for UnityEvents or code paths that need to pass
        /// one specific flag id instead of using the preconfigured list.
        ///
        /// If no <see cref="FlagManager"/> singleton exists, the method safely does nothing.
        /// </remarks>
        public void SetFlag(string flagId)
        {
            if (FlagManager.Instance == null)
            {
                return;
            }

            FlagManager.Instance.SetFlag(flagId);
        }
    }
}
