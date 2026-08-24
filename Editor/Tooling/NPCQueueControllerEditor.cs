using System.Collections.Generic;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>
    /// Compact queue-layout inspector with authoring diagnostics for references that the
    /// runtime intentionally tolerates or filters.
    /// </summary>
    [CustomEditor(typeof(NPCQueueController))]
    public sealed class NPCQueueControllerEditor : UnityEditor.Editor
    {
        private static readonly string[] DrawnProperties =
        {
            "m_Script",
            "entryPoint",
            "servicePoint",
            "exitPoint",
            "waitingPoints",
            "initialMembers",
            "maximumActiveMembers",
            "beginOnStart",
            "manageMemberVisibility",
            "arrivalDistance",
            "fallbackMovementSpeed",
            "useDirectMovementFallback",
            "fallbackTurnSpeed",
            "onMemberReadyForService",
            "onServiceStarted",
            "onMemberDeparted",
            "onQueueCompleted",
            "onMovementFailed",
        };

        private SerializedProperty entryPoint;
        private SerializedProperty servicePoint;
        private SerializedProperty exitPoint;
        private SerializedProperty waitingPoints;
        private SerializedProperty initialMembers;
        private SerializedProperty maximumActiveMembers;
        private SerializedProperty beginOnStart;
        private SerializedProperty manageMemberVisibility;
        private SerializedProperty arrivalDistance;
        private SerializedProperty fallbackMovementSpeed;
        private SerializedProperty useDirectMovementFallback;
        private SerializedProperty fallbackTurnSpeed;
        private SerializedProperty onMemberReadyForService;
        private SerializedProperty onServiceStarted;
        private SerializedProperty onMemberDeparted;
        private SerializedProperty onQueueCompleted;
        private SerializedProperty onMovementFailed;

        private ReorderableList waitingPointList;
        private ReorderableList memberList;

        private void OnEnable()
        {
            entryPoint = serializedObject.FindProperty("entryPoint");
            servicePoint = serializedObject.FindProperty("servicePoint");
            exitPoint = serializedObject.FindProperty("exitPoint");
            waitingPoints = serializedObject.FindProperty("waitingPoints");
            initialMembers = serializedObject.FindProperty("initialMembers");
            maximumActiveMembers = serializedObject.FindProperty("maximumActiveMembers");
            beginOnStart = serializedObject.FindProperty("beginOnStart");
            manageMemberVisibility = serializedObject.FindProperty("manageMemberVisibility");
            arrivalDistance = serializedObject.FindProperty("arrivalDistance");
            fallbackMovementSpeed = serializedObject.FindProperty("fallbackMovementSpeed");
            useDirectMovementFallback = serializedObject.FindProperty("useDirectMovementFallback");
            fallbackTurnSpeed = serializedObject.FindProperty("fallbackTurnSpeed");
            onMemberReadyForService = serializedObject.FindProperty("onMemberReadyForService");
            onServiceStarted = serializedObject.FindProperty("onServiceStarted");
            onMemberDeparted = serializedObject.FindProperty("onMemberDeparted");
            onQueueCompleted = serializedObject.FindProperty("onQueueCompleted");
            onMovementFailed = serializedObject.FindProperty("onMovementFailed");

            waitingPointList = CreateReferenceList(
                waitingPoints,
                "Waiting Points (nearest first)",
                "Waiting");
            memberList = CreateReferenceList(
                initialMembers,
                "Initial Members (service order)",
                "Member");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            DrawHeader("Queue Layout");
            EditorGUILayout.PropertyField(entryPoint);
            EditorGUILayout.PropertyField(servicePoint);
            EditorGUILayout.PropertyField(exitPoint);
            waitingPointList.DoLayoutList();
            DrawLayoutFeedback();

            DrawHeader("Members");
            memberList.DoLayoutList();
            EditorGUILayout.PropertyField(maximumActiveMembers);
            EditorGUILayout.PropertyField(beginOnStart);
            EditorGUILayout.PropertyField(manageMemberVisibility);
            DrawMemberFeedback();

            DrawHeader("Movement");
            EditorGUILayout.PropertyField(arrivalDistance);
            EditorGUILayout.PropertyField(useDirectMovementFallback);
            if (useDirectMovementFallback.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(fallbackMovementSpeed);
                EditorGUILayout.PropertyField(fallbackTurnSpeed);
                EditorGUI.indentLevel--;
            }

            DrawHeader("Events");
            EditorGUILayout.PropertyField(onMemberReadyForService);
            EditorGUILayout.PropertyField(onServiceStarted);
            EditorGUILayout.PropertyField(onMemberDeparted);
            EditorGUILayout.PropertyField(onQueueCompleted);
            EditorGUILayout.PropertyField(onMovementFailed);

            // Preserve visibility of future serialized fields without duplicating this layout.
            DrawPropertiesExcluding(serializedObject, DrawnProperties);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLayoutFeedback()
        {
            var missingAnchors = new List<string>();
            if (entryPoint.objectReferenceValue == null)
            {
                missingAnchors.Add("Entry Point");
            }
            if (servicePoint.objectReferenceValue == null)
            {
                missingAnchors.Add("Service Point");
            }
            if (exitPoint.objectReferenceValue == null)
            {
                missingAnchors.Add("Exit Point");
            }

            if (missingAnchors.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Unassigned queue anchors: {string.Join(", ", missingAnchors)}. " +
                    "The runtime permits this, but the corresponding movement step has no destination.",
                    MessageType.Warning);
            }

            int nullWaitingPoints = CountNullReferences(waitingPoints);
            if (nullWaitingPoints > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Waiting Points contains {nullWaitingPoints} null entr" +
                    (nullWaitingPoints == 1 ? "y." : "ies. Null entries are skipped."),
                    MessageType.Warning);
            }

            if (HasDuplicateReferences(waitingPoints))
            {
                EditorGUILayout.HelpBox(
                    "Waiting Points contains duplicate transforms; multiple queue slots will overlap.",
                    MessageType.Warning);
            }

        }

        private void DrawMemberFeedback()
        {
            int validWaitingPoints = CountNonNullReferences(waitingPoints);
            int configuredMaximum = Mathf.Max(1, maximumActiveMembers.intValue);
            int effectiveCapacity = Mathf.Max(
                1,
                Mathf.Min(configuredMaximum, 1 + validWaitingPoints));
            EditorGUILayout.LabelField(
                "Effective Active Capacity",
                effectiveCapacity.ToString());
            if (effectiveCapacity < configuredMaximum)
            {
                EditorGUILayout.HelpBox(
                    $"Maximum Active Members is limited to {effectiveCapacity} by the " +
                    "service position plus non-null waiting points.",
                    MessageType.Info);
            }

            int nullMembers = CountNullReferences(initialMembers);
            if (nullMembers > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Initial Members contains {nullMembers} null entr" +
                    (nullMembers == 1 ? "y." : "ies. Null entries are skipped."),
                    MessageType.Warning);
            }

            if (HasDuplicateReferences(initialMembers))
            {
                EditorGUILayout.HelpBox(
                    "Initial Members contains duplicates. Only the first occurrence is queued.",
                    MessageType.Warning);
            }

            if (beginOnStart.boolValue && CountNonNullReferences(initialMembers) == 0)
            {
                EditorGUILayout.HelpBox(
                    "Begin On Start is enabled, but no initial members are assigned.",
                    MessageType.Info);
            }
        }

        private ReorderableList CreateReferenceList(
            SerializedProperty property,
            string header,
            string elementLabel)
        {
            var list = new ReorderableList(
                serializedObject,
                property,
                true,
                true,
                true,
                true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, header),
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
            };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(
                    rect,
                    property.GetArrayElementAtIndex(index),
                    new GUIContent($"{elementLabel} {index}"));
            };
            return list;
        }

        private static int CountNonNullReferences(SerializedProperty array)
        {
            int count = 0;
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue != null)
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountNullReferences(SerializedProperty array)
        {
            return array.arraySize - CountNonNullReferences(array);
        }

        private static bool HasDuplicateReferences(SerializedProperty array)
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < array.arraySize; i++)
            {
                Object value = array.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null && !seen.Add(value.GetInstanceID()))
                {
                    return true;
                }
            }
            return false;
        }

        private static void DrawHeader(string label)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }
    }
}
