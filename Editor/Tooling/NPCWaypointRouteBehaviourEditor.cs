using System.Collections.Generic;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>
    /// Route-runner inspector with conditional traversal fields and route/adapter validation.
    /// </summary>
    [CustomEditor(typeof(NPCWaypointRouteBehaviour))]
    public sealed class NPCWaypointRouteBehaviourEditor : UnityEditor.Editor
    {
        private static readonly string[] DrawnProperties =
        {
            "m_Script",
            "activeOnStart",
            "route",
            "traversalMode",
            "startingWaypointIndex",
            "randomizeStartingWaypoint",
            "motor",
            "doorOpener",
            "destinationSampleRadius",
            "destinationRetryDelay",
            "animationTriggerPlayer",
            "onRouteStarted",
            "onWaypointReached",
            "onRouteCompleted",
        };

        private SerializedProperty activeOnStart;
        private SerializedProperty route;
        private SerializedProperty traversalMode;
        private SerializedProperty startingWaypointIndex;
        private SerializedProperty randomizeStartingWaypoint;
        private SerializedProperty motor;
        private SerializedProperty doorOpener;
        private SerializedProperty destinationSampleRadius;
        private SerializedProperty destinationRetryDelay;
        private SerializedProperty animationTriggerPlayer;
        private SerializedProperty onRouteStarted;
        private SerializedProperty onWaypointReached;
        private SerializedProperty onRouteCompleted;

        private bool showEvents;

        private void OnEnable()
        {
            activeOnStart = serializedObject.FindProperty("activeOnStart");
            route = serializedObject.FindProperty("route");
            traversalMode = serializedObject.FindProperty("traversalMode");
            startingWaypointIndex = serializedObject.FindProperty("startingWaypointIndex");
            randomizeStartingWaypoint = serializedObject.FindProperty("randomizeStartingWaypoint");
            motor = serializedObject.FindProperty("motor");
            doorOpener = serializedObject.FindProperty("doorOpener");
            destinationSampleRadius = serializedObject.FindProperty("destinationSampleRadius");
            destinationRetryDelay = serializedObject.FindProperty("destinationRetryDelay");
            animationTriggerPlayer = serializedObject.FindProperty("animationTriggerPlayer");
            onRouteStarted = serializedObject.FindProperty("onRouteStarted");
            onWaypointReached = serializedObject.FindProperty("onWaypointReached");
            onRouteCompleted = serializedObject.FindProperty("onRouteCompleted");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NPCWaypointRouteBehaviour behaviour =
                (NPCWaypointRouteBehaviour)target;

            NPCInspectorUtility.DrawScript(serializedObject);

            NPCInspectorUtility.DrawHeader("Behavior");
            EditorGUILayout.PropertyField(activeOnStart);

            NPCInspectorUtility.DrawHeader("Route");
            EditorGUILayout.PropertyField(route);
            EditorGUILayout.PropertyField(traversalMode);
            EditorGUILayout.PropertyField(randomizeStartingWaypoint);
            if (!randomizeStartingWaypoint.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(startingWaypointIndex);
                EditorGUI.indentLevel--;
            }
            DrawRouteWarnings();

            NPCInspectorUtility.DrawHeader("Movement");
            EditorGUILayout.PropertyField(motor);
            EditorGUILayout.PropertyField(doorOpener);
            EditorGUILayout.PropertyField(destinationSampleRadius);
            EditorGUILayout.PropertyField(destinationRetryDelay);

            NPCInspectorUtility.DrawHeader("Animation");
            EditorGUILayout.PropertyField(animationTriggerPlayer);
            NPCWaypointRoute selectedRoute =
                route.objectReferenceValue as NPCWaypointRoute;
            NPCInspectorUtility.DrawAnimatorTriggerValidation(
                animationTriggerPlayer.objectReferenceValue as NPCAnimationTrigger,
                GetWaypointTriggerNames(selectedRoute),
                "The assigned route");

            NPCInspectorUtility.DrawSameObjectWarnings(
                behaviour,
                ("Motor", motor),
                ("Door Opener", doorOpener),
                ("Animation Trigger Player", animationTriggerPlayer));
            if (motor.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Motor is unassigned. Runtime falls back to the local required NPC NavMesh Motor.",
                    MessageType.Warning);
            }

            NPCInspectorUtility.DrawHeader("Events");
            showEvents = EditorGUILayout.Foldout(showEvents, "Show Route Events", true);
            if (showEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(onRouteStarted);
                EditorGUILayout.PropertyField(onWaypointReached);
                if (IsOnceTraversal() || HasPersistentListeners(onRouteCompleted))
                {
                    EditorGUILayout.PropertyField(onRouteCompleted);
                }
                EditorGUI.indentLevel--;
            }
            if (!IsOnceTraversal() && HasPersistentListeners(onRouteCompleted))
            {
                EditorGUILayout.HelpBox(
                    "On Route Completed has listeners, but it is invoked only by Once traversal.",
                    MessageType.Warning);
            }

            if (Application.isPlaying)
            {
                NPCInspectorUtility.DrawHeader("Runtime");
                EditorGUILayout.LabelField(
                    "Waypoint Index",
                    behaviour.CurrentWaypointIndex.ToString());
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        "Current Waypoint",
                        behaviour.CurrentWaypoint,
                        typeof(NPCWaypoint),
                        true);
                    EditorGUILayout.Toggle(
                        "Waiting At Waypoint",
                        behaviour.IsWaitingAtWaypoint);
                    EditorGUILayout.Toggle(
                        "Route Complete",
                        behaviour.IsRouteComplete);
                    EditorGUILayout.Toggle(
                        "Behavior Active",
                        behaviour.IsBehaviourActive);
                }
            }

            DrawPropertiesExcluding(serializedObject, DrawnProperties);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRouteWarnings()
        {
            NPCWaypointRoute selectedRoute =
                route.objectReferenceValue as NPCWaypointRoute;
            if (selectedRoute == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a scene-owned waypoint route. The behavior does no work without one.",
                    MessageType.Warning);
                return;
            }

            int validCount = 0;
            foreach (NPCWaypoint waypoint in selectedRoute.Waypoints)
            {
                if (waypoint != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "The assigned route has no valid waypoints, so traversal cannot start.",
                    MessageType.Error);
                return;
            }

            if (!randomizeStartingWaypoint.boolValue &&
                startingWaypointIndex.intValue >= selectedRoute.Count)
            {
                EditorGUILayout.HelpBox(
                    "Starting Waypoint Index is outside the authored route. Runtime resolves " +
                    "it to the nearest valid waypoint.",
                    MessageType.Info);
            }
            else if (!randomizeStartingWaypoint.boolValue &&
                     selectedRoute.GetWaypoint(startingWaypointIndex.intValue) == null)
            {
                EditorGUILayout.HelpBox(
                    "Starting Waypoint Index points to a null entry. Runtime resolves it to " +
                    "the nearest valid waypoint.",
                    MessageType.Info);
            }
        }

        private bool IsOnceTraversal()
        {
            return (NPCWaypointTraversalMode)traversalMode.enumValueIndex ==
                   NPCWaypointTraversalMode.Once;
        }

        private static IEnumerable<string> GetWaypointTriggerNames(
            NPCWaypointRoute selectedRoute)
        {
            if (selectedRoute == null)
            {
                yield break;
            }

            foreach (NPCWaypoint waypoint in selectedRoute.Waypoints)
            {
                if (waypoint != null)
                {
                    yield return waypoint.AnimatorTrigger;
                }
            }
        }

        private static bool HasPersistentListeners(SerializedProperty unityEvent)
        {
            SerializedProperty calls = unityEvent
                .FindPropertyRelative("m_PersistentCalls")
                ?.FindPropertyRelative("m_Calls");
            return calls != null && calls.isArray && calls.arraySize > 0;
        }
    }
}
