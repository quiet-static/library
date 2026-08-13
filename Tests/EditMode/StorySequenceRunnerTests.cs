using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Narrative;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Tests.EditMode
{
    public sealed class StorySequenceRunnerTests
    {
        private GameObject flagObject;
        private GameObject runnerObject;
        private FlagManager flags;
        private StorySequenceRunner runner;
        private StorySequenceDefinition sequence;
        private FlagManager previousFlagInstance;

        [SetUp]
        public void SetUp()
        {
            previousFlagInstance = FlagManager.Instance;
            SetFlagManagerInstance(null);
            flagObject = new GameObject("Flags");
            flags = flagObject.AddComponent<FlagManager>();
            SetFlagManagerInstance(flags);
            runnerObject = new GameObject("Story Sequence Runner");
            runner = runnerObject.AddComponent<StorySequenceRunner>();
            SetField(runner, "startOnStart", false);
            sequence = ScriptableObject.CreateInstance<StorySequenceDefinition>();
            SetField(sequence, "id", "chapter.one");
            SetField(runner, "sequence", sequence);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(runnerObject);
            Object.DestroyImmediate(flagObject);
            Object.DestroyImmediate(sequence);
            SetFlagManagerInstance(previousFlagInstance);
            previousFlagInstance = null;
        }

        [Test]
        public void StartSequence_AppliesEntryFlagsAndRaisesEntryOnce()
        {
            StorySequenceDefinition.Stage opening = CreateStage(
                "opening",
                flagsOnEnter: new[] { "story.started" });
            Configure(opening);
            var entered = new StorySequenceRunner.StageUnityEvent();
            int enteredCount = 0;
            entered.AddListener(id =>
            {
                Assert.That(id, Is.EqualTo("opening"));
                enteredCount++;
            });
            SetField(runner, "onStageEntered", entered);

            runner.StartSequence();

            Assert.That(runner.CurrentStage, Is.SameAs(opening));
            Assert.That(runner.IsCurrentStageCompleted, Is.False);
            Assert.That(flags.HasFlag("story.started"), Is.True);
            Assert.That(enteredCount, Is.EqualTo(1));
        }

        [Test]
        public void CompletionFlag_CompletesStageSetsExitFlagAndAdvances()
        {
            StorySequenceDefinition.Stage opening = CreateStage(
                "opening",
                next: "search",
                completionRequirement: new FlagRequirement(
                    FlagRequirementMode.All,
                    new[] { "door.open" }),
                flagsOnComplete: new[] { "opening.complete" });
            StorySequenceDefinition.Stage search = CreateStage(
                "search",
                flagsOnEnter: new[] { "search.started" });
            Configure(opening, search);

            runner.StartSequence();
            flags.SetFlag("door.open");
            runner.EvaluateProgress();

            Assert.That(runner.CurrentStage, Is.SameAs(search));
            Assert.That(runner.IsCurrentStageCompleted, Is.False);
            Assert.That(runner.CompletedStageIds, Contains.Item("opening"));
            Assert.That(flags.HasAll(new[]
            {
                "opening.complete",
                "search.started",
            }), Is.True);
        }

        [Test]
        public void EntryRequirement_BlocksLinkedStageUntilItBecomesMet()
        {
            StorySequenceDefinition.Stage opening = CreateStage(
                "opening",
                next: "locked");
            StorySequenceDefinition.Stage locked = CreateStage(
                "locked",
                entryRequirement: new FlagRequirement(
                    FlagRequirementMode.All,
                    new[] { "access.granted" }));
            Configure(opening, locked);

            runner.StartSequence();
            runner.CompleteCurrentStage();

            Assert.That(runner.CurrentStage, Is.SameAs(opening));
            Assert.That(runner.IsCurrentStageCompleted, Is.True);

            flags.SetFlag("access.granted");
            runner.EvaluateProgress();

            Assert.That(runner.CurrentStage, Is.SameAs(locked));
            Assert.That(runner.IsCurrentStageCompleted, Is.False);
        }

        [Test]
        public void CaptureRestore_RoundTripsWithoutReplayingEntryActions()
        {
            StorySequenceDefinition.Stage opening = CreateStage(
                "opening",
                next: "search");
            StorySequenceDefinition.Stage search = CreateStage(
                "search",
                flagsOnEnter: new[] { "search.entered" });
            Configure(opening, search);
            runner.StartSequence();
            runner.CompleteCurrentStage();
            string json = runner.CaptureSaveState();
            flags.ClearFlag("search.entered");

            runner.StartSequence();
            runner.RestoreSaveState(json);

            Assert.That(runner.CurrentStage, Is.SameAs(search));
            Assert.That(runner.CompletedStageIds, Contains.Item("opening"));
            Assert.That(flags.HasFlag("search.entered"), Is.False);
            Assert.That(runner.CaptureSaveState(), Is.EqualTo(json));
        }

        [Test]
        public void CompleteTerminalStage_IsIdempotent()
        {
            StorySequenceDefinition.Stage ending = CreateStage("ending");
            Configure(ending);
            var completed = new StorySequenceRunner.StageUnityEvent();
            var sequenceCompleted = new UnityEvent();
            int stageCount = 0;
            int sequenceCount = 0;
            completed.AddListener(_ => stageCount++);
            sequenceCompleted.AddListener(() => sequenceCount++);
            SetField(runner, "onStageCompleted", completed);
            SetField(runner, "onSequenceCompleted", sequenceCompleted);

            runner.StartSequence();
            runner.CompleteCurrentStage();
            runner.CompleteCurrentStage();

            Assert.That(stageCount, Is.EqualTo(1));
            Assert.That(sequenceCount, Is.EqualTo(1));
            Assert.That(runner.IsCurrentStageCompleted, Is.True);
        }

        private void Configure(params StorySequenceDefinition.Stage[] stages)
        {
            SetField(sequence, "stages", stages);
            SetField(sequence, "startingStageId", stages[0].Id);
        }

        private static StorySequenceDefinition.Stage CreateStage(
            string id,
            string next = "",
            FlagRequirement entryRequirement = null,
            FlagRequirement completionRequirement = null,
            string[] flagsOnEnter = null,
            string[] flagsOnComplete = null)
        {
            var stage = new StorySequenceDefinition.Stage();
            SetField(stage, "id", id);
            SetField(stage, "nextStageId", next);
            if (entryRequirement != null)
            {
                SetField(stage, "entryRequirement", entryRequirement);
            }
            if (completionRequirement != null)
            {
                SetField(stage, "completionRequirement", completionRequirement);
            }
            SetField(stage, "flagsToSetOnEnter", flagsOnEnter);
            SetField(stage, "flagsToSetOnComplete", flagsOnComplete);
            return stage;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }

        private static void SetFlagManagerInstance(FlagManager value)
        {
            FieldInfo field = typeof(FlagManager).BaseType?.GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing singleton backing field.");
            field.SetValue(null, value);
        }
    }
}
