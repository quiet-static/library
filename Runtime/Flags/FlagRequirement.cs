using System;
using System.Collections.Generic;
using System.Linq;
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

        public FlagRequirement()
        {
        }

        /// <summary>Creates a requirement from a mode and stable flag IDs.</summary>
        public FlagRequirement(
            FlagRequirementMode mode,
            IEnumerable<string> flags)
        {
            this.mode = mode;
            this.flags = flags?
                .Where(flag => !string.IsNullOrWhiteSpace(flag))
                .Select(flag => flag.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
        }

        /// <summary>Gets the configured evaluation mode.</summary>
        public FlagRequirementMode Mode => mode;

        /// <summary>Gets the stable flag IDs evaluated by this requirement.</summary>
        public IReadOnlyList<string> Flags => flags ?? Array.Empty<string>();

        /// <summary>
        /// Gets whether this requirement contains an explicit flag-based condition.
        /// </summary>
        /// <remarks>
        /// <see cref="FlagRequirementMode.None"/> intentionally passes when evaluated, but it
        /// does not represent a condition that should automatically react to flag changes.
        /// Empty flag lists are also treated as unconfigured to prevent accidental activation.
        /// </remarks>
        public bool IsConfigured =>
            mode != FlagRequirementMode.None &&
            flags != null &&
            Array.Exists(flags, flag => !string.IsNullOrWhiteSpace(flag));

        /// <summary>
        /// Evaluates this requirement against the supplied or active flag manager.
        /// </summary>
        /// <param name="flagSet">Optional manager override. Uses the singleton when omitted.</param>
        /// <returns>True when the configured requirement mode is satisfied.</returns>
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
