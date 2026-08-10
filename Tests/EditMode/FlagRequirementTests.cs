using NUnit.Framework;
using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class FlagRequirementTests
    {
        private GameObject flagObject;
        private FlagManager flags;

        [SetUp]
        public void SetUp()
        {
            flagObject = new GameObject("Flags");
            flags = flagObject.AddComponent<FlagManager>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(flagObject);

        [TestCase(FlagRequirementMode.All, false)]
        [TestCase(FlagRequirementMode.Any, true)]
        [TestCase(FlagRequirementMode.NotAll, true)]
        [TestCase(FlagRequirementMode.NotAny, false)]
        public void EvaluatesEveryConditionalMode(FlagRequirementMode mode, bool expected)
        {
            flags.SetFlag("one");
            FlagRequirement requirement = new(mode, new[] { "one", "two" });
            Assert.That(requirement.IsMet(flags), Is.EqualTo(expected));
        }

        [Test]
        public void Constructor_NormalizesAndDeduplicatesFlagIds()
        {
            FlagRequirement requirement = new(
                FlagRequirementMode.All,
                new[] { "  clue  ", "clue", "", null, "door" });
            Assert.That(requirement.Flags, Is.EqualTo(new[] { "clue", "door" }));
            Assert.That(requirement.IsConfigured, Is.True);
        }

        [Test]
        public void None_PassesButIsNotAnAutomaticCondition()
        {
            FlagRequirement requirement = new(FlagRequirementMode.None, new[] { "ignored" });
            Assert.That(requirement.IsMet(flags), Is.True);
            Assert.That(requirement.IsConfigured, Is.False);
        }

        [Test]
        public void ConditionalMode_WithoutManager_DoesNotPass()
        {
            Object.DestroyImmediate(flagObject);
            flagObject = null;
            Assert.That(new FlagRequirement(FlagRequirementMode.All, new[] { "flag" }).IsMet(), Is.False);
        }
    }
}
