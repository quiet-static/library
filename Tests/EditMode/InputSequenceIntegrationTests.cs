using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Minigames;
using QuietStatic.Toolkit.Pause;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace QuietStatic.Tests.EditMode
{
    public sealed class InputSequenceIntegrationTests
    {
        [Test]
        public void RequestChannel_StopsAfterFirstRunnerAcceptsRequest()
        {
            InputSequenceRequestChannel channel =
                ScriptableObject.CreateInstance<InputSequenceRequestChannel>();
            InputSequenceDefinition definition =
                ScriptableObject.CreateInstance<InputSequenceDefinition>();
            int rejectedCalls = 0;
            int acceptedCalls = 0;
            int laterCalls = 0;

            channel.StartRequested += requested =>
            {
                rejectedCalls++;
                return false;
            };
            channel.StartRequested += requested =>
            {
                acceptedCalls++;
                return requested == definition;
            };
            channel.StartRequested += requested =>
            {
                laterCalls++;
                return true;
            };

            Assert.That(channel.TryStart(definition), Is.True);
            Assert.That(rejectedCalls, Is.EqualTo(1));
            Assert.That(acceptedCalls, Is.EqualTo(1));
            Assert.That(laterCalls, Is.Zero);

            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(channel);
        }

        [Test]
        public void Activator_RoutesOnlyItsAcceptedSequenceResult()
        {
            InputSequenceRequestChannel channel =
                ScriptableObject.CreateInstance<InputSequenceRequestChannel>();
            InputSequenceDefinition requested =
                ScriptableObject.CreateInstance<InputSequenceDefinition>();
            InputSequenceDefinition unrelated =
                ScriptableObject.CreateInstance<InputSequenceDefinition>();
            var owner = new GameObject("Minigame activator test");
            owner.SetActive(false);
            InputSequenceMinigameActivator activator =
                owner.AddComponent<InputSequenceMinigameActivator>();
            SetField(activator, "requestChannel", channel);
            SetField(activator, "sequence", requested);

            int completed = 0;
            GetField<UnityEvent>(activator, "onCompleted")
                .AddListener(() => completed++);
            channel.StartRequested += definition => definition == requested;
            owner.SetActive(true);
            InvokePrivate(activator, "SubscribeToResults");

            Assert.That(activator.TryActivate(), Is.True);
            Assert.That(activator.IsAwaitingResult, Is.True);

            ReportFinished(channel, unrelated, InputSequenceOutcome.Completed);
            Assert.That(completed, Is.Zero);
            Assert.That(activator.IsAwaitingResult, Is.True);

            ReportFinished(channel, requested, InputSequenceOutcome.Completed);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(activator.IsAwaitingResult, Is.False);

            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(unrelated);
            UnityEngine.Object.DestroyImmediate(requested);
            UnityEngine.Object.DestroyImmediate(channel);
        }

        [Test]
        public void Runner_EnablesOwnedActionsAndReleasesThemOnCancellation()
        {
            var inputAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = inputAsset.AddActionMap("Minigame");
            InputAction action = map.AddAction(
                "Up",
                InputActionType.Button,
                "<Keyboard>/upArrow");
            InputActionReference actionReference =
                InputActionReference.Create(action);
            InputSequenceDefinition definition =
                ScriptableObject.CreateInstance<InputSequenceDefinition>();
            var step = new InputSequenceDefinition.Step();
            SetStepField(step, "action", actionReference);
            SetField(
                definition,
                "steps",
                new List<InputSequenceDefinition.Step> { step });

            var owner = new GameObject("Minigame runner test");
            InputSequenceMinigame runner =
                owner.AddComponent<InputSequenceMinigame>();
            SetField(runner, "sequence", definition);
            InputSequenceRequestChannel firstChannel =
                ScriptableObject.CreateInstance<InputSequenceRequestChannel>();
            InputSequenceRequestChannel secondChannel =
                ScriptableObject.CreateInstance<InputSequenceRequestChannel>();
            int firstChannelResults = 0;
            int secondChannelResults = 0;
            firstChannel.Finished += (sequence, result) =>
                firstChannelResults++;
            secondChannel.Finished += (sequence, result) =>
                secondChannelResults++;
            InputSequenceOutcome? outcome = null;
            runner.Finished += (source, sequence, result) => outcome = result;
            runner.SetRequestChannel(firstChannel);

            Assert.That(firstChannel.TryStart(definition), Is.True);
            Assert.That(runner.IsActive, Is.True);
            Assert.That(action.enabled, Is.True);

            runner.SetRequestChannel(secondChannel);
            runner.CancelMinigame();

            Assert.That(runner.IsActive, Is.False);
            Assert.That(action.enabled, Is.False);
            Assert.That(outcome, Is.EqualTo(InputSequenceOutcome.Cancelled));
            Assert.That(firstChannelResults, Is.EqualTo(1));
            Assert.That(secondChannelResults, Is.Zero);

            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(actionReference);
            UnityEngine.Object.DestroyImmediate(inputAsset);
            UnityEngine.Object.DestroyImmediate(secondChannel);
            UnityEngine.Object.DestroyImmediate(firstChannel);
        }

        [Test]
        public void PauseInputHandler_DisablePreservesExternallyEnabledAction()
        {
            var inputAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputAction action = inputAsset.AddActionMap("UI").AddAction("Pause");
            InputActionReference actionReference = InputActionReference.Create(action);
            var owner = new GameObject("Pause input ownership test");
            owner.SetActive(false);
            PauseInputHandler handler = owner.AddComponent<PauseInputHandler>();
            handler.SetPauseAction(actionReference);
            action.Enable();

            owner.SetActive(true);
            InvokePrivate(handler, "OnEnable");
            owner.SetActive(false);
            InvokePrivate(handler, "OnDisable");

            Assert.That(action.enabled, Is.True);

            action.Disable();
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(actionReference);
            UnityEngine.Object.DestroyImmediate(inputAsset);
        }

        [Test]
        public void PauseInputHandler_RebindsActiveActionAndReleasesOwnedActions()
        {
            var inputAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = inputAsset.AddActionMap("UI");
            InputAction firstAction = map.AddAction("Pause First");
            InputAction secondAction = map.AddAction("Pause Second");
            InputActionReference firstReference = InputActionReference.Create(firstAction);
            InputActionReference secondReference = InputActionReference.Create(secondAction);
            var owner = new GameObject("Pause input rebinding test");
            owner.SetActive(false);
            PauseInputHandler handler = owner.AddComponent<PauseInputHandler>();
            handler.SetPauseAction(firstReference);

            owner.SetActive(true);
            InvokePrivate(handler, "OnEnable");
            Assert.That(firstAction.enabled, Is.True);

            handler.SetPauseAction(secondReference);

            Assert.That(firstAction.enabled, Is.False);
            Assert.That(secondAction.enabled, Is.True);
            owner.SetActive(false);
            InvokePrivate(handler, "OnDisable");
            Assert.That(secondAction.enabled, Is.False);

            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(firstReference);
            UnityEngine.Object.DestroyImmediate(secondReference);
            UnityEngine.Object.DestroyImmediate(inputAsset);
        }

        private static void ReportFinished(
            InputSequenceRequestChannel channel,
            InputSequenceDefinition definition,
            InputSequenceOutcome outcome)
        {
            typeof(InputSequenceRequestChannel)
                .GetMethod(
                    "ReportFinished",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(channel, new object[] { definition, outcome });
        }

        private static T GetField<T>(object target, string name)
        {
            return (T)typeof(InputSequenceMinigameActivator)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static void SetStepField(
            InputSequenceDefinition.Step target,
            string name,
            object value)
        {
            typeof(InputSequenceDefinition.Step)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method '{name}'.");
            method.Invoke(target, null);
        }
    }
}
