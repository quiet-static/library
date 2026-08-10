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
            SerializedProperty channel = serializedObject.FindProperty("requestChannel");
            SerializedProperty onTransitionStarted = serializedObject.FindProperty("onTransitionStarted");

            EditorGUILayout.PropertyField(requirement);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(map);
            DrawConnectionPicker(map.objectReferenceValue as SceneFlowMap, connectionId);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(targetScene);
            EditorGUILayout.PropertyField(channel);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(onTransitionStarted);

            if (map.objectReferenceValue == null &&
                string.IsNullOrWhiteSpace(
                    targetScene.FindPropertyRelative("sceneName").stringValue))
            {
                EditorGUILayout.HelpBox(
                    "Assign a map connection or a direct target scene.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawConnectionPicker(
            SceneFlowMap map,
            SerializedProperty connectionId)
        {
            if (map == null)
            {
                EditorGUILayout.PropertyField(connectionId);
                return;
            }

            string[] ids = map.Connections
                .Where(connection => connection != null)
                .Select(connection => connection.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string[] options = new[] { "<Direct target scene>" }
                .Concat(ids)
                .ToArray();
            int current = Array.IndexOf(ids, connectionId.stringValue);
            int selected = EditorGUILayout.Popup(
                "Connection",
                current + 1,
                options);
            connectionId.stringValue =
                selected <= 0 ? string.Empty : ids[selected - 1];
        }
    }
}
