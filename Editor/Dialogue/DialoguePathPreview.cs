using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Pure dialogue-condition preview that never reads or mutates FlagManager.</summary>
    public static class DialoguePathPreview
    {
        public static int[] AvailableChoiceIndexes(DialogueTree.Node node, IEnumerable<string> simulatedFlags, IReadOnlyDictionary<string, FlagValue> simulatedValues = null)
        {
            if (node?.choices == null) return Array.Empty<int>();
            var active = new HashSet<string>((simulatedFlags ?? Array.Empty<string>())
                .Where(flag => !string.IsNullOrWhiteSpace(flag)).Select(flag => flag.Trim()), StringComparer.Ordinal);
            return node.choices.Select((choice, index) => new { choice, index })
                .Where(item => item.choice != null && IsMet(item.choice.availabilityRequirement, active, simulatedValues))
                .Select(item => item.index).ToArray();
        }

        public static bool IsMet(FlagRequirement requirement, ISet<string> active, IReadOnlyDictionary<string, FlagValue> simulatedValues = null)
        {
            if (requirement == null) return true;
            active ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (var condition in requirement.Comparisons)
            {
                if (condition == null || string.IsNullOrWhiteSpace(condition.flagId)) return false;
                string id = condition.flagId.Trim();
                FlagValue value;
                if (simulatedValues == null || !simulatedValues.TryGetValue(id, out value))
                {
                    if (condition.expected.Type != FlagValueType.Boolean) return false;
                    value = FlagValue.From(active.Contains(id));
                }
                if (!condition.IsMet(value)) return false;
            }
            if (requirement.Mode == FlagRequirementMode.None) return true;
            if (requirement.Flags.Any(id => id != null && simulatedValues != null &&
                simulatedValues.TryGetValue(id.Trim(), out var value) && value.Type != FlagValueType.Boolean)) return false;
            string[] flags = requirement.Flags.Where(flag => !string.IsNullOrWhiteSpace(flag)).ToArray();
            bool all = flags.All(active.Contains);
            bool any = flags.Any(active.Contains);
            return requirement.Mode switch
            {
                FlagRequirementMode.All => all,
                FlagRequirementMode.Any => any,
                FlagRequirementMode.NotAll => !all,
                FlagRequirementMode.NotAny => !any,
                _ => true
            };
        }
    }
}
