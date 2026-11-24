using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>Selects a dialogue tree from story flags and live player conditions.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Dialogue/Dialogue Tree Resolver")]
    public sealed class DialogueTreeResolver : MonoBehaviour
    {
        /// <summary>The outcome of evaluating one dialogue rule.</summary>
        public enum DialogueRuleStatus
        {
            MissingTree,
            RequiredFlagsUnmet,
            BlockedByFlags,
            ContextUnmet,
            Available
        }

        /// <summary>One ordered conversation and its availability conditions.</summary>
        [Serializable]
        public sealed class DialogueRule
        {
            [Tooltip("Optional label explaining when this conversation applies.")]
            [SerializeField] private string label;

            [Tooltip("Tree selected when this rule is the first eligible match.")]
            [SerializeField] private DialogueTree tree;

            [Tooltip("Flag condition required for this conversation. None skips the Boolean flag check; typed comparisons still apply.")]
            [SerializeField] private FlagRequirement requirement = new();

            [Tooltip("When this configured flag condition is met, skip this conversation. Use Any for completed or departed flags.")]
            [SerializeField] private FlagRequirement blockingRequirement = new();

            [Tooltip("All assigned live player conditions must pass. An empty list adds no conditions.")]
            [SerializeField] private DialogueContextCondition[] contextConditions;

            /// <summary>Gets the authoring label.</summary>
            public string Label => label ?? string.Empty;
            /// <summary>Gets the candidate tree.</summary>
            public DialogueTree Tree => tree;
            /// <summary>Gets the required condition.</summary>
            public FlagRequirement Requirement => requirement;
            /// <summary>Gets the condition that prevents selection.</summary>
            public FlagRequirement BlockingRequirement => blockingRequirement;
            /// <summary>Gets the live player conditions required for selection.</summary>
            public IReadOnlyList<DialogueContextCondition> ContextConditions =>
                contextConditions ?? Array.Empty<DialogueContextCondition>();

            /// <summary>Evaluates this rule against current flags and the registered player.</summary>
            public bool IsAvailable(FlagManager flags = null) =>
                IsAvailable(flags, GetRegisteredPlayer(), null);

            internal bool IsAvailable(FlagManager flags, GameObject player, Interactor interactor) =>
                EvaluateCore(flags, player, interactor) == DialogueRuleStatus.Available;

            /// <summary>Evaluates the rule against supplied values, such as an editor simulation.</summary>
            public DialogueRuleStatus Evaluate(
                Func<FlagRequirement, bool> isRequirementMet,
                Func<DialogueContextCondition, bool> isContextMet)
            {
                if (isRequirementMet == null) throw new ArgumentNullException(nameof(isRequirementMet));
                if (isContextMet == null) throw new ArgumentNullException(nameof(isContextMet));

                return EvaluateCore(null, null, null, isRequirementMet, isContextMet);
            }

            private DialogueRuleStatus EvaluateCore(
                FlagManager flags,
                GameObject player,
                Interactor interactor,
                Func<FlagRequirement, bool> isRequirementMet = null,
                Func<DialogueContextCondition, bool> isContextMet = null)
            {
                if (tree == null) return DialogueRuleStatus.MissingTree;
                if (requirement != null && !IsMet(requirement, flags, isRequirementMet))
                    return DialogueRuleStatus.RequiredFlagsUnmet;
                if (blockingRequirement != null && blockingRequirement.IsConfigured &&
                    IsMet(blockingRequirement, flags, isRequirementMet))
                    return DialogueRuleStatus.BlockedByFlags;

                foreach (DialogueContextCondition condition in contextConditions ?? Array.Empty<DialogueContextCondition>())
                {
                    if (condition == null || !(isContextMet != null
                        ? isContextMet(condition)
                        : player != null && condition.IsMet(player, interactor)))
                        return DialogueRuleStatus.ContextUnmet;
                }

                return DialogueRuleStatus.Available;
            }

            private static bool IsMet(
                FlagRequirement condition, FlagManager flags, Func<FlagRequirement, bool> isRequirementMet) =>
                isRequirementMet != null ? isRequirementMet(condition) : condition.IsMet(flags);
        }

        [Tooltip("Checked from top to bottom on each request. Put the most specific conversations first.")]
        [SerializeField] private DialogueRule[] rules;

        [Tooltip("Optional tree used when no rule matches. Leave empty for no dialogue.")]
        [SerializeField] private DialogueTree fallbackTree;

        /// <summary>Gets the ordered authoring rules.</summary>
        public IReadOnlyList<DialogueRule> Rules => rules ?? Array.Empty<DialogueRule>();
        /// <summary>Gets the optional fallback.</summary>
        public DialogueTree FallbackTree => fallbackTree;

        /// <summary>Finds the first eligible tree using current flags and the registered player.</summary>
        public bool TryResolve(out DialogueTree tree, FlagManager flags = null) =>
            TryResolve(out tree, flags, null);

        /// <summary>Finds the first eligible tree using current flags and the supplied player.</summary>
        /// <param name="tree">The selected tree, or null when no rule matches and there is no fallback.</param>
        /// <param name="flags">The active flag manager when null.</param>
        /// <param name="player">The player root to evaluate, or null to use the actor or registered player.</param>
        /// <param name="interactor">The source of the interaction, when available.</param>
        public bool TryResolve(out DialogueTree tree, FlagManager flags, GameObject player, Interactor interactor = null)
        {
            if (player == null && interactor != null) player = interactor.Actor;
            if (player == null) player = GetRegisteredPlayer();

            if (rules != null)
            {
                foreach (DialogueRule rule in rules)
                {
                    if (rule == null || !rule.IsAvailable(flags, player, interactor)) continue;
                    tree = rule.Tree;
                    return true;
                }
            }

            tree = fallbackTree;
            return tree != null;
        }

        private static GameObject GetRegisteredPlayer()
        {
            PlayerManager manager = PlayerManager.Instance;
            return manager != null ? manager.Player : null;
        }
    }
}
