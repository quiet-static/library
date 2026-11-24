using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.Player;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class DialogueTreeResolverTests
    {
        private GameObject root;
        private FlagManager flags;
        private DialogueTreeResolver resolver;
        private DialogueTree first;
        private DialogueTree fallback;
        private MovementDialogueCondition movementCondition;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Dialogue resolver tests");
            flags = root.AddComponent<FlagManager>();
            resolver = root.AddComponent<DialogueTreeResolver>();
            first = ScriptableObject.CreateInstance<DialogueTree>();
            fallback = ScriptableObject.CreateInstance<DialogueTree>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(fallback);
            if (movementCondition != null) Object.DestroyImmediate(movementCondition);
        }

        [Test]
        public void Resolve_ReevaluatesOpeningAndClosingWindowWithoutConsumingConversation()
        {
            Set(resolver, "rules", new[] { Rule(first, new FlagRequirement(
                FlagRequirementMode.All, new[] { "phone-discussed" }), new FlagRequirement(
                FlagRequirementMode.Any, new[] { "departed", "conversation-complete" })) });
            Assert.That(resolver.TryResolve(out _, flags), Is.False);
            flags.SetFlag("phone-discussed");
            Assert.That(resolver.TryResolve(out var selected, flags), Is.True);
            Assert.That(selected, Is.SameAs(first));
            Assert.That(resolver.TryResolve(out _, flags), Is.True);
            flags.SetFlag("conversation-complete");
            Assert.That(resolver.TryResolve(out _, flags), Is.False);
        }

        [Test]
        public void Resolve_DepartureBlocksEvenWhenConversationWasNeverPlayed()
        {
            Set(resolver, "rules", new[] { Rule(first, null, new FlagRequirement(
                FlagRequirementMode.Any, new[] { "departed", "conversation-complete" })) });
            flags.SetFlag("departed");
            Assert.That(resolver.TryResolve(out _, flags), Is.False);
        }

        [Test]
        public void Resolve_FirstEligibleRuleWinsSkippingNullEntriesAndTrees()
        {
            Set(resolver, "rules", new[] { null, Rule(null), Rule(first), Rule(fallback) });
            Assert.That(resolver.TryResolve(out var selected, flags), Is.True);
            Assert.That(selected, Is.SameAs(first));
        }

        [Test]
        public void Resolve_UsesFallbackOnlyWhenNoRuleMatches()
        {
            Set(resolver, "fallbackTree", fallback);
            Set(resolver, "rules", new[] { Rule(first, new FlagRequirement(
                FlagRequirementMode.All, new[] { "ready" })) });
            Assert.That(resolver.TryResolve(out var selected, flags), Is.True);
            Assert.That(selected, Is.SameAs(fallback));
            flags.SetFlag("ready");
            resolver.TryResolve(out selected, flags);
            Assert.That(selected, Is.SameAs(first));
        }

        [Test]
        public void Resolve_NoRulesOrFallbackReturnsNull()
        {
            Assert.That(resolver.TryResolve(out var selected, flags), Is.False);
            Assert.That(selected, Is.Null);
        }

        [Test]
        public void Resolve_ReevaluatesLivePlayerStateTogetherWithFlags()
        {
            movementCondition = ScriptableObject.CreateInstance<MovementDialogueCondition>();
            MovementStateController movement = root.AddComponent<MovementStateController>();
            movement.SetState(EntityState.MovementState.Falling);
            Set(resolver, "fallbackTree", fallback);
            Set(resolver, "rules", new[] { Rule(first, new FlagRequirement(
                FlagRequirementMode.All, new[] { "ready" }), contextConditions: new[] { movementCondition }) });

            Assert.That(resolver.TryResolve(out var selected, flags, root), Is.True);
            Assert.That(selected, Is.SameAs(fallback));

            flags.SetFlag("ready");
            Assert.That(resolver.TryResolve(out selected, flags, root), Is.True);
            Assert.That(selected, Is.SameAs(first));

            movement.SetState(EntityState.MovementState.Idle);
            Assert.That(resolver.TryResolve(out selected, flags, root), Is.True);
            Assert.That(selected, Is.SameAs(fallback));
        }

        [Test]
        public void Resolve_MissingConditionPreventsMatchingEvenWhenOtherConditionPasses()
        {
            movementCondition = ScriptableObject.CreateInstance<MovementDialogueCondition>();
            root.AddComponent<MovementStateController>().SetState(EntityState.MovementState.Falling);
            Set(resolver, "rules", new[] { Rule(first, contextConditions: new[] { movementCondition, null }) });

            Assert.That(resolver.TryResolve(out _, flags, root), Is.False);
        }

        [Test]
        public void Resolve_LiveConditionRequiresPlayer()
        {
            movementCondition = ScriptableObject.CreateInstance<MovementDialogueCondition>();
            Set(resolver, "rules", new[] { Rule(first, contextConditions: new[] { movementCondition }) });

            Assert.That(resolver.TryResolve(out _, flags), Is.False);
        }

        [Test]
        public void Evaluate_ReportsFirstUnmetRuleCondition()
        {
            movementCondition = ScriptableObject.CreateInstance<MovementDialogueCondition>();
            var required = new FlagRequirement(FlagRequirementMode.All, new[] { "ready" });
            var blocking = new FlagRequirement(FlagRequirementMode.Any, new[] { "gone" });
            DialogueTreeResolver.DialogueRule rule = Rule(first, required, blocking, new[] { movementCondition });

            Assert.That(rule.Evaluate(_ => false, _ => true),
                Is.EqualTo(DialogueTreeResolver.DialogueRuleStatus.RequiredFlagsUnmet));
            Assert.That(rule.Evaluate(_ => true, _ => true),
                Is.EqualTo(DialogueTreeResolver.DialogueRuleStatus.BlockedByFlags));
            Assert.That(rule.Evaluate(condition => condition == required, _ => false),
                Is.EqualTo(DialogueTreeResolver.DialogueRuleStatus.ContextUnmet));
            Assert.That(rule.Evaluate(condition => condition == required, _ => true),
                Is.EqualTo(DialogueTreeResolver.DialogueRuleStatus.Available));
        }

        private static DialogueTreeResolver.DialogueRule Rule(DialogueTree tree,
            FlagRequirement requirement = null, FlagRequirement blocking = null,
            DialogueContextCondition[] contextConditions = null)
        {
            var rule = new DialogueTreeResolver.DialogueRule();
            Set(rule, "tree", tree);
            if (requirement != null) Set(rule, "requirement", requirement);
            if (blocking != null) Set(rule, "blockingRequirement", blocking);
            if (contextConditions != null) Set(rule, "contextConditions", contextConditions);
            return rule;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private sealed class MovementDialogueCondition : DialogueContextCondition
        {
            public override bool IsMet(GameObject player, Interactor interactor) =>
                player.TryGetComponent(out MovementStateController movement) &&
                movement.CurrentState == EntityState.MovementState.Falling;
        }
    }
}
