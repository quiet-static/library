using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class ScreenFadeRequestProbe : MonoBehaviour
    {
        public bool IsComplete { get; private set; }

        public void Request(
            ScreenFadeChannel channel,
            ScreenFadeTarget target,
            float duration)
        {
            StartCoroutine(RequestRoutine(channel, target, duration));
        }

        private IEnumerator RequestRoutine(
            ScreenFadeChannel channel,
            ScreenFadeTarget target,
            float duration)
        {
            yield return channel.FadeRoutine(target, duration);
            IsComplete = true;
        }
    }

    public sealed class ScreenFadePlaybackTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();
        private ScreenFadeChannel capturedChannel;
        private Action<ScreenFadeRequest> captureHandler;
        private Scene originalScene;
        private Scene destinationScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (capturedChannel != null && captureHandler != null)
            {
                capturedChannel.FadeRequested -= captureHandler;
            }

            capturedChannel = null;
            captureHandler = null;

            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
            }

            if (destinationScene.IsValid() && destinationScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(destinationScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            originalScene = default;
            destinationScene = default;

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
        public IEnumerator Fader_RoutinesApplyFinalAlphaAndRaycastState()
        {
            GameObject root = Track(new GameObject("Screen Fader"));
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            ScreenFader fader = root.AddComponent<ScreenFader>();

            Assert.That(canvasGroup.alpha, Is.Zero);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
            Assert.That(canvasGroup.interactable, Is.False);

            yield return fader.FadeToBlackRoutine(0f);

            Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
            Assert.That(canvasGroup.blocksRaycasts, Is.True);
            Assert.That(canvasGroup.interactable, Is.True);
            Assert.That(fader.IsFading, Is.False);

            yield return fader.FadeToClearRoutine(0f);

            Assert.That(canvasGroup.alpha, Is.Zero);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
            Assert.That(canvasGroup.interactable, Is.False);
            Assert.That(fader.IsFading, Is.False);
        }

        [Test]
        public void FadeRequest_CompletionIsNotPublicToObservers()
        {
            MethodInfo complete = typeof(ScreenFadeRequest).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(complete, Is.Not.Null);
            Assert.That(complete.IsPublic, Is.False,
                "Fade observers must not be able to release a handler-owned request.");
            Assert.That(complete.IsAssembly, Is.True,
                "The runtime handler should retain assembly-internal completion access.");
        }

        [UnityTest]
        public IEnumerator ChannelHandler_CompletesBlackAndClearLifecycle()
        {
            CreateChannelRig(
                out ScreenFadeChannel channel,
                out ScreenFadeChannelHandler handler,
                out ScreenFader fader,
                out CanvasGroup canvasGroup);

            Assert.That(channel.HasReceiver, Is.True);

            yield return channel.FadeRoutine(ScreenFadeTarget.Black, 0f);

            Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
            Assert.That(fader.IsFading, Is.False);

            yield return channel.FadeRoutine(ScreenFadeTarget.Clear, 0f);

            Assert.That(canvasGroup.alpha, Is.Zero);
            Assert.That(fader.IsFading, Is.False);

            handler.enabled = false;
            Assert.That(channel.HasReceiver, Is.False);
        }

        [UnityTest]
        public IEnumerator ChannelHandler_ContainsObserverFailureAndNotifiesRemainingObservers()
        {
            CreateChannelRig(
                out ScreenFadeChannel channel,
                out _,
                out _,
                out CanvasGroup canvasGroup);
            int successfulObserverCalls = 0;
            Action<ScreenFadeRequest> failingObserver = _ =>
                throw new InvalidOperationException("Expected fade observer failure.");
            Action<ScreenFadeRequest> successfulObserver = _ =>
                successfulObserverCalls++;
            channel.FadeRequested += failingObserver;
            channel.FadeRequested += successfulObserver;
            LogAssert.Expect(
                LogType.Error,
                new Regex("Exception while notifying a ScreenFadeChannel observer[.]")
            );
            LogAssert.Expect(
                LogType.Exception,
                new Regex("Expected fade observer failure[.]")
            );

            try
            {
                yield return channel.FadeRoutine(ScreenFadeTarget.Black, 0f);
            }
            finally
            {
                channel.FadeRequested -= failingObserver;
                channel.FadeRequested -= successfulObserver;
            }

            Assert.That(successfulObserverCalls, Is.EqualTo(1));
            Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ChannelHandler_SupersededRequestReleasesBothCallers()
        {
            CreateChannelRig(
                out ScreenFadeChannel channel,
                out _,
                out ScreenFader fader,
                out CanvasGroup canvasGroup);
            var requests = new List<ScreenFadeRequest>();
            capturedChannel = channel;
            captureHandler = requests.Add;
            capturedChannel.FadeRequested += captureHandler;

            ScreenFadeRequestProbe first = Track(
                new GameObject("First Fade Request"))
                .AddComponent<ScreenFadeRequestProbe>();
            ScreenFadeRequestProbe second = Track(
                new GameObject("Second Fade Request"))
                .AddComponent<ScreenFadeRequestProbe>();

            first.Request(channel, ScreenFadeTarget.Black, 1f);
            yield return null;
            Assert.That(fader.IsFading, Is.True);

            second.Request(channel, ScreenFadeTarget.Clear, 0f);

            for (int frame = 0;
                 frame < 30 && (!first.IsComplete || !second.IsComplete);
                 frame++)
            {
                yield return null;
            }

            Assert.That(first.IsComplete, Is.True);
            Assert.That(second.IsComplete, Is.True);
            Assert.That(requests, Has.Count.EqualTo(2));
            Assert.That(requests[0].IsComplete, Is.True);
            Assert.That(requests[0].WasCancelled, Is.True);
            Assert.That(requests[1].IsComplete, Is.True);
            Assert.That(requests[1].WasCancelled, Is.False);
            Assert.That(canvasGroup.alpha, Is.Zero);
            Assert.That(fader.IsFading, Is.False);
        }

        [UnityTest]
        public IEnumerator ChannelHandler_DisableCancelsInFlightRequest()
        {
            CreateChannelRig(
                out ScreenFadeChannel channel,
                out ScreenFadeChannelHandler handler,
                out ScreenFader fader,
                out CanvasGroup canvasGroup);
            ScreenFadeRequest request = null;
            capturedChannel = channel;
            captureHandler = captured => request = captured;
            capturedChannel.FadeRequested += captureHandler;
            ScreenFadeRequestProbe caller = Track(
                new GameObject("Fade Request Caller"))
                .AddComponent<ScreenFadeRequestProbe>();

            caller.Request(channel, ScreenFadeTarget.Black, 1f);
            yield return null;
            Assert.That(fader.IsFading, Is.True);

            handler.enabled = false;
            yield return null;

            Assert.That(caller.IsComplete, Is.True);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.IsComplete, Is.True);
            Assert.That(request.WasCancelled, Is.True);
            Assert.That(fader.IsFading, Is.False);
            Assert.That(canvasGroup.alpha, Is.Zero);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
            Assert.That(canvasGroup.interactable, Is.False);
            Assert.That(channel.HasReceiver, Is.False,
                "Observation alone must not be treated as a completion-capable fade handler.");
        }

        [UnityTest]
        public IEnumerator SceneFlow_HandlerDisableDuringBlackLeavesOverlayClearAndNonBlocking()
        {
            originalScene = SceneManager.GetActiveScene();
            destinationScene = SceneManager.CreateScene(
                $"Fade Disable Destination {Guid.NewGuid():N}");
            CreateChannelRig(
                out ScreenFadeChannel channel,
                out ScreenFadeChannelHandler handler,
                out _,
                out CanvasGroup channelCanvas);
            SceneFlowManager manager = CreateSceneFlowManager();
            SetField(manager, "screenFadeChannel", channel);
            SetField(manager, "transitionFadeDuration", 1f);

            bool disabledDuringBlack = false;
            capturedChannel = channel;
            captureHandler = request =>
            {
                if (request.Target != ScreenFadeTarget.Black)
                {
                    return;
                }

                handler.enabled = false;
                disabledDuringBlack = true;
            };
            capturedChannel.FadeRequested += captureHandler;

            yield return manager.TransitionToSceneRoutine(
                new SceneTransitionRequest(
                    destinationScene.name,
                    unloadOtherScenes: false));

            Assert.That(disabledDuringBlack, Is.True);
            Assert.That(channel.HasReceiver, Is.False);
            Assert.That(manager.LastTransitionResult?.Succeeded, Is.True);
            Assert.That(manager.IsTransitioning, Is.False);
            Assert.That(channelCanvas.alpha, Is.Zero);
            Assert.That(channelCanvas.blocksRaycasts, Is.False);
            Assert.That(channelCanvas.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator SceneFlow_PrefersLiveFadeChannelAndFinishesClear()
        {
            originalScene = SceneManager.GetActiveScene();
            destinationScene = SceneManager.CreateScene(
                $"Fade Channel Destination {Guid.NewGuid():N}");

            CreateChannelRig(
                out ScreenFadeChannel channel,
                out _,
                out _,
                out CanvasGroup channelCanvas);
            ScreenFader directFader = CreateFader(
                "Direct Fader Sentinel",
                out CanvasGroup directCanvas);
            SceneFlowManager manager = CreateSceneFlowManager();
            SetField(manager, "screenFadeChannel", channel);
            SetField(manager, "screenFader", directFader);
            SetField(manager, "transitionFadeDuration", 0f);

            float channelAlphaDuringEntry = -1f;
            float directAlphaDuringEntry = -1f;
            CreateDestinationDefinition(() =>
            {
                channelAlphaDuringEntry = channelCanvas.alpha;
                directAlphaDuringEntry = directCanvas.alpha;
            });

            var targets = new List<ScreenFadeTarget>();
            capturedChannel = channel;
            captureHandler = request => targets.Add(request.Target);
            capturedChannel.FadeRequested += captureHandler;

            yield return manager.TransitionToSceneRoutine(
                new SceneTransitionRequest(
                    destinationScene.name,
                    unloadOtherScenes: false,
                    conditionId: "fade.channel"));

            Assert.That(targets,
                Is.EqualTo(new[] { ScreenFadeTarget.Black, ScreenFadeTarget.Clear }));
            Assert.That(channelAlphaDuringEntry, Is.EqualTo(1f));
            Assert.That(directAlphaDuringEntry, Is.Zero);
            Assert.That(channelCanvas.alpha, Is.Zero);
            Assert.That(channelCanvas.blocksRaycasts, Is.False);
            Assert.That(channelCanvas.interactable, Is.False);
            Assert.That(directCanvas.alpha, Is.Zero);
            Assert.That(manager.LastTransitionResult?.Succeeded, Is.True);
        }

        [UnityTest]
        public IEnumerator SceneFlow_UsesDirectFaderWhenConfiguredChannelHasNoReceiver()
        {
            originalScene = SceneManager.GetActiveScene();
            destinationScene = SceneManager.CreateScene(
                $"Fade Fallback Destination {Guid.NewGuid():N}");
            ScreenFadeChannel channel =
                Track(ScriptableObject.CreateInstance<ScreenFadeChannel>());
            ScreenFader directFader = CreateFader(
                "Direct Fader Fallback",
                out CanvasGroup directCanvas);
            SceneFlowManager manager = CreateSceneFlowManager();
            SetField(manager, "screenFadeChannel", channel);
            SetField(manager, "screenFader", directFader);
            int observedChannelRequests = 0;
            capturedChannel = channel;
            captureHandler = _ => observedChannelRequests++;
            capturedChannel.FadeRequested += captureHandler;
            Assert.That(channel.HasReceiver, Is.False);

            float alphaDuringEntry = -1f;
            CreateDestinationDefinition(() => alphaDuringEntry = directCanvas.alpha);

            yield return manager.TransitionToSceneRoutine(
                new SceneTransitionRequest(
                    destinationScene.name,
                    unloadOtherScenes: false,
                    conditionId: "fade.fallback"));

            Assert.That(alphaDuringEntry, Is.EqualTo(1f));
            Assert.That(directCanvas.alpha, Is.Zero);
            Assert.That(directCanvas.blocksRaycasts, Is.False);
            Assert.That(directCanvas.interactable, Is.False);
            Assert.That(observedChannelRequests, Is.Zero,
                "An observer is not a handler and must not suppress the direct fallback.");
            Assert.That(manager.LastTransitionResult?.Succeeded, Is.True);
        }

        private void CreateChannelRig(
            out ScreenFadeChannel channel,
            out ScreenFadeChannelHandler handler,
            out ScreenFader fader,
            out CanvasGroup canvasGroup)
        {
            channel = Track(ScriptableObject.CreateInstance<ScreenFadeChannel>());
            GameObject root = Track(new GameObject("Screen Fade Channel Rig"));
            root.SetActive(false);
            canvasGroup = root.AddComponent<CanvasGroup>();
            fader = root.AddComponent<ScreenFader>();
            handler = root.AddComponent<ScreenFadeChannelHandler>();
            SetField(handler, "channel", channel);
            SetField(handler, "screenFader", fader);
            root.SetActive(true);
        }

        private ScreenFader CreateFader(
            string name,
            out CanvasGroup canvasGroup)
        {
            GameObject root = Track(new GameObject(name));
            root.SetActive(false);
            canvasGroup = root.AddComponent<CanvasGroup>();
            ScreenFader fader = root.AddComponent<ScreenFader>();
            root.SetActive(true);
            return fader;
        }

        private SceneFlowManager CreateSceneFlowManager()
        {
            GameObject root = Track(new GameObject("Scene Flow Manager"));
            root.SetActive(false);
            return root.AddComponent<SceneFlowManager>();
        }

        private void CreateDestinationDefinition(Action onEntered)
        {
            GameObject root = Track(new GameObject("Transition Definition"));
            SceneManager.MoveGameObjectToScene(root, destinationScene);
            SceneTransitionDefinition definition =
                root.AddComponent<SceneTransitionDefinition>();
            UnityEvent entered = new();
            entered.AddListener(onEntered.Invoke);
            SetField(definition, "onEntered", entered);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
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
