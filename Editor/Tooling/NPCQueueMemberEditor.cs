using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>
    /// Queue-member inspector that keeps phase configuration compact and validates its
    /// scene-local adapters, mode names, and animation triggers.
    /// </summary>
    [CustomEditor(typeof(NPCQueueMember))]
    public sealed class NPCQueueMemberEditor : UnityEditor.Editor
    {
        private static readonly string[] DrawnProperties =
        {
            "m_Script",
            "controller",
            "motor",
            "doorOpener",
            "modeController",
            "animationTrigger",
            "preServiceWaypoints",
            "enteringMode",
            "waitingMode",
            "readyMode",
            "serviceMode",
            "leavingMode",
            "readyAnimationTrigger",
            "serviceAnimationTrigger",
            "leavingAnimationTrigger",
            "onEntering",
            "onWaiting",
            "onReadyForService",
            "onServiceStarted",
            "onLeaving",
            "onCompleted",
        };

        private SerializedProperty controller;
        private SerializedProperty motor;
        private SerializedProperty doorOpener;
        private SerializedProperty modeController;
        private SerializedProperty animationTrigger;
        private SerializedProperty preServiceWaypoints;
        private SerializedProperty enteringMode;
        private SerializedProperty waitingMode;
        private SerializedProperty readyMode;
        private SerializedProperty serviceMode;
        private SerializedProperty leavingMode;
        private SerializedProperty readyAnimationTrigger;
        private SerializedProperty serviceAnimationTrigger;
        private SerializedProperty leavingAnimationTrigger;
        private SerializedProperty onEntering;
        private SerializedProperty onWaiting;
        private SerializedProperty onReadyForService;
        private SerializedProperty onServiceStarted;
        private SerializedProperty onLeaving;
        private SerializedProperty onCompleted;

        private bool showPhaseEvents;

        private void OnEnable()
        {
            controller = serializedObject.FindProperty("controller");
            motor = serializedObject.FindProperty("motor");
            doorOpener = serializedObject.FindProperty("doorOpener");
            modeController = serializedObject.FindProperty("modeController");
            animationTrigger = serializedObject.FindProperty("animationTrigger");
            preServiceWaypoints = serializedObject.FindProperty("preServiceWaypoints");
            enteringMode = serializedObject.FindProperty("enteringMode");
            waitingMode = serializedObject.FindProperty("waitingMode");
            readyMode = serializedObject.FindProperty("readyMode");
            serviceMode = serializedObject.FindProperty("serviceMode");
            leavingMode = serializedObject.FindProperty("leavingMode");
            readyAnimationTrigger = serializedObject.FindProperty("readyAnimationTrigger");
            serviceAnimationTrigger = serializedObject.FindProperty("serviceAnimationTrigger");
            leavingAnimationTrigger = serializedObject.FindProperty("leavingAnimationTrigger");
            onEntering = serializedObject.FindProperty("onEntering");
            onWaiting = serializedObject.FindProperty("onWaiting");
            onReadyForService = serializedObject.FindProperty("onReadyForService");
            onServiceStarted = serializedObject.FindProperty("onServiceStarted");
            onLeaving = serializedObject.FindProperty("onLeaving");
            onCompleted = serializedObject.FindProperty("onCompleted");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NPCQueueMember member = (NPCQueueMember)target;

            NPCInspectorUtility.DrawScript(serializedObject);

            NPCInspectorUtility.DrawHeader("NPC References");
            EditorGUILayout.PropertyField(controller);
            EditorGUILayout.PropertyField(motor);
            EditorGUILayout.PropertyField(doorOpener);
            EditorGUILayout.PropertyField(modeController);
            EditorGUILayout.PropertyField(animationTrigger);
            DrawReferenceWarnings(member);

            NPCInspectorUtility.DrawHeader("Pre-Service Route");
            EditorGUILayout.PropertyField(preServiceWaypoints, true);
            DrawWaypointWarnings();

            NPCInspectorUtility.DrawHeader("Behavior Modes");
            string[] modeNames = GetModeNames(
                modeController.objectReferenceValue as NPCModeController);
            DrawModeFields(modeNames);

            NPCInspectorUtility.DrawHeader("Animation Triggers");
            EditorGUILayout.PropertyField(readyAnimationTrigger);
            EditorGUILayout.PropertyField(serviceAnimationTrigger);
            EditorGUILayout.PropertyField(leavingAnimationTrigger);
            NPCInspectorUtility.DrawAnimatorTriggerValidation(
                animationTrigger.objectReferenceValue as NPCAnimationTrigger,
                new[]
                {
                    readyAnimationTrigger.stringValue,
                    serviceAnimationTrigger.stringValue,
                    leavingAnimationTrigger.stringValue,
                },
                "This queue member");

            NPCInspectorUtility.DrawHeader("Phase Events");
            showPhaseEvents = EditorGUILayout.Foldout(
                showPhaseEvents,
                "Show Phase Events",
                true);
            if (showPhaseEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(onEntering);
                EditorGUILayout.PropertyField(onWaiting);
                EditorGUILayout.PropertyField(onReadyForService);
                EditorGUILayout.PropertyField(onServiceStarted);
                EditorGUILayout.PropertyField(onLeaving);
                EditorGUILayout.PropertyField(onCompleted);
                EditorGUI.indentLevel--;
            }

            if (Application.isPlaying)
            {
                NPCInspectorUtility.DrawHeader("Runtime");
                EditorGUILayout.LabelField("Queue State", member.State.ToString());
            }

            DrawPropertiesExcluding(serializedObject, DrawnProperties);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferenceWarnings(NPCQueueMember member)
        {
            var missingRequired = new List<string>();
            if (controller.objectReferenceValue == null)
            {
                missingRequired.Add("Controller");
            }
            if (motor.objectReferenceValue == null)
            {
                missingRequired.Add("Motor");
            }
            if (missingRequired.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Unassigned required references: {string.Join(", ", missingRequired)}. " +
                    "Runtime falls back to local components, but explicit assignments make setup auditable.",
                    MessageType.Warning);
            }

            NPCInspectorUtility.DrawSameObjectWarnings(
                member,
                ("Controller", controller),
                ("Motor", motor),
                ("Door Opener", doorOpener),
                ("Mode Controller", modeController),
                ("Animation Trigger", animationTrigger));
        }

        private void DrawWaypointWarnings()
        {
            int nullCount = 0;
            var seen = new HashSet<int>();
            bool duplicateFound = false;
            for (int i = 0; i < preServiceWaypoints.arraySize; i++)
            {
                UnityEngine.Object value =
                    preServiceWaypoints.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value == null)
                {
                    nullCount++;
                }
                else
                {
                    duplicateFound |= !seen.Add(value.GetInstanceID());
                }
            }

            if (nullCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Pre-Service Waypoints contains {nullCount} null entr" +
                    (nullCount == 1 ? "y." : "ies. Null entries are skipped."),
                    MessageType.Warning);
            }
            if (duplicateFound)
            {
                EditorGUILayout.HelpBox(
                    "Pre-Service Waypoints contains duplicate destinations.",
                    MessageType.Info);
            }
        }

        private void DrawModeFields(string[] modeNames)
        {
            SerializedProperty[] fields =
            {
                enteringMode,
                waitingMode,
                readyMode,
                serviceMode,
                leavingMode,
            };

            if (modeNames.Length == 0)
            {
                foreach (SerializedProperty field in fields)
                {
                    EditorGUILayout.PropertyField(field);
                }

                if (modeController.objectReferenceValue != null)
                {
                    EditorGUILayout.HelpBox(
                        "The assigned Mode Controller has no named modes. Values remain editable as text.",
                        MessageType.Info);
                }
                else if (HasConfiguredValue(fields))
                {
                    EditorGUILayout.HelpBox(
                        "Mode names are configured, but no Mode Controller is assigned.",
                        MessageType.Warning);
                }
                return;
            }

            foreach (SerializedProperty field in fields)
            {
                DrawModePopup(field, modeNames);
            }
        }

        private static string[] GetModeNames(NPCModeController source)
        {
            if (source == null)
            {
                return Array.Empty<string>();
            }

            var serializedController = new SerializedObject(source);
            SerializedProperty modes = serializedController.FindProperty("modes");
            if (modes == null || !modes.isArray)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < modes.arraySize; i++)
            {
                SerializedProperty mode = modes.GetArrayElementAtIndex(i);
                string name = mode.FindPropertyRelative("modeName")?.stringValue;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string normalized = name.Trim();
                    if (seen.Add(normalized))
                    {
                        names.Add(normalized);
                    }
                }
            }
            return names.ToArray();
        }

        private static void DrawModePopup(
            SerializedProperty property,
            IReadOnlyList<string> modeNames)
        {
            string currentValue = property.stringValue ?? string.Empty;
            string normalizedCurrent = currentValue.Trim();
            var values = new List<string> { string.Empty };
            var labels = new List<string> { "<None>" };
            int currentIndex = string.IsNullOrEmpty(normalizedCurrent) ? 0 : -1;

            for (int i = 0; i < modeNames.Count; i++)
            {
                string modeName = modeNames[i];
                values.Add(modeName);
                labels.Add(modeName);
                if (string.Equals(
                        normalizedCurrent,
                        modeName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i + 1;
                }
            }

            if (currentIndex < 0)
            {
                currentIndex = values.Count;
                values.Add(currentValue);
                labels.Add($"{currentValue} (Unavailable)");
            }

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(
                new GUIContent(property.displayName, property.tooltip),
                currentIndex,
                labels.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = values[selected];
            }
        }

        private static bool HasConfiguredValue(IEnumerable<SerializedProperty> fields)
        {
            foreach (SerializedProperty field in fields)
            {
                if (!string.IsNullOrWhiteSpace(field.stringValue))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
