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
        [SerializeField] private string[] flagsToSet;

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

            foreach (string flag in flagsToSet)
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
