using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>Edits and explores the directed connections in a scene-flow map.</summary>
    public sealed class SceneFlowExplorerWindow : EditorWindow
    {
        private SceneFlowMap map;
        private SerializedObject serializedMap;
        private Vector2 scroll;
        private string filter = string.Empty;

        [MenuItem(QuietStaticMenuPaths.Toolkit + "Scene Flow/Scene Flow Explorer")]
        public static void Open()
        {
            GetWindow<SceneFlowExplorerWindow>("Scene Flow");
        }

        private void OnGUI()
        {
            DrawMapPicker();
            if (map == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or create a Scene Flow Map to configure scene connections.",
                    MessageType.Info);
                return;
            }

            serializedMap ??= new SerializedObject(map);
            serializedMap.Update();
            DrawSummary();
            DrawConnections();
            serializedMap.ApplyModifiedProperties();
        }

        private void DrawMapPicker()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                SceneFlowMap selected = (SceneFlowMap)EditorGUILayout.ObjectField(
                    map,
                    typeof(SceneFlowMap),
                    false,
                    GUILayout.MinWidth(180f));
                if (selected != map)
                {
                    map = selected;
                    serializedMap = map != null ? new SerializedObject(map) : null;
                }

                filter = GUILayout.TextField(
                    filter,
                    GUI.skin.FindStyle("ToolbarSearchTextField"),
                    GUILayout.MinWidth(120f));

                if (GUILayout.Button("Create", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                {
                    CreateMap();
                }
            }
        }

        private void DrawSummary()
        {
            IReadOnlyList<SceneFlowMap.Connection> connections = map.Connections;
            int sceneCount = connections
                .SelectMany(connection => new[]
                {
                    connection?.FromSceneName,
                    connection?.ToSceneName,
                })
                .Count(name => !string.IsNullOrWhiteSpace(name));
            int uniqueScenes = connections
                .SelectMany(connection => new[]
                {
                    connection?.FromSceneName,
                    connection?.ToSceneName,
                })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .Count();
            int invalid = connections.Count(connection =>
                connection == null ||
                string.IsNullOrWhiteSpace(connection.Id) ||
                string.IsNullOrWhiteSpace(connection.FromSceneName) ||
                string.IsNullOrWhiteSpace(connection.ToSceneName));
            int duplicateIds = connections
                .Where(connection =>
                    connection != null &&
                    !string.IsNullOrWhiteSpace(connection.Id))
                .GroupBy(connection => connection.Id, StringComparer.Ordinal)
                .Sum(group => Math.Max(0, group.Count() - 1));

            EditorGUILayout.HelpBox(
                $"{uniqueScenes} scene(s), {connections.Count} connection(s), " +
                $"{invalid} incomplete, {duplicateIds} duplicate ID(s). " +
                $"Directed endpoints: {sceneCount}.",
                invalid > 0 || duplicateIds > 0
                    ? MessageType.Warning
                    : MessageType.Info);
        }

        private void DrawConnections()
        {
            SerializedProperty connections =
                serializedMap.FindProperty("connections");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < connections.arraySize; index++)
            {
                SerializedProperty connection =
                    connections.GetArrayElementAtIndex(index);
                string id = connection.FindPropertyRelative("id").stringValue;
                string from = GetSceneName(connection, "fromScene");
                string to = GetSceneName(connection, "toScene");
                if (!MatchesFilter(id, from, to))
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        string.IsNullOrWhiteSpace(id) ? "Incomplete connection" : id,
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        $"{DisplayScene(from)}  →  {DisplayScene(to)}");
                    EditorGUILayout.PropertyField(connection, true);
                    DrawConnectedScenes(from, to);
                    if (GUILayout.Button("Remove connection"))
                    {
                        connections.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
            }

            if (GUILayout.Button("Add connection"))
            {
                connections.InsertArrayElementAtIndex(connections.arraySize);
                SerializedProperty added =
                    connections.GetArrayElementAtIndex(connections.arraySize - 1);
                added.FindPropertyRelative("id").stringValue = "NewConnection";
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawConnectedScenes(string from, string to)
        {
            string inbound = string.Join(", ", map.Connections
                .Where(connection => connection != null && connection.ToSceneName == from)
                .Select(connection => connection.FromSceneName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct());
            string outbound = string.Join(", ", map.Connections
                .Where(connection => connection != null && connection.FromSceneName == to)
                .Select(connection => connection.ToSceneName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct());
            EditorGUILayout.LabelField("Into source", string.IsNullOrEmpty(inbound) ? "—" : inbound);
            EditorGUILayout.LabelField("Out of target", string.IsNullOrEmpty(outbound) ? "—" : outbound);
        }

        private bool MatchesFilter(params string[] values)
        {
            return string.IsNullOrWhiteSpace(filter) || values.Any(value =>
                !string.IsNullOrWhiteSpace(value) &&
                value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetSceneName(
            SerializedProperty connection,
            string propertyName)
        {
            return connection.FindPropertyRelative(propertyName)
                .FindPropertyRelative("sceneName").stringValue;
        }

        private static string DisplayScene(string value) =>
            string.IsNullOrWhiteSpace(value) ? "<Scene>" : value;

        private void CreateMap()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Scene Flow Map",
                "SceneFlowMap",
                "asset",
                "Choose where to save the scene connection map.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            map = CreateInstance<SceneFlowMap>();
            AssetDatabase.CreateAsset(map, path);
            AssetDatabase.SaveAssets();
            serializedMap = new SerializedObject(map);
            Selection.activeObject = map;
        }
    }
}
