using System;
using System.Linq;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>Inspector with a map-backed connection selector.</summary>
    [CustomEditor(typeof(SceneTransitionHandler))]
    public sealed class SceneTransitionHandlerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty requirement = serializedObject.FindProperty("requirement");
            SerializedProperty map = serializedObject.FindProperty("sceneFlowMap");
            SerializedProperty connectionId = serializedObject.FindProperty("connectionId");
            SerializedProperty targetScene = serializedObject.FindProperty("targetScene");
            SerializedProperty conditionId = serializedObject.FindProperty("conditionId");
            SerializedProperty channel = serializedObject.FindProperty("requestChannel");
            SerializedProperty onTransitionStarted = serializedObject.FindProperty("onTransitionStarted");

            EditorGUILayout.PropertyField(requirement);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(map);
            string sourceSceneName =
                ((SceneTransitionHandler)target).gameObject.scene.name;
            DrawConnectionPicker(
                map.objectReferenceValue as SceneFlowMap,
                connectionId,
                sourceSceneName);

            bool usesMappedConnection =
                !string.IsNullOrWhiteSpace(connectionId.stringValue);
            if (!usesMappedConnection)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(targetScene);
                EditorGUILayout.PropertyField(conditionId);
            }

            EditorGUILayout.PropertyField(channel);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(onTransitionStarted);

            if (!usesMappedConnection &&
                string.IsNullOrWhiteSpace(
                    targetScene.FindPropertyRelative("sceneName").stringValue))
            {
                EditorGUILayout.HelpBox(
                    "Assign a map connection or a direct target scene.",
                    MessageType.Warning);
            }
            else if (usesMappedConnection &&
                     !IsAvailableConnection(
                         map.objectReferenceValue as SceneFlowMap,
                         connectionId.stringValue,
                         sourceSceneName))
            {
                EditorGUILayout.HelpBox(
                    $"Connection '{connectionId.stringValue}' is missing or cannot start in scene '{sourceSceneName}'.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawConnectionPicker(
            SceneFlowMap map,
            SerializedProperty connectionId,
            string sourceSceneName)
        {
            if (map == null)
            {
                EditorGUILayout.PropertyField(connectionId);
                return;
            }

            string[] ids = map.Connections
                .Where(connection =>
                    connection != null &&
                    (string.IsNullOrWhiteSpace(sourceSceneName) ||
                     string.IsNullOrWhiteSpace(connection.FromSceneName) ||
                     string.Equals(
                         connection.FromSceneName,
                         sourceSceneName,
                         StringComparison.Ordinal)))
                .Select(connection => connection.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            string currentId = string.IsNullOrWhiteSpace(connectionId.stringValue)
                ? string.Empty
                : connectionId.stringValue.Trim();
            bool unavailable =
                !string.IsNullOrEmpty(currentId) &&
                Array.IndexOf(ids, currentId) < 0;
            string[] selectableIds = unavailable
                ? ids.Concat(new[] { currentId }).ToArray()
                : ids;
            string[] options = new[] { "<Direct target scene>" }
                .Concat(ids)
                .Concat(unavailable
                    ? new[] { $"{currentId} (Unavailable)" }
                    : Array.Empty<string>())
                .ToArray();

            int current = Array.IndexOf(selectableIds, currentId);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(
                "Connection",
                current + 1,
                options);
            if (EditorGUI.EndChangeCheck())
            {
                connectionId.stringValue = selected <= 0
                    ? string.Empty
                    : selectableIds[selected - 1];
            }
        }

        private static bool IsAvailableConnection(
            SceneFlowMap map,
            string connectionId,
            string sourceSceneName)
        {
            if (map == null || string.IsNullOrWhiteSpace(connectionId))
            {
                return false;
            }

            string normalizedId = connectionId.Trim();
            return map.Connections.Any(connection =>
                connection != null &&
                string.Equals(
                    connection.Id,
                    normalizedId,
                    StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(connection.FromSceneName) ||
                 string.IsNullOrWhiteSpace(sourceSceneName) ||
                 string.Equals(
                     connection.FromSceneName,
                     sourceSceneName,
                     StringComparison.Ordinal)));
        }
    }
}
