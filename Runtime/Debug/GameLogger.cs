using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Provides centralized logging helpers for project scripts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This utility wraps Unity's built-in logging methods so messages have a
    /// consistent project-specific prefix and include the name of the object
    /// that produced the message.
    /// </para>
    /// <para>
    /// Individual Unity object instances can also have logging disabled. This is
    /// useful for components that expose an Inspector debug toggle and want to
    /// silence only their own messages without turning off logging globally.
    /// </para>
    /// <para>
    /// Because this is a static helper, it should stay small and general. Use it
    /// for lightweight diagnostics, not for long-term save data, analytics, or
    /// gameplay state.
    /// </para>
    /// </remarks>
    public static class GameLogger
    {
        /// <summary>
        /// Stores the Unity instance IDs that should not produce log output.
        /// </summary>
        /// <remarks>
        /// Unity objects can be destroyed while their instance IDs remain stored here.
        /// This is acceptable for normal debugging usage, but avoid calling
        /// <see cref="DisableFor(Object)"/> on large numbers of short-lived objects
        /// unless you also call <see cref="EnableFor(Object)"/> when appropriate.
        /// </remarks>
        private static readonly HashSet<int> disabledInstances = new HashSet<int>();

        /// <summary>
        /// Disables future logger output for a specific Unity object instance.
        /// </summary>
        /// <param name="obj">
        /// The Unity object whose logs should be suppressed. If this is
        /// <c>null</c>, the request is ignored.
        /// </param>
        /// <remarks>
        /// This only affects messages routed through <see cref="GameLogger"/>.
        /// Direct calls to Unity's <see cref="Debug.Log(object)"/>,
        /// <see cref="Debug.LogWarning(object)"/>, and
        /// <see cref="Debug.LogError(object)"/> are not affected.
        /// </remarks>
        public static void DisableFor(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            disabledInstances.Add(obj.GetInstanceID());
        }

        /// <summary>
        /// Re-enables logger output for a specific Unity object instance.
        /// </summary>
        /// <param name="obj">
        /// The Unity object whose logs should be allowed again. If this is
        /// <c>null</c>, the request is ignored.
        /// </param>
        public static void EnableFor(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            disabledInstances.Remove(obj.GetInstanceID());
        }

        /// <summary>
        /// Writes a standard informational message to the Unity Console.
        /// </summary>
        /// <param name="callingClass">
        /// The name of the class or system making the log call.
        /// </param>
        /// <param name="obj">
        /// The Unity object associated with the message. Unity uses this as the
        /// console context object, making it clickable in the Console window.
        /// </param>
        /// <param name="message">
        /// The message to display.
        /// </param>
        public static void Log(string callingClass, Object obj, string message)
        {
            if (!IsEnabled(obj))
            {
                return;
            }

            Debug.Log($"GAME: {GetObjectName(obj)} from {callingClass}: {message}", obj);
        }

        /// <summary>
        /// Writes a warning message to the Unity Console.
        /// </summary>
        /// <param name="callingClass">
        /// The name of the class or system making the warning call.
        /// </param>
        /// <param name="obj">
        /// The Unity object associated with the warning. Unity uses this as the
        /// console context object, making it clickable in the Console window.
        /// </param>
        /// <param name="message">
        /// The warning message to display.
        /// </param>
        public static void Warning(string callingClass, Object obj, string message)
        {
            if (!IsEnabled(obj))
            {
                return;
            }

            Debug.LogWarning($"WARNING: {GetObjectName(obj)} from {callingClass}: {message}", obj);
        }

        /// <summary>
        /// Writes an error message to the Unity Console.
        /// </summary>
        /// <param name="callingClass">
        /// The name of the class or system making the error call.
        /// </param>
        /// <param name="obj">
        /// The Unity object associated with the error. Unity uses this as the
        /// console context object, making it clickable in the Console window.
        /// </param>
        /// <param name="message">
        /// The error message to display.
        /// </param>
        public static void Error(string callingClass, Object obj, string message)
        {
            if (!IsEnabled(obj))
            {
                return;
            }

            Debug.LogError($"ERROR: {GetObjectName(obj)} from {callingClass}: {message}", obj);
        }

        /// <summary>
        /// Determines whether messages are currently allowed for the given object.
        /// </summary>
        /// <param name="obj">
        /// The Unity object being checked. If this is <c>null</c>, logging is
        /// treated as enabled because there is no instance ID to suppress.
        /// </param>
        /// <returns>
        /// <c>true</c> if logging is enabled for the object; otherwise,
        /// <c>false</c>.
        /// </returns>
        private static bool IsEnabled(Object obj)
        {
            if (obj == null)
            {
                return true;
            }

            return !disabledInstances.Contains(obj.GetInstanceID());
        }

        /// <summary>
        /// Returns a safe display name for a Unity object used in log formatting.
        /// </summary>
        /// <param name="obj">
        /// The Unity object whose name should be displayed.
        /// </param>
        /// <returns>
        /// The object's name when available; otherwise, a fallback label.
        /// </returns>
        private static string GetObjectName(Object obj)
        {
            if (obj == null)
            {
                return "No Object";
            }

            return obj.name;
        }
    }
}
