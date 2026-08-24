using System;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Audio;
using QuietStatic.Toolkit.Editor.Validation;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class CommunicationGraphTests
    {
        private sealed class TestSource : ScriptableObject
        {
            [SerializeField] private AudioRequestChannel requestChannel;
            [SerializeField] private UnityEvent signal = new();

            public UnityEvent Signal => signal;
            public event Action Published;
        }

        private sealed class TestReceiver : ScriptableObject
        {
            public void Receive() { }
        }

        [Test]
        public void Extract_ProducesStableSerializedReferenceEdgeAndChannelNode()
        {
            TestSource source = ScriptableObject.CreateInstance<TestSource>();
            AudioRequestChannel channel = ScriptableObject.CreateInstance<AudioRequestChannel>();
            TestReceiver receiver = ScriptableObject.CreateInstance<TestReceiver>();
            try
            {
                var serialized = new SerializedObject(source);
                serialized.FindProperty("requestChannel").objectReferenceValue = channel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                UnityEventTools.AddPersistentListener(source.Signal, receiver.Receive);

                CommunicationGraph first = CommunicationGraphExtractor.Extract(new Object[] { source });
                CommunicationGraph second = CommunicationGraphExtractor.Extract(new Object[] { source });

                Assert.That(first.Edges.Count, Is.EqualTo(3));
                CommunicationEdge channelEdge = first.Edges.Single(
                    edge => edge.PropertyPath == "requestChannel");
                Assert.That(channelEdge.Kind, Is.EqualTo(CommunicationEdgeKind.SerializedReference));
                Assert.That(
                    first.Nodes.Single(node => node.Id == channelEdge.TargetId).Kind,
                    Is.EqualTo(CommunicationNodeKind.Channel));
                Assert.That(
                    first.Edges.Single(edge => edge.PropertyPath.Contains("m_PersistentCalls")).Kind,
                    Is.EqualTo(CommunicationEdgeKind.UnityEventListener));
                Assert.That(
                    first.Edges.Single(edge => edge.PropertyPath == "event:Published").Kind,
                    Is.EqualTo(CommunicationEdgeKind.CSharpEventPublisher));
                Assert.That(
                    second.Edges.Select(edge => $"{edge.SourceId}|{edge.TargetId}|{edge.PropertyPath}"),
                    Is.EqualTo(first.Edges.Select(edge => $"{edge.SourceId}|{edge.TargetId}|{edge.PropertyPath}")));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(channel);
                Object.DestroyImmediate(receiver);
            }
        }
    }
}
