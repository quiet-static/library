using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.EditMode
{
    /// <summary>
    /// Covers scene-flow boundary contracts shared by authoring assets, request
    /// channels, and UnityEvent-facing handlers.
    /// </summary>
    public sealed class SceneFlowEdgeContractTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [TestCase("  opening.entry  ", "opening.entry")]
        [TestCase("   ", "")]
        [TestCase(null, "")]
        public void BootstrapProfile_InitialConditionIsNormalizedAndPropagated(
            string configuredConditionId,
            string expectedConditionId)
        {
            SceneBootstrapProfile profile = Track(
                ScriptableObject.CreateInstance<SceneBootstrapProfile>());
            SetField(profile, "initialScene", new SceneReference("Title"));
            SetField(profile, "initialConditionId", configuredConditionId);

            SceneTransitionRequest request =
                profile.CreateInitialTransitionRequest();

            Assert.That(request.TargetSceneName, Is.EqualTo("Title"));
            Assert.That(request.ConditionId, Is.EqualTo(expectedConditionId));
        }

        [Test]
        public void TryGetConnection_DuplicateNormalizedIdsWarnsAndReturnsFirstAuthoredConnection()
        {
            SceneFlowMap.Connection first = CreateConnection(
                "  route.entry  ",
                string.Empty,
                "FirstDestination");
            SceneFlowMap.Connection second = CreateConnection(
                "route.entry",
                string.Empty,
                "SecondDestination");
            SceneFlowMap map = CreateMap(first, second);
            map.name = "Scene Flow Contract Map";
            LogAssert.Expect(
                LogType.Warning,
                new Regex(
                    "TryGetConnection: SceneFlowMap contains more than one connection named " +
                    "'route\\.entry'\\. The first connection will be used\\."));

            bool found = map.TryGetConnection(
                "  route.entry  ",
                out SceneFlowMap.Connection resolved);

            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(first));
            Assert.That(resolved.ToSceneName, Is.EqualTo("FirstDestination"));
            Assert.That(resolved.CreateRequest().ConditionId, Is.EqualTo("route.entry"));
        }

        [Test]
        public void TryCreateRequest_FiltersDeduplicatesAndPreservesAdditionalSceneOrder()
        {
            SceneFlowMap.Connection connection = CreateConnection(
                "route.groups",
                "Source",
                "Destination",
                unloadOtherScenes: false,
                additionalScenesToLoad: new[]
                {
                    null,
                    new SceneReference(string.Empty),
                    new SceneReference(" Lighting "),
                    new SceneReference("Player"),
                    new SceneReference("Lighting"),
                    new SceneReference("  "),
                    new SceneReference("UI"),
                },
                additionalScenesToKeep: new[]
                {
                    new SceneReference("Loading"),
                    null,
                    new SceneReference(" Loading "),
                    new SceneReference(string.Empty),
                    new SceneReference("Player"),
                });
            SceneFlowMap map = CreateMap(connection);

            bool found = map.TryCreateRequest(
                "route.groups",
                out SceneTransitionRequest request);

            Assert.That(found, Is.True);
            Assert.That(
                request.AdditionalScenesToLoad,
                Is.EqualTo(new[] { "Lighting", "Player", "UI" }));
            Assert.That(
                request.AdditionalScenesToKeep,
                Is.EqualTo(new[] { "Loading", "Player" }));
            Assert.That(request.UnloadOtherScenes, Is.False);
            Assert.That(request.ConditionId, Is.EqualTo("route.groups"));
        }

        [Test]
        public void RequestChannel_PreservesExactDetailedTransitionRequest()
        {
            SceneFlowRequestChannel channel = Track(
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
            SceneTransitionRequest request = new SceneTransitionRequest(
                "Destination",
                new[] { "Lighting", "Player" },
                new[] { "Loading", "Player" },
                unloadOtherScenes: false,
                conditionId: "route.detailed");
            SceneFlowCommand received = default;
            int receivedCount = 0;
            channel.CommandRequested += command =>
            {
                received = command;
                receivedCount++;
            };

            bool accepted = channel.RequestTransition(request);

            Assert.That(accepted, Is.True);
            Assert.That(receivedCount, Is.EqualTo(1));
            Assert.That(received.Type, Is.EqualTo(SceneFlowCommandType.Transition));
            Assert.That(received.SceneName, Is.EqualTo("Destination"));
            Assert.That(received.Transition, Is.SameAs(request));
            Assert.That(
                received.Transition.AdditionalScenesToLoad,
                Is.EqualTo(new[] { "Lighting", "Player" }));
            Assert.That(
                received.Transition.AdditionalScenesToKeep,
                Is.EqualTo(new[] { "Loading", "Player" }));
            Assert.That(received.Transition.UnloadOtherScenes, Is.False);
            Assert.That(received.Transition.ConditionId, Is.EqualTo("route.detailed"));
        }

        [Test]
        public void Handler_MappedTransitionUsesNormalizedConnectionIdAsCondition()
        {
            SceneFlowMap.Connection connection = CreateConnection(
                "  mapped.entry  ",
                string.Empty,
                "MappedDestination");
            SceneFlowMap map = CreateMap(connection);
            SceneFlowRequestChannel channel = Track(
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
            UnityEvent started = new UnityEvent();
            int startedCount = 0;
            SceneFlowCommand received = default;
            started.AddListener(() => startedCount++);
            channel.CommandRequested += command => received = command;
            SceneTransitionHandler handler = CreateHandler(
                "Mapped Handler",
                channel,
                started);
            SetField(handler, "sceneFlowMap", map);
            SetField(handler, "connectionId", " mapped.entry ");

            bool accepted = handler.TryTransition();

            Assert.That(accepted, Is.True);
            Assert.That(received.Type, Is.EqualTo(SceneFlowCommandType.Transition));
            Assert.That(received.SceneName, Is.EqualTo("MappedDestination"));
            Assert.That(received.Transition.TargetSceneName, Is.EqualTo("MappedDestination"));
            Assert.That(received.Transition.ConditionId, Is.EqualTo("mapped.entry"));
            Assert.That(startedCount, Is.EqualTo(1));
        }

        [Test]
        public void Handler_ImmediateCorrelatedRejectionIsNotReportedAsStarted()
        {
            SceneFlowMap map = CreateMap(CreateConnection(
                "route.busy",
                string.Empty,
                "BusyDestination"));
            SceneFlowRequestChannel channel = Track(
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
            UnityEvent started = new UnityEvent();
            UnityEvent failed = new UnityEvent();
            int startedCount = 0;
            int failedCount = 0;
            started.AddListener(() => startedCount++);
            failed.AddListener(() => failedCount++);
            SceneTransitionRequest submittedRequest = null;
            channel.CommandRequested += command =>
            {
                submittedRequest = command.Transition;
                PublishResult(channel, SceneTransitionResult.Failed(
                    command.SceneName,
                    SceneTransitionFailure.AlreadyTransitioning,
                    "Busy",
                    command.Transition));
            };
            SceneTransitionHandler handler = CreateHandler(
                "Busy Handler",
                channel,
                started,
                failed);
            SetField(handler, "sceneFlowMap", map);
            SetField(handler, "connectionId", "route.busy");
            var results = new List<SceneTransitionResult>();
            handler.TransitionFinished += results.Add;

            bool accepted = handler.TryTransition();

            Assert.That(accepted, Is.False);
            Assert.That(startedCount, Is.Zero);
            Assert.That(failedCount, Is.EqualTo(1));
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(
                results[0].Failure,
                Is.EqualTo(SceneTransitionFailure.AlreadyTransitioning));
            Assert.That(results[0].Request, Is.SameAs(submittedRequest));
            Assert.That(handler.IsTransitionPending, Is.False);
        }

        [Test]
        public void Handler_FiltersResultsToItsExactPendingRequest()
        {
            SceneFlowMap map = CreateMap(CreateConnection(
                "route.filtered",
                string.Empty,
                "SharedDestination"));
            SceneFlowRequestChannel channel = Track(
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
            UnityEvent started = new UnityEvent();
            UnityEvent failed = new UnityEvent();
            int failedCount = 0;
            failed.AddListener(() => failedCount++);
            SceneTransitionRequest submittedRequest = null;
            channel.CommandRequested += command =>
                submittedRequest = command.Transition;
            SceneTransitionHandler handler = CreateHandler(
                "Filtered Handler",
                channel,
                started,
                failed);
            SetField(handler, "sceneFlowMap", map);
            SetField(handler, "connectionId", "route.filtered");
            var results = new List<SceneTransitionResult>();
            handler.TransitionFinished += results.Add;

            Assert.That(handler.TryTransition(), Is.True);
            Assert.That(handler.IsTransitionPending, Is.True);
            SceneTransitionRequest otherRequest =
                new SceneTransitionRequest("SharedDestination");
            PublishResult(channel, SceneTransitionResult.Failed(
                "SharedDestination",
                SceneTransitionFailure.LoadFailed,
                "Other request failed",
                otherRequest));

            Assert.That(results, Is.Empty);
            Assert.That(failedCount, Is.Zero);
            Assert.That(handler.IsTransitionPending, Is.True);

            SceneTransitionResult ownFailure = SceneTransitionResult.Failed(
                "SharedDestination",
                SceneTransitionFailure.LoadFailed,
                "Own request failed",
                submittedRequest);
            PublishResult(channel, ownFailure);

            Assert.That(results, Is.EqualTo(new[] { ownFailure }));
            Assert.That(failedCount, Is.EqualTo(1));
            Assert.That(handler.IsTransitionPending, Is.False);
        }

        [Test]
        public void Handler_DisabledDuringPendingRequest_DefersItsExactResultUntilReenabled()
        {
            SceneFlowMap map = CreateMap(CreateConnection(
                "route.disabled",
                string.Empty,
                "DeferredDestination"));
            SceneFlowRequestChannel channel = Track(
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
            SceneTransitionRequest submittedRequest = null;
            channel.CommandRequested += command =>
                submittedRequest = command.Transition;
            UnityEvent failed = new UnityEvent();
            int failedCount = 0;
            failed.AddListener(() => failedCount++);
            SceneTransitionHandler handler = CreateHandler(
                "Disabled Handler",
                channel,
                new UnityEvent(),
                failed);
            SetField(handler, "sceneFlowMap", map);
            SetField(handler, "connectionId", "route.disabled");
            var results = new List<SceneTransitionResult>();
            handler.TransitionFinished += results.Add;

            Assert.That(handler.TryTransition(), Is.True);
            Assert.That(handler.IsTransitionPending, Is.True);
            handler.gameObject.SetActive(false);
            SceneTransitionResult ownFailure = SceneTransitionResult.Failed(
                "DeferredDestination",
                SceneTransitionFailure.LoadFailed,
                "Deferred failure",
                submittedRequest);

            PublishResult(channel, ownFailure);
            PublishResult(channel, SceneTransitionResult.Success(
                "UnrelatedDestination",
                new SceneTransitionRequest("UnrelatedDestination")));

            Assert.That(handler.IsTransitionPending, Is.False);
            Assert.That(results, Is.Empty);
            Assert.That(failedCount, Is.Zero);

            handler.gameObject.SetActive(true);
            InvokeLifecycle(handler, "OnEnable");

            Assert.That(results, Is.EqualTo(new[] { ownFailure }));
            Assert.That(failedCount, Is.EqualTo(1));
        }

        private SceneFlowMap CreateMap(
            params SceneFlowMap.Connection[] connections)
        {
            SceneFlowMap map = Track(
                ScriptableObject.CreateInstance<SceneFlowMap>());
            SetField(map, "connections", connections);
            return map;
        }

        private SceneTransitionHandler CreateHandler(
            string name,
            SceneFlowRequestChannel channel,
            UnityEvent started,
            UnityEvent failed = null)
        {
            GameObject gameObject = Track(new GameObject(name));
            SceneTransitionHandler handler =
                gameObject.AddComponent<SceneTransitionHandler>();
            SetField(handler, "requestChannel", channel);
            SetField(handler, "onTransitionStarted", started);
            SetField(handler, "onTransitionFailed", failed);
            return handler;
        }

        private static void PublishResult(
            SceneFlowRequestChannel channel,
            SceneTransitionResult result)
        {
            MethodInfo publish = typeof(SceneFlowRequestChannel).GetMethod(
                "PublishTransitionResult",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(publish, Is.Not.Null);
            publish.Invoke(channel, new object[] { result });
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                method,
                Is.Not.Null,
                $"Expected lifecycle method '{methodName}' on {target.GetType().FullName}.");
            method.Invoke(target, null);
        }

        private static SceneFlowMap.Connection CreateConnection(
            string id,
            string from,
            string to,
            bool unloadOtherScenes = true,
            SceneReference[] additionalScenesToLoad = null,
            SceneReference[] additionalScenesToKeep = null)
        {
            SceneFlowMap.Connection connection = new();
            SetField(connection, "id", id);
            SetField(connection, "fromScene", new SceneReference(from));
            SetField(connection, "toScene", new SceneReference(to));
            SetField(
                connection,
                "additionalScenesToLoad",
                additionalScenesToLoad);
            SetField(
                connection,
                "additionalScenesToKeep",
                additionalScenesToKeep);
            SetField(connection, "unloadOtherScenes", unloadOtherScenes);
            return connection;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            Assert.That(target, Is.Not.Null, "Cannot set a field on a null target.");
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                $"Expected serialized field '{fieldName}' on " +
                $"{target.GetType().FullName}.");
            Assert.That(
                value == null || field.FieldType.IsInstanceOfType(value),
                Is.True,
                $"Value for '{fieldName}' must be assignable to " +
                $"{field.FieldType.FullName}.");
            field.SetValue(target, value);
        }
    }
}
