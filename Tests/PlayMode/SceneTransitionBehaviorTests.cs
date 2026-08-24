using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class SceneTransitionBehaviorTests
    {
        private GameObject managerObject;
        private Scene originalScene;
        private Scene destinationScene;
        private Action<string> transitionCompletedHandler;
        private SceneFlowManager manager;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null)
            {
                manager.TransitionCompleted -= transitionCompletedHandler;
            }
            transitionCompletedHandler = null;

            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
            }

            if (destinationScene.IsValid() && destinationScene.isLoaded)
            {
                AsyncOperation unload =
                    SceneManager.UnloadSceneAsync(destinationScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            if (managerObject != null)
            {
                UnityEngine.Object.Destroy(managerObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Manager_AppliesDifferentResponsesForTheSameDestination()
        {
            originalScene = SceneManager.GetActiveScene();
            Assert.That(originalScene.IsValid(), Is.True);
            Assert.That(originalScene.isLoaded, Is.True);

            destinationScene = SceneManager.CreateScene(
                $"Transition Destination {Guid.NewGuid():N}");
            GameObject destinationRoot = new GameObject(
                "Transition Definition");
            SceneManager.MoveGameObjectToScene(
                destinationRoot,
                destinationScene);
            SceneTransitionDefinition definition =
                destinationRoot.AddComponent<SceneTransitionDefinition>();

            var lifecycle = new List<string>();
            SetField(
                definition,
                "onEntered",
                CreateEvent(() => lifecycle.Add("entered")));
            SetField(
                definition,
                "responses",
                new[]
                {
                    CreateResponse(
                        "route.house",
                        () => lifecycle.Add("house")),
                    CreateResponse(
                        "route.cellar",
                        () => lifecycle.Add("cellar")),
                    CreateResponse(
                        "route.throw",
                        () => throw new InvalidOperationException(
                            "Destination response failed.")),
                });

            managerObject = new GameObject("Inactive Scene Flow Manager");
            managerObject.SetActive(false);
            manager =
                managerObject.AddComponent<SceneFlowManager>();
            SetField(manager, "fadeDuringTransitions", false);

            transitionCompletedHandler = sceneName =>
            {
                if (sceneName == destinationScene.name)
                {
                    lifecycle.Add("completed");
                }
            };
            manager.TransitionCompleted += transitionCompletedHandler;

            yield return manager.TransitionToSceneRoutine(
                new SceneTransitionRequest(
                    destinationScene.name,
                    unloadOtherScenes: false,
                    conditionId: "route.house"));

            Assert.That(
                lifecycle,
                Is.EqualTo(new[] { "entered", "house", "completed" }));

            lifecycle.Clear();
            Assert.That(
                SceneManager.SetActiveScene(originalScene),
                Is.True);

            yield return manager.TransitionToSceneRoutine(
                new SceneTransitionRequest(
                    destinationScene.name,
                    unloadOtherScenes: false,
                    conditionId: "route.cellar"));

            Assert.That(
                lifecycle,
                Is.EqualTo(new[] { "entered", "cellar", "completed" }));
            Assert.That(
                SceneManager.GetActiveScene(),
                Is.EqualTo(destinationScene));

            lifecycle.Clear();
            Assert.That(
                SceneManager.SetActiveScene(originalScene),
                Is.True);
            LogAssert.Expect(
                LogType.Exception,
                new Regex("Destination response failed"));

            yield return manager.TransitionToSceneRoutine(
                new SceneTransitionRequest(
                    destinationScene.name,
                    unloadOtherScenes: false,
                    conditionId: "route.throw"));

            Assert.That(
                lifecycle,
                Is.EqualTo(new[] { "entered", "completed" }));
            Assert.That(manager.IsTransitioning, Is.False);
        }

        private static SceneTransitionDefinition.Response CreateResponse(
            string conditionId,
            Action listener)
        {
            SceneTransitionDefinition.Response response = new();
            SetField(response, "conditionId", conditionId);
            SetField(response, "requirement", new FlagRequirement());
            SetField(response, "onEntered", CreateEvent(listener));
            return response;
        }

        private static UnityEvent CreateEvent(Action listener)
        {
            UnityEvent result = new UnityEvent();
            result.AddListener(listener.Invoke);
            return result;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
