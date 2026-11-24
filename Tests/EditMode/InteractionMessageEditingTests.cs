using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Interactions;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class InteractionMessageEditingTests
    {
        [Test]
        public void EventTextEditingPreservesWiringAndSupportsUndo()
        {
            var owner = new GameObject("Message editing test");
            var channel = ScriptableObject.CreateInstance<InteractionUIChannel>();
            try
            {
                var interaction = owner.AddComponent<Interactable>();
                var callbacks = new UnityEvent();
                typeof(Interactable).GetField("onInteractionFailed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(interaction, callbacks);
                UnityEventTools.AddStringPersistentListener(callbacks, channel.ShowMessage, "Original message");
                UnityEventTools.AddVoidPersistentListener(callbacks, channel.HidePrompt);
                using var data = new SerializedObject(interaction);
                var fields = MessageFields(data).ToArray();
                Assert.That(fields.Length, Is.EqualTo(1), "Only supported string message calls should be exposed.");
                Assert.That(fields[0].Label, Does.Contain("On Interaction Failed"));
                string before = EditorJsonUtility.ToJson(interaction);
                Undo.IncrementCurrentGroup();
                fields[0].Text.stringValue = "Edited message";
                data.ApplyModifiedProperties();
                Assert.That(EditorJsonUtility.ToJson(interaction), Is.EqualTo(before.Replace("Original message", "Edited message")));
                Assert.That(callbacks.GetPersistentTarget(0), Is.SameAs(channel));
                Assert.That(callbacks.GetPersistentMethodName(0), Is.EqualTo("ShowMessage"));
                Assert.That(callbacks.GetPersistentMethodName(1), Is.EqualTo("HidePrompt"));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(EditorJsonUtility.ToJson(interaction), Is.EqualTo(before));
            }
            finally
            {
                Undo.ClearUndo(owner.GetComponent<Interactable>());
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(channel);
            }
        }

        [TestCase(typeof(Interactable), "onInteractionSucceeded")]
        [TestCase(typeof(Interactable), "onInteractionFailed")]
        [TestCase(typeof(HoldInteractable), "onCompleted")]
        [TestCase(typeof(HoldInteractable), "onRequirementFailed")]
        [TestCase(typeof(ActivatedProgressInteractable), "onCompleted")]
        [TestCase(typeof(ActivatedProgressInteractable), "onRequirementFailed")]
        public void AddOutcomeMessagePreservesExistingListenersAndSupportsUndo(Type type, string eventName)
        {
            var owner = new GameObject("Add outcome message test");
            var channel = ScriptableObject.CreateInstance<InteractionUIChannel>();
            Component interaction = owner.AddComponent(type);
            try
            {
                var callbacks = new UnityEvent();
                type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(interaction, callbacks);
                UnityEventTools.AddVoidPersistentListener(callbacks, channel.HidePrompt);
                string before = EditorJsonUtility.ToJson(interaction);
                using var data = new SerializedObject(interaction);
                Undo.IncrementCurrentGroup();
                typeof(InteractionOutcomeEditor).GetMethod("AddEventMessage", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { data, eventName, channel });
                data.ApplyModifiedProperties();
                Assert.That(callbacks.GetPersistentEventCount(), Is.EqualTo(2));
                Assert.That(callbacks.GetPersistentMethodName(0), Is.EqualTo("HidePrompt"));
                Assert.That(callbacks.GetPersistentTarget(1), Is.SameAs(channel));
                Assert.That(callbacks.GetPersistentMethodName(1), Is.EqualTo("ShowMessage"));
                var messages = MessageFields(data).ToArray();
                Assert.That(messages.Length, Is.EqualTo(1));
                messages[0].Text.stringValue = "Outcome message";
                data.ApplyModifiedProperties();
                Assert.That(MessageFields(data).Single().Text.stringValue, Is.EqualTo("Outcome message"));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(EditorJsonUtility.ToJson(interaction), Is.EqualTo(before));
            }
            finally
            {
                Undo.ClearUndo(interaction);
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(channel);
            }
        }

        [TestCase(typeof(Interactable), "onInteractionSucceeded")]
        [TestCase(typeof(Interactable), "onInteractionFailed")]
        [TestCase(typeof(HoldInteractable), "onCompleted")]
        [TestCase(typeof(HoldInteractable), "onRequirementFailed")]
        [TestCase(typeof(ActivatedProgressInteractable), "onCompleted")]
        [TestCase(typeof(ActivatedProgressInteractable), "onRequirementFailed")]
        public void RemoveOutcomeMessagePreservesOtherCallbacksAndSupportsUndo(Type type, string eventName)
        {
            var owner = new GameObject("Remove outcome message test");
            var channel = ScriptableObject.CreateInstance<InteractionUIChannel>();
            Component interaction = owner.AddComponent(type);
            try
            {
                var callbacks = new UnityEvent();
                type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(interaction, callbacks);
                UnityEventTools.AddVoidPersistentListener(callbacks, channel.HidePrompt);
                UnityEventTools.AddStringPersistentListener(callbacks, channel.ShowMessage, "Remove this message");
                UnityEventTools.AddStringPersistentListener(callbacks, channel.ShowMessage, "Keep this message");
                string before = EditorJsonUtility.ToJson(interaction);
                using var data = new SerializedObject(interaction);
                Undo.IncrementCurrentGroup();
                typeof(InteractionOutcomeEditor).GetMethod("RemoveEventMessage", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { MessageFields(data).First().Text });
                data.ApplyModifiedProperties();
                Assert.That(callbacks.GetPersistentEventCount(), Is.EqualTo(2));
                Assert.That(callbacks.GetPersistentMethodName(0), Is.EqualTo("HidePrompt"));
                Assert.That(callbacks.GetPersistentTarget(1), Is.SameAs(channel));
                Assert.That(callbacks.GetPersistentMethodName(1), Is.EqualTo("ShowMessage"));
                Assert.That(MessageFields(data).Single().Text.stringValue, Is.EqualTo("Keep this message"));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(EditorJsonUtility.ToJson(interaction), Is.EqualTo(before));
            }
            finally
            {
                Undo.ClearUndo(interaction);
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(channel);
            }
        }

        [TestCase(typeof(Interactable), "onInteractionSucceeded")]
        [TestCase(typeof(Interactable), "onInteractionFailed")]
        [TestCase(typeof(HoldInteractable), "onCompleted")]
        [TestCase(typeof(HoldInteractable), "onRequirementFailed")]
        [TestCase(typeof(ActivatedProgressInteractable), "onCompleted")]
        [TestCase(typeof(ActivatedProgressInteractable), "onRequirementFailed")]
        public void AddingConditionalMessagesWiresOutcomeAndSupportsUndo(Type type, string eventName)
        {
            var owner = new GameObject("Conditional message authoring test");
            var channel = ScriptableObject.CreateInstance<InteractionUIChannel>();
            var interaction = owner.AddComponent(type);
            try
            {
                var callbacks = new UnityEvent();
                type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(interaction, callbacks);
                UnityEventTools.AddVoidPersistentListener(callbacks, channel.HidePrompt);
                string before = EditorJsonUtility.ToJson(interaction);
                typeof(InteractionOutcomeEditor).GetMethod("AddConditionalMessage", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { interaction, eventName, channel });
                var conditional = owner.GetComponent<ConditionalInteractionMessage>();
                Assert.That(conditional, Is.Not.Null);
                Assert.That(conditional.Rules.Count, Is.EqualTo(1));
                Assert.That(callbacks.GetPersistentEventCount(), Is.EqualTo(2));
                Assert.That(callbacks.GetPersistentMethodName(0), Is.EqualTo("HidePrompt"));
                Assert.That(callbacks.GetPersistentMethodName(1), Is.EqualTo("ShowMessage"));
                Assert.That(callbacks.GetPersistentTarget(1), Is.SameAs(conditional));
                using (var settings = new SerializedObject(conditional))
                    Assert.That(settings.FindProperty("channel").objectReferenceValue, Is.SameAs(channel));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(owner.GetComponent<ConditionalInteractionMessage>(), Is.Null);
                Assert.That(EditorJsonUtility.ToJson(interaction), Is.EqualTo(before));
            }
            finally
            {
                Undo.ClearUndo(interaction);
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(channel);
            }
        }

        [Test]
        public void AddingConditionalRuleClearsInheritedRequirementsAndMessage()
        {
            var owner = new GameObject("Conditional rule editing test");
            try
            {
                var conditional = owner.AddComponent<ConditionalInteractionMessage>();
                using var data = new SerializedObject(conditional);
                var add = typeof(InteractionOutcomeEditor).GetMethod("AddConditionalRule", BindingFlags.Static | BindingFlags.NonPublic);
                add.Invoke(null, new object[] { data });
                var first = data.FindProperty("rules").GetArrayElementAtIndex(0);
                first.FindPropertyRelative("label").stringValue = "Existing rule";
                first.FindPropertyRelative("message").stringValue = "Existing message";
                var requirement = first.FindPropertyRelative("requirement");
                requirement.FindPropertyRelative("mode").enumValueIndex = 1;
                requirement.FindPropertyRelative("flags").arraySize = 1;
                requirement.FindPropertyRelative("flags").GetArrayElementAtIndex(0).stringValue = "existing.flag";
                data.ApplyModifiedProperties();
                add.Invoke(null, new object[] { data });
                data.ApplyModifiedProperties();
                Assert.That(conditional.Rules.Count, Is.EqualTo(2));
                Assert.That(conditional.Rules[0].Message, Is.EqualTo("Existing message"));
                Assert.That(conditional.Rules[0].Requirement.Flags, Is.EqualTo(new[] { "existing.flag" }));
                Assert.That(conditional.Rules[1].Message, Is.Empty);
                Assert.That(conditional.Rules[1].Label, Is.Empty);
                Assert.That(conditional.Rules[1].Requirement.IsConfigured, Is.False);
                data.FindProperty("rules").DeleteArrayElementAtIndex(0);
                data.ApplyModifiedProperties();
                Assert.That(conditional.Rules.Count, Is.EqualTo(1));
                Assert.That(conditional.Rules[0].Message, Is.Empty);
            }
            finally
            {
                Undo.ClearUndo(owner.GetComponent<ConditionalInteractionMessage>());
                Object.DestroyImmediate(owner);
            }
        }

        private static IEnumerable<(string Label, SerializedProperty Text)> MessageFields(SerializedObject data) =>
            (IEnumerable<(string Label, SerializedProperty Text)>)typeof(InteractionOutcomeEditor)
                .GetMethod("EventMessages", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { data });
    }
}
