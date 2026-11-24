using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Editor.Tooling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Shows the loaded scene players, their cinematic definitions, and the scene objects each beat uses.</summary>
    public sealed class CinematicLaunchOverview : EditorWindow
    {
        private readonly List<CinematicScenePlayer> players = new();
        private readonly List<CinematicSceneLauncher> launchers = new();
        private readonly HashSet<string> loadedSceneNames = new(StringComparer.Ordinal);
        private CinematicScenePlayer selectedPlayer;
        private CinematicDefinition selectedDefinition;
        private Vector2 scroll;
        private bool showBindings;

        internal static void Open() => GetWindow<CinematicLaunchOverview>("Cinematic Launch Routes");

        private void OnEnable()
        {
            Refresh();
            EditorApplication.hierarchyChanged += Refresh;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            Undo.undoRedoPerformed -= Repaint;
        }

        private void Refresh()
        {
            players.Clear();
            launchers.Clear();
            loadedSceneNames.Clear();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
                loadedSceneNames.Add(scene.name);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    players.AddRange(root.GetComponentsInChildren<CinematicScenePlayer>(true));
                    launchers.AddRange(root.GetComponentsInChildren<CinematicSceneLauncher>(true));
                }
            }
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Loaded Scenes", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) Refresh();
            }

            using var view = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = view.scrollPosition;
            CinematicScenePlayer[] available = players.Where(player => player != null).ToArray();
            if (available.Length == 0)
            {
                EditorGUILayout.HelpBox("Open a scene with a Cinematic Scene Player to inspect its routes.", MessageType.Info);
            }
            else
            {
                string[] labels = available.Select(player => $"{player.gameObject.scene.name} / {player.name}").ToArray();
                int index = Mathf.Max(0, Array.IndexOf(available, selectedPlayer));
                selectedPlayer = available[EditorGUILayout.Popup("Scene Player", index, labels)];
                DrawPlayer(selectedPlayer);
            }

            if (launchers.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Scene Launchers", EditorStyles.boldLabel);
                foreach (CinematicSceneLauncher launcher in launchers)
                    if (launcher != null) DrawLauncher(launcher);
            }
        }

        private void DrawPlayer(CinematicScenePlayer player)
        {
            using var data = new SerializedObject(player);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.PropertyField(data.FindProperty("locationId"));
                EditorGUILayout.PropertyField(data.FindProperty("database"));
                EditorGUILayout.PropertyField(data.FindProperty("launchChannel"));
                EditorGUILayout.PropertyField(data.FindProperty("definition"), new GUIContent("Default Definition"));
                EditorGUILayout.PropertyField(data.FindProperty("playDefaultOnStart"));
                showBindings = EditorGUILayout.Foldout(showBindings, "Edit Scene Bindings", true);
                if (showBindings)
                {
                    foreach (string property in new[] { "shots", "characters", "activities", "beatEvents" })
                        EditorGUILayout.PropertyField(data.FindProperty(property), true);
                }
                data.ApplyModifiedProperties();
            }

            var database = data.FindProperty("database").objectReferenceValue as CinematicDatabase;
            CinematicDefinition[] definitions = GetDefinitions(player.Definition, database);
            if (definitions.Length == 0)
            {
                EditorGUILayout.HelpBox("Assign a cinematic database or a default definition to see its beats and bindings.", MessageType.Info);
                return;
            }

            string[] labels = definitions.Select(definition =>
                (string.IsNullOrWhiteSpace(definition.Id) ? definition.name : definition.Id) +
                (definition == player.Definition ? " (Default)" : string.Empty)).ToArray();
            int index = Mathf.Max(0, Array.IndexOf(definitions, selectedDefinition));
            selectedDefinition = definitions[EditorGUILayout.Popup("Cinematic", index, labels)];
            if (GUILayout.Button("Open Definition in Workspace")) ContentWorkspaceWindow.Open("cinematics", selectedDefinition);

            if (string.IsNullOrWhiteSpace(selectedDefinition.Id))
                EditorGUILayout.HelpBox("This definition has no cinematic ID.", MessageType.Warning);
            else if (database != null && database.Cinematics.Count(value => value != null && value.Id == selectedDefinition.Id) > 1)
                EditorGUILayout.HelpBox("Several definitions use this ID. Launches use the first match in the database.", MessageType.Warning);

            foreach (BindingRow binding in ReadBindings(player, selectedDefinition)) DrawBinding(binding);
            if (selectedDefinition.Beats.Count == 0)
                EditorGUILayout.HelpBox("This definition has no beats.", MessageType.Info);
        }

        private static CinematicDefinition[] GetDefinitions(CinematicDefinition fallback, CinematicDatabase database)
        {
            IEnumerable<CinematicDefinition> catalog = database != null ? database.Cinematics : Array.Empty<CinematicDefinition>();
            return catalog.Prepend(fallback).Where(definition => definition != null).Distinct().ToArray();
        }

        internal readonly struct BindingRow
        {
            internal readonly string Label;
            internal readonly Object Target;
            internal readonly string Problem;

            internal BindingRow(string label, Object target = null, string problem = null)
            {
                Label = label;
                Target = target;
                Problem = problem;
            }
        }

        internal static IReadOnlyList<BindingRow> ReadBindings(CinematicScenePlayer player, CinematicDefinition definition)
        {
            var rows = new List<BindingRow>();
            using var data = new SerializedObject(player);
            Scene scene = player.gameObject.scene;
            for (int index = 0; index < definition.Beats.Count; index++)
            {
                CinematicDefinition.Beat beat = definition.Beats[index];
                string label = $"Beat {index + 1}";
                if (beat == null)
                {
                    rows.Add(new BindingRow(label, problem: "The beat is empty."));
                    continue;
                }

                bool hasId = !string.IsNullOrWhiteSpace(beat.id);
                string beatLabel = hasId ? $"{label}  {beat.id}" : label;
                rows.Add(new BindingRow(beatLabel, problem: hasId ? null : "The beat needs a stable ID for event routes."));
                if (!string.IsNullOrWhiteSpace(beat.activityId))
                    rows.Add(Resolve(data.FindProperty("activities"), "Activity", beat.activityId, scene, "component"));
                if (!string.IsNullOrWhiteSpace(beat.cameraShotId))
                    rows.Add(Resolve(data.FindProperty("shots"), "Camera Shot", beat.cameraShotId, scene, "pose", "cameraTransform"));
                if (beat.characterAnimations == null) continue;
                foreach (CinematicDefinition.CharacterAnimation command in beat.characterAnimations)
                {
                    if (command == null) continue;
                    rows.Add(string.IsNullOrWhiteSpace(command.characterId)
                        ? new BindingRow("Character Animation", problem: "Assign a character ID in the definition.")
                        : Resolve(data.FindProperty("characters"), "Character", command.characterId, scene, "animator"));
                }
            }
            return rows;
        }

        private static BindingRow Resolve(SerializedProperty bindings, string role, string id, Scene scene,
            string targetProperty, string secondProperty = null)
        {
            string label = $"{role}  {id}";
            Object target = null;
            string problem = null;
            int matches = 0;
            for (int index = 0; index < bindings.arraySize; index++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
                if (!string.Equals(binding.FindPropertyRelative("id").stringValue, id, StringComparison.Ordinal)) continue;
                matches++;
                if (matches != 1) continue;

                target = binding.FindPropertyRelative(targetProperty).objectReferenceValue;
                Object secondary = secondProperty != null ? binding.FindPropertyRelative(secondProperty).objectReferenceValue : target;
                if (target == null || secondary == null) problem = "Assign the scene objects on this binding.";
                else if (InAnotherScene(target, scene) || InAnotherScene(secondary, scene))
                    problem = "The binding points to another scene. Assign objects in the player's scene.";
            }
            if (matches == 0) problem = "Add a scene binding with this exact ID.";
            if (matches > 1) problem = "Several bindings use this ID. Playback uses the first match.";
            return new BindingRow(label, target, problem);
        }

        private static bool InAnotherScene(Object value, Scene scene) =>
            value is Component component && component.gameObject.scene != scene;

        private static void DrawBinding(BindingRow row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(row.Label, GUILayout.MinWidth(140));
                if (row.Target != null && GUILayout.Button(row.Target.name, EditorStyles.miniButton, GUILayout.MaxWidth(175)))
                    Select(row.Target);
                if (row.Target is CutsceneSequenceRunner runner && GUILayout.Button("Edit Steps", EditorStyles.miniButton, GUILayout.Width(70)))
                    CutsceneExplorerWindow.Open(runner);
            }
            if (!string.IsNullOrEmpty(row.Problem)) EditorGUILayout.HelpBox(row.Problem, MessageType.Warning);
        }

        private void DrawLauncher(CinematicSceneLauncher launcher)
        {
            using var data = new SerializedObject(launcher);
            string sceneName = data.FindProperty("targetScene").FindPropertyRelative("sceneName").stringValue;
            string location = data.FindProperty("locationId").stringValue?.Trim();
            string cinematic = data.FindProperty("cinematicId").stringValue?.Trim();
            var channel = data.FindProperty("launchChannel").objectReferenceValue as CinematicLaunchChannel;

            using var card = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            DrawBinding(new BindingRow($"{launcher.gameObject.scene.name} → {sceneName}  /  {cinematic}", launcher));
            if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(cinematic) ||
                channel == null || data.FindProperty("sceneFlowChannel").objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a destination scene, location, cinematic ID, and both channels on this launcher.", MessageType.Warning);
            }
            else if (!loadedSceneNames.Contains(sceneName))
            {
                EditorGUILayout.LabelField("Open the destination scene to check its player binding.", EditorStyles.wordWrappedMiniLabel);
            }
            else if (!players.Any(player => MatchesLauncher(player, sceneName, location, channel, cinematic)))
            {
                EditorGUILayout.HelpBox("No player in the destination scene matches the location, channel, and cinematic ID.", MessageType.Warning);
            }
        }

        private static bool MatchesLauncher(CinematicScenePlayer player, string sceneName, string location,
            CinematicLaunchChannel channel, string cinematic)
        {
            if (player == null || player.gameObject.scene.name != sceneName) return false;
            using var data = new SerializedObject(player);
            return data.FindProperty("locationId").stringValue == location &&
                data.FindProperty("launchChannel").objectReferenceValue == channel &&
                data.FindProperty("database").objectReferenceValue is CinematicDatabase database && database.Find(cinematic) != null;
        }

        private static void Select(Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
