using QuietStatic;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Jumpscare;
using QuietStatic.Toolkit.Objectives;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.DebugTools
{
    /// <summary>
    /// Observes global gameplay notifications and copies them into <see cref="DebugTrace"/>.
    /// It never publishes gameplay events. Trace entries may retain an optional scene-object
    /// context until they are evicted or cleared.
    /// </summary>
    /// <remarks>
    /// Dialogue and log text is flattened and truncated after 100 source characters (plus an
    /// ellipsis). Unity log stack traces are not copied into the runtime trace. Subscriptions exist
    /// only while this component is enabled, which prevents a persistent dashboard from accumulating
    /// duplicate callbacks as components or scenes are re-enabled.
    /// </remarks>
    [AddComponentMenu("Quiet Static/Debug Tools/Event Monitor")]
    public sealed class DebugEventMonitor : MonoBehaviour
    {
        [Tooltip("Include Log-severity Unity messages in the trace. Warnings, assertions, errors, and exceptions are captured regardless.")]
        [SerializeField] private bool captureInfoLogs;

        private bool isMonitoring;

        /// <summary>Connects the monitor to every supported global notification source.</summary>
        private void OnEnable()
        {
            if (isMonitoring)
            {
                return;
            }

            try
            {
                ConnectCallbacks();
                isMonitoring = true;
            }
            catch (System.Exception exception)
            {
                LogDebugToolsError(exception, "connecting global callbacks", this);
                DisconnectCallbacks();
            }
            finally
            {
                DebugTrace.Record("Monitor", "Event callbacks connected", this);
            }
        }

        /// <summary>Disconnects all callbacks previously attached by <see cref="OnEnable"/>.</summary>
        private void OnDisable()
        {
            if (!isMonitoring)
            {
                return;
            }

            try
            {
                DisconnectCallbacks();
            }
            catch (System.Exception exception)
            {
                LogDebugToolsError(exception, "disconnecting global callbacks", this);
            }
            finally
            {
                isMonitoring = false;
            }
        }

        private void ConnectCallbacks()
        {
            // Keep this list paired one-for-one with DisconnectCallbacks. Most publishers are static,
            // so a missed unsubscribe can retain this monitor after its scene or GameObject is gone.
            FlagManager.OnFlagSet += OnFlagSet;
            FlagManager.OnFlagCleared += OnFlagCleared;
            GameStateManager.OnGameStateChanged += OnStateChanged;
            ObjectiveManager.OnObjectiveLifecycleChanged += OnObjectiveChanged;
            CutsceneSequenceRunner.OnSequenceStarted += OnSequenceStarted;
            CutsceneSequenceRunner.OnSequenceEnded += OnSequenceEnded;
            Interactable.OnInteractionSucceeded += OnInteractionSucceeded;
            Interactable.OnInteractionFailed += OnInteractionFailed;
            DialogueRunner.OnNodeChanged += OnDialogueNodeChanged;
            JumpscareEvent.OnJumpscareStarted += OnJumpscareStarted;
            JumpscareEvent.OnJumpscareFinished += OnJumpscareFinished;
            SceneFlowManager.OnTransitionStarted += OnTransitionStarted;
            SceneFlowManager.OnTransitionCompleted += OnTransitionCompleted;
            SceneFlowManager.OnSceneLoaded += OnSceneLoaded;
            SceneFlowManager.OnSceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private void DisconnectCallbacks()
        {
            FlagManager.OnFlagSet -= OnFlagSet;
            FlagManager.OnFlagCleared -= OnFlagCleared;
            GameStateManager.OnGameStateChanged -= OnStateChanged;
            ObjectiveManager.OnObjectiveLifecycleChanged -= OnObjectiveChanged;
            CutsceneSequenceRunner.OnSequenceStarted -= OnSequenceStarted;
            CutsceneSequenceRunner.OnSequenceEnded -= OnSequenceEnded;
            Interactable.OnInteractionSucceeded -= OnInteractionSucceeded;
            Interactable.OnInteractionFailed -= OnInteractionFailed;
            DialogueRunner.OnNodeChanged -= OnDialogueNodeChanged;
            JumpscareEvent.OnJumpscareStarted -= OnJumpscareStarted;
            JumpscareEvent.OnJumpscareFinished -= OnJumpscareFinished;
            SceneFlowManager.OnTransitionStarted -= OnTransitionStarted;
            SceneFlowManager.OnTransitionCompleted -= OnTransitionCompleted;
            SceneFlowManager.OnSceneLoaded -= OnSceneLoaded;
            SceneFlowManager.OnSceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        // These callbacks are deliberately thin adapters: gameplay publishers remain unaware of
        // the dashboard, while DebugTrace standardizes the information rendered by its log window.

        /// <summary>Records the id of a runtime flag that has just been set.</summary>
        /// <param name="id">Flag id reported by <see cref="FlagManager"/>.</param>
        private static void OnFlagSet(string id) =>
            RecordEvent("Flag", $"+ {id}");

        /// <summary>Records the id of a runtime flag that has just been cleared.</summary>
        /// <param name="id">Flag id reported by <see cref="FlagManager"/>.</param>
        private static void OnFlagCleared(string id) =>
            RecordEvent("Flag", $"- {id}");

        /// <summary>Records a transition between two named game states.</summary>
        /// <param name="previousState">State active before the change.</param>
        /// <param name="state">State active after the change.</param>
        private static void OnStateChanged(string previousState, string state) =>
            RecordEvent("State", $"{previousState} -> {state}");

        /// <summary>Records the active objective after an objective-lifecycle notification.</summary>
        private static void OnObjectiveChanged()
        {
            // The lifecycle event carries no objective payload, so sample the manager's current
            // value at callback time. A missing singleton represents an empty objective state.
            ObjectiveDefinition objective = ObjectiveManager.Instance != null
                ? ObjectiveManager.Instance.ActiveObjective
                : null;
            RecordEvent(
                "Objective",
                objective != null ? objective.DisplayText : "(none)"
            );
        }

        /// <summary>Records that a cutscene sequence has begun.</summary>
        private static void OnSequenceStarted() => RecordEvent("Cutscene", "Started");

        /// <summary>Records that a cutscene sequence has ended or been stopped.</summary>
        private static void OnSequenceEnded() => RecordEvent("Cutscene", "Ended");

        /// <summary>Records a successful interaction and retains its target as trace context.</summary>
        /// <param name="target">Interactable that accepted the interaction.</param>
        /// <param name="actor">Interactor that attempted the interaction.</param>
        private static void OnInteractionSucceeded(Interactable target, Interactor actor) =>
            RecordEvent(
                "Interaction",
                $"Succeeded: {NameOf(target)} by {NameOf(actor)}",
                target);

        /// <summary>Records a failed interaction and retains its target as trace context.</summary>
        /// <param name="target">Interactable that rejected the interaction.</param>
        /// <param name="actor">Interactor that attempted the interaction.</param>
        private static void OnInteractionFailed(Interactable target, Interactor actor) =>
            RecordEvent(
                "Interaction",
                $"Failed: {NameOf(target)} by {NameOf(actor)}",
                target);

        /// <summary>Records the speaker and shortened line for a newly active dialogue node.</summary>
        /// <param name="runner">Runner retained as trace context.</param>
        /// <param name="node">New dialogue node, or <see langword="null"/> when none is active.</param>
        private static void OnDialogueNodeChanged(DialogueRunner runner, DialogueTree.Node node) =>
            RecordEvent(
                "Dialogue",
                $"Node: {node?.speaker ?? "(no speaker)"} — {Shorten(node?.line)}",
                runner);

        /// <summary>Records the source of a jumpscare that has begun.</summary>
        /// <param name="source">Jumpscare component retained as trace context.</param>
        private static void OnJumpscareStarted(JumpscareEvent source) =>
            RecordEvent("Jumpscare", $"Started: {NameOf(source)}", source);

        /// <summary>Records the source of a jumpscare that has finished.</summary>
        /// <param name="source">Jumpscare component retained as trace context.</param>
        private static void OnJumpscareFinished(JumpscareEvent source) =>
            RecordEvent("Jumpscare", $"Finished: {NameOf(source)}", source);

        /// <summary>Records the target of a scene transition that has begun.</summary>
        /// <param name="scene">Target scene name reported by scene flow.</param>
        private static void OnTransitionStarted(string scene) =>
            RecordEvent("Scene", $"Transition started → {scene}");

        /// <summary>Records the target of a scene transition that has completed.</summary>
        /// <param name="scene">Target scene name reported by scene flow.</param>
        private static void OnTransitionCompleted(string scene) =>
            RecordEvent("Scene", $"Transition completed → {scene}");

        /// <summary>Records a scene-flow service load notification.</summary>
        /// <param name="scene">Name of the loaded scene.</param>
        private static void OnSceneLoaded(string scene) =>
            RecordEvent("Scene", $"Loaded: {scene}");

        /// <summary>Records a scene-flow service unload notification.</summary>
        /// <param name="scene">Name of the unloaded scene.</param>
        private static void OnSceneUnloaded(string scene) =>
            RecordEvent("Scene", $"Unloaded: {scene}");

        /// <summary>Records Unity's change from one active scene to another.</summary>
        /// <param name="previous">Scene that was active before the change.</param>
        /// <param name="current">Scene that Unity made active.</param>
        private static void OnActiveSceneChanged(Scene previous, Scene current) =>
            RecordEvent("Scene", $"Active: {previous.name} → {current.name}");

        /// <summary>Copies a Unity Console message into the bounded runtime trace.</summary>
        /// <param name="condition">Console message text.</param>
        /// <param name="stackTrace">
        /// Console stack trace. Intentionally ignored to keep dashboard entries concise.
        /// </param>
        /// <param name="type">Severity reported by Unity.</param>
        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            // Warnings, assertions, exceptions, and errors are useful enough to keep unconditionally.
            if (type == LogType.Log && !captureInfoLogs)
            {
                return;
            }

            // DebugTrace does not call Debug.Log, avoiding recursion through logMessageReceived.
            RecordEvent(type == LogType.Log ? "Log" : type.ToString(), Shorten(condition));
        }

        /// <summary>Returns the current display name for an optional Unity object.</summary>
        private static string NameOf(UnityEngine.Object value) => value != null ? value.name : "(none)";

        /// <summary>Normalizes arbitrary text for one compact dashboard trace row.</summary>
        /// <param name="value">Potentially blank or multiline source text.</param>
        /// <returns>A nonblank, single-line string no longer than 101 display characters.</returns>
        private static string Shorten(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "(empty)";
            }

            const int maximumLength = 100;
            // Replace both newline variants because dialogue and platform logs may use either one.
            string singleLine = value.Replace('\n', ' ').Replace('\r', ' ');
            // The ellipsis is appended after the first 100 source characters.
            return singleLine.Length <= maximumLength ? singleLine : singleLine.Substring(0, maximumLength) + "…";
        }

        /// <summary>
        /// Centralized guard around <see cref="DebugTrace.Record"/> so a bad record write cannot
        /// destabilize the publishing pipeline.
        /// </summary>
        private static void RecordEvent(string category, string message, UnityEngine.Object context = null)
        {
            try
            {
                DebugTrace.Record(category, message, context);
            }
            catch (System.Exception exception)
            {
                LogDebugToolsError(exception, "recording debug trace event", context);
            }
        }

        private static void LogDebugToolsError(System.Exception exception, string operation, UnityEngine.Object context)
        {
            if (exception == null)
            {
                return;
            }

            Debug.LogError($"DebugTools error while {operation}.", context);
            Debug.LogException(exception, context);
        }
    }
}
