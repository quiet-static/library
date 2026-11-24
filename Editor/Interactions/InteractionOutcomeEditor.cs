using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    internal static class InteractionOutcomeEditor
    {
        private static void DrawConditionalMessages(
            ConditionalInteractionMessage conditional)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(
                "Conditional UI Messages",
                EditorStyles.boldLabel);

            using var data = new SerializedObject(conditional);
            data.Update();
            EditorGUILayout.PropertyField(data.FindProperty("channel"));
            EditorGUILayout.HelpBox("Rules run from top to bottom. The first matching, non-empty message is shown; otherwise the fallback is used.", MessageType.Info);
            SerializedProperty rules = data.FindProperty("rules");
            for (int index = 0; index < rules.arraySize; index++)
            {
                bool changedOrder = false;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SerializedProperty rule = rules.GetArrayElementAtIndex(index);
                    EditorGUILayout.LabelField($"Rule {index + 1}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("label"));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("requirement"), new GUIContent("Flag Requirements"), true);
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("message"), new GUIContent("Message"));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(index == 0))
                            if (GUILayout.Button("Move Up")) { rules.MoveArrayElement(index, index - 1); changedOrder = true; }
                        using (new EditorGUI.DisabledScope(index == rules.arraySize - 1))
                            if (GUILayout.Button("Move Down")) { rules.MoveArrayElement(index, index + 1); changedOrder = true; }
                        if (GUILayout.Button("Remove Rule")) { rules.DeleteArrayElementAtIndex(index); changedOrder = true; }
                    }
                }
                if (changedOrder) break;
            }
            if (GUILayout.Button("Add Conditional Rule")) AddConditionalRule(data);
            EditorGUILayout.PropertyField(data.FindProperty("defaultMessage"), new GUIContent("Fallback"));
            EditorGUILayout.PropertyField(data.FindProperty("useCustomDuration"));
            if (data.FindProperty("useCustomDuration").boolValue)
                EditorGUILayout.PropertyField(data.FindProperty("messageDuration"));
            data.ApplyModifiedProperties();
        }

        /// <summary>Adds an empty rule without inheriting the preceding rule's condition or text.</summary>
        private static void AddConditionalRule(SerializedObject data)
        {
            SerializedProperty rules = data.FindProperty("rules");
            int index = rules.arraySize++;
            SerializedProperty rule = rules.GetArrayElementAtIndex(index);
            rule.FindPropertyRelative("label").stringValue = string.Empty;
            rule.FindPropertyRelative("message").stringValue = string.Empty;
            SerializedProperty requirement = rule.FindPropertyRelative("requirement");
            requirement.FindPropertyRelative("mode").enumValueIndex = 0;
            requirement.FindPropertyRelative("flags").arraySize = 0;
            requirement.FindPropertyRelative("comparisons").arraySize = 0;
        }

        private static void AddConditionalMessage(Component target, string eventName, InteractionUIChannel channel)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Conditional Interaction Message");
            var conditional = Undo.AddComponent<ConditionalInteractionMessage>(target.gameObject);
            using var settings = new SerializedObject(conditional);
            settings.FindProperty("channel").objectReferenceValue = channel;
            AddConditionalRule(settings);
            settings.ApplyModifiedProperties();
            using var data = new SerializedObject(target);
            AddEventCallback(data, eventName, conditional, nameof(ConditionalInteractionMessage.ShowMessage), UnityEngine.Events.PersistentListenerMode.Void);
            data.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
        }

        /// <summary>Edits the existing requirement and channel-backed outcome callbacks.</summary>
        internal static void DrawInteractionDetails(SerializedObject data)
        {
            SerializedProperty requirement = data.FindProperty("requirement");
            if (requirement != null)
                EditorGUILayout.PropertyField(requirement, new GUIContent("Flag Requirements"), true);
            DrawEventMessages(data);
            InteractionTargetAuthoring targetType = InteractionTargetAuthoring.For(data.targetObject);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAddMessage(data, targetType.SuccessEvent, "Add Success Message");
                DrawAddMessage(data, targetType.FailureEvent, "Add Failure Message");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAddMessage(data, targetType.SuccessEvent, "Add Conditional Success Messages", true);
                DrawAddMessage(data, targetType.FailureEvent, "Add Conditional Failure Messages", true);
            }
            if (data.targetObject is Component target)
                foreach (var conditional in target.GetComponents<ConditionalInteractionMessage>())
                    DrawConditionalMessages(conditional);
        }

        private static void DrawAddMessage(SerializedObject data, string eventName, string label, bool conditional = false)
        {
            if (data.FindProperty(eventName) == null || !GUILayout.Button(label)) return;
            UnityEngine.Object target = data.targetObject;
            var menu = new GenericMenu();
            foreach (string guid in AssetDatabase.FindAssets("t:InteractionUIChannel"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var channel = AssetDatabase.LoadAssetAtPath<InteractionUIChannel>(path);
                if (channel == null) continue;
                menu.AddItem(new GUIContent(path), false, () =>
                {
                    if (target == null || channel == null) return;
                    if (conditional && target is Component component)
                    {
                        AddConditionalMessage(component, eventName, channel);
                        return;
                    }
                    using var serialized = new SerializedObject(target);
                    serialized.Update();
                    AddEventMessage(serialized, eventName, channel);
                    serialized.ApplyModifiedProperties();
                });
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("Create an Interaction UI Channel asset first"));
            menu.ShowAsContext();
        }

        /// <summary>Appends a static ShowMessage callback without changing existing listeners.</summary>
        private static void AddEventMessage(SerializedObject data, string eventName, InteractionUIChannel channel)
        {
            AddEventCallback(data, eventName, channel, nameof(InteractionUIChannel.ShowMessage), UnityEngine.Events.PersistentListenerMode.String);
        }

        private static void AddEventCallback(SerializedObject data, string eventName, UnityEngine.Object target, string method, UnityEngine.Events.PersistentListenerMode mode)
        {
            SerializedProperty calls = data.FindProperty(eventName).FindPropertyRelative("m_PersistentCalls.m_Calls");
            int index = calls.arraySize++;
            SerializedProperty call = calls.GetArrayElementAtIndex(index);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").intValue = (int)mode;
            call.FindPropertyRelative("m_CallState").intValue = (int)UnityEngine.Events.UnityEventCallState.RuntimeOnly;
            SerializedProperty arguments = call.FindPropertyRelative("m_Arguments");
            arguments.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
            arguments.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = typeof(UnityEngine.Object).AssemblyQualifiedName;
            arguments.FindPropertyRelative("m_IntArgument").intValue = 0;
            arguments.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
            arguments.FindPropertyRelative("m_BoolArgument").boolValue = false;
            arguments.FindPropertyRelative("m_StringArgument").stringValue = string.Empty;
        }

        /// <summary>Exposes only static string arguments on interaction UI calls, preserving all event wiring.</summary>
        private static void DrawEventMessages(SerializedObject data)
        {
            foreach (var message in EventMessages(data))
            {
                bool remove;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(message.Label, EditorStyles.miniBoldLabel);
                    remove = GUILayout.Button(new GUIContent("Remove", "Remove this message callback. Other interaction callbacks are preserved."), GUILayout.Width(65f));
                }
                if (remove)
                {
                    RemoveEventMessage(message.Text);
                    break;
                }
                message.Text.stringValue = EditorGUILayout.TextArea(message.Text.stringValue, GUILayout.MinHeight(42f));
            }
        }

        /// <summary>Removes only the persistent listener owning this message's string argument.</summary>
        private static void RemoveEventMessage(SerializedProperty text)
        {
            const string element = ".Array.data[";
            string path = text.propertyPath;
            int start = path.LastIndexOf(element, StringComparison.Ordinal);
            int end = path.IndexOf(']', start);
            int index = int.Parse(path.Substring(start + element.Length, end - start - element.Length));
            SerializedProperty calls = text.serializedObject.FindProperty(path.Substring(0, start));
            calls.DeleteArrayElementAtIndex(index);
        }

        internal static IEnumerable<(string Label, SerializedProperty Text)> EventMessages(SerializedObject data)
        {
            SerializedProperty property = data.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false)
            {
                if (property.propertyType != SerializedPropertyType.Generic) continue;
                SerializedProperty calls = property.FindPropertyRelative("m_PersistentCalls.m_Calls");
                if (calls == null || !calls.isArray) continue;
                for (int index = 0; index < calls.arraySize; index++)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(index);
                    if (call.FindPropertyRelative("m_Target").objectReferenceValue is not InteractionUIChannel ||
                        call.FindPropertyRelative("m_Mode").intValue != (int)UnityEngine.Events.PersistentListenerMode.String)
                        continue;
                    string method = call.FindPropertyRelative("m_MethodName").stringValue;
                    if (method != nameof(InteractionUIChannel.ShowMessage) && method != nameof(InteractionUIChannel.ShowPrompt))
                        continue;
                    yield return ($"{property.displayName} / {method} [{index + 1}]",
                        call.FindPropertyRelative("m_Arguments.m_StringArgument"));
                }
            }
        }

    }
}
