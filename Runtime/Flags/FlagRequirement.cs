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
        [Tooltip("How the Boolean flags are evaluated. None skips this Boolean gate; typed comparisons still apply.")]
        [SerializeField] private FlagRequirementMode mode = FlagRequirementMode.None;

        [Tooltip("Boolean flags checked by this requirement. Use Comparisons for numeric or string values.")]
        [FlagId]
        [SerializeField] private string[] flags;

        [Tooltip("Additional typed comparisons. Every comparison must pass, together with the Boolean requirement.")]
        [SerializeField] private FlagComparison[] comparisons;

        /// <summary>Additional typed comparisons, combined using AND.</summary>
        public IReadOnlyList<FlagComparison> Comparisons => comparisons ?? Array.Empty<FlagComparison>();

        public FlagRequirement()
        {
        }

        /// <summary>Creates a Boolean requirement from a mode and stable IDs.</summary>
        public FlagRequirement(FlagRequirementMode mode, IEnumerable<string> flags) : this(mode, flags, null) { }

        /// <summary>Creates a Boolean requirement with additional typed comparisons.</summary>
        public FlagRequirement(
            FlagRequirementMode mode,
            IEnumerable<string> flags,
            IEnumerable<FlagComparison> comparisons)
        {
            this.mode = mode;
            this.comparisons = comparisons?.ToArray() ?? Array.Empty<FlagComparison>();
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
        /// Boolean mode None and empty Boolean lists alone are unconfigured. An explicit
        /// typed comparison also configures the requirement, including with Boolean mode None.
        /// </remarks>
        public bool IsConfigured =>
            (mode != FlagRequirementMode.None && flags != null &&
             Array.Exists(flags, flag => !string.IsNullOrWhiteSpace(flag))) ||
            Comparisons.Any(condition => condition != null && !string.IsNullOrWhiteSpace(condition.flagId));

        /// <summary>
        /// Evaluates this requirement against the supplied or active flag manager.
        /// </summary>
        /// <param name="flagSet">Optional manager override. Uses the singleton when omitted.</param>
        /// <returns>True when the configured requirement mode is satisfied.</returns>
        public bool IsMet(FlagManager flagSet = null)
        {
            if (mode == FlagRequirementMode.None && Comparisons.Count == 0)
            {
                return true;
            }

            flagSet ??= FlagManager.Instance;

            if (flagSet == null)
            {
                return false;
            }

            if (mode != FlagRequirementMode.None && Flags.Any(id =>
                flagSet.TryGetValue(id, out var value) && value.Type != FlagValueType.Boolean)) return false;
            if (Comparisons.Any(condition => condition == null || !condition.IsMet(flagSet))) return false;

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
