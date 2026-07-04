using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Flags
{
    /// <summary>
    /// Stores the runtime state of gameplay flags defined by an optional
    /// <see cref="FlagDatabase"/>.
    /// </summary>
    public class FlagManager : ToolkitSingleton<FlagManager>
    {
        [Serializable]
        public class StringUnityEvent : UnityEvent<string>
        {
        }

        /// <summary>
        /// Describes a flag that is automatically set after all required flags are active.
        /// </summary>
        [Serializable]
        public class FlagDependency
        {
            [Tooltip("Flag automatically set when every required flag is active.")]
            [SerializeField] private string resultFlag;

            [Tooltip("All of these flags must be active before Result Flag is set.")]
            [SerializeField] private string[] requiredFlags;

            public string ResultFlag => resultFlag;
            public IReadOnlyList<string> RequiredFlags => requiredFlags;
        }

        [Header("Database")]
        [Tooltip("Optional central list of valid flag IDs. When assigned, unknown IDs cannot be set.")]
        [SerializeField] private FlagDatabase flagDatabase;

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
        /// Raised whenever the active flag collection changes.
        /// </summary>
        public static event Action OnFlagsChanged;

        /// <summary>
        /// Stores currently active runtime flag IDs.
        /// </summary>
        private readonly HashSet<string> activeFlags = new();

        /// <summary>
        /// Cached valid IDs from the assigned database.
        /// </summary>
        private readonly HashSet<string> knownFlagIds = new();

        /// <summary>
        /// Prevents dependency evaluation from recursively restarting itself.
        /// </summary>
        private bool isApplyingDependencies;

        /// <summary>
        /// Gets the assigned database, if one exists.
        /// </summary>
        public FlagDatabase Database => flagDatabase;

        /// <summary>
        /// Gets all currently active flags.
        /// </summary>
        public IReadOnlyCollection<string> ActiveFlags => activeFlags;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            CacheDatabaseFlags();

            if (startingFlags != null)
            {
                foreach (string flagId in startingFlags)
                {
                    AddFlagSilently(flagId);
                }
            }

            ApplyDependencies();
        }

        /// <summary>
        /// Returns whether the database contains a flag ID.
        /// When no database is assigned, any non-empty ID is considered valid.
        /// </summary>
        public bool IsKnownFlag(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return false;
            }

            return flagDatabase == null || knownFlagIds.Contains(flagId.Trim());
        }

        /// <summary>
        /// Checks whether a specific flag is currently active.
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
        /// Activates a known flag and notifies listeners.
        /// </summary>
        public void SetFlag(string flagId)
        {
            if (!AddFlagSilently(flagId))
            {
                return;
            }

            RaiseFlagSetEvents(flagId.Trim());
            ApplyDependencies();
        }

        /// <summary>
        /// Removes an active known flag and notifies listeners.
        /// </summary>
        public void ClearFlag(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return;
            }

            string normalizedFlagId = flagId.Trim();

            if (!IsKnownFlag(normalizedFlagId))
            {
                GameLogger.Warning(
                    "ClearFlag",
                    this,
                    $"[{nameof(FlagManager)}] Cannot clear unknown flag '{normalizedFlagId}'. " +
                    $"Add it to the assigned {nameof(FlagDatabase)} first."
                );

                return;
            }

            if (!activeFlags.Remove(normalizedFlagId))
            {
                return;
            }

            OnFlagCleared?.Invoke(normalizedFlagId);
            onFlagCleared?.Invoke(normalizedFlagId);
            OnFlagsChanged?.Invoke();
        }

        /// <summary>
        /// Removes every active flag.
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
        /// Copies valid IDs from the assigned database into a runtime lookup set.
        /// </summary>
        private void CacheDatabaseFlags()
        {
            knownFlagIds.Clear();

            if (flagDatabase == null || flagDatabase.Flags == null)
            {
                return;
            }

            foreach (FlagDatabase.FlagDefinition definition in flagDatabase.Flags)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                {
                    continue;
                }

                string normalizedId = definition.id.Trim();

                if (!knownFlagIds.Add(normalizedId))
                {
                    GameLogger.Warning(
                        "CacheDatabaseFlags",
                        this,
                        $"[{nameof(FlagManager)}] The assigned {nameof(FlagDatabase)} contains a duplicate flag ID: '{normalizedId}'."
                    );
                }
            }
        }

        /// <summary>
        /// Evaluates dependency rules until no additional result flags can be set.
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

                        RaiseFlagSetEvents(dependency.ResultFlag.Trim());
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
        /// Adds a valid flag without triggering events.
        /// </summary>
        private bool AddFlagSilently(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return false;
            }

            string normalizedFlagId = flagId.Trim();

            if (!IsKnownFlag(normalizedFlagId))
            {
                GameLogger.Warning(
                    "AddFlagSilently",
                    this,
                    $"[{nameof(FlagManager)}] Cannot set unknown flag '{normalizedFlagId}'. " +
                    $"Add it to the assigned {nameof(FlagDatabase)} first."
                );

                return false;
            }

            return activeFlags.Add(normalizedFlagId);
        }

        /// <summary>
        /// Invokes all events associated with activating a flag.
        /// </summary>
        private void RaiseFlagSetEvents(string flagId)
        {
            OnFlagSet?.Invoke(flagId);
            onFlagSet?.Invoke(flagId);
            OnFlagsChanged?.Invoke();

            ToolkitEvents.RaiseFlagSet(flagId);
        }
    }
}