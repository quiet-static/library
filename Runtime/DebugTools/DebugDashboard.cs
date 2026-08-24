using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.SceneFlow;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.DebugTools
{
    /// <summary>
    /// Runtime development dashboard with movable scene-flow, flag, cutscene, teleport,
    /// performance, and trace windows, plus dialogue-trigger controls and a compact state view.
    /// </summary>
    /// <remarks>
    /// The dashboard uses Unity's immediate-mode GUI so it has no prefab or Canvas dependency and
    /// remains available while debugging incomplete scenes. Expensive scene/object discovery is
    /// performed only while the corresponding visible window is being drawn. The owning support
    /// scene controls its lifetime. The component temporarily owns two
    /// pieces of global state: cursor presentation
    /// while visible and the dialogue-trigger setting for its lifetime. Cursor state is restored
    /// when the overlay hides or the component is disabled; the captured dialogue-trigger setting
    /// is restored when the owning dashboard is destroyed.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DebugEventMonitor))]
    [AddComponentMenu("Quiet Static/Debug Tools/Debug Dashboard")]
    public sealed class DebugDashboard : MonoBehaviour
    {
        private const float Megabyte = 1024f * 1024f;
        private const float TitleBarHeight = 24f;

        [Header("Visibility")]
        [Tooltip("Show the overlay when this component first starts.")]
        [SerializeField] private bool visibleOnStart = true;

        [Tooltip("Keyboard shortcut used to show or hide the entire debug overlay.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        [Tooltip("Show and unlock the cursor while the dashboard is visible, then restore its previous state.")]
        [SerializeField] private bool unlockCursorWhileVisible = true;

        [Header("Initial Windows")]
        [Tooltip("Open the performance metrics window when the dashboard first appears.")]
        [SerializeField] private bool showPerformance = true;

        [Tooltip("Open the runtime scene-state and transition window when the dashboard first appears.")]
        [SerializeField] private bool showRuntimeState = true;

        [Tooltip("Open the flag manipulation window when the dashboard first appears.")]
        [SerializeField] private bool showFlags = true;

        [Tooltip("Open the logs window when the dashboard first appears.")]
        [SerializeField] private bool showTrace = true;

        [Tooltip("Open the scene teleport window when the dashboard first appears.")]
        [SerializeField] private bool showTeleport = true;

        [Tooltip("Open the cutscene explorer/player window when the dashboard first appears.")]
        [SerializeField] private bool showCutscenes = true;

        [Header("Flags")]
        [Tooltip("Optional database used for flag descriptions and controls. Falls back to the active FlagManager database.")]
        [SerializeField] private FlagDatabase flagDatabase;

        [Header("Scene Flow")]
        [Tooltip("Optional configured scene graph shown above the low-level Build Settings controls when a SceneFlowManager is available.")]
        [SerializeField] private SceneFlowMap sceneFlowMap;

        [Tooltip("Scene-flow command channel used by every dashboard scene operation.")]
        [RequiredCommandChannel]
        [SerializeField] private SceneFlowRequestChannel sceneFlowRequestChannel;

        [Tooltip("Show connections from every source scene instead of only connections leaving the active scene.")]
        [SerializeField] private bool showAllSceneConnections;

        [Header("Sampling & Trace")]
        [Tooltip("Seconds between performance display refreshes.")]
        [Min(0.05f)]
        [SerializeField] private float samplePeriod = 0.25f;

        [Tooltip("Maximum entries retained by the process-wide shared trace. This setting affects every trace consumer.")]
        [Range(10, 500)]
        [SerializeField] private int traceCapacity = 100;

        [Header("Layout")]
        [Tooltip("Toolbar and compact-view position. Runtime layout controls their rendered size.")]
        [SerializeField] private Rect windowRect = new(12f, 12f, 520f, 680f);

        [Tooltip("Initial scene transition window rectangle in screen pixels.")]
        [SerializeField] private Rect sceneWindowRect = new(12f, 74f, 470f, 560f);

        [Tooltip("Initial flag manipulation window rectangle in screen pixels.")]
        [SerializeField] private Rect flagWindowRect = new(494f, 74f, 430f, 560f);

        [Tooltip("Initial performance metrics window rectangle in screen pixels.")]
        [SerializeField] private Rect performanceWindowRect = new(936f, 74f, 400f, 210f);

        [Tooltip("Initial logs and events window rectangle in screen pixels.")]
        [SerializeField] private Rect logWindowRect = new(936f, 296f, 560f, 420f);

        [Tooltip("Initial scene teleport window rectangle in screen pixels.")]
        [SerializeField] private Rect teleportWindowRect = new(494f, 646f, 430f, 360f);

        [Tooltip("Initial cutscene explorer window rectangle in screen pixels.")]
        [SerializeField] private Rect cutsceneWindowRect = new(12f, 646f, 470f, 420f);

        // These runtime booleans are initialized from the serialized "Initial Windows" settings,
        // then become independent so users can open and close tools without changing configuration.
        private bool isVisible;
        private bool compactView;
        private bool sceneWindowOpen;
        private bool flagWindowOpen;
        private bool performanceWindowOpen;
        private bool logWindowOpen;
        private string traceFilter = string.Empty;
        private bool teleportWindowOpen;
        private bool cutsceneWindowOpen;
        // Performance values are sampled with unscaled time so pause menus and timeScale changes do
        // not make the diagnostic display misleading. The worst value persists until explicitly reset.
        private int framesInSample;
        private float sampleStartTime;
        private float fps;
        private float frameMilliseconds;
        private float worstFrameMilliseconds;
        // IMGUI controls are rebuilt every event, so scroll positions and text inputs must be retained.
        private Vector2 sceneScroll;
        private Vector2 flagScroll;
        private Vector2 logScroll;
        private Vector2 teleportScroll;
        private Vector2 cutsceneScroll;
        private string flagFilter = string.Empty;
        private string customFlag = string.Empty;
        private string customScene = string.Empty;
        private string sceneConnectionFilter = string.Empty;
        private string cutsceneFilter = string.Empty;
        private string cutsceneTargetScene = string.Empty;
        private string destinationCutscene = string.Empty;
        // Runtime collaborators and lazily constructed styles. Styles cannot be safely built before
        // OnGUI because GUI.skin is only guaranteed to exist during an IMGUI event.
        private CutsceneTransitionPlayer cutsceneTransitionPlayer;
        private GUIStyle headerStyle;
        private GUIStyle dimStyle;
        private GUIStyle eventStyle;
        // Cursor ownership records exactly what the dashboard changed, allowing Hide/disable/destroy
        // paths to restore the state that gameplay had established before the overlay opened.
        private CursorLockMode cursorLockStateBeforeOpening;
        private bool cursorVisibleBeforeOpening;
        private bool ownsCursor;
        private bool hasStarted;
        // Dialogue trigger starts are a static global switch. Capture it once and put it back when
        // this accepted singleton instance is destroyed.
        private bool originalDialogueTriggerState;
        private bool ownsDialogueTriggerState;

        /// <summary>Gets the requested overlay visibility state, independent of component activation.</summary>
        public bool IsVisible => isVisible;

        /// <summary>Initializes runtime tool state owned by this dashboard's support scene.</summary>
        private void Awake()
        {
            originalDialogueTriggerState = DialogueEventPlayer.TriggerStartsEnabled;
            ownsDialogueTriggerState = true;

            // Transition-and-play controls require this helper. Adding it here keeps the debug
            // prefab/scene backward compatible when the component was not serialized previously.
            cutsceneTransitionPlayer = GetComponent<CutsceneTransitionPlayer>();
            if (cutsceneTransitionPlayer == null)
            {
                cutsceneTransitionPlayer = gameObject.AddComponent<CutsceneTransitionPlayer>();
            }
            // Copy configuration into mutable runtime state; the serialized defaults remain intact.
            sceneWindowOpen = showRuntimeState;
            flagWindowOpen = showFlags;
            performanceWindowOpen = showPerformance;
            logWindowOpen = showTrace;
            teleportWindowOpen = showTeleport;
            cutsceneWindowOpen = showCutscenes;
            DebugTrace.SetCapacity(traceCapacity);
            DebugTrace.SetEnabled(true);

        }

        /// <summary>Starts performance sampling and applies the configured initial visibility.</summary>
        private void Start()
        {
            // OnEnable runs before Start, so this marker prevents it from acquiring a cursor for an
            // overlay whose visibleOnStart preference has not yet been applied.
            hasStarted = true;
            sampleStartTime = Time.realtimeSinceStartup;
            SetVisible(visibleOnStart);
        }

        /// <summary>Reacquires cursor ownership when a visible dashboard is re-enabled.</summary>
        private void OnEnable()
        {
            if (hasStarted && isVisible)
            {
                AcquireCursor();
            }
        }

        /// <summary>Returns cursor ownership whenever Unity disables the component or GameObject.</summary>
        private void OnDisable() => ReleaseCursor();

        /// <summary>
        /// Restores any owned cursor state and the captured dialogue-trigger state, then releases
        /// this dashboard's shared debug state.
        /// </summary>
        private void OnDestroy()
        {
            ReleaseCursor();
            if (ownsDialogueTriggerState)
            {
                // Do not leave a debug-session toggle active after the dashboard/Play Mode exits.
                DialogueEventPlayer.TriggerStartsEnabled = originalDialogueTriggerState;
            }
            DebugTrace.SetEnabled(false);
        }

        /// <summary>Accumulates unscaled frame-time statistics for the performance windows.</summary>
        private void Update()
        {
            framesInSample++;
            worstFrameMilliseconds = Mathf.Max(worstFrameMilliseconds, Time.unscaledDeltaTime * 1000f);

            float now = Time.realtimeSinceStartup;
            float elapsed = now - sampleStartTime;
            if (elapsed < samplePeriod)
            {
                return;
            }

            // Average over the requested interval rather than displaying a noisy one-frame inverse.
            fps = framesInSample / Mathf.Max(0.0001f, elapsed);
            frameMilliseconds = 1000f / Mathf.Max(0.01f, fps);
            framesInSample = 0;
            sampleStartTime = now;
        }

        /// <summary>Keeps the cursor usable if gameplay code attempts to relock it this frame.</summary>
        private void LateUpdate()
        {
            if (isVisible && unlockCursorWhileVisible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Handles the overlay shortcut and renders either the compact view or the hub plus every
        /// currently open tool window.
        /// </summary>
        private void OnGUI()
        {
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == toggleKey)
            {
                // Consume the key so an underlying IMGUI control does not also act on the shortcut.
                SetVisible(!isVisible);
                current.Use();
            }

            if (!isVisible)
            {
                return;
            }

            EnsureStyles();
            if (compactView)
            {
                // Compact view deliberately reuses the toolbar position so switching modes feels
                // like resizing one hub rather than opening another unrelated window.
                windowRect.width = 390f;
                windowRect.height = 142f;
                windowRect = DrawClampedWindow(GetInstanceID(), windowRect, DrawCompactWindow, "DEBUG TOOLS — MINI");
                return;
            }

            Rect hubRect = new(windowRect.x, windowRect.y, 840f, 54f);
            hubRect = DrawClampedWindow(GetInstanceID(), hubRect, DrawHubWindow, $"DEBUG TOOLS — {toggleKey} to hide");
            windowRect.x = hubRect.x;
            windowRect.y = hubRect.y;

            if (sceneWindowOpen)
            {
                sceneWindowRect = DrawClampedWindow(GetInstanceID() + 1, sceneWindowRect, DrawSceneWindow, "SCENE TRANSITIONS");
            }

            if (flagWindowOpen)
            {
                flagWindowRect = DrawClampedWindow(GetInstanceID() + 2, flagWindowRect, DrawFlagWindow, "FLAG MANIPULATION");
            }

            if (performanceWindowOpen)
            {
                performanceWindowRect = DrawClampedWindow(GetInstanceID() + 3, performanceWindowRect, DrawPerformanceWindow, "PERFORMANCE METRICS");
            }

            if (logWindowOpen)
            {
                logWindowRect = DrawClampedWindow(GetInstanceID() + 4, logWindowRect, DrawLogWindow, "LOGS & EVENTS");
            }

            if (teleportWindowOpen)
            {
                teleportWindowRect = DrawClampedWindow(
                    GetInstanceID() + 5,
                    teleportWindowRect,
                    DrawTeleportWindow,
                    "TELEPORT");
            }

            if (cutsceneWindowOpen)
            {
                cutsceneWindowRect = DrawClampedWindow(
                    GetInstanceID() + 6,
                    cutsceneWindowRect,
                    DrawCutsceneWindow,
                    "CUTSCENE EXPLORER / PLAYER");
            }
        }

        /// <summary>Shows the dashboard and, when configured, unlocks and displays the cursor.</summary>
        /// <remarks>This parameterless method is suitable for a debug button or UnityEvent.</remarks>
        public void Show() => SetVisible(true);

        /// <summary>
        /// Hides the entire dashboard and restores the cursor state if this dashboard captured it.
        /// </summary>
        /// <remarks>This parameterless method is suitable for a debug button or UnityEvent.</remarks>
        public void Hide() => SetVisible(false);

        /// <summary>Toggles the entire dashboard, including its optional cursor ownership.</summary>
        /// <remarks>This parameterless method is suitable for a debug button or UnityEvent.</remarks>
        public void Toggle() => SetVisible(!isVisible);

        /// <summary>Applies a requested overlay visibility state and its cursor side effects.</summary>
        /// <param name="visible"><see langword="true"/> to display the overlay.</param>
        private void SetVisible(bool visible)
        {
            if (isVisible == visible)
            {
                if (visible)
                {
                    // Idempotent Show calls can repair cursor ownership after a component toggle.
                    AcquireCursor();
                }
                return;
            }

            isVisible = visible;
            if (isVisible)
            {
                AcquireCursor();
            }
            else
            {
                ReleaseCursor();
            }
        }

        /// <summary>Snapshots gameplay's cursor state and switches to an unlocked visible cursor.</summary>
        /// <remarks>
        /// The ownership guard is essential: taking a second snapshot after unlocking would lose
        /// the original state that must be restored when the overlay closes.
        /// </remarks>
        private void AcquireCursor()
        {
            if (!unlockCursorWhileVisible || ownsCursor)
            {
                return;
            }

            cursorLockStateBeforeOpening = Cursor.lockState;
            cursorVisibleBeforeOpening = Cursor.visible;
            ownsCursor = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Restores the cursor state captured by <see cref="AcquireCursor"/>.</summary>
        private void ReleaseCursor()
        {
            if (!ownsCursor)
            {
                return;
            }

            Cursor.lockState = cursorLockStateBeforeOpening;
            Cursor.visible = cursorVisibleBeforeOpening;
            ownsCursor = false;
        }

        /// <summary>Draws the top-level tool toggles and global dialogue-trigger control.</summary>
        /// <param name="id">
        /// IMGUI identifier supplied by <see cref="GUI.Window(int, Rect, GUI.WindowFunction, string)"/>.
        /// </param>
        private void DrawHubWindow(int id)
        {
            GUILayout.BeginHorizontal();
            DrawWindowToggle("Scenes", ref sceneWindowOpen);
            DrawWindowToggle("Flags", ref flagWindowOpen);
            DrawWindowToggle("Performance", ref performanceWindowOpen);
            DrawWindowToggle("Logs", ref logWindowOpen);
            DrawWindowToggle("Teleport", ref teleportWindowOpen);
            DrawWindowToggle("Cutscenes", ref cutsceneWindowOpen);
            bool dialogueTriggersEnabled = GUILayout.Toggle(
                DialogueEventPlayer.TriggerStartsEnabled,
                "Dialogue triggers",
                GUI.skin.button,
                GUILayout.Width(112f));
            if (dialogueTriggersEnabled != DialogueEventPlayer.TriggerStartsEnabled)
            {
                // This toolkit switch affects collider-started dialogue globally. The dashboard
                // records changes so a future debugging pass can explain why triggers did/did not run.
                DialogueEventPlayer.TriggerStartsEnabled = dialogueTriggersEnabled;
                DebugTrace.Record(
                    "Dialogue",
                    dialogueTriggersEnabled
                        ? "Collider dialogue triggers enabled."
                        : "Collider dialogue triggers disabled.",
                    this);
            }
            if (GUILayout.Button("Mini view", GUILayout.Width(82f)))
            {
                compactView = true;
            }
            GUILayout.EndHorizontal();
            // Restrict dragging to the title bar so interacting with a control does not move the hub.
            GUI.DragWindow(new Rect(0f, 0f, 840f, TitleBarHeight));
        }

        /// <summary>
        /// Draws controls for cross-scene cutscene playback and all loaded sequence runners.
        /// </summary>
        /// <param name="id">
        /// IMGUI identifier supplied by <see cref="GUI.Window(int, Rect, GUI.WindowFunction, string)"/>.
        /// </param>
        private void DrawCutsceneWindow(int id)
        {
            if (DrawCloseButton(cutsceneWindowRect.width))
            {
                cutsceneWindowOpen = false;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", dimStyle, GUILayout.Width(38f));
            cutsceneFilter = GUILayout.TextField(cutsceneFilter ?? string.Empty);
            GUILayout.EndHorizontal();

            DrawHeader("TRANSITION TO CUTSCENE");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Scene", dimStyle, GUILayout.Width(62f));
            cutsceneTargetScene = GUILayout.TextField(cutsceneTargetScene ?? string.Empty);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cutscene", dimStyle, GUILayout.Width(62f));
            destinationCutscene = GUILayout.TextField(destinationCutscene ?? string.Empty);
            GUI.enabled = cutsceneTransitionPlayer != null &&
                          !cutsceneTransitionPlayer.IsPending &&
                          !string.IsNullOrWhiteSpace(cutsceneTargetScene) &&
                          !string.IsNullOrWhiteSpace(destinationCutscene);
            if (GUILayout.Button("Transition + Play", GUILayout.Width(118f)))
            {
                bool accepted = cutsceneTransitionPlayer.TransitionAndPlay(
                    cutsceneTargetScene,
                    destinationCutscene);
                DebugTrace.Record(
                    "Cutscene",
                    accepted
                        ? $"Transitioning to '{cutsceneTargetScene}' for cutscene '{destinationCutscene}'."
                        : "Cutscene transition request was rejected.",
                    this);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // Include inactive runners for inspection, while the individual Play buttons remain
            // disabled until a runner is active and can actually execute.
            CutsceneSequenceRunner[] runners = FindObjectsByType<CutsceneSequenceRunner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            cutsceneScroll = GUILayout.BeginScrollView(cutsceneScroll);
            bool drewRunner = false;
            foreach (CutsceneSequenceRunner runner in runners.OrderBy(item => item.DisplayName))
            {
                if (!string.IsNullOrWhiteSpace(cutsceneFilter) &&
                    !ContainsIgnoreCase(runner.DisplayName, cutsceneFilter) &&
                    !runner.Steps.Any(step => step != null && ContainsIgnoreCase(step.name, cutsceneFilter)))
                {
                    continue;
                }

                drewRunner = true;
                DrawHeader(runner.DisplayName);
                GUILayout.Label(
                    runner.IsRunning
                        ? $"Playing step {runner.CurrentStepIndex + 1} of {runner.Steps.Count}"
                        : $"{runner.Steps.Count} step(s)",
                    runner.IsRunning ? GUI.skin.label : dimStyle);
                GUILayout.BeginHorizontal();
                // GUI.enabled is global IMGUI state, so every conditional block restores it before
                // drawing the next control or runner.
                GUI.enabled = runner.isActiveAndEnabled && !runner.IsRunning;
                if (GUILayout.Button("Play all"))
                {
                    runner.Play();
                    DebugTrace.Record("Cutscene", $"Debug play: {runner.DisplayName}", runner);
                }
                GUI.enabled = runner.IsRunning;
                if (GUILayout.Button("Stop"))
                {
                    runner.Stop();
                    DebugTrace.Record("Cutscene", $"Debug stop: {runner.DisplayName}", runner);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                for (int stepIndex = 0; stepIndex < runner.Steps.Count; stepIndex++)
                {
                    CutsceneSequenceRunner.Step step = runner.Steps[stepIndex];
                    string stepName = step == null || string.IsNullOrWhiteSpace(step.name)
                        ? $"Step {stepIndex + 1}"
                        : step.name;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{stepIndex + 1}. {stepName}");
                    GUI.enabled = runner.isActiveAndEnabled && !runner.IsRunning && step != null;
                    int capturedIndex = stepIndex;
                    if (GUILayout.Button("Play", GUILayout.Width(48f)))
                    {
                        runner.PlayStep(capturedIndex);
                        DebugTrace.Record(
                            "Cutscene",
                            $"Debug play: {runner.DisplayName} / {stepName}",
                            runner);
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            if (!drewRunner)
            {
                GUILayout.Label("No loaded cutscene runners match the filter.", dimStyle);
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, cutsceneWindowRect.width - 34f, TitleBarHeight));
        }

        /// <summary>Draws a compact read-only health summary with expand and hide actions.</summary>
        /// <param name="id">
        /// IMGUI identifier supplied by <see cref="GUI.Window(int, Rect, GUI.WindowFunction, string)"/>.
        /// </param>
        private void DrawCompactWindow(int id)
        {
            Scene active = SceneManager.GetActiveScene();
            string state = GameStateManager.Instance != null
                ? GameStateManager.Instance.CurrentState
                : "No state manager";
            FlagManager flagManager = FlagManager.Instance;
            int activeFlagCount = flagManager != null ? flagManager.ActiveFlags.Count() : 0;
            SceneFlowManager flow = SceneFlowManager.Instance;
            string transition = flow != null && flow.IsTransitioning ? "TRANSITIONING" : "Stable";

            GUILayout.Label($"{fps:0} FPS  |  {frameMilliseconds:0.0} ms  |  {active.name}  |  {transition}");
            GUILayout.Label(
                $"{state}  |  {activeFlagCount} flags  |  {DebugTrace.Entries.Count} log entries  |  " +
                $"dialogue triggers {(DialogueEventPlayer.TriggerStartsEnabled ? "on" : "off")}",
                dimStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Expand tools"))
            {
                compactView = false;
            }
            if (GUILayout.Button("Hide"))
            {
                SetVisible(false);
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 390f, TitleBarHeight));
        }

        /// <summary>
        /// Draws current scene state, configured flow-map routes, and low-level scene controls.
        /// </summary>
        /// <param name="id">
        /// IMGUI identifier supplied by <see cref="GUI.Window(int, Rect, GUI.WindowFunction, string)"/>.
        /// </param>
        private void DrawSceneWindow(int id)
        {
            if (DrawCloseButton(sceneWindowRect.width))
            {
                sceneWindowOpen = false;
            }

            sceneScroll = GUILayout.BeginScrollView(sceneScroll);
            DrawRuntimeState();

            SceneFlowManager flow = SceneFlowManager.Instance;
            if (flow == null)
            {
                GUILayout.Label("No SceneFlowManager. Add one to the persistent Systems scene.", dimStyle);
            }
            else
            {
                if (sceneFlowRequestChannel == null || !sceneFlowRequestChannel.HasReceivers)
                {
                    GUILayout.Label(
                        "Scene commands unavailable: assign a channel with an active receiver.",
                        dimStyle);
                }
                DrawConfiguredSceneConnections(flow);

                // Build Settings provide an authoritative fallback even when no SceneFlowMap was
                // assigned, which is useful while bringing up or repairing a content scene.
                DrawHeader("BUILD SCENES");
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                {
                    string sceneName = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                    DrawBuildSceneRow(flow, sceneName);
                }

                DrawHeader("CUSTOM SCENE NAME");
                GUILayout.BeginHorizontal();
                customScene = GUILayout.TextField(customScene ?? string.Empty);
                GUI.enabled = CanRequestSceneFlow() &&
                              !string.IsNullOrWhiteSpace(customScene) &&
                              !flow.IsTransitioning;
                if (GUILayout.Button("Transition", GUILayout.Width(82f)))
                {
                    RequestDebugTransition(
                        new SceneTransitionRequest(customScene.Trim()),
                        $"custom scene '{customScene.Trim()}'");
                }
                if (GUILayout.Button("Additive", GUILayout.Width(70f)))
                {
                    sceneFlowRequestChannel?.TryLoadAdditive(customScene.Trim());
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, sceneWindowRect.width - 34f, TitleBarHeight));
        }

        /// <summary>Draws transition, additive-load, activation, and unload actions for one scene.</summary>
        /// <param name="flow">Active scene-flow service that owns the requested operations.</param>
        /// <param name="sceneName">Build Settings scene name without its file extension.</param>
        private void DrawBuildSceneRow(SceneFlowManager flow, string sceneName)
        {
            bool loaded = flow.IsSceneLoaded(sceneName);
            bool active = SceneManager.GetActiveScene().name == sceneName;
            GUILayout.BeginHorizontal();
            // '*' marks Unity's active scene, '+' an additively loaded non-active scene.
            GUILayout.Label($"{(active ? "*" : loaded ? "+" : " ")} {sceneName}");
            GUI.enabled = CanRequestSceneFlow() && !flow.IsTransitioning && !active;
            if (GUILayout.Button("Go", GUILayout.Width(42f)))
            {
                RequestDebugTransition(
                    new SceneTransitionRequest(sceneName),
                    $"build scene '{sceneName}'");
            }
            GUI.enabled = CanRequestSceneFlow() && !flow.IsTransitioning && !loaded;
            if (GUILayout.Button("Load", GUILayout.Width(48f)))
            {
                sceneFlowRequestChannel?.TryLoadAdditive(sceneName);
            }
            GUI.enabled = CanRequestSceneFlow() && loaded && !active;
            if (GUILayout.Button("Active", GUILayout.Width(52f)))
            {
                sceneFlowRequestChannel?.TrySetActive(sceneName);
            }
            if (GUILayout.Button("Unload", GUILayout.Width(58f)))
            {
                sceneFlowRequestChannel?.TryUnload(sceneName);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>Draws the optional high-level routes declared by the configured scene-flow map.</summary>
        /// <param name="flow">Active manager used to gate requests during an existing transition.</param>
        private void DrawConfiguredSceneConnections(SceneFlowManager flow)
        {
            if (sceneFlowMap == null)
            {
                return;
            }

            DrawHeader("CONFIGURED CONNECTIONS");
            string activeScene = SceneManager.GetActiveScene().name;
            GUILayout.BeginHorizontal();
            showAllSceneConnections = GUILayout.Toggle(
                showAllSceneConnections,
                "Show all routes",
                GUILayout.Width(112f));
            GUILayout.Label("Filter", dimStyle, GUILayout.Width(38f));
            sceneConnectionFilter = GUILayout.TextField(
                sceneConnectionFilter ?? string.Empty);
            GUILayout.EndHorizontal();

            // Normal mode presents configured exits from the active scene. "Show all" is a debug
            // escape hatch for forcing a configured request whose source scene is not currently active.
            IEnumerable<SceneFlowMap.Connection> visibleConnections =
                showAllSceneConnections
                    ? sceneFlowMap.Connections
                    : sceneFlowMap.GetConnectionsFrom(activeScene);
            bool drewConnection = false;
            foreach (SceneFlowMap.Connection connection in visibleConnections)
            {
                if (connection == null || !MatchesConnectionFilter(connection))
                {
                    continue;
                }

                drewConnection = true;
                bool isCurrentSource =
                    string.IsNullOrWhiteSpace(connection.FromSceneName) ||
                    connection.FromSceneName == activeScene;
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    $"{connection.FromSceneName}  ->  {connection.ToSceneName}\n{connection.Id}",
                    isCurrentSource ? GUI.skin.label : dimStyle);
                GUI.enabled = CanRequestSceneFlow() &&
                              !flow.IsTransitioning &&
                              (isCurrentSource || showAllSceneConnections) &&
                              !string.IsNullOrWhiteSpace(connection.ToSceneName);
                string buttonLabel = isCurrentSource ? "Travel" : "Force";
                if (GUILayout.Button(buttonLabel, GUILayout.Width(58f)))
                {
                    RequestDebugTransition(
                        connection.CreateRequest(),
                        $"connection '{connection.Id}'");
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            if (!drewConnection)
            {
                GUILayout.Label(
                    showAllSceneConnections
                        ? "No configured connections match the filter."
                        : $"No configured connections leave '{activeScene}'.",
                    dimStyle);
            }
        }

        /// <summary>Checks a route's id, source, and destination against the current text filter.</summary>
        /// <param name="connection">Route whose identifying strings should be searched.</param>
        /// <returns><see langword="true"/> when the filter is blank or any field contains it.</returns>
        private bool MatchesConnectionFilter(SceneFlowMap.Connection connection)
        {
            if (string.IsNullOrWhiteSpace(sceneConnectionFilter))
            {
                return true;
            }

            return ContainsIgnoreCase(connection.Id, sceneConnectionFilter) ||
                   ContainsIgnoreCase(connection.FromSceneName, sceneConnectionFilter) ||
                   ContainsIgnoreCase(connection.ToSceneName, sceneConnectionFilter);
        }

        /// <summary>Performs a null-safe ordinal case-insensitive substring test.</summary>
        /// <param name="value">Potentially blank string to search.</param>
        /// <param name="filter">Substring to find. Callers provide a nonblank value.</param>
        /// <returns><see langword="true"/> when <paramref name="value"/> contains the filter.</returns>
        private static bool ContainsIgnoreCase(string value, string filter)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Sends a transition through the configured command channel.</summary>
        /// <param name="request">Fully configured scene-flow request to dispatch.</param>
        /// <param name="description">Human-readable source included in the diagnostic trace.</param>
        private void RequestDebugTransition(
            SceneTransitionRequest request,
            string description)
        {
            bool accepted = sceneFlowRequestChannel != null &&
                            sceneFlowRequestChannel.RequestTransition(request);

            DebugTrace.Record(
                "Scene Flow",
                accepted
                    ? $"Requested transition via {description}."
                    : $"Could not request transition via {description}; no receiver is available.",
                this);
        }

        private bool CanRequestSceneFlow() =>
            sceneFlowRequestChannel != null && sceneFlowRequestChannel.HasReceivers;

        /// <summary>Draws game state and a snapshot of every loaded Unity scene.</summary>
        private void DrawRuntimeState()
        {
            string state = GameStateManager.Instance != null
                ? GameStateManager.Instance.CurrentState
                : "(no GameStateManager)";
            Scene active = SceneManager.GetActiveScene();
            SceneFlowManager flow = SceneFlowManager.Instance;
            GUILayout.Label($"State: {state}  —  Frame: {Time.frameCount}  —  Uptime: {Time.realtimeSinceStartup:0.0}s");
            GUILayout.Label($"Active: {active.name}  —  Loaded: {SceneManager.sceneCount}  —  " +
                            $"Transition: {(flow != null && flow.IsTransitioning ? "running" : "idle")}");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                GUILayout.Label($"  {(scene == active ? "*" : "-")} {scene.name} ({scene.rootCount} roots)", dimStyle);
            }
        }

        /// <summary>Draws active, known inactive, and custom runtime-flag controls.</summary>
        /// <param name="id">
        /// IMGUI identifier supplied by <see cref="GUI.Window(int, Rect, GUI.WindowFunction, string)"/>.
        /// </param>
        private void DrawFlagWindow(int id)
        {
            if (DrawCloseButton(flagWindowRect.width))
            {
                flagWindowOpen = false;
            }

            flagScroll = GUILayout.BeginScrollView(flagScroll);
            FlagManager flagManager = FlagManager.Instance;
            if (flagManager == null)
            {
                GUILayout.Label("No active FlagManager. Add one to the persistent Systems scene.", dimStyle);
                GUILayout.EndScrollView();
                return;
            }

            // Snapshot and sort the enumerable before drawing. A button may mutate the manager's
            // active collection during this same IMGUI pass, so iterating it directly would be unsafe.
            string[] activeFlags = flagManager.ActiveFlags.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            GUILayout.Label($"Active: {activeFlags.Length}");
            GUILayout.Label("Filter", dimStyle);
            flagFilter = GUILayout.TextField(flagFilter ?? string.Empty);

            foreach (string flag in activeFlags)
            {
                if (!MatchesFlagFilter(flag))
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label($"[x] {flag}");
                if (GUILayout.Button("Clear", GUILayout.Width(64f)))
                {
                    flagManager.ClearFlag(flag);
                }
                GUILayout.EndHorizontal();
            }

            // An explicitly assigned database is useful for debugging another content set; otherwise
            // mirror the live manager's database so labels and controls reflect its configuration.
            FlagDatabase displayDatabase = flagDatabase != null ? flagDatabase : flagManager.Database;
            if (displayDatabase != null && displayDatabase.Flags != null)
            {
                DrawHeader("KNOWN INACTIVE FLAGS");
                foreach (FlagDatabase.FlagDefinition definition in displayDatabase.Flags)
                {
                    if (definition == null || string.IsNullOrWhiteSpace(definition.id) ||
                        flagManager.HasFlag(definition.id) || !MatchesFlagFilter(definition.id))
                    {
                        continue;
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"[ ] {definition.id}");
                    if (GUILayout.Button("Set", GUILayout.Width(64f)))
                    {
                        flagManager.SetFlag(definition.id);
                    }
                    GUILayout.EndHorizontal();

                    if (!string.IsNullOrWhiteSpace(definition.description))
                    {
                        GUILayout.Label($"    {definition.description}", dimStyle);
                    }
                }
            }

            DrawHeader("CUSTOM FLAG");
            GUILayout.BeginHorizontal();
            customFlag = GUILayout.TextField(customFlag ?? string.Empty);
            GUI.enabled = !string.IsNullOrWhiteSpace(customFlag);
            if (GUILayout.Button("Set", GUILayout.Width(64f)))
            {
                flagManager.SetFlag(customFlag.Trim());
                customFlag = string.Empty;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, flagWindowRect.width - 34f, TitleBarHeight));
        }

        /// <summary>Draws sampled timing, profiler memory totals, and display timing information.</summary>
        /// <param name="id">
        /// IMGUI identifier supplied by <see cref="GUI.Window(int, Rect, GUI.WindowFunction, string)"/>.
        /// </param>
        private void DrawPerformanceWindow(int id)
        {
            if (DrawCloseButton(performanceWindowRect.width))
            {
                performanceWindowOpen = false;
            }

            GUILayout.Label($"{fps:0.0} FPS  —  {frameMilliseconds:0.00} ms avg  —  {worstFrameMilliseconds:0.00} ms worst");
            GUILayout.Label(
                $"Allocated {Profiler.GetTotalAllocatedMemoryLong() / Megabyte:0.0} MB  —  " +
                $"Reserved {Profiler.GetTotalReservedMemoryLong() / Megabyte:0.0} MB");
            GUILayout.Label(
                $"Time scale {Time.timeScale:0.##}  —  {Screen.width}x{Screen.height}  —  VSync {QualitySettings.vSyncCount}");

            if (GUILayout.Button("Reset worst frame", GUILayout.Width(140f)))
            {
                worstFrameMilliseconds = 0f;
            }
            GUI.DragWindow(new Rect(0f, 0f, performanceWindowRect.width - 34f, TitleBarHeight));
        }

        /// <summary>Draws destinations registered by loaded scenes and teleports the managed player.</summary>
        /// <param name="id">
        /// IMGUI identifier supplied by <see cref="GUI.Window(int, Rect, GUI.WindowFunction, string)"/>.
        /// </param>
        private void DrawTeleportWindow(int id)
        {
            if (DrawCloseButton(teleportWindowRect.width))
            {
                teleportWindowOpen = false;
            }

            GameObject player = PlayerManager.Instance != null ? PlayerManager.Instance.Player : null;
            if (player == null)
            {
                GUILayout.Label("No player is assigned to the persistent PlayerManager.", dimStyle);
            }

            teleportScroll = GUILayout.BeginScrollView(teleportScroll);
            // ActiveAreas is maintained by each area's enable/disable lifecycle, so this list
            // naturally tracks additive scene loads and unloads without scanning the whole scene.
            IReadOnlyList<DebugTeleportArea> areas = DebugTeleportArea.ActiveAreas;
            bool drewArea = false;

            for (int areaIndex = 0; areaIndex < areas.Count; areaIndex++)
            {
                DebugTeleportArea area = areas[areaIndex];
                if (area == null || area.DestinationCount == 0)
                {
                    continue;
                }

                drewArea = true;
                DrawHeader($"{area.SceneName} / {area.DisplayName}");
                for (int destinationIndex = 0; destinationIndex < area.DestinationCount; destinationIndex++)
                {
                    Transform destination = area.GetDestination(destinationIndex);
                    if (destination == null)
                    {
                        continue;
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(destination.name);
                    GUI.enabled = player != null;
                    if (GUILayout.Button("Teleport", GUILayout.Width(76f)) &&
                        area.Teleport(player.transform, destinationIndex))
                    {
                        DebugTrace.Record(
                            "Teleport",
                            $"Moved {player.name} to {area.SceneName}/{area.DisplayName}/{destination.name}.");
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            if (!drewArea)
            {
                GUILayout.Label(
                    "No destinations found. Add a DebugTeleportArea to a scene object and create named children beneath it.",
                    dimStyle);
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, teleportWindowRect.width - 34f, TitleBarHeight));
        }

        /// <summary>Draws the bounded shared diagnostic trace with the newest event first.</summary>
        /// <param name="id">
        /// IMGUI identifier supplied by <see cref="GUI.Window(int, Rect, GUI.WindowFunction, string)"/>.
        /// </param>
        private void DrawLogWindow(int id)
        {
            if (DrawCloseButton(logWindowRect.width))
            {
                logWindowOpen = false;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{DebugTrace.Entries.Count} retained entries", dimStyle);
            if (GUILayout.Button("Clear", GUILayout.Width(64f)))
            {
                DebugTrace.Clear();
            }
            GUILayout.EndHorizontal();
            traceFilter = GUILayout.TextField(traceFilter ?? string.Empty);

            logScroll = GUILayout.BeginScrollView(logScroll);
            IReadOnlyList<DebugTrace.Entry> entries = DebugTrace.Entries;
            if (entries.Count == 0)
            {
                GUILayout.Label("No activity recorded yet.", dimStyle);
            }
            else
            {
                float now = Time.realtimeSinceStartup;
                // DebugTrace stores oldest-to-newest; reverse traversal surfaces current activity.
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    DebugTrace.Entry entry = entries[i];
                    if (!MatchesTraceFilter(entry))
                    {
                        continue;
                    }
                    string correlation = string.IsNullOrEmpty(entry.CorrelationId)
                        ? string.Empty
                        : $" [{entry.CorrelationId}]";
                    GUILayout.Label(
                        $"-{now - entry.Time,6:0.00}s  f{entry.Frame,-6} [{entry.Category}]{correlation} {entry.Message}",
                        eventStyle);
                }
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, logWindowRect.width - 34f, TitleBarHeight));
        }

        private bool MatchesTraceFilter(DebugTrace.Entry entry)
        {
            if (string.IsNullOrWhiteSpace(traceFilter))
            {
                return true;
            }

            string filter = traceFilter.Trim();
            return ContainsIgnoreCase(entry.Category, filter) ||
                   ContainsIgnoreCase(entry.Message, filter) ||
                   ContainsIgnoreCase(entry.Source, filter) ||
                   ContainsIgnoreCase(entry.EventType, filter) ||
                   ContainsIgnoreCase(entry.Receiver, filter) ||
                   ContainsIgnoreCase(entry.Outcome, filter) ||
                   ContainsIgnoreCase(entry.CorrelationId, filter);
        }

        /// <summary>Draws a title-bar close button aligned to the current window's right edge.</summary>
        /// <param name="windowWidth">Current screen-space window width.</param>
        /// <returns><see langword="true"/> during the IMGUI event in which it is clicked.</returns>
        private static bool DrawCloseButton(float windowWidth)
        {
            return GUI.Button(new Rect(windowWidth - 24f, 2f, 20f, 20f), "×");
        }

        /// <summary>Draws a button-styled toggle and stores the resulting window-open state.</summary>
        /// <param name="label">Text displayed on the toggle button.</param>
        /// <param name="open">Current state, replaced with the user's selected state.</param>
        private static void DrawWindowToggle(string label, ref bool open)
        {
            bool next = GUILayout.Toggle(open, label, GUI.skin.button);
            open = next;
        }

        /// <summary>Checks a flag id against the current case-insensitive substring filter.</summary>
        /// <param name="value">Flag id to test.</param>
        /// <returns><see langword="true"/> when the filter is blank or the id contains it.</returns>
        private bool MatchesFlagFilter(string value)
        {
            return string.IsNullOrWhiteSpace(flagFilter) ||
                   value.IndexOf(flagFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Draws a consistently spaced section heading inside a dashboard window.</summary>
        /// <param name="label">Heading text.</param>
        private void DrawHeader(string label)
        {
            GUILayout.Space(6f);
            GUILayout.Label(label, headerStyle);
        }

        /// <summary>
        /// Draws a movable window, applies the dashboard's minimum size, and clamps its position as
        /// far as the current screen dimensions permit.
        /// </summary>
        /// <param name="id">Stable IMGUI id unique among this dashboard's windows.</param>
        /// <param name="rect">Requested screen-space window rectangle.</param>
        /// <param name="draw">Callback that renders the window contents.</param>
        /// <param name="title">Text displayed in the title bar.</param>
        /// <returns>The size-constrained, moved, and position-clamped rectangle for the next event.</returns>
        private Rect DrawClampedWindow(int id, Rect rect, GUI.WindowFunction draw, string title)
        {
            // Width/height are clamped before drawing because GUI.Window needs its final dimensions;
            // position is clamped afterward because the callback may have dragged it this event.
            rect.width = Mathf.Clamp(rect.width, 280f, Mathf.Max(280f, Screen.width));
            rect.height = Mathf.Clamp(rect.height, 54f, Mathf.Max(54f, Screen.height));
            rect = GUI.Window(id, rect, draw, title);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
            return rect;
        }

        /// <summary>Lazily creates styles derived from the active IMGUI skin.</summary>
        private void EnsureStyles()
        {
            if (headerStyle != null)
            {
                return;
            }

            // Clone skin styles instead of mutating GUI.skin.label globally for other IMGUI users.
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = new Color(0.45f, 0.9f, 1f) }
            };
            dimStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            eventStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
        }
    }
}
