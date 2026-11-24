using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Edits scene-local dialogue selectors together in the Content Workspace.</summary>
    [ContentWorkspaceTab]
    public sealed class DialogueResolversWorkspaceTab : IContentWorkspaceTab
    {
        private readonly List<Scene> scenes = new();
        private readonly List<DialogueTreeResolver> resolvers = new();
        private readonly Dictionary<int, SerializedObject> serialized = new();
        private readonly HashSet<int> collapsed = new();
        private readonly Dictionary<int, DialogueResolverPreview> previews = new();
        private IReadOnlyDictionary<string, FlagValue> flagDefaults;
        private bool hasFlagCatalogs;
        private IMGUIContainer gui;
        private int sceneHandle;
        private string search = string.Empty;
        private Vector2 scroll;
        private bool observing;

        /// <inheritdoc />
        public string Id => "dialogue-resolvers";
        /// <inheritdoc />
        public string DisplayName => "Dialogue Resolvers";
        /// <inheritdoc />
        public int Order => 51;

        /// <inheritdoc />
        public VisualElement CreateContent() =>
            gui = new IMGUIContainer(Draw) { style = { flexGrow = 1f } };

        /// <inheritdoc />
        public void SetSearch(string value)
        {
            search = value?.Trim() ?? string.Empty;
            gui?.MarkDirtyRepaint();
        }

        /// <inheritdoc />
        public void OnSelected()
        {
            if (!observing)
            {
                EditorApplication.hierarchyChanged += Refresh;
                EditorApplication.projectChanged += InvalidateFlagDefaults;
                Undo.undoRedoPerformed += Refresh;
                observing = true;
            }
            Refresh();
        }

        /// <inheritdoc />
        public void OnDeselected()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            EditorApplication.projectChanged -= InvalidateFlagDefaults;
            Undo.undoRedoPerformed -= Refresh;
            observing = false;
            DisposeEditors();
        }

        /// <inheritdoc />
        public void Refresh()
        {
            DisposeEditors();
            InvalidateFlagDefaults();
            scenes.Clear();
            resolvers.Clear();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                scenes.Add(scene);
                foreach (GameObject root in scene.GetRootGameObjects())
                    resolvers.AddRange(root.GetComponentsInChildren<DialogueTreeResolver>(true));
            }
            if (!scenes.Any(scene => scene.handle == sceneHandle)) sceneHandle = 0;
            resolvers.Sort((left, right) => string.Compare(
                ObjectLabel(left), ObjectLabel(right), StringComparison.OrdinalIgnoreCase));
            collapsed.RemoveWhere(id => !resolvers.Any(item => item.GetInstanceID() == id));
            foreach (int id in previews.Keys.Where(id => !resolvers.Any(item => item.GetInstanceID() == id)).ToArray())
                previews.Remove(id);
            gui?.MarkDirtyRepaint();
        }

        private void Draw()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string[] options = new[] { "All loaded scenes" }.Concat(scenes.Select(scene => scene.name)).ToArray();
                int current = sceneHandle == 0 ? 0 : scenes.FindIndex(scene => scene.handle == sceneHandle) + 1;
                int picked = EditorGUILayout.Popup(current, options, EditorStyles.toolbarPopup);
                if (picked != current) sceneHandle = picked == 0 ? 0 : scenes[picked - 1].handle;
                if (GUILayout.Button("Expand All", EditorStyles.toolbarButton, GUILayout.Width(80f))) collapsed.Clear();
                if (GUILayout.Button("Collapse All", EditorStyles.toolbarButton, GUILayout.Width(85f)))
                    foreach (DialogueTreeResolver item in resolvers.Where(item => item != null)) collapsed.Add(item.GetInstanceID());
            }

            DialogueTreeResolver[] visible = resolvers.Where(item => item != null &&
                (sceneHandle == 0 || item.gameObject.scene.handle == sceneHandle) && Matches(item)).ToArray();
            EditorGUILayout.HelpBox(
                $"{visible.Length} dialogue resolver(s). Rules are evaluated from top to bottom; the first match wins. " +
                "Edits apply directly to scene components. Save the scene to keep changes.", MessageType.Info);
            if (visible.Length == 0)
                EditorGUILayout.HelpBox("No dialogue resolvers match this scene and search. Inactive objects are included.", MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (DialogueTreeResolver item in visible) DrawResolver(item);
            EditorGUILayout.EndScrollView();
        }

        private void DrawResolver(DialogueTreeResolver item)
        {
            int id = item.GetInstanceID();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool expanded = EditorGUILayout.Foldout(!collapsed.Contains(id), ObjectLabel(item), true, EditorStyles.foldoutHeader);
                    if (expanded) collapsed.Remove(id); else collapsed.Add(id);
                    if (GUILayout.Button("Select", GUILayout.Width(55f)))
                    {
                        Selection.activeGameObject = item.gameObject;
                        EditorGUIUtility.PingObject(item.gameObject);
                    }
                }
                if (collapsed.Contains(id)) return;
                if (!serialized.TryGetValue(id, out SerializedObject data))
                {
                    serialized[id] = data = new SerializedObject(item);
                    SerializedProperty rules = data.FindProperty("rules");
                    rules.isExpanded = true;
                    for (int index = 0; index < rules.arraySize; index++)
                        rules.GetArrayElementAtIndex(index).isExpanded = true;
                }
                data.Update();
                EditorGUILayout.PropertyField(data.FindProperty("rules"), true);
                EditorGUILayout.PropertyField(data.FindProperty("fallbackTree"));
                // SerializedObject preserves Undo, prefab overrides, property drawers,
                // and scene dirty state just like editing the component in the Inspector.
                data.ApplyModifiedProperties();
                DrawPreview(item, id);
            }
        }

        private void DrawPreview(DialogueTreeResolver resolver, int id)
        {
            previews.TryGetValue(id, out DialogueResolverPreview preview);
            bool expanded = EditorGUILayout.Foldout(preview?.Expanded == true, "Rule Preview", true);
            if (!expanded)
            {
                if (preview != null) preview.Expanded = false;
                return;
            }

            if (preview == null) previews[id] = preview = new DialogueResolverPreview();
            preview.Expanded = true;
            flagDefaults ??= DialogueResolverPreview.LoadFlagDefaults(out hasFlagCatalogs);
            preview.Draw(resolver, flagDefaults, hasFlagCatalogs);
        }

        private void InvalidateFlagDefaults()
        {
            flagDefaults = null;
            gui?.MarkDirtyRepaint();
        }

        private bool Matches(DialogueTreeResolver item) => string.IsNullOrEmpty(search) ||
            Contains(ObjectLabel(item)) ||
            (item.FallbackTree != null && Contains(item.FallbackTree.name)) ||
            item.Rules.Any(rule => rule != null && (Contains(rule.Label) ||
                (rule.Tree != null && Contains(rule.Tree.name))));

        private bool Contains(string value) => value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string ObjectLabel(DialogueTreeResolver item) =>
            item.gameObject.scene.name + " / " + HierarchyPath(item.transform);

        private static string HierarchyPath(Transform target) => target.parent == null
            ? target.name : HierarchyPath(target.parent) + "/" + target.name;

        private void DisposeEditors()
        {
            foreach (SerializedObject editor in serialized.Values) editor.Dispose();
            serialized.Clear();
        }
    }
}
