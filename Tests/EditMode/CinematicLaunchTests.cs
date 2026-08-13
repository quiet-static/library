using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Tests.EditMode
{
    public sealed class CinematicLaunchTests
    {
        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void DatabaseFind_UsesExactStableIdentityAndIgnoresNullEntries()
        {
            CinematicDefinition first = CreateDefinition("opening");
            CinematicDefinition second = CreateDefinition("Opening");
            CinematicDatabase database = Track(
                ScriptableObject.CreateInstance<CinematicDatabase>());
            SetField(database, "cinematics", new List<CinematicDefinition>
            {
                null,
                first,
                second,
            });

            Assert.That(database.Find("opening"), Is.SameAs(first));
            Assert.That(database.Find("Opening"), Is.SameAs(second));
            Assert.That(database.Find(" opening "), Is.Null);
            Assert.That(database.Find("missing"), Is.Null);
            Assert.That(database.Find("   "), Is.Null);
        }

        [Test]
        public void LaunchChannel_ConsumesOnlyMatchingLocationOnce()
        {
            CinematicLaunchChannel channel = Track(
                ScriptableObject.CreateInstance<CinematicLaunchChannel>());

            Assert.That(channel.Request("  gym  ", "  dance  "), Is.True);
            Assert.That(channel.HasPendingRequest, Is.True);
            Assert.That(channel.TryConsume("Gym", out _), Is.False);
            Assert.That(channel.HasPendingRequest, Is.True);

            Assert.That(
                channel.TryConsume("gym", out string cinematicId),
                Is.True);
            Assert.That(cinematicId, Is.EqualTo("dance"));
            Assert.That(channel.HasPendingRequest, Is.False);
            Assert.That(channel.TryConsume("gym", out cinematicId), Is.False);
            Assert.That(cinematicId, Is.Empty);
        }

        [Test]
        public void LaunchChannel_NewValidRequestReplacesPendingSelection()
        {
            CinematicLaunchChannel channel = Track(
                ScriptableObject.CreateInstance<CinematicLaunchChannel>());

            Assert.That(channel.Request("gym", "opening"), Is.True);
            Assert.That(channel.Request("hall", "closing"), Is.True);

            Assert.That(channel.TryConsume("gym", out _), Is.False);
            Assert.That(
                channel.TryConsume("hall", out string cinematicId),
                Is.True);
            Assert.That(cinematicId, Is.EqualTo("closing"));
        }

        [Test]
        public void SceneLauncher_AcceptedDispatchPreservesPendingSelection()
        {
            CinematicLaunchChannel launchChannel = Track(
                ScriptableObject.CreateInstance<CinematicLaunchChannel>());
            SceneFlowRequestChannel sceneFlowChannel = Track(
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
            CinematicSceneLauncher launcher = CreateLauncher(
                launchChannel,
                sceneFlowChannel,
                out UnityEvent accepted,
                out UnityEvent rejected);
            SceneFlowCommand received = default;
            int acceptedCount = 0;
            int rejectedCount = 0;
            accepted.AddListener(() => acceptedCount++);
            rejected.AddListener(() => rejectedCount++);
            sceneFlowChannel.CommandRequested += command => received = command;

            bool result = launcher.Launch("  dance  ");

            Assert.That(result, Is.True);
            Assert.That(received.Type, Is.EqualTo(SceneFlowCommandType.Transition));
            Assert.That(received.SceneName, Is.EqualTo("SharedCinematic"));
            Assert.That(acceptedCount, Is.EqualTo(1));
            Assert.That(rejectedCount, Is.Zero);
            Assert.That(
                launchChannel.TryConsume("gym", out string cinematicId),
                Is.True);
            Assert.That(cinematicId, Is.EqualTo("dance"));
        }

        [Test]
        public void SceneLauncher_RejectedDispatchClearsPendingSelection()
        {
            CinematicLaunchChannel launchChannel = Track(
                ScriptableObject.CreateInstance<CinematicLaunchChannel>());
            SceneFlowRequestChannel sceneFlowChannel = Track(
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
            CinematicSceneLauncher launcher = CreateLauncher(
                launchChannel,
                sceneFlowChannel,
                out UnityEvent accepted,
                out UnityEvent rejected);
            int acceptedCount = 0;
            int rejectedCount = 0;
            accepted.AddListener(() => acceptedCount++);
            rejected.AddListener(() => rejectedCount++);

            bool result = launcher.Launch("dance");

            Assert.That(result, Is.False);
            Assert.That(launchChannel.HasPendingRequest, Is.False);
            Assert.That(acceptedCount, Is.Zero);
            Assert.That(rejectedCount, Is.EqualTo(1));
        }

        private CinematicDefinition CreateDefinition(string id)
        {
            CinematicDefinition definition = Track(
                ScriptableObject.CreateInstance<CinematicDefinition>());
            SetField(definition, "id", id);
            return definition;
        }

        private CinematicSceneLauncher CreateLauncher(
            CinematicLaunchChannel launchChannel,
            SceneFlowRequestChannel sceneFlowChannel,
            out UnityEvent accepted,
            out UnityEvent rejected)
        {
            GameObject gameObject = Track(new GameObject("Cinematic Launcher"));
            CinematicSceneLauncher launcher =
                gameObject.AddComponent<CinematicSceneLauncher>();
            accepted = new UnityEvent();
            rejected = new UnityEvent();
            SetField(launcher, "targetScene", new SceneReference("SharedCinematic"));
            SetField(launcher, "locationId", "gym");
            SetField(launcher, "launchChannel", launchChannel);
            SetField(launcher, "sceneFlowChannel", sceneFlowChannel);
            SetField(launcher, "onAccepted", accepted);
            SetField(launcher, "onRejected", rejected);
            return launcher;
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
