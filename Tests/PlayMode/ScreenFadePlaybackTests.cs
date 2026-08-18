using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using UnityEngine;
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (capturedChannel != null && captureHandler != null)
            {
                capturedChannel.FadeRequested -= captureHandler;
            }

            capturedChannel = null;
            captureHandler = null;

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
                out _);
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
            Assert.That(channel.HasReceiver, Is.True,
                "Only the test probe should remain subscribed after the handler disables.");
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
