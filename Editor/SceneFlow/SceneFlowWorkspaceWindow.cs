using System;
using System.Linq;
using System.Collections.Generic;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>Unified graph and compact-list editor for scene transition maps.</summary>
    public sealed class SceneFlowWorkspaceWindow : EditorWindow
    {
        private SceneFlowMap map;
        private SceneFlowGraphModel model;
        private SceneFlowGraphCanvas canvas;
        private IMGUIContainer details;
        private Label diagnostics;
        private Vector2 detailsScroll;
        private SceneFlowConnectionChangePreview pendingChange;
        private string renameValue = string.Empty;
        private HashSet<string> destinationResponseIds;

        public static void Open() => GetWindow<SceneFlowWorkspaceWindow>("Scene Flow");

        public static void Open(SceneFlowMap selected)
        {
            SceneFlowWorkspaceWindow window = GetWindow<SceneFlowWorkspaceWindow>("Scene Flow");
            window.SetMap(selected);
        }

        [MenuItem("Assets/Open in Scene Flow Workspace", true)]
        private static bool ValidateOpenSelected() => Selection.activeObject is SceneFlowMap;

        [MenuItem("Assets/Open in Scene Flow Workspace")]
        private static void OpenSelected()
        {
            SceneFlowWorkspaceWindow window = GetWindow<SceneFlowWorkspaceWindow>("Scene Flow");
            window.SetMap(Selection.activeObject as SceneFlowMap);
        }

        public void CreateGUI()
        {
            var toolbar = new Toolbar();
            var picker = new ObjectField { objectType = typeof(SceneFlowMap), allowSceneObjects = false };
            picker.style.minWidth = 230f;
            picker.RegisterValueChangedCallback(change => SetMap(change.newValue as SceneFlowMap));
            picker.SetValueWithoutNotify(map);
            toolbar.Add(picker);
            toolbar.Add(new ToolbarButton(CreateMap) { text = "Create" });
            toolbar.Add(new ToolbarButton(() => canvas?.FrameAll()) { text = "Frame All" });
            toolbar.Add(new ToolbarButton(() => { if (map != null) AssetDatabase.SaveAssetIfDirty(map); }) { text = "Save" });
            toolbar.Add(new ToolbarButton(Revert) { text = "Revert" });
            toolbar.Add(new ToolbarButton(ScanDestinationResponses) { text = "Scan Responses" });
            diagnostics = new Label { style = { flexGrow = 1f, unityTextAlign = TextAnchor.MiddleRight } };
            toolbar.Add(diagnostics);
            rootVisualElement.Add(toolbar);

            var split = new TwoPaneSplitView(1, 350f, TwoPaneSplitViewOrientation.Horizontal);
            canvas = new SceneFlowGraphCanvas();
            canvas.Changed += Refresh;
            canvas.DeleteRequested = PreviewAndApplyDelete;
            split.Add(canvas);
            details = new IMGUIContainer(DrawDetails);
            split.Add(details);
            rootVisualElement.Add(split);
            Refresh();
        }

        private void SetMap(SceneFlowMap value)
        {
            map = value;
            destinationResponseIds = null;
            Refresh();
        }

        private void ScanDestinationResponses()
        {
            if (map == null) return;
            string path = AssetDatabase.GetAssetPath(map);
            string[] ids = map.Connections.Where(connection => connection != null)
                .Select(connection => connection.Id).Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal).ToArray();
            var matches = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (SceneFlowConnectionChangeService.FindReferences(id, map)
                    .Any(reference => reference.Kind == SceneFlowConnectionReferenceKind.DestinationResponse))
                    matches.Add(id);
                map = AssetDatabase.LoadAssetAtPath<SceneFlowMap>(path);
            }
            destinationResponseIds = matches;
            Refresh();
        }

        private void Refresh()
        {
            if (canvas == null) return;
            string[] built = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path)).ToArray();
            model = map == null ? null : SceneFlowGraphModelBuilder.Build(
                map, built, destinationResponseIds);
            canvas.Populate(map, model);
            diagnostics.text = model == null
                ? "Select a SceneFlowMap"
                : $"{model.Nodes.Count} scenes • {model.Edges.Count} connections • {model.Issues.Count} issue(s)";
            details?.MarkDirtyRepaint();
        }

        private void DrawDetails()
        {
            if (map == null)
            {
                EditorGUILayout.HelpBox("Assign or create a Scene Flow Map.", MessageType.Info);
                return;
            }

            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
            EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);
            var serialized = new SerializedObject(map);
            serialized.Update();
            SerializedProperty connections = serialized.FindProperty("connections");
            string deleteRequest = null;
            string renameRequest = null;
            string renameReplacement = null;
            for (int index = 0; index < connections.arraySize; index++)
            {
                SerializedProperty connection = connections.GetArrayElementAtIndex(index);
                string id = connection.FindPropertyRelative("id").stringValue.Trim();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.TextField("Stable ID", id);
                    EditorGUILayout.PropertyField(connection.FindPropertyRelative("fromScene"));
                    EditorGUILayout.PropertyField(connection.FindPropertyRelative("toScene"));
                    EditorGUILayout.PropertyField(connection.FindPropertyRelative("additionalScenesToLoad"), true);
                    EditorGUILayout.PropertyField(connection.FindPropertyRelative("additionalScenesToKeep"), true);
                    EditorGUILayout.PropertyField(connection.FindPropertyRelative("unloadOtherScenes"));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Rename…"))
                        {
                            renameValue = id;
                            pendingChange = null;
                            GUI.FocusControl(null);
                        }
                        if (GUILayout.Button("Delete…"))
                            deleteRequest = id;
                    }
                    if (renameValue == id)
                    {
                        renameValue = EditorGUILayout.TextField("New ID", renameValue);
                        if (GUILayout.Button("Preview Rename"))
                        {
                            renameRequest = id;
                            renameReplacement = renameValue;
                        }
                    }
                }
            }
            if (serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(map);
                EditorApplication.delayCall += Refresh;
            }
            if (deleteRequest != null)
            {
                pendingChange = SceneFlowConnectionChangeService.PreviewDelete(map, deleteRequest);
                map = AssetDatabase.LoadAssetAtPath<SceneFlowMap>(pendingChange.MapPath);
            }
            else if (renameRequest != null)
            {
                pendingChange = SceneFlowConnectionChangeService.PreviewRename(
                    map, renameRequest, renameReplacement);
                map = AssetDatabase.LoadAssetAtPath<SceneFlowMap>(pendingChange.MapPath);
            }
            DrawPendingChange();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            foreach (SceneFlowGraphIssue issue in model?.Issues ?? Array.Empty<SceneFlowGraphIssue>())
                EditorGUILayout.HelpBox(issue.Message, MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPendingChange()
        {
            if (pendingChange == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                pendingChange.IsDelete
                    ? $"Delete '{pendingChange.OldId}'"
                    : $"Rename '{pendingChange.OldId}' to '{pendingChange.NewId}'",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"{pendingChange.References.Count} serialized consumer(s) will be " +
                (pendingChange.IsDelete ? "cleared." : "updated."),
                pendingChange.References.Count > 0 ? MessageType.Warning : MessageType.Info);
            foreach (SceneFlowConnectionReference reference in pendingChange.References.Take(20))
                EditorGUILayout.LabelField($"• {reference.Kind}: {reference.Label}", EditorStyles.wordWrappedMiniLabel);
            if (pendingChange.References.Count > 20)
                EditorGUILayout.LabelField($"…and {pendingChange.References.Count - 20} more");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply"))
                {
                    string path = pendingChange.MapPath;
                    SceneFlowConnectionChangeService.Apply(pendingChange);
                    map = AssetDatabase.LoadAssetAtPath<SceneFlowMap>(path);
                    pendingChange = null;
                    renameValue = string.Empty;
                    EditorApplication.delayCall += Refresh;
                }
                if (GUILayout.Button("Cancel"))
                {
                    pendingChange = null;
                    renameValue = string.Empty;
                }
            }
        }

        private bool PreviewAndApplyDelete(string connectionId)
        {
            SceneFlowConnectionChangePreview preview =
                SceneFlowConnectionChangeService.PreviewDelete(map, connectionId);
            string references = preview.References.Count == 0
                ? "No serialized consumers were found."
                : $"{preview.References.Count} serialized consumer(s) will be cleared.";
            if (!EditorUtility.DisplayDialog(
                    "Delete Scene Connection?",
                    $"Delete '{connectionId}'?\n\n{references}",
                    "Delete",
                    "Cancel"))
                return true;

            string path = preview.MapPath;
            SceneFlowConnectionChangeService.Apply(preview);
            map = AssetDatabase.LoadAssetAtPath<SceneFlowMap>(path);
            return true;
        }

        private void CreateMap()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Scene Flow Map", "SceneFlowMap", "asset", "Choose a location for the map.");
            if (string.IsNullOrWhiteSpace(path)) return;
            var created = CreateInstance<SceneFlowMap>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = created;
            SetMap(created);
        }

        private void Revert()
        {
            if (map == null) return;
            string path = AssetDatabase.GetAssetPath(map);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport |
                                            ImportAssetOptions.ForceUpdate);
            Refresh();
        }
    }
}
