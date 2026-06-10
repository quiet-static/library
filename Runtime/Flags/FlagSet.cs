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
    /// <para>
    /// A flag is a simple string id that represents a piece of game state, such as
    /// a completed interaction, unlocked door, collected item, viewed cutscene, or
    /// one-shot event.
    /// </para>
    /// <para>
    /// This class intentionally uses strings instead of project-specific enums so it
    /// can be reused across different projects and prototypes. For larger projects,
    /// pair this with <see cref="FlagDatabase"/> to keep a documented list of known
    /// flag ids.
    /// </para>
    /// <para>
    /// The manager inherits from <see cref="ToolkitSingleton{T}"/>, so other systems
    /// can access the active instance through <see cref="ToolkitSingleton{T}.Instance"/>.
    /// </para>
    /// </remarks>
    public class FlagSet : ToolkitSingleton<FlagSet>
    {
        /// <summary>
        /// UnityEvent wrapper that passes a single string value.
        /// </summary>
        /// <remarks>
        /// Used so the Inspector can react to newly set flags without requiring
        /// another script to subscribe to the static <see cref="OnFlagSet"/> event.
        /// </remarks>
        [Serializable]
        public class StringUnityEvent : UnityEvent<string> { }

        [Header("Starting State")]
        [Tooltip("Flags that should already be active when this flag set initializes.")]
        [SerializeField] private string[] startingFlags;

        [Header("Events")]
        [Tooltip("Invoked when a new flag is set. The string argument is the flag id that was added.")]
        [SerializeField] private StringUnityEvent onFlagSet;

        /// <summary>
        /// Runtime storage for every active flag id.
        /// </summary>
        /// <remarks>
        /// A <see cref="HashSet{T}"/> is used so duplicate flags are ignored automatically
        /// and lookup checks remain fast.
        /// </remarks>
        private readonly HashSet<string> activeFlags = new HashSet<string>();

        /// <summary>
        /// Raised when a new flag is successfully added to the active flag set.
        /// </summary>
        /// <remarks>
        /// This event is only raised when the flag was not already active. Calling
        /// <see cref="SetFlag"/> with a duplicate flag id will not fire this event.
        /// </remarks>
        public static event Action<string> OnFlagSet;

        /// <summary>
        /// Gets a read-only view of all currently active flag ids.
        /// </summary>
        public IReadOnlyCollection<string> ActiveFlags => activeFlags;

        /// <summary>
        /// Initializes the singleton and applies any configured starting flags.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            foreach (string flag in startingFlags)
            {
                AddFlagSilently(flag);
            }
        }

        /// <summary>
        /// Checks whether a specific flag is currently active.
        /// </summary>
        /// <param name="flagId">The flag id to check.</param>
        /// <returns>
        /// <c>true</c> if the flag id is valid and active; otherwise, <c>false</c>.
        /// </returns>
        public bool HasFlag(string flagId)
        {
            return !string.IsNullOrWhiteSpace(flagId) && activeFlags.Contains(flagId);
        }

        /// <summary>
        /// Checks whether every valid flag id in the collection is active.
        /// </summary>
        /// <param name="flagIds">The flag ids to check.</param>
        /// <returns>
        /// <c>true</c> if all non-blank flag ids are active.
        /// Returns <c>true</c> when <paramref name="flagIds"/> is <c>null</c>,
        /// because there are no requirements to fail.
        /// </returns>
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
        /// Checks whether at least one valid flag id in the collection is active.
        /// </summary>
        /// <param name="flagIds">The flag ids to check.</param>
        /// <returns>
        /// <c>true</c> if any non-blank flag id is active.
        /// Returns <c>false</c> when <paramref name="flagIds"/> is <c>null</c>.
        /// </returns>
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
        /// Adds a flag to the active flag set and raises flag-set events.
        /// </summary>
        /// <param name="flagId">The flag id to activate.</param>
        /// <remarks>
        /// Blank flag ids are ignored. Duplicate flag ids are also ignored and do not
        /// raise events.
        /// </remarks>
        public void SetFlag(string flagId)
        {
            if (!AddFlagSilently(flagId))
            {
                return;
            }

            OnFlagSet?.Invoke(flagId);
            onFlagSet?.Invoke(flagId);
            ToolkitEvents.RaiseFlagSet(flagId);
        }

        /// <summary>
        /// Removes a single flag from the active flag set.
        /// </summary>
        /// <param name="flagId">The flag id to clear.</param>
        /// <remarks>
        /// Clearing a flag currently does not raise an event. Add a separate event if
        /// other systems need to react to flags being removed.
        /// </remarks>
        public void ClearFlag(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return;
            }

            activeFlags.Remove(flagId);
        }

        /// <summary>
        /// Removes every active flag from this flag set.
        /// </summary>
        /// <remarks>
        /// This is useful when resetting a game, returning to a title menu, or starting
        /// a new save file. This does not automatically re-apply starting flags.
        /// </remarks>
        public void ClearAllFlags()
        {
            activeFlags.Clear();
        }

        /// <summary>
        /// Adds a flag without raising any flag-set events.
        /// </summary>
        /// <param name="flagId">The flag id to add.</param>
        /// <returns>
        /// <c>true</c> if the flag id was valid and newly added; otherwise, <c>false</c>.
        /// </returns>
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
