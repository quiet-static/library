using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
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

        private static DialogueTreeResolver.DialogueRule Rule(DialogueTree tree,
            FlagRequirement requirement = null, FlagRequirement blocking = null)
        {
            var rule = new DialogueTreeResolver.DialogueRule();
            Set(rule, "tree", tree);
            if (requirement != null) Set(rule, "requirement", requirement);
            if (blocking != null) Set(rule, "blockingRequirement", blocking);
            return rule;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
