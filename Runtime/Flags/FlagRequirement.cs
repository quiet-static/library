using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags
{
    public enum FlagRequirementMode
    {
        None,
        All,
        Any,
        NotAll,
        NotAny
    }

    /// <summary>
    /// Serializable progression requirement checked against a FlagManager.
    /// </summary>
    [Serializable]
    public class FlagRequirement
    {
        [Header("Requirement")]
        [Tooltip("How the listed flags should be evaluated. None always passes.")]
        [SerializeField] private FlagRequirementMode mode = FlagRequirementMode.None;

        [Tooltip("Flags checked by this requirement.")]
        [FlagId]
        [SerializeField] private string[] flags;

        public bool IsMet(FlagManager flagSet = null)
        {
            if (mode == FlagRequirementMode.None)
            {
                return true;
            }

            flagSet ??= FlagManager.Instance;

            if (flagSet == null)
            {
                return false;
            }

            return mode switch
            {
                FlagRequirementMode.All => flagSet.HasAll(flags),
                FlagRequirementMode.Any => flagSet.HasAny(flags),
                FlagRequirementMode.NotAll => !flagSet.HasAll(flags),
                FlagRequirementMode.NotAny => !flagSet.HasAny(flags),
                _ => true
            };
        }
    }
}
