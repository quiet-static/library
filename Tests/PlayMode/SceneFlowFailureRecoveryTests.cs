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
        private Scene supportScene;
        private Scene unrelatedScene;
        private Action<string> transitionStartedHandler;
        private Action<string> transitionCompletedHandler;
        private Action<SceneTransitionResult> transitionFinishedHandler;
        private Action<SceneTransitionResult> channelFinishedHandler;
        private SceneFlowRequestChannel requestChannel;
        private SceneFlowRequestChannel replacementRequestChannel;
        private SceneFlowManager manager;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null)
            {
                manager.TransitionStarted -= transitionStartedHandler;
                manager.TransitionCompleted -= transitionCompletedHandler;
                manager.TransitionFinished -= transitionFinishedHandler;
            }
            transitionStartedHandler = null;
            transitionCompletedHandler = null;
            transitionFinishedHandler = null;

            if (requestChannel != null)
            {
                requestChannel.TransitionFinished -= channelFinishedHandler;
                UnityEngine.Object.Destroy(requestChannel);
            }
            channelFinishedHandler = null;
            requestChannel = null;

            if (replacementRequestChannel != null)
            {
                UnityEngine.Object.Destroy(replacementRequestChannel);
            }
            replacementRequestChannel = null;

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

            if (supportScene.IsValid() && supportScene.isLoaded)
            {
                AsyncOperation unload =
                    SceneManager.UnloadSceneAsync(supportScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            if (unrelatedScene.IsValid() && unrelatedScene.isLoaded)
            {
                AsyncOperation unload =
                    SceneManager.UnloadSceneAsync(unrelatedScene);
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
            manager =
                managerObject.AddComponent<SceneFlowManager>();
            SetField(manager, "fadeDuringTransitions", false);

            var startedScenes = new List<string>();
            var completedScenes = new List<string>();
            var results = new List<SceneTransitionResult>();
            transitionStartedHandler = startedScenes.Add;
            transitionCompletedHandler = completedScenes.Add;
            manager.TransitionStarted += transitionStartedHandler;
            manager.TransitionCompleted += transitionCompletedHandler;
            transitionFinishedHandler = results.Add;
            manager.TransitionFinished += transitionFinishedHandler;
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

            SceneTransitionRequest missingRequest =
                new SceneTransitionRequest(
                    missingSceneName,
                    unloadOtherScenes: false);
            yield return manager.TransitionToSceneRoutine(missingRequest);

            Assert.That(manager.IsTransitioning, Is.False);
            Assert.That(startedScenes, Is.EqualTo(new[] { missingSceneName }));
            Assert.That(completedScenes, Is.Empty);
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Succeeded, Is.False);
            Assert.That(results[0].Destination, Is.EqualTo(missingSceneName));
            Assert.That(results[0].Failure, Is.EqualTo(SceneTransitionFailure.LoadFailed));
            Assert.That(results[0].Request, Is.SameAs(missingRequest));

            SceneTransitionRequest successfulRequest =
                new SceneTransitionRequest(
                    laterDestination.name,
                    unloadOtherScenes: false);
            yield return manager.TransitionToSceneRoutine(successfulRequest);

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
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[1].Succeeded, Is.True);
            Assert.That(results[1].Request, Is.SameAs(successfulRequest));
            Assert.That(manager.LastTransitionResult, Is.EqualTo(results[1]));
        }

        [UnityTest]
        public IEnumerator RejectedRequests_PublishExplicitTerminalFailures()
        {
            managerObject = new GameObject("Rejected Scene Flow Manager");
            managerObject.SetActive(false);
            manager = managerObject.AddComponent<SceneFlowManager>();
            var results = new List<SceneTransitionResult>();
            var channelResults = new List<SceneTransitionResult>();
            transitionFinishedHandler = results.Add;
            manager.TransitionFinished += transitionFinishedHandler;
            requestChannel =
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>();
            channelFinishedHandler = channelResults.Add;
            requestChannel.TransitionFinished += channelFinishedHandler;
            manager.SetRequestChannel(requestChannel);

            SceneTransitionRequest emptyRequest =
                new SceneTransitionRequest(string.Empty);
            yield return manager.TransitionToSceneRoutine(emptyRequest);
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(SceneTransitionFailure.EmptyTarget));
            Assert.That(results[0].Request, Is.SameAs(emptyRequest));
            Assert.That(channelResults, Is.EqualTo(results));

            SetField(manager, "isTransitioning", true);
            SceneTransitionRequest rejectedRequest =
                new SceneTransitionRequest("Rejected Destination");
            yield return manager.TransitionToSceneRoutine(rejectedRequest);
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(
                results[1].Failure,
                Is.EqualTo(SceneTransitionFailure.AlreadyTransitioning));
            Assert.That(results[1].Destination, Is.EqualTo("Rejected Destination"));
            Assert.That(results[1].Request, Is.SameAs(rejectedRequest));
            Assert.That(
                manager.IsTransitioning,
                Is.True,
                "Rejecting a second request must not release the active transition guard.");
            Assert.That(channelResults, Is.EqualTo(results));
            SetField(manager, "isTransitioning", false);
        }

        [UnityTest]
        public IEnumerator RequiredSupportScenes_AreRetainedDuringCleanup()
        {
            originalScene = SceneManager.GetActiveScene();
            laterDestination = SceneManager.CreateScene(
                $"Support Target {Guid.NewGuid():N}");
            supportScene = SceneManager.CreateScene(
                $"Required Support {Guid.NewGuid():N}");
            unrelatedScene = SceneManager.CreateScene(
                $"Unrelated Content {Guid.NewGuid():N}");
            string unrelatedSceneName = unrelatedScene.name;
            if (SceneManager.GetActiveScene() != originalScene)
            {
                Assert.That(SceneManager.SetActiveScene(originalScene), Is.True);
            }
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
            managerObject = new GameObject("Support Scene Flow Manager");
            managerObject.SetActive(false);
            manager = managerObject.AddComponent<SceneFlowManager>();
            SetField(manager, "fadeDuringTransitions", false);
            manager.ConfigurePersistentScenes(new[] { originalScene.name });
            var results = new List<SceneTransitionResult>();
            transitionFinishedHandler = results.Add;
            manager.TransitionFinished += transitionFinishedHandler;
            SceneTransitionRequest request = new(
                laterDestination.name,
                additionalScenesToLoad: new[] { supportScene.name },
                unloadOtherScenes: true);

            yield return manager.TransitionToSceneRoutine(request);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Succeeded, Is.True);
            Assert.That(results[0].Request, Is.SameAs(request));
            Assert.That(
                SceneManager.GetSceneByName(supportScene.name).isLoaded,
                Is.True,
                "A required support scene must survive this transition's cleanup.");
            Assert.That(
                SceneManager.GetSceneByName(unrelatedSceneName).isLoaded,
                Is.False);
        }

        [UnityTest]
        public IEnumerator MissingRequiredSupportScene_FailsCorrelatedRequest()
        {
            originalScene = SceneManager.GetActiveScene();
            laterDestination = SceneManager.CreateScene(
                $"Support Failure Target {Guid.NewGuid():N}");
            if (SceneManager.GetActiveScene() != originalScene)
            {
                Assert.That(SceneManager.SetActiveScene(originalScene), Is.True);
            }
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
            string missingSupport =
                $"Missing Support {Guid.NewGuid():N}";
            managerObject = new GameObject("Support Failure Scene Flow Manager");
            managerObject.SetActive(false);
            manager = managerObject.AddComponent<SceneFlowManager>();
            SetField(manager, "fadeDuringTransitions", false);
            var results = new List<SceneTransitionResult>();
            transitionFinishedHandler = results.Add;
            manager.TransitionFinished += transitionFinishedHandler;
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Scene '" + Regex.Escape(missingSupport) +
                    "' couldn't be loaded because it has not been added"));
            LogAssert.Expect(
                LogType.Warning,
                new Regex(
                    "SceneFlowManager could not begin loading scene '" +
                    Regex.Escape(missingSupport) +
                    "'[.]"));
            SceneTransitionRequest request = new(
                laterDestination.name,
                additionalScenesToLoad: new[] { missingSupport },
                unloadOtherScenes: true);

            yield return manager.TransitionToSceneRoutine(request);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Succeeded, Is.False);
            Assert.That(
                results[0].Failure,
                Is.EqualTo(SceneTransitionFailure.LoadFailed));
            Assert.That(results[0].Request, Is.SameAs(request));
            Assert.That(results[0].Message, Does.Contain(missingSupport));
            Assert.That(manager.IsTransitioning, Is.False);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
        }

        [UnityTest]
        public IEnumerator RuntimeChannelRebind_PublishesToOriginatingChannel()
        {
            originalScene = SceneManager.GetActiveScene();
            laterDestination = SceneManager.CreateScene(
                $"Rebind Target {Guid.NewGuid():N}");
            managerObject = new GameObject("Rebound Scene Flow Manager");
            managerObject.SetActive(false);
            manager = managerObject.AddComponent<SceneFlowManager>();
            SetField(manager, "fadeDuringTransitions", false);
            requestChannel =
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>();
            replacementRequestChannel =
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>();
            var originResults = new List<SceneTransitionResult>();
            var replacementResults = new List<SceneTransitionResult>();
            channelFinishedHandler = originResults.Add;
            requestChannel.TransitionFinished += channelFinishedHandler;
            replacementRequestChannel.TransitionFinished +=
                replacementResults.Add;
            manager.SetRequestChannel(requestChannel);
            SceneTransitionRequest request = new(
                laterDestination.name,
                unloadOtherScenes: false);
            IEnumerator routine = manager.TransitionToSceneRoutine(request);

            manager.SetRequestChannel(replacementRequestChannel);
            yield return routine;

            Assert.That(originResults, Has.Count.EqualTo(1));
            Assert.That(originResults[0].Succeeded, Is.True);
            Assert.That(originResults[0].Request, Is.SameAs(request));
            Assert.That(replacementResults, Is.Empty);
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
