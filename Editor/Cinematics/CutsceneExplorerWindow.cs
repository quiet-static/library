using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Browses, validates, creates, and previews cutscene runners.</summary>
    public sealed class CutsceneExplorerWindow : EditorWindow
    {
        private readonly List<CutsceneSequenceRunner> runners = new();
        private Vector2 scroll;
        private string search = string.Empty;

        [MenuItem("Tools/Quiet Static/Cinematics/Cutscene Explorer")]
        public static void Open() => GetWindow<CutsceneExplorerWindow>("Cutscenes");

        private void OnEnable()
        {
            Refresh();
            EditorApplication.hierarchyChanged += Refresh;
        }

        private void OnDisable() => EditorApplication.hierarchyChanged -= Refresh;

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"));
                if (GUILayout.Button("Create", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    CreateCutscene();
                }
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65f)))
                {
                    Refresh();
                }
            }

            EditorGUILayout.HelpBox(
                $"{runners.Count} cutscene runner(s) in loaded scenes. Preview controls require Play Mode.",
                MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (CutsceneSequenceRunner runner in runners.Where(MatchesSearch))
            {
                DrawRunner(runner);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawRunner(CutsceneSequenceRunner runner)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(runner.DisplayName, EditorStyles.boldLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(55f)))
                    {
                        Selection.activeObject = runner.gameObject;
                        EditorGUIUtility.PingObject(runner.gameObject);
                    }
                }

                EditorGUILayout.LabelField("Scene", runner.gameObject.scene.name);
                EditorGUILayout.LabelField("Steps", runner.Steps.Count.ToString());
                for (int index = 0; index < runner.Steps.Count; index++)
                {
                    CutsceneSequenceRunner.Step step = runner.Steps[index];
                    string label = step == null || string.IsNullOrWhiteSpace(step.name)
                        ? $"Step {index + 1}"
                        : step.name;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{index + 1}. {label}");
                        GUI.enabled = EditorApplication.isPlaying && !runner.IsRunning && step != null;
                        if (GUILayout.Button("Play", GUILayout.Width(48f))) runner.PlayStep(index);
                        GUI.enabled = true;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = EditorApplication.isPlaying && !runner.IsRunning;
                    if (GUILayout.Button("Play all")) runner.Play();
                    GUI.enabled = EditorApplication.isPlaying && runner.IsRunning;
                    if (GUILayout.Button("Stop")) runner.Stop();
                    GUI.enabled = true;
                }
            }
        }

        private bool MatchesSearch(CutsceneSequenceRunner runner)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            return runner.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   runner.Steps.Any(step => step != null && !string.IsNullOrWhiteSpace(step.name) &&
                       step.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void Refresh()
        {
            runners.Clear();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    runners.AddRange(root.GetComponentsInChildren<CutsceneSequenceRunner>(true));
                }
            }
            runners.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            Repaint();
        }

        private static void CreateCutscene()
        {
            GameObject root = new("Cutscene");
            Undo.RegisterCreatedObjectUndo(root, "Create Cutscene");
            CutsceneSequenceRunner runner = Undo.AddComponent<CutsceneSequenceRunner>(root);
            SerializedObject serializedRunner = new(runner);
            SerializedProperty steps = serializedRunner.FindProperty("steps");
            steps.arraySize = 1;
            steps.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = "Opening Shot";
            serializedRunner.ApplyModifiedProperties();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }
    }
}
