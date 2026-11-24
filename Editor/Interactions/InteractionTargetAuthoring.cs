using System.Collections.Generic;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    internal sealed class InteractionTargetAuthoring
    {
        private static readonly InteractionTargetAuthoring OneShot = new(
            "One-shot", "displayName", "Interact", "flagsToSetOnSuccess",
            "onInteractionSucceeded", "onInteractionFailed");
        private static readonly InteractionTargetAuthoring Hold = new(
            "Hold", "hoverPrompt", "Hold to interact", "flagsToSetOnCompletion",
            "onCompleted", "onRequirementFailed");
        private static readonly InteractionTargetAuthoring Progress = new(
            "Progress", "hoverPrompt", "Start", "flagsToSetOnCompletion",
            "onCompleted", "onRequirementFailed");

        internal readonly string Kind;
        internal readonly string PromptProperty;
        internal readonly string DefaultPrompt;
        internal readonly string CompletionFlags;
        internal readonly string SuccessEvent;
        internal readonly string FailureEvent;

        private InteractionTargetAuthoring(string kind, string promptProperty, string defaultPrompt,
            string completionFlags, string successEvent, string failureEvent)
        {
            Kind = kind;
            PromptProperty = promptProperty;
            DefaultPrompt = defaultPrompt;
            CompletionFlags = completionFlags;
            SuccessEvent = successEvent;
            FailureEvent = failureEvent;
        }

        internal static InteractionTargetAuthoring For(Object target) => target switch
        {
            Interactable => OneShot,
            HoldInteractable => Hold,
            ActivatedProgressInteractable => Progress,
            _ => null,
        };

        internal static IEnumerable<Component> FindTargets(GameObject root)
        {
            foreach (Interactable target in root.GetComponentsInChildren<Interactable>(true))
                yield return target;
            foreach (HoldInteractable target in root.GetComponentsInChildren<HoldInteractable>(true))
                yield return target;
            foreach (ActivatedProgressInteractable target in root.GetComponentsInChildren<ActivatedProgressInteractable>(true))
                yield return target;
        }

        internal static SerializedProperty DrawPrompt(SerializedObject data)
        {
            InteractionTargetAuthoring target = For(data.targetObject);
            SerializedProperty prompt = data.FindProperty(target.PromptProperty);
            EditorGUILayout.PropertyField(prompt, new GUIContent("Prompt"));
            SerializedProperty progress = data.FindProperty("progressName");
            if (progress != null)
                EditorGUILayout.PropertyField(progress, new GUIContent("Progress Label"));
            return prompt;
        }
    }
}
