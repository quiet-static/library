using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class SceneFlowFailureRecoveryTests
    {
        private GameObject managerObject;
        private Scene originalScene;
        private Scene laterDestination;
        private Action<string> transitionStartedHandler;
        private Action<string> transitionCompletedHandler;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneFlowManager.OnTransitionStarted -= transitionStartedHandler;
            SceneFlowManager.OnTransitionCompleted -=
                transitionCompletedHandler;
            transitionStartedHandler = null;
            transitionCompletedHandler = null;

            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
            }

            if (laterDestination.IsValid() && laterDestination.isLoaded)
            {
                AsyncOperation unload =
                    SceneManager.UnloadSceneAsync(laterDestination);
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
        public IEnumerator MissingTarget_RecoversAndDoesNotBlockLaterTransition()
        {
            originalScene = SceneManager.GetActiveScene();
            Assert.That(originalScene.IsValid(), Is.True);
            Assert.That(originalScene.isLoaded, Is.True);

            laterDestination = SceneManager.CreateScene(
                $"Later Destination {Guid.NewGuid():N}");
            string missingSceneName =
                $"Missing Scene {Guid.NewGuid():N}";
            managerObject = new GameObject("Failure Recovery Scene Flow Manager");
            managerObject.SetActive(false);
            SceneFlowManager manager =
                managerObject.AddComponent<SceneFlowManager>();
            SetField(manager, "fadeDuringTransitions", false);

            var startedScenes = new List<string>();
            var completedScenes = new List<string>();
            transitionStartedHandler = startedScenes.Add;
            transitionCompletedHandler = completedScenes.Add;
            SceneFlowManager.OnTransitionStarted += transitionStartedHandler;
            SceneFlowManager.OnTransitionCompleted +=
                transitionCompletedHandler;
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Scene '" + Regex.Escape(missingSceneName) +
                    "' couldn't be loaded because it has not been added"));
            LogAssert.Expect(
                LogType.Warning,
                new Regex(
                    "SceneFlowManager could not begin loading scene '" +
                    Regex.Escape(missingSceneName) +
                    "'[.]"));

            yield return manager.TransitionToSceneRoutine(
                new SceneTransitionRequest(
                    missingSceneName,
                    unloadOtherScenes: false));

            Assert.That(manager.IsTransitioning, Is.False);
            Assert.That(startedScenes, Is.EqualTo(new[] { missingSceneName }));
            Assert.That(completedScenes, Is.Empty);

            yield return manager.TransitionToSceneRoutine(
                new SceneTransitionRequest(
                    laterDestination.name,
                    unloadOtherScenes: false));

            Assert.That(manager.IsTransitioning, Is.False);
            Assert.That(
                startedScenes,
                Is.EqualTo(new[]
                {
                    missingSceneName,
                    laterDestination.name,
                }));
            Assert.That(
                completedScenes,
                Is.EqualTo(new[] { laterDestination.name }));
            Assert.That(
                SceneManager.GetActiveScene(),
                Is.EqualTo(laterDestination));
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
