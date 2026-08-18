using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.DebugTools
{
    /// <summary>
    /// Lightweight, bounded runtime trace shared by debug tools.
    /// </summary>
    /// <remarks>
    /// Entries live in process-wide static state, so they remain available while scenes load and
    /// unload. They are normally reset by Unity's scripting-domain reload; when domain reload is
    /// disabled, a caller can use <see cref="Clear"/> at its chosen session boundary. This type is
    /// intended for calls from Unity's main thread and does not synchronize concurrent list access.
    /// Recording an entry does not emit a Unity Console message, which also makes it safe for the
    /// application's log callback to use without recursively generating more log callbacks.
    /// </remarks>
    public static class DebugTrace
    {
        /// <summary>
        /// Immutable snapshot of one recorded diagnostic event and its point in Unity's runtime.
        /// </summary>
        public readonly struct Entry
        {
            /// <summary>Creates a trace-entry snapshot.</summary>
            /// <param name="frame">Unity frame in which the event was recorded.</param>
            /// <param name="time">Unscaled seconds from <see cref="Time.realtimeSinceStartup"/>.</param>
            /// <param name="category">Normalized diagnostic category.</param>
            /// <param name="message">Normalized diagnostic message.</param>
            /// <param name="context">Optional Unity object retained until the entry is removed.</param>
            public Entry(int frame, float time, string category, string message, UnityEngine.Object context)
            {
                Frame = frame;
                Time = time;
                Category = category;
                Message = message;
                Context = context;
            }

            /// <summary>Gets the Unity frame in which this entry was recorded.</summary>
            public int Frame { get; }

            /// <summary>Gets the unscaled realtime-since-startup timestamp.</summary>
            public float Time { get; }

            /// <summary>Gets the normalized diagnostic category.</summary>
            public string Category { get; }

            /// <summary>Gets the normalized diagnostic message.</summary>
            public string Message { get; }

            /// <summary>
            /// Gets the optional Unity object associated with the event. Callers should still use
            /// Unity's null checks because a retained object can be destroyed before this entry is.
            /// </summary>
            public UnityEngine.Object Context { get; }
        }

        private const int DefaultCapacity = 100;
        private static readonly List<Entry> entries = new(DefaultCapacity);
        private static int capacity = DefaultCapacity;

        /// <summary>
        /// Gets a read-only view of the trace in oldest-to-newest order.
        /// </summary>
        /// <remarks>
        /// The returned interface does not expose mutation, but it references the live underlying
        /// list and changes as entries are recorded, trimmed, or cleared.
        /// </remarks>
        public static IReadOnlyList<Entry> Entries => entries;

        /// <summary>Sets the maximum retained entry count, clamped to at least one.</summary>
        /// <param name="value">Requested maximum number of entries to retain.</param>
        /// <remarks>
        /// Lowering the capacity immediately discards the oldest entries until the new limit is met.
        /// </remarks>
        public static void SetCapacity(int value)
        {
            capacity = Mathf.Max(1, value);
            Trim();
        }

        /// <summary>
        /// Records a diagnostic event without writing to the Unity Console. Blank categories
        /// and messages receive defaults; nonblank values are trimmed. The optional context is
        /// retained until the entry is evicted or the trace is cleared.
        /// </summary>
        /// <param name="category">Short grouping label displayed by trace consumers.</param>
        /// <param name="message">Human-readable diagnostic details.</param>
        /// <param name="context">Optional Unity object associated with the event.</param>
        public static void Record(string category, string message, UnityEngine.Object context = null)
        {
            Entry entry = new(
                Time.frameCount,
                Time.realtimeSinceStartup,
                string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                string.IsNullOrWhiteSpace(message) ? "(no details)" : message.Trim(),
                context);

            entries.Add(entry);
            Trim();
        }

        /// <summary>
        /// Clears all retained entries without changing the configured capacity.
        /// </summary>
        public static void Clear()
        {
            entries.Clear();
        }

        /// <summary>Removes the oldest entries until the current capacity is satisfied.</summary>
        private static void Trim()
        {
            int excess = entries.Count - capacity;
            if (excess > 0)
            {
                // Entries are appended chronologically, so the excess always occupies the front.
                entries.RemoveRange(0, excess);
            }
        }
    }
}
