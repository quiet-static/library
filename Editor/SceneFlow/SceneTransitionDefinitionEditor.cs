using System;
using System.Linq;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>
    /// Inspector for destination responses with an inbound map-connection picker.
    /// </summary>
    [CustomEditor(typeof(SceneTransitionDefinition))]
    public sealed class SceneTransitionDefinitionEditor : UnityEditor.Editor
    {
        private const float Spacing = 2f;

        private SerializedProperty sceneFlowMap;
        private SerializedProperty onEntered;
        private SerializedProperty responses;
        private ReorderableList responseList;

        private void OnEnable()
        {
            sceneFlowMap = serializedObject.FindProperty("sceneFlowMap");
            onEntered = serializedObject.FindProperty("onEntered");
            responses = serializedObject.FindProperty("responses");

            responseList = new ReorderableList(
                serializedObject,
                responses,
                true,
                true,
                true,
                true)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(
                        rect,
                        "Conditional Responses (first eligible wins)"),
                drawElementCallback = DrawResponse,
                elementHeightCallback = GetResponseHeight,
                onAddCallback = AddResponse,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(sceneFlowMap);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(onEntered);
            EditorGUILayout.Space();
            responseList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawResponse(
            Rect rect,
            int index,
            bool isActive,
            bool isFocused)
        {
            SerializedProperty response =
                responses.GetArrayElementAtIndex(index);
            SerializedProperty label =
                response.FindPropertyRelative("label");
            SerializedProperty conditionId =
                response.FindPropertyRelative("conditionId");
            SerializedProperty requirement =
                response.FindPropertyRelative("requirement");
            SerializedProperty responseEvent =
                response.FindPropertyRelative("onEntered");

            rect.y += Spacing;
            DrawProperty(ref rect, label);
            DrawCondition(ref rect, conditionId);
            DrawProperty(ref rect, requirement);
            DrawProperty(ref rect, responseEvent);
        }

        private float GetResponseHeight(int index)
        {
            SerializedProperty response =
                responses.GetArrayElementAtIndex(index);
            SerializedProperty label =
                response.FindPropertyRelative("label");
            SerializedProperty conditionId =
                response.FindPropertyRelative("conditionId");
            SerializedProperty requirement =
                response.FindPropertyRelative("requirement");
            SerializedProperty responseEvent =
                response.FindPropertyRelative("onEntered");

            float height = Spacing;
            height += PropertyHeight(label);
            height += ConditionHeight(conditionId);
            height += PropertyHeight(requirement);
            height += PropertyHeight(responseEvent);
            return height + Spacing;
        }

        private void DrawCondition(
            ref Rect rect,
            SerializedProperty conditionId)
        {
            SceneFlowMap map = sceneFlowMap.objectReferenceValue as SceneFlowMap;
            if (map == null)
            {
                DrawProperty(ref rect, conditionId);
                return;
            }

            string[] ids = GetInboundConnectionIds(map);
            string currentId = string.IsNullOrWhiteSpace(conditionId.stringValue)
                ? string.Empty
                : conditionId.stringValue.Trim();
            int currentIndex = Array.IndexOf(ids, currentId);
            string[] options = new[] { "<Custom condition>" }
                .Concat(ids)
                .ToArray();

            Rect popupRect = TakeLine(ref rect);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(
                popupRect,
                "Mapped Connection",
                currentIndex + 1,
                options);
            if (EditorGUI.EndChangeCheck())
            {
                conditionId.stringValue = selected <= 0
                    ? string.Empty
                    : ids[selected - 1];
                currentIndex = selected - 1;
            }

            if (currentIndex < 0)
            {
                DrawProperty(ref rect, conditionId);
            }
        }

        private float ConditionHeight(SerializedProperty conditionId)
        {
            SceneFlowMap map = sceneFlowMap.objectReferenceValue as SceneFlowMap;
            if (map == null)
            {
                return PropertyHeight(conditionId);
            }

            string currentId = string.IsNullOrWhiteSpace(conditionId.stringValue)
                ? string.Empty
                : conditionId.stringValue.Trim();
            bool isMapped = Array.IndexOf(
                GetInboundConnectionIds(map),
                currentId) >= 0;
            return EditorGUIUtility.singleLineHeight + Spacing +
                   (isMapped ? 0f : PropertyHeight(conditionId));
        }

        private string[] GetInboundConnectionIds(SceneFlowMap map)
        {
            string sceneName =
                ((SceneTransitionDefinition)target).gameObject.scene.name;
            return map.Connections
                .Where(connection =>
                    connection != null &&
                    !string.IsNullOrWhiteSpace(connection.Id) &&
                    (string.IsNullOrWhiteSpace(sceneName) ||
                     string.Equals(
                         connection.ToSceneName,
                         sceneName,
                         StringComparison.Ordinal)))
                .Select(connection => connection.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static void DrawProperty(
            ref Rect rect,
            SerializedProperty property)
        {
            float height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, height),
                property,
                true);
            rect.y += height + Spacing;
        }

        private static Rect TakeLine(ref Rect rect)
        {
            Rect line = new Rect(
                rect.x,
                rect.y,
                rect.width,
                EditorGUIUtility.singleLineHeight);
            rect.y += EditorGUIUtility.singleLineHeight + Spacing;
            return line;
        }

        private static float PropertyHeight(SerializedProperty property)
        {
            return EditorGUI.GetPropertyHeight(property, true) + Spacing;
        }

        private void AddResponse(ReorderableList list)
        {
            int index = responses.arraySize;
            responses.arraySize++;
            SerializedProperty response =
                responses.GetArrayElementAtIndex(index);
            response.FindPropertyRelative("label").stringValue = string.Empty;
            response.FindPropertyRelative("conditionId").stringValue = string.Empty;

            SerializedProperty requirement =
                response.FindPropertyRelative("requirement");
            requirement.FindPropertyRelative("mode").enumValueIndex = 0;
            requirement.FindPropertyRelative("flags").arraySize = 0;

            SerializedProperty calls = response
                .FindPropertyRelative("onEntered")
                .FindPropertyRelative("m_PersistentCalls")
                ?.FindPropertyRelative("m_Calls");
            if (calls != null)
            {
                calls.arraySize = 0;
            }

            list.index = index;
        }
    }
}
