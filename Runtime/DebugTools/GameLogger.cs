using System;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace QuietStatic.Toolkit.DebugTools
{
    /// <summary>
    /// Centralized runtime logger that combines context-rich messages with consistent exception
    /// handling for event callbacks and dispatch logic.
    /// </summary>
    public static class GameLogger
    {
        /// <summary>Writes an informational runtime message.</summary>
        public static void Info(string message, Object context = null) =>
            Debug.Log(Normalize(message, "Info"), context);

        /// <summary>Writes a warning runtime message.</summary>
        public static void Warning(string message, Object context = null) =>
            Debug.LogWarning(Normalize(message, "Warning"), context);

        /// <summary>Writes an error runtime message.</summary>
        public static void Error(string message, Object context = null) =>
            Debug.LogError(Normalize(message, "Error"), context);

        /// <summary>
        /// Backward-compatible signature kept for existing runtime callsites that pass
        /// a calling class name first and the context object second.
        /// </summary>
        /// <param name="callingClass">Calling class name used in historical log output.</param>
        /// <param name="context">Unity context object for log anchoring.</param>
        /// <param name="message">Message produced by the caller.</param>
        public static void Log(string callingClass, Object context, string message) =>
            Info(BuildLegacyMessage(callingClass, message), context);

        /// <summary>Backward-compatible signature for legacy warning calls.</summary>
        public static void Warning(string callingClass, Object context, string message) =>
            Warning(BuildLegacyMessage(callingClass, message), context);

        /// <summary>Backward-compatible signature for legacy error calls.</summary>
        public static void Error(string callingClass, Object context, string message) =>
            Error(BuildLegacyMessage(callingClass, message), context);

        /// <summary>Legacy no-op retained for compatibility with prior per-instance suppression calls.</summary>
        public static void DisableFor(Object obj)
        {
            _ = obj;
        }

        /// <summary>Legacy no-op retained for compatibility with prior per-instance suppression calls.</summary>
        public static void EnableFor(Object obj)
        {
            _ = obj;
        }

        /// <summary>Logs an exception with operation context so callers can diagnose event failures.</summary>
        /// <param name="exception">Exception encountered while handling input, event, or state flow.</param>
        /// <param name="operation">What was being attempted when the exception occurred.</param>
        /// <param name="context">Unity context object to anchor this log entry.</param>
        public static void Exception(Exception exception, string operation, Object context = null)
        {
            if (exception == null)
            {
                return;
            }

            string operationContext = string.IsNullOrWhiteSpace(operation)
                ? "Unhandled exception."
                : $"Exception while {operation}.";
            Debug.LogError(Normalize(operationContext, "Error"), context);
            Debug.LogException(exception, context);
        }

        /// <summary>
        /// Executes an action and logs a contextualized exception if the action throws.
        /// </summary>
        /// <param name="operation">Name used in diagnostics if execution fails.</param>
        /// <param name="action">Invokable operation guarded by this method.</param>
        /// <param name="context">Unity context object to anchor this log entry.</param>
        public static void SafeExecute(string operation, Action action, Object context = null)
        {
            if (action == null)
            {
                Warning($"Skip {operation}; no action was provided.", context);
                return;
            }

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Exception(exception, operation, context);
            }
        }

        /// <summary>
        /// Invokes an event through a failure-safe wrapper so one failing subscriber cannot
        /// silently break gameplay flow.
        /// </summary>
        public static void SafeInvoke(string operation, UnityEvent unityEvent, Object context = null)
        {
            if (unityEvent == null)
            {
                return;
            }

            SafeExecute(operation, unityEvent.Invoke, context);
        }

        /// <summary>Normalizes a log message and retains helpful context.</summary>
        private static string Normalize(string message, string prefix)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return $"[{prefix}] (no details)";
            }

            string normalized = message.Trim();
            return normalized.StartsWith("[", StringComparison.Ordinal)
                ? normalized
                : $"[{prefix}] {normalized}";
        }

        private static string BuildLegacyMessage(string callingClass, string message)
        {
            if (string.IsNullOrWhiteSpace(callingClass))
            {
                return message;
            }

            return $"{callingClass}: {message}";
        }
    }
}
