using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>Selects a dialogue tree from current story flags without starting or consuming it.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Dialogue/Dialogue Tree Resolver")]
    public sealed class DialogueTreeResolver : MonoBehaviour
    {
        /// <summary>One ordered conversation and its availability conditions.</summary>
        [Serializable]
        public sealed class DialogueRule
        {
            [Tooltip("Optional label explaining when this conversation applies.")]
            [SerializeField] private string label;

            [Tooltip("Tree selected when this rule is the first eligible match.")]
            [SerializeField] private DialogueTree tree;

            [Tooltip("Condition required for this conversation. None always passes.")]
            [SerializeField] private FlagRequirement requirement = new();

            [Tooltip("When this configured condition is met, skip this conversation. Use Any for completed or departed flags. None does not block.")]
            [SerializeField] private FlagRequirement blockingRequirement = new();

            /// <summary>Gets the authoring label.</summary>
            public string Label => label ?? string.Empty;
            /// <summary>Gets the candidate tree.</summary>
            public DialogueTree Tree => tree;
            /// <summary>Gets the required condition.</summary>
            public FlagRequirement Requirement => requirement;
            /// <summary>Gets the condition that prevents selection.</summary>
            public FlagRequirement BlockingRequirement => blockingRequirement;

            /// <summary>Evaluates this rule without changing story flags.</summary>
            public bool IsAvailable(FlagManager flags = null) =>
                tree != null &&
                (requirement == null || requirement.IsMet(flags)) &&
                !(blockingRequirement != null && blockingRequirement.IsConfigured &&
                  blockingRequirement.IsMet(flags));
        }

        [Tooltip("Checked from top to bottom on each request. Put the most specific conversations first.")]
        [SerializeField] private DialogueRule[] rules;

        [Tooltip("Optional tree used when no rule matches. Leave empty for no dialogue.")]
        [SerializeField] private DialogueTree fallbackTree;

        /// <summary>Gets the ordered authoring rules.</summary>
        public IReadOnlyList<DialogueRule> Rules => rules ?? Array.Empty<DialogueRule>();
        /// <summary>Gets the optional fallback.</summary>
        public DialogueTree FallbackTree => fallbackTree;

        /// <summary>Finds the first eligible tree using current flags; never mutates progression.</summary>
        public bool TryResolve(out DialogueTree tree, FlagManager flags = null)
        {
            if (rules != null)
            {
                foreach (DialogueRule rule in rules)
                {
                    if (rule == null || !rule.IsAvailable(flags)) continue;
                    tree = rule.Tree;
                    return true;
                }
            }

            tree = fallbackTree;
            return tree != null;
        }
    }
}
