using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Scene and hierarchy scoped editing for authored interaction content.</summary>
    [ContentWorkspaceTab]
    public sealed class SceneContentWorkspaceTab : IContentWorkspaceTab
    {
        private IMGUIContainer gui;
        private readonly List<Scene> scenes = new();
        private Scene selectedScene;
        private GameObject scope;
        private string search = string.Empty;
        private Vector2 scroll;

        public string Id => "scene-content";
        public string DisplayName => "Scene Content";
        public int Order => 5;

        public VisualElement CreateContent()
        {
            gui = new IMGUIContainer(Draw) { style = { flexGrow = 1f } };
            return gui;
        }

        public void SetSearch(string value)
        {
            search = value?.Trim() ?? string.Empty;
            gui?.MarkDirtyRepaint();
        }

        public void Refresh()
        {
            scenes.Clear();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded) scenes.Add(scene);
            }
            if (!selectedScene.IsValid() || !scenes.Contains(selectedScene))
                selectedScene = scenes.FirstOrDefault();
            if (scope != null && scope.scene != selectedScene) scope = null;
            gui?.MarkDirtyRepaint();
        }

        public void OnSelected() => Refresh();
        public void OnDeselected() { }

        private void Draw()
        {
            if (scenes.Count == 0)
            {
                EditorGUILayout.HelpBox("Open a scene to inspect its authored content.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int current = Mathf.Max(0, scenes.FindIndex(scene => scene == selectedScene));
                int picked = EditorGUILayout.Popup(current, scenes.Select(SceneLabel).ToArray(), EditorStyles.toolbarPopup);
                if (picked != current) { selectedScene = scenes[picked]; scope = null; }
                GUILayout.Label("GameObject scope", GUILayout.Width(112f));
                GameObject pickedScope = EditorGUILayout.ObjectField(scope, typeof(GameObject), true) as GameObject;
                if (pickedScope != scope && (pickedScope == null || pickedScope.scene == selectedScene)) scope = pickedScope;
                if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(90f)) &&
                    Selection.activeGameObject != null && Selection.activeGameObject.scene == selectedScene)
                    scope = Selection.activeGameObject;
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45f))) scope = null;
            }

            Transform[] transforms = Roots().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            FlavorTextInteraction[] flavor = Components<FlavorTextInteraction>(transforms);
            DialogueEventPlayer[] dialogue = Components<DialogueEventPlayer>(transforms);
            Interactable[] other = Components<Interactable>(transforms)
                .Where(item => item.GetComponent<FlavorTextInteraction>() == null).ToArray();

            EditorGUILayout.HelpBox(
                $"{SceneLabel(selectedScene)} • {(scope == null ? "whole scene" : Path(scope.transform))} • " +
                $"{flavor.Length} flavor, {dialogue.Length} dialogue, {other.Length} other interaction target(s)",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawFlavor(flavor);
            DrawDialogue(dialogue);
            DrawOther(other);
            EditorGUILayout.EndScrollView();
        }

        private void DrawFlavor(IEnumerable<FlavorTextInteraction> items)
        {
            EditorGUILayout.LabelField("Flavor Interactions", EditorStyles.boldLabel);
            foreach (FlavorTextInteraction item in items.Where(Matches))
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawObjectHeader(item.gameObject);
                var flavor = new SerializedObject(item);
                var interaction = new SerializedObject(item.GetComponent<Interactable>());
                flavor.Update(); interaction.Update();
                EditorGUILayout.PropertyField(interaction.FindProperty("displayName"), new GUIContent("Prompt"));
                EditorGUILayout.PropertyField(flavor.FindProperty("flavorText"), new GUIContent("Flavor Text"));
                EditorGUILayout.PropertyField(flavor.FindProperty("channel"));
                if (flavor.ApplyModifiedProperties() | interaction.ApplyModifiedProperties()) MarkDirty(item.gameObject);
            }
        }

        private void DrawDialogue(IEnumerable<DialogueEventPlayer> items)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Dialogues", EditorStyles.boldLabel);
            foreach (DialogueEventPlayer item in items.Where(Matches))
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawObjectHeader(item.gameObject);
                var serialized = new SerializedObject(item);
                serialized.Update();
                SerializedProperty treeProperty = serialized.FindProperty("dialogueTree");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(treeProperty, new GUIContent("Dialogue Tree"));
                    using (new EditorGUI.DisabledScope(treeProperty.objectReferenceValue == null))
                        if (GUILayout.Button("Open Graph", GUILayout.Width(85f)))
                            DialogueWorkspaceWindow.Open(treeProperty.objectReferenceValue as DialogueTree);
                }
                EditorGUILayout.PropertyField(serialized.FindProperty("focusTarget"));
                EditorGUILayout.PropertyField(serialized.FindProperty("speaker"));
                if (serialized.ApplyModifiedProperties()) MarkDirty(item.gameObject);
            }
        }

        private void DrawOther(IEnumerable<Interactable> items)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Other Interaction Targets", EditorStyles.boldLabel);
            foreach (Interactable item in items.Where(Matches))
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawObjectHeader(item.gameObject);
                var serialized = new SerializedObject(item);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("displayName"), new GUIContent("Prompt"));
                if (serialized.ApplyModifiedProperties()) MarkDirty(item.gameObject);
            }
        }

        private IEnumerable<GameObject> Roots() => scope != null
            ? new[] { scope }
            : selectedScene.GetRootGameObjects();

        private static T[] Components<T>(IEnumerable<Transform> transforms) where T : Component =>
            transforms.Select(transform => transform.GetComponent<T>()).Where(item => item != null).ToArray();

        private bool Matches(Component item) => string.IsNullOrEmpty(search) ||
            item.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
            Path(item.transform).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void DrawObjectHeader(GameObject owner)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(Path(owner.transform), EditorStyles.boldLabel);
                if (GUILayout.Button("Select", GUILayout.Width(55f))) Selection.activeGameObject = owner;
            }
        }

        private static void MarkDirty(GameObject owner)
        {
            EditorUtility.SetDirty(owner);
            EditorSceneManager.MarkSceneDirty(owner.scene);
        }

        private static string SceneLabel(Scene scene) => string.IsNullOrEmpty(scene.path) ? scene.name : scene.name;
        private static string Path(Transform target) => target.parent == null ? target.name : Path(target.parent) + "/" + target.name;
    }
}
