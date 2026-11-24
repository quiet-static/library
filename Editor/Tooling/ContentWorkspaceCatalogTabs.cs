using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Editor.Flags;
using QuietStatic.Toolkit.Editor.SceneFlow;
using QuietStatic.Toolkit.Editor.Validation;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Narrative;
using QuietStatic.Toolkit.Objectives;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Shared searchable asset catalog used by focused Content Workspace tabs.</summary>
    public abstract class AssetCatalogWorkspaceTab : IContentWorkspaceTab
    {
        private readonly List<UnityEngine.Object> assets = new();
        private VisualElement root;
        private IMGUIContainer gui;
        private UnityEngine.Object selected;
        private UnityEditor.Editor inspector;
        private Vector2 listScroll;
        private Vector2 inspectorScroll;
        private string search = string.Empty;
        private readonly List<string> references = new();
        private bool showSafeIdRename;

        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract int Order { get; }
        protected abstract IReadOnlyList<Type> AssetTypes { get; }
        protected UnityEngine.Object Selected => selected;

        public VisualElement CreateContent()
        {
            root = new VisualElement();
            gui = new IMGUIContainer(Draw) { style = { flexGrow = 1f } };
            root.Add(gui);
            return root;
        }

        public void SetSearch(string value) { search = value?.Trim() ?? string.Empty; gui?.MarkDirtyRepaint(); }
        public void Refresh()
        {
            assets.Clear();
            foreach (Type type in AssetTypes)
            {
                foreach (string guid in AssetDatabase.FindAssets($"t:{type.Name}"))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);
                    if (asset != null && !assets.Contains(asset)) assets.Add(asset);
                }
            }
            assets.Sort((left, right) => string.Compare(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right), StringComparison.Ordinal));
            if (selected == null || !assets.Contains(selected))
            {
                string savedGuid = EditorPrefs.GetString($"QuietStatic.ContentWorkspace.Asset.{Id}", string.Empty);
                string savedPath = AssetDatabase.GUIDToAssetPath(savedGuid);
                Select(assets.FirstOrDefault(asset => AssetDatabase.GetAssetPath(asset) == savedPath) ?? assets.FirstOrDefault());
            }
            else if (inspector == null)
            {
                inspector = UnityEditor.Editor.CreateEditor(selected);
            }
            gui?.MarkDirtyRepaint();
        }

        public void OnSelected() => Refresh();
        public void OnDeselected() { if (inspector != null) UnityEngine.Object.DestroyImmediate(inspector); inspector = null; }

        protected virtual void DrawActions(UnityEngine.Object asset) { }
        protected virtual void DrawSupplementary(UnityEngine.Object asset) { }

        private void Draw()
        {
            List<UnityEngine.Object> visible = Visible();
            float listWidth = Mathf.Clamp(root.resolvedStyle.width * 0.29f, 128f, 280f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(listWidth)))
                {
                    EditorGUILayout.LabelField($"Assets ({visible.Count})", EditorStyles.boldLabel);
                    if (GUILayout.Button("Create Asset")) ShowCreateMenu();
                    listScroll = EditorGUILayout.BeginScrollView(listScroll, EditorStyles.helpBox);
                    if (visible.Count == 0)
                        EditorGUILayout.HelpBox(assets.Count == 0 ? "No assets yet." : "No assets match this search.", MessageType.Info);
                    foreach (UnityEngine.Object asset in visible)
                    {
                        Color previous = GUI.backgroundColor;
                        if (asset == selected) GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
                        if (GUILayout.Button(new GUIContent(asset.name, AssetDatabase.GetAssetPath(asset)), EditorStyles.miniButton, GUILayout.Height(24f))) Select(asset);
                        GUI.backgroundColor = previous;
                    }
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    if (selected == null) return;
                    EditorGUILayout.LabelField(selected.name, EditorStyles.largeLabel);
                    EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(selected), EditorStyles.miniLabel);
                    if (!visible.Contains(selected))
                        EditorGUILayout.LabelField("Selected asset is outside the current search.", EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawActions(selected);
                        Rect actionsRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(74f));
                        if (EditorGUI.DropdownButton(actionsRect,
                                new GUIContent("Actions", "Locate, find references, duplicate, or delete the selected asset."),
                                FocusType.Keyboard, EditorStyles.miniPullDown))
                            ShowAssetActions(actionsRect);
                        GUILayout.FlexibleSpace();
                    }
                    inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
                    DrawSupplementary(selected);
                    DrawInspector(selected, inspector);
                    if (references.Count > 0)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField($"Definite Asset References ({references.Count})", EditorStyles.boldLabel);
                        foreach (string path in references)
                            if (GUILayout.Button(path, EditorStyles.miniButton))
                            {
                                UnityEngine.Object consumer = AssetDatabase.LoadMainAssetAtPath(path);
                                Selection.activeObject = consumer;
                                EditorGUIUtility.PingObject(consumer);
                            }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        protected virtual void DrawInspector(UnityEngine.Object asset, UnityEditor.Editor editor) => editor?.OnInspectorGUI();

        private List<UnityEngine.Object> Visible() => assets.Where(asset => string.IsNullOrWhiteSpace(search) ||
            asset.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
            EditorJsonUtility.ToJson(asset).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        private void Select(UnityEngine.Object value)
        {
            selected = value;
            references.Clear();
            showSafeIdRename = false;
            inspectorScroll = Vector2.zero;
            string path = selected == null ? string.Empty : AssetDatabase.GetAssetPath(selected);
            EditorPrefs.SetString($"QuietStatic.ContentWorkspace.Asset.{Id}", AssetDatabase.AssetPathToGUID(path));
            if (inspector != null) UnityEngine.Object.DestroyImmediate(inspector);
            inspector = selected == null ? null : UnityEditor.Editor.CreateEditor(selected);
        }

        internal void RevealAsset(UnityEngine.Object asset)
        {
            if (asset != selected && assets.Contains(asset)) Select(asset);
        }

        protected bool ShowSafeIdRename()
        {
            showSafeIdRename = EditorGUILayout.Foldout(showSafeIdRename,
                new GUIContent("Safe ID Rename", "Preview the serialized references that will change before renaming an ID."), true);
            return showSafeIdRename;
        }

        private void ShowAssetActions(Rect anchor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Locate in Project"), false, () =>
            {
                Selection.activeObject = selected;
                EditorGUIUtility.PingObject(selected);
            });
            menu.AddItem(new GUIContent("Find Asset References"), false, ScanReferences);
            menu.AddItem(new GUIContent("Duplicate Asset"), false, DuplicateSelected);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete Asset…"), false, DeleteSelected);
            menu.DropDown(anchor);
        }

        private void ScanReferences()
        {
            references.Clear();
            string selectedPath = AssetDatabase.GetAssetPath(selected);
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)))
                if (path != selectedPath && AssetDatabase.GetDependencies(path, false).Contains(selectedPath)) references.Add(path);
            references.Sort(StringComparer.Ordinal);
            gui.MarkDirtyRepaint();
        }

        private void DuplicateSelected()
        {
            string source = AssetDatabase.GetAssetPath(selected);
            string destination = AssetDatabase.GenerateUniqueAssetPath(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(source) ?? "Assets", selected.name + " Copy.asset").Replace('\\', '/'));
            if (!AssetDatabase.CopyAsset(source, destination)) return;
            AssetDatabase.SaveAssets();
            Refresh();
            Select(AssetDatabase.LoadMainAssetAtPath(destination));
        }

        private void DeleteSelected()
        {
            string path = AssetDatabase.GetAssetPath(selected);
            if (!EditorUtility.DisplayDialog("Delete Content Asset?", $"Delete '{path}'?\n\nSerialized consumers are not rewritten.", "Delete", "Cancel")) return;
            AssetDatabase.DeleteAsset(path);
            selected = null;
            Refresh();
        }

        protected void SetReferenceResults(IEnumerable<string> paths)
        {
            references.Clear();
            references.AddRange((paths ?? Array.Empty<string>()).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal));
            references.Sort(StringComparer.Ordinal);
            gui.MarkDirtyRepaint();
        }

        protected void SelectAsset(UnityEngine.Object asset) => Select(asset);

        protected void PreviewAndApplyRename(UnityEngine.Object source, ContentIdKind kind, string oldId, string newId)
        {
            try
            {
                ContentIdChangePreview preview = ContentIdChangeService.PreviewRename(source, kind, oldId, newId);
                ContentIdReferenceWindow.PreviewRename(preview, Refresh);
            }
            catch (Exception exception) { EditorUtility.DisplayDialog("Rename Failed", exception.Message, "OK"); }
        }

        private void ShowCreateMenu()
        {
            var menu = new GenericMenu();
            foreach (Type type in AssetTypes.Where(type => typeof(ScriptableObject).IsAssignableFrom(type)))
                menu.AddItem(new GUIContent(type.Name), false, () => CreateAsset(type));
            menu.ShowAsContext();
        }

        private void CreateAsset(Type type)
        {
            string path = EditorUtility.SaveFilePanelInProject($"Create {type.Name}", type.Name, "asset", "Choose a location for the new asset.");
            if (string.IsNullOrWhiteSpace(path)) return;
            var created = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            Refresh();
            Select(created);
        }
    }

    [ContentWorkspaceTab]
    public sealed class FlagsWorkspaceTab : AssetCatalogWorkspaceTab
    {
        private string oldId = string.Empty;
        private string newId = string.Empty;
        public override string Id => "flags";
        public override string DisplayName => "Flags";
        public override int Order => 10;
        protected override IReadOnlyList<Type> AssetTypes => new[] { typeof(FlagDatabase) };
        protected override void DrawActions(UnityEngine.Object asset)
        {
            if (asset is not FlagDatabase database) return;
            if (GUILayout.Button("Scan Usage", GUILayout.Width(80f)))
            {
                var ids = (database.Flags ?? Array.Empty<FlagDatabase.FlagDefinition>()).Where(flag => flag != null).Select(flag => flag.id);
                try
                {
                    ContentIdReferenceWindow.ShowFlagUsage(ContentIdChangeService.FindReferenceMatches(
                        ContentIdKind.Flag, ids, AssetDatabase.GetAssetPath(database), includeClosedScenes: false));
                }
                catch (Exception exception) { EditorUtility.DisplayDialog("Flag Usage Scan Failed", exception.Message, "OK"); }
            }
            if (GUILayout.Button("Export JSON", GUILayout.Width(85f))) FlagCatalogJsonExporter.ExportWithSavePanel(database);
        }
        protected override void DrawSupplementary(UnityEngine.Object asset)
        {
            if (asset is not FlagDatabase || !ShowSafeIdRename()) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                oldId = EditorGUILayout.TextField("Current ID", oldId);
                newId = EditorGUILayout.TextField("New ID", newId);
                if (GUILayout.Button("Preview and Rename")) PreviewAndApplyRename(asset, ContentIdKind.Flag, oldId, newId);
            }
        }
    }

    [ContentWorkspaceTab]
    public sealed class ObjectivesWorkspaceTab : AssetCatalogWorkspaceTab
    {
        private string newId = string.Empty;
        public override string Id => "objectives";
        public override string DisplayName => "Objectives";
        public override int Order => 20;
        protected override IReadOnlyList<Type> AssetTypes => new[] { typeof(ObjectiveDatabase), typeof(ObjectiveDefinition) };
        protected override void DrawActions(UnityEngine.Object asset)
        {
            if (asset is ObjectiveDatabase database && GUILayout.Button("Export JSON", GUILayout.Width(85f)))
                NarrativeContentJsonExporter.ExportObjectivesWithSavePanel(database);
        }
        protected override void DrawSupplementary(UnityEngine.Object asset)
        {
            if (asset is not ObjectiveDefinition definition || !ShowSafeIdRename()) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current ID", definition.Id);
                newId = EditorGUILayout.TextField("New ID", newId);
                if (GUILayout.Button("Preview and Rename")) PreviewAndApplyRename(asset, ContentIdKind.Objective, definition.Id, newId);
            }
        }
    }
    [ContentWorkspaceTab]
    public sealed class GameStatesWorkspaceTab : AssetCatalogWorkspaceTab
    {
        private string oldId = string.Empty;
        private string newId = string.Empty;
        public override string Id => "game-states";
        public override string DisplayName => "Game States";
        public override int Order => 30;
        protected override IReadOnlyList<Type> AssetTypes => new[] { typeof(GameStateDatabase) };
        protected override void DrawSupplementary(UnityEngine.Object asset)
        {
            if (!ShowSafeIdRename()) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                oldId = EditorGUILayout.TextField("Current ID", oldId);
                newId = EditorGUILayout.TextField("New ID", newId);
                if (GUILayout.Button("Preview and Rename")) PreviewAndApplyRename(asset, ContentIdKind.GameState, oldId, newId);
            }
        }
    }
    [ContentWorkspaceTab]
    public sealed class CinematicsWorkspaceTab : AssetCatalogWorkspaceTab
    {
        private string newId = string.Empty;
        public override string Id => "cinematics";
        public override string DisplayName => "Cinematics";
        public override int Order => 40;
        protected override IReadOnlyList<Type> AssetTypes => new[] { typeof(CinematicDatabase), typeof(CinematicDefinition) };
        protected override void DrawSupplementary(UnityEngine.Object asset)
        {
            if (asset is not CinematicDefinition definition || !ShowSafeIdRename()) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current ID", definition.Id);
                newId = EditorGUILayout.TextField("New ID", newId);
                if (GUILayout.Button("Preview and Rename")) PreviewAndApplyRename(asset, ContentIdKind.Cinematic, definition.Id, newId);
            }
        }
    }
    [ContentWorkspaceTab]
    public sealed class DialogueCatalogWorkspaceTab : AssetCatalogWorkspaceTab
    {
        private DialogueTree editingTree;
        private int editingNode;
        public override string Id => "dialogue";
        public override string DisplayName => "Dialogue";
        public override int Order => 50;
        protected override IReadOnlyList<Type> AssetTypes => new[] { typeof(DialogueTree) };
        protected override void DrawActions(UnityEngine.Object asset)
        {
            if (GUILayout.Button("Open Graph", GUILayout.Width(80f))) DialogueWorkspaceWindow.Open(asset as DialogueTree);
        }
        protected override void DrawInspector(UnityEngine.Object asset, UnityEditor.Editor editor)
        {
            if (asset is not DialogueTree tree) return;
            if (editingTree != tree) { editingTree = tree; editingNode = tree.StartNodeIndex; }
            if (tree.GeneratedFromJson)
            {
                EditorGUILayout.HelpBox("Generated dialogue is read-only. Open the graph workspace to create an editable copy.", MessageType.Info);
            }
            using var data = new SerializedObject(tree);
            data.Update();
            using (new EditorGUI.DisabledScope(tree.GeneratedFromJson))
            {
                if (GUILayout.Button("Add Node"))
                {
                    DialogueGraphCommands.AddNode(tree, "node-" + Guid.NewGuid().ToString("N")[..8]);
                    editingNode = tree.Nodes.Length - 1;
                    data.Update();
                }
                editingNode = DialogueWorkspaceWindow.DrawNodePicker(tree, editingNode);
                SerializedProperty nodes = data.FindProperty("nodes");
                if (editingNode >= 0 && editingNode < nodes.arraySize)
                {
                    DialogueWorkspaceWindow.DrawNodeFields(tree, nodes.GetArrayElementAtIndex(editingNode), editingNode);
                    if (GUILayout.Button("Set as Entry Node"))
                        data.FindProperty("startNodeIndex").intValue = editingNode;
                }
                data.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Save Tree")) AssetDatabase.SaveAssetIfDirty(tree);
        }
    }
    [ContentWorkspaceTab]
    public sealed class SceneFlowCatalogWorkspaceTab : AssetCatalogWorkspaceTab
    {
        public override string Id => "scene-flow";
        public override string DisplayName => "Scene Flow";
        public override int Order => 60;
        protected override IReadOnlyList<Type> AssetTypes => new[] { typeof(SceneFlowMap) };
        protected override void DrawActions(UnityEngine.Object asset)
        {
            if (GUILayout.Button("Open Graph", GUILayout.Width(80f))) SceneFlowWorkspaceWindow.Open(asset as SceneFlowMap);
        }
    }
    [ContentWorkspaceTab] public sealed class ReadablesWorkspaceTab : AssetCatalogWorkspaceTab { public override string Id => "readables"; public override string DisplayName => "Readables"; public override int Order => 70; protected override IReadOnlyList<Type> AssetTypes => new[] { typeof(ReadableContentDefinition) }; }
    [ContentWorkspaceTab] public sealed class StorySequencesWorkspaceTab : AssetCatalogWorkspaceTab { public override string Id => "story-sequences"; public override string DisplayName => "Story Sequences"; public override int Order => 80; protected override IReadOnlyList<Type> AssetTypes => new[] { typeof(StorySequenceDefinition) }; }

    /// <summary>Combined, navigable validation output using the canonical toolkit rules.</summary>
    [ContentWorkspaceTab]
    public sealed class ProblemsWorkspaceTab : IContentWorkspaceTab
    {
        private IReadOnlyList<ValidationIssue> issues = Array.Empty<ValidationIssue>();
        private IMGUIContainer gui;
        private Vector2 scroll;
        private string search = string.Empty;
        public string Id => "problems";
        public string DisplayName => "Problems";
        public int Order => 100;
        public VisualElement CreateContent()
        {
            gui = new IMGUIContainer(Draw) { style = { flexGrow = 1f } };
            return gui;
        }
        public void SetSearch(string value) { search = value ?? string.Empty; gui?.MarkDirtyRepaint(); }
        public void Refresh()
        {
            issues = ToolkitValidation.ScanNarrative()
                .Concat(NarrativeSynchronization.ScanProject())
                .Concat(ToolkitValidation.ScanOpenScenes())
                .Concat(ArchitectureValidation.ScanProjectConfiguration())
                .OrderByDescending(issue => issue.Severity).ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal).ToArray();
            gui?.MarkDirtyRepaint();
        }
        public void OnSelected() => Refresh();
        public void OnDeselected() { }
        private void Draw()
        {
            ValidationIssue[] visible = issues.Where(issue => string.IsNullOrWhiteSpace(search) ||
                issue.Code.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                issue.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                issue.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            int errors = visible.Count(issue => issue.Severity == ValidationSeverity.Error);
            int warnings = visible.Count(issue => issue.Severity == ValidationSeverity.Warning);
            EditorGUILayout.HelpBox($"{errors} error(s) • {warnings} warning(s) • {visible.Length} total result(s)",
                errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ValidationIssue issue in visible)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"[{issue.Code}] {issue.Category}: {issue.Message}", EditorStyles.wordWrappedLabel);
                    using (new EditorGUI.DisabledScope(issue.Context == null))
                        if (GUILayout.Button("Select", GUILayout.Width(60f)))
                        { Selection.activeObject = issue.Context; EditorGUIUtility.PingObject(issue.Context); }
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>Adds a direct Content Workspace shortcut to the normal database Inspector.</summary>
    [CustomEditor(typeof(ObjectiveDatabase))]
    public sealed class ObjectiveDatabaseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Open in Content Workspace")) ContentWorkspaceWindow.Open("objectives", target);
        }
    }
}
