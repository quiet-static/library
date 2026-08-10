using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public class SceneFlowMapTests
    {
        private SceneFlowMap map;

        [SetUp]
        public void SetUp()
        {
            map = ScriptableObject.CreateInstance<SceneFlowMap>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(map);
        }

        [Test]
        public void TryCreateRequest_UsesConfiguredDestinationAndPolicy()
        {
            SceneFlowMap.Connection connection = CreateConnection(
                "HouseToStreet",
                "House",
                "Street",
                unloadOtherScenes: false);
            SetConnections(connection);

            bool found = map.TryCreateRequest(
                "HouseToStreet",
                out SceneTransitionRequest request);

            Assert.That(found, Is.True);
            Assert.That(request.TargetSceneName, Is.EqualTo("Street"));
            Assert.That(request.UnloadOtherScenes, Is.False);
        }

        [Test]
        public void GetConnectionsFrom_ReturnsOnlyOutgoingConnections()
        {
            SceneFlowMap.Connection outgoing = CreateConnection(
                "HouseToStreet", "House", "Street", true);
            SceneFlowMap.Connection incoming = CreateConnection(
                "CellarToHouse", "Cellar", "House", true);
            SetConnections(outgoing, incoming);

            Assert.That(map.GetConnectionsFrom("House"), Is.EqualTo(new[] { outgoing }));
        }

        [Test]
        public void TryCreateRequest_RejectsMissingDestination()
        {
            SetConnections(CreateConnection("Broken", "House", string.Empty, true));

            Assert.That(map.TryCreateRequest("Broken", out _), Is.False);
        }

        private static SceneFlowMap.Connection CreateConnection(
            string id,
            string from,
            string to,
            bool unloadOtherScenes)
        {
            SceneFlowMap.Connection connection = new();
            SetField(connection, "id", id);
            SetField(connection, "fromScene", new SceneReference(from));
            SetField(connection, "toScene", new SceneReference(to));
            SetField(connection, "unloadOtherScenes", unloadOtherScenes);
            return connection;
        }

        private void SetConnections(params SceneFlowMap.Connection[] connections)
        {
            SetField(map, "connections", connections);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {name}");
            field.SetValue(target, value);
        }
    }
}
