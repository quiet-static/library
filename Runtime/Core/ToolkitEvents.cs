using System;

namespace QuietStatic.Toolkit.Core
{
    /// <summary>
    /// Provides a small global event hub for reusable toolkit systems that need to
    /// communicate without holding direct references to each other.
    /// </summary>
    /// <remarks>
    /// This class is intended for broad, cross-system notifications such as state,
    /// flag, objective, dialogue, and cutscene changes.
    ///
    /// Prefer direct references, scene-specific <c>UnityEvent</c> callbacks, or
    /// dedicated events on individual components when the relationship between
    /// systems is local and explicit. Use this hub when loose coupling is more
    /// useful than direct wiring.
    ///
    /// Because these events are static, listeners should unsubscribe when they are
    /// disabled or destroyed to avoid callbacks firing on objects that are no
    /// longer active.
    /// </remarks>
    public static class ToolkitEvents
    {
        /// <summary>
        /// Raised when the global toolkit/game state changes.
        /// </summary>
        /// <remarks>
        /// The string argument is the new state identifier. In projects using
        /// <see cref="GameStateController"/>, this is usually the current
        /// <see cref="ToolkitGameState"/> converted to a string.
        /// </remarks>
        public static event Action<string> StateChanged;

        /// <summary>
        /// Raised when a gameplay or progression flag is set.
        /// </summary>
        /// <remarks>
        /// The string argument should be a stable flag id, such as a ScriptableObject
        /// id, enum name, or other project-defined identifier.
        /// </remarks>
        public static event Action<string> FlagSet;

        /// <summary>
        /// Raised when the active objective text or objective id changes.
        /// </summary>
        /// <remarks>
        /// The string argument may be display text, an objective id, or any other
        /// project-defined objective value.
        /// </remarks>
        public static event Action<string> ObjectiveChanged;

        /// <summary>
        /// Raised when dialogue begins.
        /// </summary>
        /// <remarks>
        /// This event does not include a dialogue id or runner reference. Use a
        /// dedicated dialogue system event instead if listeners need to know which
        /// dialogue started.
        /// </remarks>
        public static event Action DialogueStarted;

        /// <summary>
        /// Raised when dialogue ends.
        /// </summary>
        /// <remarks>
        /// This event is intended for general systems that only need to know that
        /// dialogue is no longer running, such as input locks or simple state logic.
        /// </remarks>
        public static event Action DialogueEnded;

        /// <summary>
        /// Raised when a cutscene begins.
        /// </summary>
        /// <remarks>
        /// This is useful for broad systems such as player input, camera control,
        /// UI visibility, and game state handling.
        /// </remarks>
        public static event Action CutsceneStarted;

        /// <summary>
        /// Raised when a cutscene ends.
        /// </summary>
        /// <remarks>
        /// This is useful for restoring player control, returning UI, resuming
        /// gameplay state, or starting post-cutscene logic.
        /// </remarks>
        public static event Action CutsceneEnded;

        /// <summary>
        /// Notifies listeners that the global toolkit/game state changed.
        /// </summary>
        /// <param name="state">The new state identifier.</param>
        public static void RaiseStateChanged(string state)
        {
            StateChanged?.Invoke(state);
        }

        /// <summary>
        /// Notifies listeners that a progression flag was set.
        /// </summary>
        /// <param name="flagId">The id of the flag that was set.</param>
        public static void RaiseFlagSet(string flagId)
        {
            FlagSet?.Invoke(flagId);
        }

        /// <summary>
        /// Notifies listeners that the active objective changed.
        /// </summary>
        /// <param name="objective">The new objective text or objective identifier.</param>
        public static void RaiseObjectiveChanged(string objective)
        {
            ObjectiveChanged?.Invoke(objective);
        }

        /// <summary>
        /// Notifies listeners that dialogue started.
        /// </summary>
        public static void RaiseDialogueStarted()
        {
            DialogueStarted?.Invoke();
        }

        /// <summary>
        /// Notifies listeners that dialogue ended.
        /// </summary>
        public static void RaiseDialogueEnded()
        {
            DialogueEnded?.Invoke();
        }

        /// <summary>
        /// Notifies listeners that a cutscene started.
        /// </summary>
        public static void RaiseCutsceneStarted()
        {
            CutsceneStarted?.Invoke();
        }

        /// <summary>
        /// Notifies listeners that a cutscene ended.
        /// </summary>
        public static void RaiseCutsceneEnded()
        {
            CutsceneEnded?.Invoke();
        }
    }
}
