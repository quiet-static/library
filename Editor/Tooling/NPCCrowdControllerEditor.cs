using System.Collections.Generic;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>
    /// Ordered crowd inspector with skipped/duplicate member diagnostics and cumulative
    /// startup timing previews.
    /// </summary>
    [CustomEditor(typeof(NPCCrowdController))]
    public sealed class NPCCrowdControllerEditor : UnityEditor.Editor
    {
        private static readonly string[] DrawnProperties =
        {
            "m_Script",
            "members",
            "startOnStart",
            "restartRoutesWhenStarted",
            "initialDelay",
            "staggerInterval",
            "maximumStaggerJitter",
            "stopMembersOnDisable",
            "onCrowdStarted",
            "onAllMembersStarted",
            "onCrowdStopped",
        };

        private const float ElementSpacing = 2f;

        private SerializedProperty members;
        private SerializedProperty startOnStart;
        private SerializedProperty restartRoutesWhenStarted;
        private SerializedProperty initialDelay;
        private SerializedProperty staggerInterval;
        private SerializedProperty maximumStaggerJitter;
        private SerializedProperty stopMembersOnDisable;
        private SerializedProperty onCrowdStarted;
        private SerializedProperty onAllMembersStarted;
        private SerializedProperty onCrowdStopped;

        private ReorderableList memberList;
        private bool showEvents;

        private void OnEnable()
        {
            members = serializedObject.FindProperty("members");
            startOnStart = serializedObject.FindProperty("startOnStart");
            restartRoutesWhenStarted = serializedObject.FindProperty("restartRoutesWhenStarted");
            initialDelay = serializedObject.FindProperty("initialDelay");
            staggerInterval = serializedObject.FindProperty("staggerInterval");
            maximumStaggerJitter = serializedObject.FindProperty("maximumStaggerJitter");
            stopMembersOnDisable = serializedObject.FindProperty("stopMembersOnDisable");
            onCrowdStarted = serializedObject.FindProperty("onCrowdStarted");
            onAllMembersStarted = serializedObject.FindProperty("onAllMembersStarted");
            onCrowdStopped = serializedObject.FindProperty("onCrowdStopped");

            memberList = new ReorderableList(
                serializedObject,
                members,
                true,
                true,
                true,
                true)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, "Crowd Members (start order)"),
                drawElementCallback = DrawMember,
                elementHeight =
                    EditorGUIUtility.singleLineHeight * 3f + ElementSpacing * 4f,
                onAddCallback = AddMember,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NPCCrowdController crowd = (NPCCrowdController)target;

            NPCInspectorUtility.DrawScript(serializedObject);

            NPCInspectorUtility.DrawHeader("Startup");
            EditorGUILayout.PropertyField(startOnStart);
            EditorGUILayout.PropertyField(restartRoutesWhenStarted);
            EditorGUILayout.PropertyField(initialDelay);
            EditorGUILayout.PropertyField(staggerInterval);
            EditorGUILayout.PropertyField(maximumStaggerJitter);

            NPCInspectorUtility.DrawHeader("Members");
            memberList.DoLayoutList();
            DrawMemberWarnings();

            NPCInspectorUtility.DrawHeader("Lifecycle");
            EditorGUILayout.PropertyField(stopMembersOnDisable);

            NPCInspectorUtility.DrawHeader("Events");
            showEvents = EditorGUILayout.Foldout(showEvents, "Show Crowd Events", true);
            if (showEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(onCrowdStarted);
                EditorGUILayout.PropertyField(onAllMembersStarted);
                EditorGUILayout.PropertyField(onCrowdStopped);
                EditorGUI.indentLevel--;
            }

            if (Application.isPlaying)
            {
                NPCInspectorUtility.DrawHeader("Runtime");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Toggle("Running", crowd.IsRunning);
                }
            }

            DrawPropertiesExcluding(serializedObject, DrawnProperties);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMember(
            Rect rect,
            int index,
            bool isActive,
            bool isFocused)
        {
            SerializedProperty member = members.GetArrayElementAtIndex(index);
            SerializedProperty routeBehaviour =
                member.FindPropertyRelative("routeBehaviour");
            SerializedProperty additionalDelay =
                member.FindPropertyRelative("additionalStartDelay");

            rect.y += ElementSpacing;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(
                rect,
                routeBehaviour,
                new GUIContent($"Member {index}"));

            rect.y += EditorGUIUtility.singleLineHeight + ElementSpacing;
            EditorGUI.PropertyField(rect, additionalDelay);

            rect.y += EditorGUIUtility.singleLineHeight + ElementSpacing;
            EditorGUI.LabelField(
                rect,
                GetStartTimingLabel(index),
                EditorStyles.miniLabel);
        }

        private string GetStartTimingLabel(int targetIndex)
        {
            float cumulativeMinimum = 0f;
            float cumulativeMaximum = 0f;
            int validIndex = 0;

            for (int i = 0; i <= targetIndex; i++)
            {
                SerializedProperty member = members.GetArrayElementAtIndex(i);
                SerializedProperty route = member.FindPropertyRelative("routeBehaviour");
                if (route.objectReferenceValue == null)
                {
                    if (i == targetIndex)
                    {
                        return "Skipped — assign a route behavior";
                    }
                    continue;
                }

                float baseDelay = validIndex == 0
                    ? Mathf.Max(0f, initialDelay.floatValue)
                    : Mathf.Max(0f, staggerInterval.floatValue);
                float additional = Mathf.Max(
                    0f,
                    member.FindPropertyRelative("additionalStartDelay").floatValue);
                cumulativeMinimum += baseDelay + additional;
                cumulativeMaximum +=
                    baseDelay + additional +
                    Mathf.Max(0f, maximumStaggerJitter.floatValue);
                validIndex++;
            }

            return Mathf.Approximately(cumulativeMinimum, cumulativeMaximum)
                ? $"Cumulative start: {cumulativeMinimum:0.##} s"
                : $"Cumulative start: {cumulativeMinimum:0.##}–{cumulativeMaximum:0.##} s";
        }

        private void DrawMemberWarnings()
        {
            int missingRouteCount = 0;
            int emptyRouteCount = 0;
            bool duplicateFound = false;
            var seen = new HashSet<int>();

            for (int i = 0; i < members.arraySize; i++)
            {
                SerializedProperty member = members.GetArrayElementAtIndex(i);
                NPCWaypointRouteBehaviour routeBehaviour = member
                    .FindPropertyRelative("routeBehaviour")
                    .objectReferenceValue as NPCWaypointRouteBehaviour;
                if (routeBehaviour == null)
                {
                    missingRouteCount++;
                    continue;
                }

                duplicateFound |= !seen.Add(routeBehaviour.GetInstanceID());
                if (routeBehaviour.Route == null ||
                    CountValidWaypoints(routeBehaviour.Route) == 0)
                {
                    emptyRouteCount++;
                }
            }

            if (missingRouteCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{missingRouteCount} crowd member" +
                    (missingRouteCount == 1 ? " is" : "s are") +
                    " missing a route behavior and will be skipped.",
                    MessageType.Warning);
            }
            if (duplicateFound)
            {
                EditorGUILayout.HelpBox(
                    "The same route behavior appears more than once and will receive repeated start calls.",
                    MessageType.Warning);
            }
            if (emptyRouteCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{emptyRouteCount} assigned route behavior" +
                    (emptyRouteCount == 1 ? " has" : "s have") +
                    " no valid waypoint route.",
                    MessageType.Warning);
            }
        }

        private void AddMember(ReorderableList list)
        {
            int index = members.arraySize;
            members.arraySize++;
            SerializedProperty member = members.GetArrayElementAtIndex(index);
            member.FindPropertyRelative("routeBehaviour").objectReferenceValue = null;
            member.FindPropertyRelative("additionalStartDelay").floatValue = 0f;
            list.index = index;
        }

        private static int CountValidWaypoints(NPCWaypointRoute route)
        {
            int count = 0;
            foreach (NPCWaypoint waypoint in route.Waypoints)
            {
                if (waypoint != null)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
