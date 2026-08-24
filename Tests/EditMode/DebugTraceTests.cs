using NUnit.Framework;
using QuietStatic.Toolkit.DebugTools;
using QuietStatic.Toolkit.SceneFlow;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class DebugTraceTests
    {
        private sealed class TestChannel : CrossSceneCommandChannel<string>
        {
            public bool Send(string value) => Dispatch(value);
        }

        [SetUp]
        public void SetUp()
        {
            DebugTrace.Clear();
            DebugTrace.SetCapacity(100);
            DebugTrace.SetEnabled(false);
        }

        [TearDown]
        public void TearDown()
        {
            DebugTrace.SetEnabled(false);
            DebugTrace.Clear();
        }

        [Test]
        public void DisabledTrace_DoesNotRecord()
        {
            DebugTrace.Record("Test", "Ignored");
            Assert.That(DebugTrace.Entries.Count, Is.Zero);
        }

        [Test]
        public void ChannelSubmissionAndAcceptanceShareCorrelationId()
        {
            TestChannel channel = ScriptableObject.CreateInstance<TestChannel>();
            try
            {
                channel.CommandRequested += _ => { };
                DebugTrace.SetEnabled(true);

                Assert.That(channel.Send("payload"), Is.True);

                Assert.That(DebugTrace.Entries.Count, Is.EqualTo(2));
                Assert.That(DebugTrace.Entries[0].Outcome, Is.EqualTo("Submitted"));
                Assert.That(DebugTrace.Entries[1].Outcome, Is.EqualTo("Accepted"));
                Assert.That(
                    DebugTrace.Entries[1].CorrelationId,
                    Is.EqualTo(DebugTrace.Entries[0].CorrelationId));
            }
            finally
            {
                Object.DestroyImmediate(channel);
            }
        }

        [Test]
        public void MissingReceiverRecordsRejectedReason()
        {
            TestChannel channel = ScriptableObject.CreateInstance<TestChannel>();
            try
            {
                DebugTrace.SetEnabled(true);
                Assert.That(channel.Send("payload"), Is.False);
                Assert.That(DebugTrace.Entries[1].Outcome, Is.EqualTo("Rejected: no receiver"));
            }
            finally
            {
                Object.DestroyImmediate(channel);
            }
        }

        [Test]
        public void CapacityEvictsOldestEntryDeterministically()
        {
            DebugTrace.SetCapacity(2);
            DebugTrace.SetEnabled(true);
            DebugTrace.Record("Test", "first");
            DebugTrace.Record("Test", "second");
            DebugTrace.Record("Test", "third");

            Assert.That(DebugTrace.Entries.Count, Is.EqualTo(2));
            Assert.That(DebugTrace.Entries[0].Message, Is.EqualTo("second"));
            Assert.That(DebugTrace.Entries[1].Message, Is.EqualTo("third"));
        }

        [Test]
        public void SceneTransitionCompletion_ReusesDispatchCorrelationId()
        {
            SceneFlowRequestChannel channel =
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>();
            try
            {
                channel.CommandRequested += _ => { };
                DebugTrace.SetEnabled(true);
                Assert.That(channel.TryTransitionToScene("Office"), Is.True);
                typeof(SceneFlowRequestChannel).GetMethod(
                        "PublishTransitionCompleted",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(channel, new object[] { "Office" });

                Assert.That(DebugTrace.Entries.Count, Is.EqualTo(3));
                Assert.That(DebugTrace.Entries[2].Outcome, Is.EqualTo("Completed"));
                Assert.That(
                    DebugTrace.Entries[2].CorrelationId,
                    Is.EqualTo(DebugTrace.Entries[0].CorrelationId));
            }
            finally
            {
                Object.DestroyImmediate(channel);
            }
        }

        [Test]
        public void OverlappingSceneRequests_KeepTheirOwnCompletionCorrelations()
        {
            SceneFlowRequestChannel channel =
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>();
            try
            {
                SceneTransitionRequest first =
                    new SceneTransitionRequest("Office");
                SceneTransitionRequest second =
                    new SceneTransitionRequest("Cellar");
                MethodInfo publish = typeof(SceneFlowRequestChannel).GetMethod(
                    "PublishTransitionResult",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(publish, Is.Not.Null);
                channel.CommandRequested += command =>
                {
                    if (ReferenceEquals(command.Transition, second))
                    {
                        publish.Invoke(channel, new object[]
                        {
                            SceneTransitionResult.Failed(
                                second.TargetSceneName,
                                SceneTransitionFailure.AlreadyTransitioning,
                                "Busy",
                                second),
                        });
                    }
                };
                DebugTrace.SetEnabled(true);

                Assert.That(channel.RequestTransition(first), Is.True);
                string firstCorrelation = DebugTrace.Entries[0].CorrelationId;
                Assert.That(channel.RequestTransition(second), Is.True);
                string secondCorrelation = DebugTrace.Entries[2].CorrelationId;
                publish.Invoke(channel, new object[]
                {
                    SceneTransitionResult.Success(
                        first.TargetSceneName,
                        first),
                });

                DebugTrace.Entry firstCompletion = DebugTrace.Entries
                    .Single(entry =>
                        entry.Outcome == "Completed" &&
                        entry.Payload == first.TargetSceneName);
                DebugTrace.Entry secondFailure = DebugTrace.Entries
                    .Single(entry => entry.Outcome ==
                        $"Failed: {SceneTransitionFailure.AlreadyTransitioning}");
                Assert.That(
                    firstCompletion.CorrelationId,
                    Is.EqualTo(firstCorrelation));
                Assert.That(
                    secondFailure.CorrelationId,
                    Is.EqualTo(secondCorrelation));
                Assert.That(firstCorrelation, Is.Not.EqualTo(secondCorrelation));
            }
            finally
            {
                Object.DestroyImmediate(channel);
            }
        }
    }
}
