using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Validation;

namespace QuietStatic.Tests.EditMode
{
    public sealed class WiringSnapshotExporterTests
    {
        [Test]
        public void BuildJson_IsByteIdenticalForEquivalentUnorderedGraphs()
        {
            var firstNode = new CommunicationNode("a", "Caller", CommunicationNodeKind.Handler, null);
            var secondNode = new CommunicationNode("b", "Channel", CommunicationNodeKind.Channel, null);
            var edge = new CommunicationEdge("a", "b", "requestChannel");
            var first = new CommunicationGraph(
                new[] { secondNode, firstNode },
                new[] { edge });
            var second = new CommunicationGraph(
                new[] { firstNode, secondNode },
                new[] { edge });

            Assert.That(
                WiringSnapshotExporter.BuildJson(first),
                Is.EqualTo(WiringSnapshotExporter.BuildJson(second)));
        }

        [Test]
        public void BuildJson_ChangesWhenWiringChanges()
        {
            var nodes = new[]
            {
                new CommunicationNode("a", "Caller", CommunicationNodeKind.Handler, null),
                new CommunicationNode("b", "Channel", CommunicationNodeKind.Channel, null),
            };
            var before = new CommunicationGraph(
                nodes,
                new[] { new CommunicationEdge("a", "b", "requestChannel") });
            var after = new CommunicationGraph(
                nodes,
                new[] { new CommunicationEdge("a", "b", "alternateChannel") });

            Assert.That(
                WiringSnapshotExporter.BuildJson(before),
                Is.Not.EqualTo(WiringSnapshotExporter.BuildJson(after)));
        }
    }
}
