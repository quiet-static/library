using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Provides centralized logging helpers for game objects, with support for
    /// disabling logs for specific Unity object instances.
    /// </summary>
    public static class GameLogger
    {
        /// <summary>
        /// Stores the instance IDs of Unity objects that should not produce log output.
        /// </summary>
        private static HashSet<int> disabledInstances = new HashSet<int>();

        /// <summary>
        /// Disables logging for a specific Unity object instance.
        /// </summary>
        /// <param name="obj">
        /// The Unity object to suppress logs for.
        /// If <c>null</c>, the call is ignored.
        /// </param>
        public static void DisableFor(Object obj)
        {
            if (obj == null) return;
            disabledInstances.Add(obj.GetInstanceID());
        }

        /// <summary>
        /// Re-enables logging for a specific Unity object instance.
        /// </summary>
        /// <param name="obj">
        /// The Unity object to allow logs for again.
        /// If <c>null</c>, the call is ignored.
        /// </param>
        public static void EnableFor(Object obj)
        {
            if (obj == null) return;
            disabledInstances.Remove(obj.GetInstanceID());
        }

        /// <summary>
        /// Determines whether logging is currently enabled for the given Unity object.
        /// </summary>
        /// <param name="obj">
        /// The Unity object being checked.
        /// If <c>null</c>, logging is treated as enabled.
        /// </param>
        /// <returns>
        /// <c>true</c> if logging is enabled for the object; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsEnabled(Object obj)
        {
            if (obj == null) return true;
            return !disabledInstances.Contains(obj.GetInstanceID());
        }

        /// <summary>
        /// Writes a standard log message for the given object, unless logging is disabled for it.
        /// </summary>
        /// <param name="callingClass">The name of the class making the log call.</param>
        /// <param name="obj">The Unity object associated with the message.</param>
        /// <param name="message">The message to log.</param>
        public static void Log(string callingClass, Object obj, string message)
        {
            if (!IsEnabled(obj)) return;

            Debug.Log($"GAME: {obj.name} from {callingClass}: {message}", obj);
        }

        /// <summary>
        /// Writes a warning log message for the given object, unless logging is disabled for it.
        /// </summary>
        /// <param name="callingClass">The name of the class making the warning call.</param>
        /// <param name="obj">The Unity object associated with the warning.</param>
        /// <param name="message">The warning message to log.</param>
        public static void Warning(string callingClass, Object obj, string message)
        {
            if (!IsEnabled(obj)) return;

            Debug.LogWarning($"WARNING: {obj.name} from {callingClass}: {message}", obj);
        }

        /// <summary>
        /// Writes an error log message for the given object, unless logging is disabled for it.
        /// </summary>
        /// <param name="callingClass">The name of the class making the error call.</param>
        /// <param name="obj">The Unity object associated with the error.</param>
        /// <param name="message">The error message to log.</param>
        public static void Error(string callingClass, Object obj, string message)
        {
            if (!IsEnabled(obj)) return;

            Debug.LogError($"ERROR: {obj.name} from {callingClass}: {message}", obj);
        }
    }
}