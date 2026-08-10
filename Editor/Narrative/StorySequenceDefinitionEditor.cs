using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Narrative;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Narrative
{
    /// <summary>Inspector validation for story sequence graphs.</summary>
    [CustomEditor(typeof(StorySequenceDefinition))]
    public sealed class StorySequenceDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            StorySequenceDefinition sequence =
                (StorySequenceDefinition)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sequence Validation", EditorStyles.boldLabel);

            List<string> issues = Validate(sequence);
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"{sequence.Stages.Count} stage(s); all IDs and links are valid.",
                    MessageType.Info);
                return;
            }

            foreach (string issue in issues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
        }

        internal static List<string> Validate(StorySequenceDefinition sequence)
        {
            List<string> issues = new();
            if (string.IsNullOrWhiteSpace(sequence.Id))
                issues.Add("The sequence needs a stable ID for save data.");
            if (sequence.GetStartingStage() == null)
                issues.Add("Starting Stage ID does not resolve to a stage.");

            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (StorySequenceDefinition.Stage stage in sequence.Stages)
            {
                if (stage == null)
                {
                    issues.Add("The stages array contains a missing stage.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(stage.Id))
                    issues.Add("A stage has no stable ID.");
                else if (!ids.Add(stage.Id))
                    issues.Add($"Duplicate stage ID '{stage.Id}'.");
            }

            foreach (StorySequenceDefinition.Stage stage in sequence.Stages)
            {
                if (stage != null && !string.IsNullOrWhiteSpace(stage.NextStageId) &&
                    sequence.FindStage(stage.NextStageId) == null)
                    issues.Add($"Stage '{stage.Id}' links to missing stage '{stage.NextStageId}'.");
            }
            return issues;
        }
    }
}
