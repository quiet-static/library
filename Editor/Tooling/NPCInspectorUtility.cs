using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>Small shared helpers for the focused NPC authoring inspectors.</summary>
    internal static class NPCInspectorUtility
    {
        internal static void DrawScript(SerializedObject serializedObject)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }
        }

        internal static void DrawHeader(string label)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        }

        internal static void DrawSameObjectWarnings(
            Component owner,
            params (string Label, SerializedProperty Property)[] references)
        {
            var externalReferences = new List<string>();
            foreach ((string label, SerializedProperty property) in references)
            {
                if (property?.objectReferenceValue is Component component &&
                    component.gameObject != owner.gameObject)
                {
                    externalReferences.Add(label);
                }
            }

            if (externalReferences.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"References expected on this NPC point to another GameObject: " +
                    $"{string.Join(", ", externalReferences)}.",
                    MessageType.Warning);
            }
        }

        internal static void DrawAnimatorTriggerValidation(
            NPCAnimationTrigger adapter,
            IEnumerable<string> configuredNames,
            string context)
        {
            string[] names = (configuredNames ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (names.Length == 0)
            {
                return;
            }

            if (adapter == null)
            {
                EditorGUILayout.HelpBox(
                    $"{context} configures Animator triggers, but no NPC Animation Trigger adapter is assigned.",
                    MessageType.Warning);
                return;
            }

            Animator animator = ResolveAnimator(adapter);
            if (animator == null)
            {
                EditorGUILayout.HelpBox(
                    $"{context} trigger names cannot be validated because the adapter has no discoverable Animator.",
                    MessageType.Warning);
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                EditorGUILayout.HelpBox(
                    $"{context} trigger names cannot be validated because the Animator has no controller.",
                    MessageType.Warning);
                return;
            }

            var availableTriggers = new HashSet<string>(
                ReadAnimatorParameters(animator)
                    .Where(parameter =>
                        parameter.type == AnimatorControllerParameterType.Trigger)
                    .Select(parameter => parameter.name),
                StringComparer.Ordinal);
            string[] unavailable = names
                .Where(name => !availableTriggers.Contains(name))
                .ToArray();
            if (unavailable.Length > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Animator trigger{(unavailable.Length == 1 ? string.Empty : "s")} " +
                    $"not found: {string.Join(", ", unavailable)}.",
                    MessageType.Warning);
            }
        }

        internal static Animator ResolveAnimator(Component adapter)
        {
            if (adapter == null) return null;

            using var serializedAdapter = new SerializedObject(adapter);
            SerializedProperty animatorProperty =
                serializedAdapter.FindProperty("animator");
            if (animatorProperty?.objectReferenceValue is Animator assigned && assigned != null) return assigned;

            Animator activeAnimator = adapter.GetComponentInChildren<Animator>();
            if (activeAnimator != null || adapter.gameObject.activeInHierarchy) return activeAnimator;

            return adapter.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate => RemainsActiveWhenNpcEnables(candidate.transform, adapter.transform));
        }

        internal static IReadOnlyList<AnimatorControllerParameter> ReadAnimatorParameters(Animator animator)
        {
            if (animator == null) return Array.Empty<AnimatorControllerParameter>();

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
            if (controller == null) return Array.Empty<AnimatorControllerParameter>();

            return controller is AnimatorController editable ? editable.parameters : animator.parameters;
        }

        private static bool RemainsActiveWhenNpcEnables(Transform candidate, Transform root)
        {
            for (Transform current = candidate; current != root; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
            }

            return true;
        }
    }
}
