using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Pause;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class ComponentLifecyclePlayModeTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (UnityEngine.Object createdObject in createdObjects)
            {
                if (createdObject is GameObject gameObject && gameObject != null)
                {
                    gameObject.SetActive(false);
                }
            }

            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.Destroy(createdObjects[index]);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseInputHandler_RebindsAndReleasesActionsThroughUnityLifecycle()
        {
            InputActionAsset inputAsset = Track(
                ScriptableObject.CreateInstance<InputActionAsset>());
            InputActionMap map = inputAsset.AddActionMap("UI");
            InputAction firstAction = map.AddAction("Pause First");
            InputAction secondAction = map.AddAction("Pause Second");
            InputActionReference firstReference = Track(
                InputActionReference.Create(firstAction));
            InputActionReference secondReference = Track(
                InputActionReference.Create(secondAction));
            GameObject owner = Track(new GameObject("Pause Input Lifecycle"));
            owner.SetActive(false);
            PauseInputHandler handler = owner.AddComponent<PauseInputHandler>();
            handler.SetPauseAction(firstReference);

            owner.SetActive(true);
            Assert.That(firstAction.enabled, Is.True);

            handler.SetPauseAction(secondReference);
            Assert.That(firstAction.enabled, Is.False);
            Assert.That(secondAction.enabled, Is.True);

            owner.SetActive(false);
            Assert.That(secondAction.enabled, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneTransitionHandler_DisabledRequestDefersItsResultUntilEnabled()
        {
            SceneFlowMap map = Track(
                ScriptableObject.CreateInstance<SceneFlowMap>());
            SceneFlowMap.Connection connection = new();
            SetField(connection, "id", "route.disabled");
            SetField(connection, "fromScene", new SceneReference(string.Empty));
            SetField(connection, "toScene", new SceneReference("DeferredDestination"));
            SetField(map, "connections", new[] { connection });

            SceneFlowRequestChannel channel = Track(
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
            SceneTransitionRequest submittedRequest = null;
            channel.CommandRequested += command =>
                submittedRequest = command.Transition;

            GameObject owner = Track(new GameObject("Scene Transition Lifecycle"));
            owner.SetActive(false);
            SceneTransitionHandler handler =
                owner.AddComponent<SceneTransitionHandler>();
            UnityEvent failed = new();
            int failedCount = 0;
            failed.AddListener(() => failedCount++);
            SetField(handler, "sceneFlowMap", map);
            SetField(handler, "connectionId", "route.disabled");
            SetField(handler, "requestChannel", channel);
            SetField(handler, "onTransitionFailed", failed);
            var results = new List<SceneTransitionResult>();
            handler.TransitionFinished += results.Add;
            owner.SetActive(true);

            Assert.That(handler.TryTransition(), Is.True);
            owner.SetActive(false);
            SceneTransitionResult ownFailure = SceneTransitionResult.Failed(
                "DeferredDestination",
                SceneTransitionFailure.LoadFailed,
                "Deferred failure",
                submittedRequest);
            PublishResult(channel, ownFailure);

            Assert.That(handler.IsTransitionPending, Is.False);
            Assert.That(results, Is.Empty);
            Assert.That(failedCount, Is.Zero);

            owner.SetActive(true);
            Assert.That(results, Is.EqualTo(new[] { ownFailure }));
            Assert.That(failedCount, Is.EqualTo(1));
            yield return null;
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

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            createdObjects.Add(value);
            return value;
        }
    }
}
