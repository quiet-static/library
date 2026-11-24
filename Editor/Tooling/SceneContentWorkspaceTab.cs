using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Editor.Interactions;
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
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.isLoaded && !scenes.Contains(stage.scene))
                scenes.Add(stage.scene);
            if (!selectedScene.IsValid() || !scenes.Contains(selectedScene))
                selectedScene = scenes.FirstOrDefault();
            if (scope != null && scope.scene != selectedScene) scope = null;
            gui?.MarkDirtyRepaint();
        }

        public void OnSelected() => Refresh();
        public void OnDeselected() { }

        internal void SetScope(GameObject target)
        {
            if (target == null || !target.scene.isLoaded) return;
            if (!scenes.Contains(target.scene)) Refresh();
            if (!scenes.Contains(target.scene)) return;
            selectedScene = target.scene;
            scope = target;
            scroll = Vector2.zero;
            gui?.MarkDirtyRepaint();
        }

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
                using (new EditorGUI.DisabledScope(Selection.activeGameObject == null ||
                           !Selection.activeGameObject.scene.isLoaded))
                    if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                        SetScope(Selection.activeGameObject);
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45f))) scope = null;
            }

            GameObject[] roots = Roots().ToArray();
            FlavorTextInteraction[] flavor = Components<FlavorTextInteraction>(roots);
            DialogueEventPlayer[] dialogue = Components<DialogueEventPlayer>(roots);
            Component[] other = roots.SelectMany(InteractionTargetAuthoring.FindTargets)
                .Where(item => item is not Interactable || item.GetComponent<FlavorTextInteraction>() == null).ToArray();

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
                InteractionTargetAuthoring.DrawPrompt(interaction);
                EditorGUILayout.PropertyField(flavor.FindProperty("flavorText"), new GUIContent("Flavor Text"));
                EditorGUILayout.PropertyField(flavor.FindProperty("channel"));
                InteractionOutcomeEditor.DrawInteractionDetails(interaction);
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
                using var serialized = new SerializedObject(item);
                serialized.Update();
                SerializedProperty treeProperty = serialized.FindProperty("dialogueTree");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(treeProperty, new GUIContent("Dialogue Tree"));
                    using (new EditorGUI.DisabledScope(treeProperty.objectReferenceValue == null))
                        if (GUILayout.Button("Open Graph", GUILayout.Width(85f)))
                            DialogueWorkspaceWindow.Open(treeProperty.objectReferenceValue as DialogueTree);
                }
                EditorGUILayout.PropertyField(serialized.FindProperty("dialogueResolver"));
                EditorGUILayout.PropertyField(serialized.FindProperty("focusTarget"));
                EditorGUILayout.PropertyField(serialized.FindProperty("speaker"));
                SerializedProperty startOnTriggerEnter = serialized.FindProperty("startOnTriggerEnter");
                EditorGUILayout.PropertyField(startOnTriggerEnter);
                if (startOnTriggerEnter.boolValue)
                {
                    EditorGUILayout.PropertyField(serialized.FindProperty("requiredTag"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("triggerOnce"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("startRequirement"), true);
                }
                if (serialized.ApplyModifiedProperties()) MarkDirty(item.gameObject);
            }
        }

        private void DrawOther(IEnumerable<Component> items)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Other Interaction Targets", EditorStyles.boldLabel);
            foreach (Component item in items.Where(Matches))
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawObjectHeader(item.gameObject, InteractionTargetAuthoring.For(item).Kind);
                using var serialized = new SerializedObject(item);
                serialized.Update();
                InteractionTargetAuthoring.DrawPrompt(serialized);
                InteractionOutcomeEditor.DrawInteractionDetails(serialized);
                if (serialized.ApplyModifiedProperties()) MarkDirty(item.gameObject);
            }
        }

        private IEnumerable<GameObject> Roots() => scope != null
            ? new[] { scope }
            : selectedScene.GetRootGameObjects();

        private static T[] Components<T>(IEnumerable<GameObject> roots) where T : Component =>
            roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private bool Matches(Component item) => string.IsNullOrEmpty(search) ||
            item.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
            Path(item.transform).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void DrawObjectHeader(GameObject owner, string kind = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string path = Path(owner.transform);
                EditorGUILayout.LabelField(kind == null ? path : $"{path}  [{kind}]", EditorStyles.boldLabel);
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
