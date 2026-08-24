using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>Inspector diagnostics for the interaction-backed NPC path-door adapter.</summary>
    [CustomEditor(typeof(NPCPathDoor))]
    public sealed class NPCPathDoorEditor : UnityEditor.Editor
    {
        private static readonly string[] DrawnProperties =
        {
            "m_Script",
            "interactionRules",
            "animatedState",
            "allowNPCOpening",
            "clearanceDelay",
        };

        private SerializedProperty interactionRules;
        private SerializedProperty animatedState;
        private SerializedProperty allowNPCOpening;
        private SerializedProperty clearanceDelay;

        private void OnEnable()
        {
            interactionRules = serializedObject.FindProperty("interactionRules");
            animatedState = serializedObject.FindProperty("animatedState");
            allowNPCOpening = serializedObject.FindProperty("allowNPCOpening");
            clearanceDelay = serializedObject.FindProperty("clearanceDelay");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NPCPathDoor door = (NPCPathDoor)target;

            NPCInspectorUtility.DrawScript(serializedObject);
            NPCInspectorUtility.DrawHeader("Door Interaction");
            EditorGUILayout.PropertyField(interactionRules);
            EditorGUILayout.PropertyField(animatedState);
            EditorGUILayout.PropertyField(allowNPCOpening);

            NPCInspectorUtility.DrawSameObjectWarnings(
                door,
                ("Interaction Rules", interactionRules),
                ("Animated State", animatedState));
            DrawDoorWarnings(door);

            NPCInspectorUtility.DrawHeader("Passage Timing");
            EditorGUILayout.PropertyField(clearanceDelay);

            if (Application.isPlaying)
            {
                NPCInspectorUtility.DrawHeader("Runtime");
                EditorGUILayout.LabelField("Current State", door.CurrentState.ToString());
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Toggle("Passable", door.IsPassable);
                }
            }

            DrawPropertiesExcluding(serializedObject, DrawnProperties);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDoorWarnings(NPCPathDoor door)
        {
            InteractableUnlock state =
                animatedState.objectReferenceValue as InteractableUnlock;
            if (state == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign the local Interactable Unlock that owns the door animation state.",
                    MessageType.Error);
            }
            else if (!state.IsBinary)
            {
                EditorGUILayout.HelpBox(
                    "The assigned Interactable Unlock must use Binary States so NPC passage " +
                    "can distinguish open from closed.",
                    MessageType.Error);
            }

            if (door.GetComponentInChildren<Collider>(true) == null)
            {
                EditorGUILayout.HelpBox(
                    "No collider exists on this door or its children. NPC door probes cannot detect it.",
                    MessageType.Warning);
            }
        }
    }

    /// <summary>Conditional probe inspector with layer and runtime detection feedback.</summary>
    [CustomEditor(typeof(NPCDoorOpener))]
    public sealed class NPCDoorOpenerEditor : UnityEditor.Editor
    {
        private static readonly string[] DrawnProperties =
        {
            "m_Script",
            "motor",
            "probeOrigin",
            "verticalOffset",
            "probeRadius",
            "probeDistance",
            "doorLayers",
        };

        private SerializedProperty motor;
        private SerializedProperty probeOrigin;
        private SerializedProperty verticalOffset;
        private SerializedProperty probeRadius;
        private SerializedProperty probeDistance;
        private SerializedProperty doorLayers;

        private void OnEnable()
        {
            motor = serializedObject.FindProperty("motor");
            probeOrigin = serializedObject.FindProperty("probeOrigin");
            verticalOffset = serializedObject.FindProperty("verticalOffset");
            probeRadius = serializedObject.FindProperty("probeRadius");
            probeDistance = serializedObject.FindProperty("probeDistance");
            doorLayers = serializedObject.FindProperty("doorLayers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NPCDoorOpener opener = (NPCDoorOpener)target;

            NPCInspectorUtility.DrawScript(serializedObject);
            NPCInspectorUtility.DrawHeader("Movement");
            EditorGUILayout.PropertyField(motor);
            NPCInspectorUtility.DrawSameObjectWarnings(
                opener,
                ("Motor", motor));

            NPCInspectorUtility.DrawHeader("Probe");
            EditorGUILayout.PropertyField(probeOrigin);
            if (probeOrigin.objectReferenceValue == null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(verticalOffset);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(probeRadius);
            EditorGUILayout.PropertyField(probeDistance);
            EditorGUILayout.PropertyField(doorLayers);
            if (doorLayers.intValue == 0)
            {
                EditorGUILayout.HelpBox(
                    "Door Layers is empty, so the probe cannot detect any path-door collider.",
                    MessageType.Error);
            }

            if (Application.isPlaying)
            {
                NPCInspectorUtility.DrawHeader("Runtime");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        "Detected Door",
                        opener.DetectedDoor,
                        typeof(NPCPathDoor),
                        true);
                }
                if (opener.DetectedDoor != null)
                {
                    EditorGUILayout.LabelField(
                        "Detected State",
                        opener.DetectedDoor.CurrentState.ToString());
                }
            }

            DrawPropertiesExcluding(serializedObject, DrawnProperties);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
