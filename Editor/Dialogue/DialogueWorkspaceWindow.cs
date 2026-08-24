using System;
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
        private DialogueTree tree;
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

        [MenuItem("Assets/Open in Dialogue Workspace", true)]
        private static bool ValidateOpenSelected() => Selection.activeObject is DialogueTree;

        [MenuItem("Assets/Open in Dialogue Workspace")]
        private static void OpenSelected()
        {
            DialogueWorkspaceWindow window = GetWindow<DialogueWorkspaceWindow>("Dialogue");
            window.SetTree(Selection.activeObject as DialogueTree);
        }

        public void CreateGUI()
        {
            var toolbar = new Toolbar();
            var picker = new ObjectField { objectType = typeof(DialogueTree), allowSceneObjects = false };
            picker.style.minWidth = 230f;
            picker.SetValueWithoutNotify(tree);
            picker.RegisterValueChangedCallback(change => SetTree(change.newValue as DialogueTree));
            toolbar.Add(picker);
            toolbar.Add(new ToolbarButton(CreateTree) { text = "Create" });
            toolbar.Add(new ToolbarButton(AddNode) { text = "Add Node" });
            toolbar.Add(new ToolbarButton(DuplicateEntry) { text = "Duplicate Entry" });
            toolbar.Add(new ToolbarButton(() => canvas?.FrameAll()) { text = "Frame All" });
            var search = new ToolbarSearchField { style = { width = 150f } };
            search.RegisterValueChangedCallback(change => graphSearch = change.newValue);
            search.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Return) canvas?.FrameNode(graphSearch); });
            toolbar.Add(search);
            toolbar.Add(new ToolbarButton(() => { if (tree != null) AssetDatabase.SaveAssetIfDirty(tree); }) { text = "Save" });
            toolbar.Add(new ToolbarButton(Revert) { text = "Revert" });
            toolbar.Add(new ToolbarButton(Export) { text = "Export JSON" });
            toolbar.Add(new ToolbarButton(CreateEditableCopy) { text = "Editable Copy" });
            var graphTab = new ToolbarToggle { text = "Graph", value = true };
            var browseTab = new ToolbarToggle { text = "Browse" };
            graphTab.RegisterValueChangedCallback(change => { if (change.newValue) ShowGraph(graphTab, browseTab); });
            browseTab.RegisterValueChangedCallback(change => { if (change.newValue) ShowBrowser(graphTab, browseTab); });
            toolbar.Add(graphTab);
            toolbar.Add(browseTab);
            status = new Label { style = { flexGrow = 1f, unityTextAlign = TextAnchor.MiddleRight } };
            toolbar.Add(status);
            rootVisualElement.Add(toolbar);

            graphPane = new TwoPaneSplitView(1, 390f, TwoPaneSplitViewOrientation.Horizontal);
            canvas = new DialogueGraphCanvas();
            canvas.Changed += Refresh;
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

        private void SetTree(DialogueTree value) { tree = value; Refresh(); }

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
            var serialized = new SerializedObject(tree);
            serialized.Update();
            using (new EditorGUI.DisabledScope(tree.GeneratedFromJson))
            {
                SerializedProperty nodes = serialized.FindProperty("nodes");
                EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);
                for (int index = 0; index < nodes.arraySize; index++)
                {
                    SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.PropertyField(node.FindPropertyRelative("id"), new GUIContent(index == tree.StartNodeIndex ? "Entry ID" : "Stable ID"));
                        EditorGUILayout.PropertyField(node.FindPropertyRelative("speaker"));
                        EditorGUILayout.PropertyField(node.FindPropertyRelative("line"));
                        EditorGUILayout.PropertyField(node.FindPropertyRelative("flagsToSetOnEnter"), true);
                        EditorGUILayout.PropertyField(node.FindPropertyRelative("choices"), true);
                        int[] available = DialoguePathPreview.AvailableChoiceIndexes(tree.Nodes[index], previewFlags);
                        if ((tree.Nodes[index].choices?.Length ?? 0) > 0)
                            EditorGUILayout.LabelField("Preview Available", available.Length == 0 ? "<None>" : string.Join(", ", available.Select(choice => (choice + 1).ToString())));
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Set Entry")) DialogueGraphCommands.SetEntryNode(tree, node.FindPropertyRelative("id").stringValue);
                            if (GUILayout.Button("Delete")) DeleteNode(node.FindPropertyRelative("id").stringValue);
                        }
                    }
                }
                if (serialized.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(tree);
                    EditorApplication.delayCall += Refresh;
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
                    if (GUILayout.Button("Open", GUILayout.Width(60f))) SetTree(report.Tree);
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
            Refresh();
        }

        private void DuplicateEntry()
        {
            if (tree == null || tree.GeneratedFromJson || tree.StartNodeIndex < 0 || tree.StartNodeIndex >= tree.Nodes.Length) return;
            string id = tree.Nodes[tree.StartNodeIndex].id + "-copy-" + Guid.NewGuid().ToString("N")[..6];
            DialogueGraphCommands.DuplicateNode(tree, tree.Nodes[tree.StartNodeIndex].id, id);
            Refresh();
        }

        private bool DeleteNode(string id)
        {
            if (tree == null || tree.GeneratedFromJson) return false;
            DialogueNodeDeletePreview preview = DialogueGraphCommands.PreviewDeleteNode(tree, id);
            string impact = preview.Incoming.Count == 0 ? "No incoming links will change." : $"{preview.Incoming.Count} incoming link(s) will be cleared.";
            if (!EditorUtility.DisplayDialog("Delete Dialogue Node?", $"Delete '{id}'?\n\n{impact}", "Delete", "Cancel")) return false;
            DialogueGraphCommands.DeleteNode(tree, preview);
            EditorApplication.delayCall += Refresh;
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
            tree = AssetDatabase.LoadAssetAtPath<DialogueTree>(path);
            Refresh();
        }
    }
}
