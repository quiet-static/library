using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Validation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Unified editable dialogue graph and compact authoring workspace.</summary>
    public sealed class DialogueWorkspaceWindow : EditorWindow
    {
        [SerializeField] private DialogueTree tree;
        [SerializeField] private string selectedNodeId;
        private ObjectField treePicker;
        private ToolbarToggle graphToggle;
        private ToolbarToggle browserToggle;
        private DialogueGraphModel model;
        private DialogueGraphCanvas canvas;
        private IMGUIContainer details;
        private Label status;
        private Vector2 scroll;
        private VisualElement graphPane;
        private IMGUIContainer browserPane;
        private DialogueAssetReport[] reports = Array.Empty<DialogueAssetReport>();
        private string browserSearch = string.Empty;
        private string speakerFilter = string.Empty;
        private string flagFilter = string.Empty;
        private bool issuesOnly;
        private Vector2 browserScroll;
        private string simulatedFlags = string.Empty;
        [SerializeField, Tooltip("Explicit typed values for path simulation. Missing non-Boolean values fail their comparisons.")]
        private List<QuietStatic.Toolkit.Flags.FlagValueEntry> simulatedValues = new();
        private string graphSearch = string.Empty;

        public static void Open()
        {
            DialogueWorkspaceWindow window = GetWindow<DialogueWorkspaceWindow>("Dialogue");
            if (Selection.activeObject is DialogueTree selected) window.SetTree(selected);
        }

        public static void Open(DialogueTree selected)
        {
            DialogueWorkspaceWindow window = GetWindow<DialogueWorkspaceWindow>("Dialogue");
            window.SetTree(selected);
        }

        [MenuItem(QuietStaticMenuPaths.AssetOpen + "Dialogue", true)]
        private static bool ValidateOpenSelected() => Selection.activeObject is DialogueTree;

        [MenuItem(QuietStaticMenuPaths.AssetOpen + "Dialogue")]
        private static void OpenSelected()
        {
            DialogueWorkspaceWindow window = GetWindow<DialogueWorkspaceWindow>("Dialogue");
            window.SetTree(Selection.activeObject as DialogueTree);
        }

        public void CreateGUI()
        {
            var toolbar = new Toolbar();
            rootVisualElement.Clear();
            var picker = new ObjectField { objectType = typeof(DialogueTree), allowSceneObjects = false };
            treePicker = picker;
            picker.style.minWidth = 230f;
            picker.SetValueWithoutNotify(tree);
            picker.RegisterValueChangedCallback(change => SetTree(change.newValue as DialogueTree));
            toolbar.Add(picker);
            toolbar.Add(new ToolbarButton(CreateTree) { text = "Create" });
            toolbar.Add(new ToolbarButton(AddNode) { text = "Add Node" });
            toolbar.Add(new ToolbarButton(DuplicateSelectedNode) { text = "Duplicate Node" });
            toolbar.Add(new ToolbarButton(() => canvas?.FrameAll()) { text = "Frame All" });
            var search = new ToolbarSearchField { style = { width = 150f } };
            search.RegisterValueChangedCallback(change => graphSearch = change.newValue);
            search.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Return) canvas?.FrameNode(graphSearch); });
            toolbar.Add(search);
            toolbar.Add(new ToolbarButton(() => { if (tree != null) AssetDatabase.SaveAssetIfDirty(tree); }) { text = "Save" });
            toolbar.Add(new ToolbarButton(Revert) { text = "Revert" });
            toolbar.Add(new ToolbarButton(Export) { text = "Export JSON" });
            toolbar.Add(new ToolbarButton(CreateEditableCopy) { text = "Editable Copy" });
            var graphTab = graphToggle = new ToolbarToggle { text = "Graph", value = true };
            var browseTab = browserToggle = new ToolbarToggle { text = "Browse" };
            graphTab.RegisterValueChangedCallback(change => { if (change.newValue) ShowGraph(graphTab, browseTab); });
            browseTab.RegisterValueChangedCallback(change => { if (change.newValue) ShowBrowser(graphTab, browseTab); });
            toolbar.Add(graphTab);
            toolbar.Add(browseTab);
            status = new Label { style = { flexGrow = 1f, unityTextAlign = TextAnchor.MiddleRight } };
            toolbar.Add(status);
            rootVisualElement.Add(toolbar);

            graphPane = new TwoPaneSplitView(1, 390f, TwoPaneSplitViewOrientation.Horizontal);
            canvas = new DialogueGraphCanvas();
            canvas.Changed += QueueRefresh;
            canvas.NodeSelected += id => { selectedNodeId = id; details?.MarkDirtyRepaint(); };
            canvas.DeleteNodeRequested = DeleteNode;
            graphPane.Add(canvas);
            details = new IMGUIContainer(DrawDetails);
            graphPane.Add(details);
            rootVisualElement.Add(graphPane);
            browserPane = new IMGUIContainer(DrawBrowser) { style = { flexGrow = 1f, display = DisplayStyle.None } };
            rootVisualElement.Add(browserPane);
            Refresh();
        }

        private void ShowGraph(ToolbarToggle graph, ToolbarToggle browse)
        {
            graph.SetValueWithoutNotify(true);
            browse.SetValueWithoutNotify(false);
            graphPane.style.display = DisplayStyle.Flex;
            browserPane.style.display = DisplayStyle.None;
        }

        private void ShowBrowser(ToolbarToggle graph, ToolbarToggle browse)
        {
            graph.SetValueWithoutNotify(false);
            browse.SetValueWithoutNotify(true);
            reports = DialogueAnalysis.ScanProject().ToArray();
            graphPane.style.display = DisplayStyle.None;
            browserPane.style.display = DisplayStyle.Flex;
            browserPane.MarkDirtyRepaint();
        }

        private void OnEnable() => Undo.undoRedoPerformed += Refresh;
        private void OnDisable() => Undo.undoRedoPerformed -= Refresh;
        private void OnFocus() => Refresh();

        private void QueueRefresh() => EditorApplication.delayCall += () => { if (this != null) Refresh(); };

        private void SetTree(DialogueTree value)
        {
            if (tree != value) selectedNodeId = null;
            tree = value;
            treePicker?.SetValueWithoutNotify(tree);
            if (graphPane != null && browserPane != null) ShowGraph(graphToggle, browserToggle);
            Refresh();
        }

        private void Refresh()
        {
            if (canvas == null) return;
            model = tree == null ? null : DialogueGraphModelBuilder.Build(tree);
            canvas.Populate(tree, model);
            status.text = tree == null ? "Select a DialogueTree" :
                $"{model.Nodes.Count} nodes • {model.Edges.Count} links • " +
                $"{model.UnreachableNodeIndexes.Count} unreachable" + (tree.GeneratedFromJson ? " • GENERATED / READ-ONLY" : string.Empty);
            details?.MarkDirtyRepaint();
        }

        private void DrawDetails()
        {
            if (tree == null)
            {
                EditorGUILayout.HelpBox("Assign or create a DialogueTree.", MessageType.Info);
                return;
            }
            if (tree.GeneratedFromJson)
            {
                EditorGUILayout.HelpBox("This tree is generated and intentionally read-only. Reimport its source or create an editable copy.", MessageType.Warning);
                EditorGUILayout.LabelField("Source", string.IsNullOrWhiteSpace(tree.SourceJsonPath) ? "<Missing source path>" : tree.SourceJsonPath);
                if (GUILayout.Button("Create Editable Copy")) CreateEditableCopy();
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Path Preview", EditorStyles.boldLabel);
            simulatedFlags = EditorGUILayout.TextField(new GUIContent("Simulated Flags", "Comma-separated flags; runtime state is never changed."), simulatedFlags);
            string[] previewFlags = simulatedFlags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(flag => flag.Trim()).Where(flag => flag.Length > 0).ToArray();
            using var simulation = new SerializedObject(this);
            EditorGUILayout.PropertyField(simulation.FindProperty("simulatedValues"), new GUIContent("Simulated Values"), true);
            simulation.ApplyModifiedProperties();
            var previewValues = simulatedValues.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.flagId))
                .GroupBy(entry => entry.flagId.Trim(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().value, StringComparer.Ordinal);
            using var serialized = new SerializedObject(tree);
            serialized.Update();
            using (new EditorGUI.DisabledScope(tree.GeneratedFromJson))
            {
                SerializedProperty nodes = serialized.FindProperty("nodes");
                int index = Array.FindIndex(tree.Nodes ?? Array.Empty<DialogueTree.Node>(), node => node?.id == selectedNodeId);
                index = DrawNodePicker(tree, index);
                if (index >= 0 && index < nodes.arraySize)
                {
                    SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                    selectedNodeId = node.FindPropertyRelative("id").stringValue;
                    DrawNodeFields(tree, node, index);
                    int[] available = DialoguePathPreview.AvailableChoiceIndexes(tree.Nodes[index], previewFlags, previewValues);
                    if ((tree.Nodes[index].choices?.Length ?? 0) > 0)
                        EditorGUILayout.LabelField("Preview Available", available.Length == 0 ? "<None>" : string.Join(", ", available.Select(choice => (choice + 1).ToString())));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Frame Node")) canvas?.FrameNode(selectedNodeId);
                        if (GUILayout.Button("Set Entry"))
                        {
                            serialized.ApplyModifiedProperties();
                            DialogueGraphCommands.SetEntryNode(tree, selectedNodeId);
                            QueueRefresh();
                        }
                        if (GUILayout.Button("Delete Node"))
                        {
                            serialized.ApplyModifiedProperties();
                            DeleteNode(selectedNodeId);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
                if (serialized.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(tree);
                    QueueRefresh();
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            foreach (int index in model?.UnreachableNodeIndexes ?? Array.Empty<int>())
                EditorGUILayout.HelpBox($"Node '{model.Nodes[index].StableId}' is unreachable from the entry node.", MessageType.Warning);
            foreach (string id in model?.DuplicateStableIds ?? Array.Empty<string>())
                EditorGUILayout.HelpBox($"Stable node ID '{id}' is duplicated.", MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Selects a node by its authored identity and speaker.</summary>
        internal static int DrawNodePicker(DialogueTree source, int index)
        {
            if ((source.Nodes?.Length ?? 0) == 0)
            {
                EditorGUILayout.HelpBox("This tree has no nodes. Add a node to begin.", MessageType.Info);
                return -1;
            }
            return EditorGUILayout.Popup("Edit Node", Mathf.Clamp(index, 0, source.Nodes.Length - 1),
                source.Nodes.Select((node, i) => NodeLabel(node, i)).ToArray());
        }

        private static string NodeLabel(DialogueTree.Node node, int index) =>
            $"{index}: {(string.IsNullOrWhiteSpace(node?.id) ? "<No ID>" : node.id)}" +
            (string.IsNullOrWhiteSpace(node?.speaker) ? string.Empty : $" - {node.speaker}");

        /// <summary>Edits node content, choices, and named local destinations through Unity serialization.</summary>
        internal static void DrawNodeFields(DialogueTree source, SerializedProperty node, int index)
        {
            EditorGUILayout.LabelField(index == source.StartNodeIndex ? "Entry Node" : "Dialogue Node", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(node.FindPropertyRelative("id"), new GUIContent("Stable ID"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("speaker"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("line"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("presentationTags"), true);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("flagsToSetOnEnter"), true);
            DrawDestination(source, node.FindPropertyRelative("nextNodeIndex"), "Next Node / Fallback");
            EditorGUILayout.HelpBox("Drag an output to a node's In port to connect it, or choose a destination below. Next is used when no choices are available. End Dialogue closes this conversation.", MessageType.Info);
            SerializedProperty choices = node.FindPropertyRelative("choices");
            EditorGUILayout.LabelField($"Choices ({choices.arraySize})", EditorStyles.boldLabel);
            for (int choiceIndex = 0; choiceIndex < choices.arraySize; choiceIndex++)
            {
                SerializedProperty choice = choices.GetArrayElementAtIndex(choiceIndex);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Choice {choiceIndex + 1}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("text"));
                    DrawDestination(source, choice.FindPropertyRelative("nextNodeIndex"), "Destination");
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("flagsToSet"), true);
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("availabilityRequirement"), true);
                    if (GUILayout.Button("Remove Choice"))
                    {
                        choices.DeleteArrayElementAtIndex(choiceIndex);
                        break;
                    }
                }
            }
            if (GUILayout.Button("Add Choice")) AddChoice(choices);
        }

        private static void AddChoice(SerializedProperty choices)
        {
            int added = choices.arraySize++;
            SerializedProperty choice = choices.GetArrayElementAtIndex(added);
            choice.FindPropertyRelative("text").stringValue = "New choice";
            choice.FindPropertyRelative("nextNodeIndex").intValue = -1;
            choice.FindPropertyRelative("flagsToSet").arraySize = 0;
            SerializedProperty requirement = choice.FindPropertyRelative("availabilityRequirement");
            requirement.FindPropertyRelative("mode").enumValueIndex = 0;
            requirement.FindPropertyRelative("flags").arraySize = 0;
            requirement.FindPropertyRelative("comparisons").arraySize = 0;
        }

        private static void DrawDestination(DialogueTree source, SerializedProperty target, string label)
        {
            string[] labels = new[] { "End Dialogue" }.Concat((source.Nodes ?? Array.Empty<DialogueTree.Node>())
                .Select((node, i) => NodeLabel(node, i))).ToArray();
            bool invalid = target.intValue < -1 || target.intValue >= (source.Nodes?.Length ?? 0);
            if (invalid) labels = labels.Concat(new[] { $"<Missing node index {target.intValue}>" }).ToArray();
            int current = invalid ? labels.Length - 1 : target.intValue + 1;
            int picked = EditorGUILayout.Popup(label, current, labels);
            if (picked != current) target.intValue = picked - 1;
        }

        private void DrawBrowser()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65f)))
                    reports = DialogueAnalysis.ScanProject().ToArray();
                browserSearch = GUILayout.TextField(browserSearch, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(160f));
                GUILayout.Label("Speaker", GUILayout.Width(50f));
                speakerFilter = GUILayout.TextField(speakerFilter, EditorStyles.toolbarTextField, GUILayout.Width(110f));
                GUILayout.Label("Flag", GUILayout.Width(28f));
                flagFilter = GUILayout.TextField(flagFilter, EditorStyles.toolbarTextField, GUILayout.Width(110f));
                issuesOnly = GUILayout.Toggle(issuesOnly, "Issues only", EditorStyles.toolbarButton, GUILayout.Width(80f));
            }
            DialogueAssetReport[] visible = reports.Where(ReportMatches).ToArray();
            EditorGUILayout.LabelField($"Dialogue Assets ({visible.Length}/{reports.Length})", EditorStyles.boldLabel);
            browserScroll = EditorGUILayout.BeginScrollView(browserScroll);
            foreach (DialogueAssetReport report in visible)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(report.Tree.name, GUILayout.MinWidth(180f));
                    EditorGUILayout.LabelField($"{report.Nodes.Count} nodes • {report.ErrorCount} errors • {report.WarningCount} warnings • {report.ReferencePaths.Count} references");
                    if (GUILayout.Button("Open", GUILayout.Width(60f)))
                    {
                        SetTree(report.Tree);
                        ShowGraph(graphToggle, browserToggle);
                    }
                    if (GUILayout.Button("Ping", GUILayout.Width(55f))) { Selection.activeObject = report.Tree; EditorGUIUtility.PingObject(report.Tree); }
                }
            }
            if (visible.Length == 0) EditorGUILayout.HelpBox("No dialogue assets match these filters.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private bool ReportMatches(DialogueAssetReport report)
        {
            if (issuesOnly && report.Issues.Count == 0) return false;
            bool Contains(string source, string value) => string.IsNullOrWhiteSpace(value) ||
                (!string.IsNullOrWhiteSpace(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
            return Contains(report.Tree.name, browserSearch) || report.Nodes.Any(node =>
                (Contains(node.Node?.speaker, browserSearch) || Contains(node.Node?.line, browserSearch) || node.Flags.Any(flag => Contains(flag, browserSearch))) &&
                Contains(node.Node?.speaker, speakerFilter) &&
                (string.IsNullOrWhiteSpace(flagFilter) || node.Flags.Any(flag => Contains(flag, flagFilter))));
        }

        private void AddNode()
        {
            if (tree == null || tree.GeneratedFromJson) return;
            string id = "node-" + Guid.NewGuid().ToString("N")[..8];
            DialogueGraphCommands.AddNode(tree, id);
            selectedNodeId = id;
            Refresh();
        }

        private void DuplicateSelectedNode()
        {
            if (tree == null || tree.GeneratedFromJson) return;
            string sourceId = (tree.Nodes ?? Array.Empty<DialogueTree.Node>()).FirstOrDefault(node => node?.id == selectedNodeId)?.id;
            if (string.IsNullOrWhiteSpace(sourceId)) return;
            string id = sourceId + "-copy-" + Guid.NewGuid().ToString("N")[..6];
            DialogueGraphCommands.DuplicateNode(tree, sourceId, id);
            selectedNodeId = id;
            Refresh();
        }

        private bool DeleteNode(string id)
        {
            if (tree == null || tree.GeneratedFromJson) return false;
            DialogueNodeDeletePreview preview = DialogueGraphCommands.PreviewDeleteNode(tree, id);
            string impact = preview.Incoming.Count == 0 ? "No incoming links will change." : $"{preview.Incoming.Count} incoming link(s) will be cleared.";
            if (!EditorUtility.DisplayDialog("Delete Dialogue Node?", $"Delete '{id}'?\n\n{impact}", "Delete", "Cancel")) return false;
            DialogueGraphCommands.DeleteNode(tree, preview);
            QueueRefresh();
            return true;
        }

        private void CreateTree()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Dialogue Tree", "DialogueTree", "asset", "Choose a location for the tree.");
            if (string.IsNullOrWhiteSpace(path)) return;
            var created = CreateInstance<DialogueTree>();
            AssetDatabase.CreateAsset(created, path);
            DialogueGraphCommands.AddNode(created, "start");
            AssetDatabase.SaveAssets();
            Selection.activeObject = created;
            SetTree(created);
        }

        private void CreateEditableCopy()
        {
            if (tree == null) return;
            string path = EditorUtility.SaveFilePanelInProject("Create Editable Dialogue Copy", tree.name + "-editable", "asset", "Choose a location for the editable copy.");
            if (string.IsNullOrWhiteSpace(path)) return;
            DialogueTree copy = DialogueAssetCommands.CreateEditableCopy(tree, path);
            Selection.activeObject = copy;
            SetTree(copy);
        }

        private void Export()
        {
            if (tree == null) return;
            try { DialogueJsonExporter.ExportWithSavePanel(tree); }
            catch (Exception exception) { EditorUtility.DisplayDialog("Dialogue Export Failed", exception.Message, "OK"); }
        }

        private void Revert()
        {
            if (tree == null) return;
            string path = AssetDatabase.GetAssetPath(tree);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            SetTree(AssetDatabase.LoadAssetAtPath<DialogueTree>(path));
        }
    }
}
