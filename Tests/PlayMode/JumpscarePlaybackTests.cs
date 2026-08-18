using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Jumpscare;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class JumpscarePlaybackTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();
        private Action<JumpscareEvent> startedHandler;
        private Action<JumpscareEvent> finishedHandler;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            JumpscareEvent.OnJumpscareStarted -= startedHandler;
            JumpscareEvent.OnJumpscareFinished -= finishedHandler;
            startedHandler = null;
            finishedHandler = null;

            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.Destroy(createdObjects[index]);
                }
            }

            createdObjects.Clear();
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Play_RaisesFullLifecycleAndRestoresRevealTargets()
        {
            GameObject root = Track(new GameObject("Jumpscare"));
            GameObject primary = Track(new GameObject("Primary Reveal"));
            GameObject secondary = Track(new GameObject("Secondary Reveal"));
            primary.SetActive(false);
            secondary.SetActive(false);
            Light revealLight = root.AddComponent<Light>();
            revealLight.enabled = false;
            JumpscareEvent sequence = root.AddComponent<JumpscareEvent>();
            var lifecycle = new List<string>();

            SetField(sequence, "scareObject", primary);
            SetField(sequence, "additionalScareObjects", new[] { secondary });
            SetField(sequence, "revealLights", new[] { revealLight });
            SetField(sequence, "visibleDuration", 0.02f);
            SetField(sequence, "recoveryDuration", 0f);
            SetField(sequence, "onAnticipation", Event(() => lifecycle.Add("anticipation")));
            SetField(sequence, "onStarted", Event(() => lifecycle.Add("local-started")));
            SetField(sequence, "onRevealed", Event(() => lifecycle.Add("revealed")));
            SetField(sequence, "onCleanedUp", Event(() => lifecycle.Add("cleaned")));
            SetField(sequence, "onFinished", Event(() => lifecycle.Add("local-finished")));
            startedHandler = _ => lifecycle.Add("global-started");
            finishedHandler = _ => lifecycle.Add("global-finished");
            JumpscareEvent.OnJumpscareStarted += startedHandler;
            JumpscareEvent.OnJumpscareFinished += finishedHandler;

            sequence.Play();

            Assert.That(sequence.IsRunning, Is.True);
            Assert.That(primary.activeSelf, Is.True);
            Assert.That(secondary.activeSelf, Is.True);
            Assert.That(revealLight.enabled, Is.True);

            for (int frame = 0; frame < 120 && sequence.IsRunning; frame++)
            {
                yield return null;
            }

            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(primary.activeSelf, Is.False);
            Assert.That(secondary.activeSelf, Is.False);
            Assert.That(revealLight.enabled, Is.False);
            Assert.That(lifecycle, Is.EqualTo(new[]
            {
                "anticipation",
                "global-started",
                "local-started",
                "revealed",
                "cleaned",
                "global-finished",
                "local-finished",
            }));

            sequence.Stop();
            Assert.That(lifecycle, Has.Count.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator Stop_DuringFadeCancelsAndRestoresPresentation()
        {
            GameObject root = Track(new GameObject("Jumpscare"));
            GameObject primary = Track(new GameObject("Primary Reveal"));
            primary.SetActive(false);
            Transform shakeTarget = Track(new GameObject("Shake Target")).transform;
            Vector3 originalPosition = new(2f, 3f, 4f);
            shakeTarget.localPosition = originalPosition;
            ScreenFader fader = CreateFader(out CanvasGroup fadeCanvas);
            JumpscareEvent sequence = root.AddComponent<JumpscareEvent>();
            int cleanedCount = 0;
            int finishedCount = 0;

            SetField(sequence, "scareObject", primary);
            SetField(sequence, "shakeTarget", shakeTarget);
            SetField(sequence, "shakeDuration", 5f);
            SetField(sequence, "shakeAmplitude", 0.25f);
            SetField(sequence, "visibleDuration", 0f);
            SetField(sequence, "fader", fader);
            SetField(sequence, "onCleanedUp", Event(() => cleanedCount++));
            SetField(sequence, "onFinished", Event(() => finishedCount++));

            sequence.Play();
            for (int frame = 0; frame < 60 && !fader.IsFading; frame++)
            {
                yield return null;
            }

            Assert.That(sequence.IsRunning, Is.True);
            Assert.That(primary.activeSelf, Is.True);
            Assert.That(fader.IsFading, Is.True);

            sequence.Stop();

            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(primary.activeSelf, Is.False);
            Assert.That(shakeTarget.localPosition, Is.EqualTo(originalPosition));
            Assert.That(fader.IsFading, Is.False);
            Assert.That(fadeCanvas.alpha, Is.Zero);
            Assert.That(cleanedCount, Is.EqualTo(1));
            Assert.That(finishedCount, Is.EqualTo(1));

            yield return null;
            Assert.That(finishedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Stop_DuringAnticipationDoesNotMoveUnstartedShakeTarget()
        {
            GameObject root = Track(new GameObject("Jumpscare"));
            Transform shakeTarget = Track(new GameObject("Shake Target")).transform;
            Vector3 originalPosition = new(-3f, 1.5f, 8f);
            shakeTarget.localPosition = originalPosition;
            JumpscareEvent sequence = root.AddComponent<JumpscareEvent>();
            int startedCount = 0;
            int finishedCount = 0;

            SetField(sequence, "shakeTarget", shakeTarget);
            SetField(sequence, "startDelay", 10f);
            SetField(sequence, "onStarted", Event(() => startedCount++));
            SetField(sequence, "onFinished", Event(() => finishedCount++));

            sequence.Play();
            Assert.That(sequence.IsRunning, Is.True);

            sequence.Stop();

            Assert.That(sequence.IsRunning, Is.False);
            Assert.That(startedCount, Is.Zero);
            Assert.That(finishedCount, Is.EqualTo(1));
            Assert.That(shakeTarget.localPosition, Is.EqualTo(originalPosition));
            yield return null;
        }

        private ScreenFader CreateFader(out CanvasGroup canvasGroup)
        {
            GameObject root = Track(new GameObject("Screen Fader"));
            canvasGroup = root.AddComponent<CanvasGroup>();
            return root.AddComponent<ScreenFader>();
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static UnityEvent Event(UnityAction listener)
        {
            UnityEvent result = new();
            result.AddListener(listener);
            return result;
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
