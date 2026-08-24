using System.Collections.Generic;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>
    /// Inspector for explicit route ordering with an idempotent, Undo-aware hierarchy refresh.
    /// </summary>
    [CustomEditor(typeof(NPCWaypointRoute))]
    public sealed class NPCWaypointRouteEditor : UnityEditor.Editor
    {
        private SerializedProperty waypoints;
        private ReorderableList waypointList;

        private void OnEnable()
        {
            waypoints = serializedObject.FindProperty("waypoints");
            waypointList = new ReorderableList(
                serializedObject,
                waypoints,
                true,
                true,
                true,
                true)
            {
                drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, "Waypoints (traversal order)"),
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                drawElementCallback = DrawWaypoint,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            waypointList.DoLayoutList();
            DrawValidation();

            NPCWaypointRoute route = (NPCWaypointRoute)target;
            bool matchesChildren = MatchesChildOrder(route);
            if (!matchesChildren)
            {
                EditorGUILayout.HelpBox(
                    "The authored route differs from child-waypoint hierarchy order. " +
                    "Refresh only when hierarchy order should replace the manual route order.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(matchesChildren))
            {
                if (GUILayout.Button("Refresh From Child Waypoints"))
                {
                    RefreshFromChildren(route);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWaypoint(
            Rect rect,
            int index,
            bool isActive,
            bool isFocused)
        {
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(
                rect,
                waypoints.GetArrayElementAtIndex(index),
                new GUIContent($"Waypoint {index}"));
        }

        private void DrawValidation()
        {
            int validCount = 0;
            int nullCount = 0;
            var seen = new HashSet<int>();
            bool duplicateFound = false;

            for (int i = 0; i < waypoints.arraySize; i++)
            {
                Object value = waypoints.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value == null)
                {
                    nullCount++;
                    continue;
                }

                validCount++;
                duplicateFound |= !seen.Add(value.GetInstanceID());
            }

            EditorGUILayout.LabelField(
                "Valid Waypoints",
                $"{validCount} / {waypoints.arraySize}");

            if (nullCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"The route contains {nullCount} null entr" +
                    (nullCount == 1 ? "y." : "ies. Null entries are skipped."),
                    MessageType.Warning);
            }

            if (duplicateFound)
            {
                EditorGUILayout.HelpBox(
                    "The route contains duplicate waypoints. This is valid for an " +
                    "intentional revisit, but verify the authored order.",
                    MessageType.Info);
            }
        }

        private void RefreshFromChildren(NPCWaypointRoute route)
        {
            if (route == null || MatchesChildOrder(route))
            {
                return;
            }

            const string undoName = "Refresh NPC Waypoints";
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(route, undoName);

            route.RefreshWaypointsFromChildren();
            if (PrefabUtility.IsPartOfPrefabInstance(route))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(route);
            }
            EditorUtility.SetDirty(route);

            Scene scene = route.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Undo.CollapseUndoOperations(undoGroup);
            serializedObject.Update();
        }

        private static bool MatchesChildOrder(NPCWaypointRoute route)
        {
            NPCWaypoint[] childWaypoints =
                route.GetComponentsInChildren<NPCWaypoint>(true);
            IReadOnlyList<NPCWaypoint> authoredWaypoints = route.Waypoints;
            if (authoredWaypoints.Count != childWaypoints.Length)
            {
                return false;
            }

            for (int i = 0; i < childWaypoints.Length; i++)
            {
                if (authoredWaypoints[i] != childWaypoints[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
