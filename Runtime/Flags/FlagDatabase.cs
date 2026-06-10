using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags
{
    /// <summary>
    /// Defines an optional project-level database of known gameplay flag identifiers.
    /// </summary>
    /// <remarks>
    /// This asset is useful when a project wants a central reference list for flags that
    /// may be set by dialogue, interactions, cutscenes, objectives, or other gameplay systems.
    ///
    /// The runtime flag manager can still support arbitrary string ids. This database is
    /// primarily intended for organization, documentation, editor setup, and reuse across scenes.
    /// </remarks>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/Flags/Flag Database")]
    public class FlagDatabase : ScriptableObject
    {
        /// <summary>
        /// Describes one known gameplay flag.
        /// </summary>
        /// <remarks>
        /// The <see cref="id"/> should be the exact string used by runtime systems.
        /// The <see cref="description"/> is editor-facing documentation for designers,
        /// programmers, or anyone wiring flag-dependent behavior in the Inspector.
        /// </remarks>
        [Serializable]
        public class FlagDefinition
        {
            [Header("Flag Identity")]
            [Tooltip("Unique string id used by runtime systems when setting or checking this flag.")]
            public string id;

            [Header("Documentation")]
            [Tooltip("Human-readable explanation of what this flag represents and when it should be set.")]
            [TextArea]
            public string description;
        }

        [Header("Known Flags")]
        [Tooltip("Optional reference list of known gameplay flags for this project. Runtime systems may still support ids that are not listed here.")]
        [SerializeField] private FlagDefinition[] flags;

        /// <summary>
        /// Gets the known flag definitions stored in this database.
        /// </summary>
        /// <remarks>
        /// The returned array is intended for lookup, validation, editor tooling, or display.
        /// Runtime systems should avoid modifying this array directly.
        /// </remarks>
        public FlagDefinition[] Flags => flags;
    }
}
