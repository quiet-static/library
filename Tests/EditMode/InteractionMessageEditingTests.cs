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

        private static IEnumerable<(string Label, SerializedProperty Text)> MessageFields(SerializedObject data) =>
            (IEnumerable<(string Label, SerializedProperty Text)>)typeof(InteractableExplorerWindow)
                .GetMethod("EventMessages", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { data });
    }
}
