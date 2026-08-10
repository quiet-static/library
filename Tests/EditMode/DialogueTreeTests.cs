using System;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class DialogueTreeTests
    {
        private DialogueTree tree;

        [SetUp]
        public void SetUp() => tree = ScriptableObject.CreateInstance<DialogueTree>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(tree);

        [Test]
        public void GetChoiceTexts_PreservesOrderAndNormalizesNullEntries()
        {
            var node = new DialogueTree.Node
            {
                choices = new[]
                {
                    new DialogueTree.Choice { text = "Open" },
                    null,
                    new DialogueTree.Choice { text = null }
                }
            };

            CollectionAssert.AreEqual(
                new[] { "Open", string.Empty, string.Empty },
                node.GetChoiceTexts());
        }

        [Test]
        public void GetChoiceTexts_WithoutChoices_ReturnsEmptyArray()
        {
            var node = new DialogueTree.Node();

            Assert.That(node.HasChoices, Is.False);
            Assert.That(node.GetChoiceTexts(), Is.Empty);
        }

        [Test]
        public void AvailableChoices_FilterByRequirementsAndPreserveAuthoredIndexes()
        {
            var flagObject = new GameObject("Flags");
            FlagManager flags = flagObject.AddComponent<FlagManager>();
            try
            {
                var node = new DialogueTree.Node
                {
                    choices = new[]
                    {
                        new DialogueTree.Choice { text = "Always" },
                        new DialogueTree.Choice
                        {
                            text = "Known secret",
                            availabilityRequirement = new FlagRequirement(
                                FlagRequirementMode.All,
                                new[] { "KnowsSecret" })
                        },
                        new DialogueTree.Choice
                        {
                            text = "Still hidden",
                            availabilityRequirement = new FlagRequirement(
                                FlagRequirementMode.All,
                                new[] { "OtherSecret" })
                        }
                    }
                };

                flags.SetFlag("KnowsSecret");

                Assert.That(node.GetAvailableChoiceIndexes(flags), Is.EqualTo(new[] { 0, 1 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(flagObject);
            }
        }

        [Test]
        public void AvailableChoices_SupportNegativeFlagRequirements()
        {
            GameObject flagObject = new("Flags");
            FlagManager flags = flagObject.AddComponent<FlagManager>();
            try
            {
                DialogueTree.Node node = new()
                {
                    choices = new[]
                    {
                        new DialogueTree.Choice
                        {
                            text = "Only before discovery",
                            availabilityRequirement = new FlagRequirement(
                                FlagRequirementMode.NotAny, new[] { "Discovered" })
                        }
                    }
                };
                Assert.That(node.GetAvailableChoiceIndexes(flags), Is.EqualTo(new[] { 0 }));
                flags.SetFlag("Discovered");
                Assert.That(node.GetAvailableChoiceIndexes(flags), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(flagObject); }
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void TryGetNode_WithOutOfRangeIndex_ReturnsFalse(int index)
        {
            SetNodes(new DialogueTree.Node(), new DialogueTree.Node());

            Assert.That(tree.TryGetNode(index, out DialogueTree.Node node), Is.False);
            Assert.That(node, Is.Null);
        }

        [Test]
        public void TryGetNode_WithValidIndex_ReturnsExactNode()
        {
            var expected = new DialogueTree.Node { id = "second" };
            SetNodes(new DialogueTree.Node { id = "first" }, expected);

            Assert.That(tree.TryGetNode(1, out DialogueTree.Node actual), Is.True);
            Assert.That(actual, Is.SameAs(expected));
        }

        private void SetNodes(params DialogueTree.Node[] nodes)
        {
            FieldInfo field = typeof(DialogueTree).GetField(
                "nodes",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                throw new MissingFieldException(typeof(DialogueTree).FullName, "nodes");
            }

            field.SetValue(tree, nodes);
        }
    }
}
