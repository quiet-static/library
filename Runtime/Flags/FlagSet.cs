using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Flags
{
    /// <summary>
    /// Stores and manages a global set of string-based gameplay flags.
    /// </summary>
    /// <remarks>
    /// Flags are simple string identifiers representing game progression, completed
    /// interactions, unlocked content, viewed cutscenes, collected items, and similar state.
    ///
    /// This class intentionally does not know about dialogue, interactables, scenes,
    /// objectives, or other project-specific systems.
    /// </remarks>
    public class FlagSet : ToolkitSingleton<FlagSet>
    {
        [Serializable]
        public class StringUnityEvent : UnityEvent<string>
        {
        }

        /// <summary>
        /// Describes a flag that should automatically be set once all required flags are active.
        /// </summary>
        [Serializable]
        public class FlagDependency
        {
            [Tooltip("Flag automatically set when every required flag is active.")]
            [SerializeField] private string resultFlag;

            [Tooltip("All of these flags must be active before Result Flag is set.")]
            [SerializeField] private string[] requiredFlags;

            /// <summary>
            /// Gets the flag automatically set when this dependency is satisfied.
            /// </summary>
            public string ResultFlag => resultFlag;

            /// <summary>
            /// Gets the flags required before the result flag can be set.
            /// </summary>
            public IReadOnlyList<string> RequiredFlags => requiredFlags;
        }

        [Header("Starting State")]
        [Tooltip("Flags that should already be active when this flag set initializes.")]
        [SerializeField] private string[] startingFlags;

        [Header("Dependencies")]
        [Tooltip("Optional rules that automatically set a result flag once all required flags are active.")]
        [SerializeField] private FlagDependency[] dependencies;

        [Header("Unity Events")]
        [Tooltip("Invoked when a new flag is set. The argument is the flag ID.")]
        [SerializeField] private StringUnityEvent onFlagSet;

        [Tooltip("Invoked when a flag is removed. The argument is the flag ID.")]
        [SerializeField] private StringUnityEvent onFlagCleared;

        /// <summary>
        /// Raised when a new flag is successfully added.
        /// </summary>
        public static event Action<string> OnFlagSet;

        /// <summary>
        /// Raised when an active flag is removed.
        /// </summary>
        public static event Action<string> OnFlagCleared;

        /// <summary>
        /// Raised whenever the flag collection changes.
        /// </summary>
        public static event Action OnFlagsChanged;

        /// <summary>
        /// Runtime storage for active flag IDs.
        /// </summary>
        private readonly HashSet<string> activeFlags = new();

        /// <summary>
        /// Prevents dependency evaluation from recursively restarting itself.
        /// </summary>
        private bool isApplyingDependencies;

        /// <summary>
        /// Gets a read-only view of all active flag IDs.
        /// </summary>
        public IReadOnlyCollection<string> ActiveFlags => activeFlags;

        /// <summary>
        /// Initializes the singleton and applies configured starting flags.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            if (startingFlags == null)
            {
                return;
            }

            foreach (string flag in startingFlags)
            {
                AddFlagSilently(flag);
            }

            ApplyDependencies();
        }

        /// <summary>
        /// Checks whether a specific flag is active.
        /// </summary>
        public bool HasFlag(string flagId)
        {
            return !string.IsNullOrWhiteSpace(flagId) &&
                   activeFlags.Contains(flagId.Trim());
        }

        /// <summary>
        /// Checks whether every valid flag in a collection is active.
        /// </summary>
        public bool HasAll(IEnumerable<string> flagIds)
        {
            if (flagIds == null)
            {
                return true;
            }

            return flagIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .All(HasFlag);
        }

        /// <summary>
        /// Checks whether at least one valid flag in a collection is active.
        /// </summary>
        public bool HasAny(IEnumerable<string> flagIds)
        {
            if (flagIds == null)
            {
                return false;
            }

            return flagIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Any(HasFlag);
        }

        /// <summary>
        /// Adds a flag and notifies listeners if it was newly added.
        /// </summary>
        public void SetFlag(string flagId)
        {
            if (!AddFlagSilently(flagId))
            {
                return;
            }

            string normalizedFlagId = flagId.Trim();

            OnFlagSet?.Invoke(normalizedFlagId);
            onFlagSet?.Invoke(normalizedFlagId);
            OnFlagsChanged?.Invoke();

            ToolkitEvents.RaiseFlagSet(normalizedFlagId);

            ApplyDependencies();
        }

        /// <summary>
        /// Removes a flag and notifies listeners if it was active.
        /// </summary>
        public void ClearFlag(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return;
            }

            string normalizedFlagId = flagId.Trim();

            if (!activeFlags.Remove(normalizedFlagId))
            {
                return;
            }

            OnFlagCleared?.Invoke(normalizedFlagId);
            onFlagCleared?.Invoke(normalizedFlagId);
            OnFlagsChanged?.Invoke();
        }

        /// <summary>
        /// Removes all active flags.
        /// </summary>
        public void ClearAllFlags()
        {
            if (activeFlags.Count == 0)
            {
                return;
            }

            activeFlags.Clear();
            OnFlagsChanged?.Invoke();
        }

        /// <summary>
        /// Evaluates configured dependency rules and adds any newly satisfied result flags.
        /// </summary>
        private void ApplyDependencies()
        {
            if (isApplyingDependencies || dependencies == null || dependencies.Length == 0)
            {
                return;
            }

            isApplyingDependencies = true;

            try
            {
                bool addedFlag;

                do
                {
                    addedFlag = false;

                    foreach (FlagDependency dependency in dependencies)
                    {
                        if (dependency == null ||
                            string.IsNullOrWhiteSpace(dependency.ResultFlag) ||
                            HasFlag(dependency.ResultFlag) ||
                            dependency.RequiredFlags == null ||
                            dependency.RequiredFlags.Count == 0 ||
                            !HasAll(dependency.RequiredFlags))
                        {
                            continue;
                        }

                        if (!AddFlagSilently(dependency.ResultFlag))
                        {
                            continue;
                        }

                        string resultFlag = dependency.ResultFlag.Trim();

                        OnFlagSet?.Invoke(resultFlag);
                        onFlagSet?.Invoke(resultFlag);
                        OnFlagsChanged?.Invoke();

                        ToolkitEvents.RaiseFlagSet(resultFlag);

                        addedFlag = true;
                    }
                }
                while (addedFlag);
            }
            finally
            {
                isApplyingDependencies = false;
            }
        }

        /// <summary>
        /// Adds a flag without raising events.
        /// </summary>
        private bool AddFlagSilently(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return false;
            }

            return activeFlags.Add(flagId.Trim());
        }
    }
}