using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Narrative;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class StorySequenceDefinitionTests
    {
        private StorySequenceDefinition sequence;

        [SetUp]
        public void SetUp() =>
            sequence = ScriptableObject.CreateInstance<StorySequenceDefinition>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(sequence);

        [Test]
        public void FindsStartingAndLinkedStagesByStableId()
        {
            StorySequenceDefinition.Stage first = CreateStage("opening", "search");
            StorySequenceDefinition.Stage second = CreateStage("search", string.Empty);
            SetField(sequence, "id", "chapter.one");
            SetField(sequence, "startingStageId", "opening");
            SetField(sequence, "stages", new[] { first, second });

            Assert.That(sequence.GetStartingStage(), Is.SameAs(first));
            Assert.That(sequence.FindStage(first.NextStageId), Is.SameAs(second));
            Assert.That(sequence.FindStage("missing"), Is.Null);
        }

        private static StorySequenceDefinition.Stage CreateStage(string id, string next)
        {
            StorySequenceDefinition.Stage stage = new();
            SetField(stage, "id", id);
            SetField(stage, "nextStageId", next);
            return stage;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
