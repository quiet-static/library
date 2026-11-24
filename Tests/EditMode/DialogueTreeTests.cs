using System;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Dialogue;
using UnityEditor;
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
        public void TearDown()
        {
            Undo.ClearUndo(tree);
            UnityEngine.Object.DestroyImmediate(tree);
        }

        [Test]
        public void AddingChoiceStartsWithCleanConditionsAndSupportsUndo()
        {
            var original = new DialogueTree.Choice
            {
                text = "Existing", nextNodeIndex = 0, flagsToSet = new[] { "existing.flag" },
                availabilityRequirement = new FlagRequirement(FlagRequirementMode.All, new[] { "required.flag" })
            };
            typeof(DialogueTree).GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(tree, new[] { new DialogueTree.Node { id = "start", choices = new[] { original } } });
            string before = EditorJsonUtility.ToJson(tree);
            using var data = new SerializedObject(tree);
            Undo.IncrementCurrentGroup();
            typeof(DialogueWorkspaceWindow).GetMethod("AddChoice", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { data.FindProperty("nodes").GetArrayElementAtIndex(0).FindPropertyRelative("choices") });
            data.ApplyModifiedProperties();
            Assert.That(tree.Nodes[0].choices.Length, Is.EqualTo(2));
            Assert.That(tree.Nodes[0].choices[0].text, Is.EqualTo("Existing"));
            Assert.That(tree.Nodes[0].choices[0].availabilityRequirement.Flags, Is.EqualTo(new[] { "required.flag" }));
            var added = tree.Nodes[0].choices[1];
            Assert.That(added.nextNodeIndex, Is.EqualTo(-1));
            Assert.That(added.flagsToSet, Is.Empty);
            Assert.That(added.availabilityRequirement.Mode, Is.EqualTo(FlagRequirementMode.None));
            Assert.That(added.availabilityRequirement.Flags, Is.Empty);
            Assert.That(added.availabilityRequirement.Comparisons, Is.Empty);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(EditorJsonUtility.ToJson(tree), Is.EqualTo(before));
            Undo.ClearUndo(tree);
        }

        [Test]
        public void ReconnectingNodesChangesOnlyTheSelectedTree()
        {
            var other = ScriptableObject.CreateInstance<DialogueTree>();
            try
            {
                DialogueGraphCommands.AddNode(tree, "start");
                DialogueGraphCommands.AddNode(tree, "end");
                DialogueGraphCommands.AddNode(other, "start");
                DialogueGraphCommands.AddNode(other, "end");
                DialogueGraphCommands.ReconnectLinear(tree, "start", "end");
                Assert.That(tree.Nodes[0].nextNodeIndex, Is.EqualTo(1));
                Assert.That(other.Nodes[0].nextNodeIndex, Is.EqualTo(-1));
                DialogueGraphCommands.ReconnectLinear(tree, "start", null);
                Assert.That(tree.Nodes[0].nextNodeIndex, Is.EqualTo(-1));
            }
            finally
            {
                Undo.ClearUndo(tree);
                Undo.ClearUndo(other);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

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
