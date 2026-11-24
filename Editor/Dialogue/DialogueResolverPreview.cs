using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEngine;
using RuleStatus = QuietStatic.Toolkit.Dialogue.DialogueTreeResolver.DialogueRuleStatus;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Simulates an authored resolver against temporary flag and player-condition values.</summary>
    internal sealed class DialogueResolverPreview
    {
        private readonly Dictionary<string, FlagValue> flagOverrides = new(StringComparer.Ordinal);
        private readonly HashSet<DialogueContextCondition> passingConditions = new();

        public bool Expanded { get; set; }

        internal static IReadOnlyDictionary<string, FlagValue> LoadFlagDefaults(out bool hasCatalogs)
        {
            FlagDatabase[] catalogs = AssetDatabase.FindAssets("t:FlagDatabase")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FlagDatabase>)
                .Where(catalog => catalog != null).ToArray();
            hasCatalogs = catalogs.Length > 0;

            return catalogs.SelectMany(catalog => catalog.Flags ?? Array.Empty<FlagDatabase.FlagDefinition>())
                .Where(flag => flag != null && !string.IsNullOrWhiteSpace(flag.id))
                .GroupBy(flag => flag.id.Trim(), StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.First().DefaultValue, StringComparer.Ordinal);
        }

        internal void Draw(DialogueTreeResolver resolver, IReadOnlyDictionary<string, FlagValue> flagDefaults,
            bool hasFlagCatalogs)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                if (GUILayout.Button("Reset Simulation", GUILayout.Width(125f)))
                {
                    flagOverrides.Clear();
                    passingConditions.Clear();
                }

                var rules = resolver.Rules.Where(rule => rule != null);
                string[] flagIds = rules.SelectMany(rule => FlagIds(rule.Requirement).Concat(FlagIds(rule.BlockingRequirement)))
                    .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                var values = DrawFlags(flagIds, flagDefaults, hasFlagCatalogs);
                DrawConditions(rules.SelectMany(rule => rule.ContextConditions)
                    .Where(condition => condition != null).Distinct().OrderBy(condition => condition.name));
                DrawResults(resolver, values);
            }
        }

        private Dictionary<string, FlagValue> DrawFlags(IReadOnlyList<string> flagIds,
            IReadOnlyDictionary<string, FlagValue> flagDefaults, bool hasCatalogs)
        {
            var values = new Dictionary<string, FlagValue>(StringComparer.Ordinal);
            if (flagIds.Count == 0) return values;

            EditorGUILayout.LabelField(new GUIContent("Simulated Flags", "Uses project catalog defaults when no value is entered."),
                EditorStyles.boldLabel);
            foreach (string id in flagIds)
            {
                if (!flagDefaults.TryGetValue(id, out FlagValue initial))
                {
                    if (hasCatalogs)
                    {
                        EditorGUILayout.LabelField(id, "Missing or duplicate catalog entry");
                        continue;
                    }
                    initial = FlagValue.From(false);
                }

                FlagValue value = flagOverrides.TryGetValue(id, out FlagValue overridden) && overridden.Type == initial.Type
                    ? overridden : initial;
                value = value.Type switch
                {
                    FlagValueType.Boolean => FlagValue.From(EditorGUILayout.Toggle(id, value.Boolean)),
                    FlagValueType.Integer => FlagValue.From(EditorGUILayout.IntField(id, value.Integer)),
                    FlagValueType.Float => FlagValue.From(EditorGUILayout.FloatField(id, value.Float)),
                    FlagValueType.String => FlagValue.From(EditorGUILayout.TextField(id, value.String)),
                    _ => value
                };

                if (value.Equals(initial)) flagOverrides.Remove(id);
                else flagOverrides[id] = value;
                values.Add(id, value);
            }

            return values;
        }

        private void DrawConditions(IEnumerable<DialogueContextCondition> conditions)
        {
            DialogueContextCondition[] assets = conditions.ToArray();
            if (assets.Length == 0) return;

            EditorGUILayout.LabelField(new GUIContent("Simulated Player Conditions", "Check each condition that should pass."),
                EditorStyles.boldLabel);
            foreach (DialogueContextCondition condition in assets)
            {
                var label = new GUIContent(condition.name, AssetDatabase.GetAssetPath(condition));
                if (EditorGUILayout.ToggleLeft(label, passingConditions.Contains(condition))) passingConditions.Add(condition);
                else passingConditions.Remove(condition);
            }
        }

        private void DrawResults(DialogueTreeResolver resolver, IReadOnlyDictionary<string, FlagValue> values)
        {
            var activeFlags = new HashSet<string>(values.Where(entry => entry.Value.Type == FlagValueType.Boolean && entry.Value.Boolean)
                .Select(entry => entry.Key), StringComparer.Ordinal);
            var outcomes = new List<string>(resolver.Rules.Count);
            int winner = -1;
            for (int index = 0; index < resolver.Rules.Count; index++)
            {
                DialogueTreeResolver.DialogueRule rule = resolver.Rules[index];
                string label = rule?.Label;
                if (string.IsNullOrWhiteSpace(label))
                    label = rule != null && rule.Tree != null ? rule.Tree.name : "Unnamed rule";
                string outcome = winner >= 0 ? $"Skipped because rule {winner + 1} matched" : "Missing rule";
                if (winner < 0 && rule != null)
                {
                    RuleStatus status = rule.Evaluate(RequirementMet, passingConditions.Contains);
                    outcome = Describe(status, rule);
                    if (status == RuleStatus.Available) winner = index;
                }
                outcomes.Add($"{index + 1}. {label} — {outcome}");
            }

            string selected = winner >= 0 ? $"{resolver.Rules[winner].Tree.name} (rule {winner + 1})" :
                resolver.FallbackTree != null ? $"{resolver.FallbackTree.name} (fallback)" : "No match or fallback";
            EditorGUILayout.LabelField("Selected Tree", selected);
            foreach (string outcome in outcomes)
                EditorGUILayout.LabelField(outcome, EditorStyles.wordWrappedMiniLabel);

            bool RequirementMet(FlagRequirement requirement) =>
                requirement.Comparisons.All(comparison => comparison != null &&
                    !string.IsNullOrWhiteSpace(comparison.flagId) && values.ContainsKey(comparison.flagId.Trim())) &&
                DialoguePathPreview.IsMet(requirement, activeFlags, values);
        }

        private string Describe(RuleStatus status, DialogueTreeResolver.DialogueRule rule)
        {
            if (status == RuleStatus.ContextUnmet)
            {
                DialogueContextCondition unmet = rule.ContextConditions.FirstOrDefault(condition =>
                    condition == null || !passingConditions.Contains(condition));
                return unmet == null ? "Missing player condition asset" : $"Player condition failed ({unmet.name})";
            }

            return status switch
            {
                RuleStatus.MissingTree => "Missing tree",
                RuleStatus.RequiredFlagsUnmet => "Required flag condition failed",
                RuleStatus.BlockedByFlags => "Blocking flag condition matched",
                RuleStatus.Available => "Matched",
                _ => "Skipped"
            };
        }

        private static IEnumerable<string> FlagIds(FlagRequirement requirement)
        {
            if (requirement == null) yield break;
            if (requirement.Mode != FlagRequirementMode.None)
            {
                foreach (string id in requirement.Flags)
                    if (!string.IsNullOrWhiteSpace(id)) yield return id.Trim();
            }

            foreach (FlagComparison comparison in requirement.Comparisons)
                if (!string.IsNullOrWhiteSpace(comparison?.flagId)) yield return comparison.flagId.Trim();
        }
    }
}
