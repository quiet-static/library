using System;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace QuietStatic
{
    /// <summary>
    /// Backward-compatibility facade that keeps legacy <c>GameLogger</c> callsites in the
    /// <c>QuietStatic</c> namespace working after logger types were moved under
    /// <c>QuietStatic.Toolkit.DebugTools</c>.
    /// </summary>
    public static class GameLogger
    {
        /// <summary>Writes an informational message with optional context.</summary>
        public static void Info(string message, Object context = null) =>
            Toolkit.DebugTools.GameLogger.Info(message, context);

        /// <summary>
        /// Retains legacy 1-argument callsite shape used across project code.
        /// </summary>
        public static void Warning(string message) =>
            Toolkit.DebugTools.GameLogger.Warning(message);

        /// <summary>Writes a warning with optional context.</summary>
        public static void Warning(string message, Object context) =>
            Toolkit.DebugTools.GameLogger.Warning(message, context);

        /// <summary>Writes an error with optional context.</summary>
        public static void Error(string message, Object context = null) =>
            Toolkit.DebugTools.GameLogger.Error(message, context);

        /// <summary>
        /// Backward-compatible overload for <c>GameLogger.Log(callingClass, context, message)</c>.
        /// </summary>
        public static void Log(string callingClass, Object context, string message) =>
            Toolkit.DebugTools.GameLogger.Log(callingClass, context, message);

        /// <summary>Retains simple legacy message log calls.</summary>
        public static void Log(string message, Object context = null) =>
            Toolkit.DebugTools.GameLogger.Info(message, context);

        /// <summary>
        /// Backward-compatible overload for <c>GameLogger.Warning(callingClass, context, message)</c>.
        /// </summary>
        public static void Warning(string callingClass, Object context, string message) =>
            Toolkit.DebugTools.GameLogger.Warning(callingClass, context, message);

        /// <summary>
        /// Backward-compatible overload for <c>GameLogger.Error(callingClass, context, message)</c>.
        /// </summary>
        public static void Error(string callingClass, Object context, string message) =>
            Toolkit.DebugTools.GameLogger.Error(callingClass, context, message);

        /// <summary>Logs an exception with contextual operation text.</summary>
        public static void Exception(Exception exception, string operation, Object context = null) =>
            Toolkit.DebugTools.GameLogger.Exception(exception, operation, context);

        /// <summary>Executes an action with guarded exception logging.</summary>
        public static void SafeExecute(string operation, Action action, Object context = null) =>
            Toolkit.DebugTools.GameLogger.SafeExecute(operation, action, context);

        /// <summary>Invokes a UnityEvent through a guarded wrapper.</summary>
        public static void SafeInvoke(string operation, UnityEvent unityEvent, Object context = null) =>
            Toolkit.DebugTools.GameLogger.SafeInvoke(operation, unityEvent, context);

        /// <summary>Compatibility shim for legacy per-instance suppression calls.</summary>
        public static void DisableFor(Object obj) =>
            Toolkit.DebugTools.GameLogger.DisableFor(obj);

        /// <summary>Compatibility shim for legacy per-instance suppression calls.</summary>
        public static void EnableFor(Object obj) =>
            Toolkit.DebugTools.GameLogger.EnableFor(obj);
    }
}
