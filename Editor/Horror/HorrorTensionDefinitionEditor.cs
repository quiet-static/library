using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Horror;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Horror
{
    [CustomEditor(typeof(HorrorTensionDefinition))]
    public sealed class HorrorTensionDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            HorrorTensionDefinition definition = (HorrorTensionDefinition)target;
            HashSet<string> ids = new(StringComparer.Ordinal);
            List<string> issues = new();
            foreach (HorrorTensionDefinition.State state in definition.States)
            {
                if (state == null) { issues.Add("A state entry is missing."); continue; }
                if (string.IsNullOrWhiteSpace(state.Id)) issues.Add("A state has no stable ID.");
                else if (!ids.Add(state.Id)) issues.Add($"Duplicate state ID '{state.Id}'.");
                if (state.MusicAction == TensionMusicAction.Play && state.Music == null)
                    issues.Add($"State '{state.Id}' is set to Play music but has no clip.");
            }
            if (!string.IsNullOrWhiteSpace(definition.DefaultStateId) &&
                definition.FindState(definition.DefaultStateId) == null)
                issues.Add("Default State ID does not resolve to a state.");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tension Validation", EditorStyles.boldLabel);
            if (issues.Count == 0)
                EditorGUILayout.HelpBox($"{definition.States.Count} state(s); configuration is valid.", MessageType.Info);
            else foreach (string issue in issues)
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }
    }
}
