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
        public static int[] AvailableChoiceIndexes(DialogueTree.Node node, IEnumerable<string> simulatedFlags)
        {
            if (node?.choices == null) return Array.Empty<int>();
            var active = new HashSet<string>((simulatedFlags ?? Array.Empty<string>())
                .Where(flag => !string.IsNullOrWhiteSpace(flag)).Select(flag => flag.Trim()), StringComparer.Ordinal);
            return node.choices.Select((choice, index) => new { choice, index })
                .Where(item => item.choice != null && IsMet(item.choice.availabilityRequirement, active))
                .Select(item => item.index).ToArray();
        }

        public static bool IsMet(FlagRequirement requirement, ISet<string> active)
        {
            if (requirement == null || requirement.Mode == FlagRequirementMode.None) return true;
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
