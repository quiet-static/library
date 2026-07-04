using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags
{
    /// <summary>
    /// Defines how a <see cref="FlagRequirement"/> should evaluate its configured flag ids.
    /// </summary>
    /// <remarks>
    /// These modes allow gameplay systems to express common progression checks such as
    /// requiring every listed flag, requiring at least one listed flag, or requiring that
    /// certain flags have not been set yet.
    /// </remarks>
    public enum FlagRequirementMode
    {
        /// <summary>
        /// No flags are required. The requirement is always considered met.
        /// </summary>
        None,

        /// <summary>
        /// Every configured flag id must be set in the target <see cref="FlagManager"/>.
        /// </summary>
        All,

        /// <summary>
        /// At least one configured flag id must be set in the target <see cref="FlagManager"/>.
        /// </summary>
        Any,

        /// <summary>
        /// The requirement is met when not every configured flag id is set.
        /// </summary>
        NotAll,

        /// <summary>
        /// The requirement is met when none of the configured flag ids are set.
        /// </summary>
        NotAny
    }

    /// <summary>
    /// Serializable progression requirement that checks one or more string flag ids
    /// against a <see cref="FlagManager"/>.
    /// </summary>
    /// <remarks>
    /// This class is designed to be embedded inside other Inspector-facing data types,
    /// such as interactables, objectives, dialogue choices, cutscene triggers, or scene
    /// transitions. It does not store state itself; it only describes the condition that
    /// must be true when evaluated.
    /// </remarks>
    [Serializable]
    public class FlagRequirement
    {
        [Header("Requirement")]
        [Tooltip("How the listed flags should be evaluated. None means this requirement always passes.")]
        [SerializeField] private FlagRequirementMode mode = FlagRequirementMode.None;

        [Tooltip("Flag ids checked by this requirement. These should match ids stored in the active FlagManager.")]
        [SerializeField] private string[] flags;

        /// <summary>
        /// Determines whether this requirement is currently satisfied.
        /// </summary>
        /// <param name="flagSet">
        /// Optional flag set to evaluate against. If omitted, this method uses
        /// <see cref="FlagManager.Instance"/>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the configured requirement mode passes against the chosen flag set;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// A requirement using <see cref="FlagRequirementMode.None"/> always returns
        /// <c>true</c>. All other modes require a valid <see cref="FlagManager"/> instance.
        /// </remarks>
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
