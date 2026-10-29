using System;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Setup;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class GameObjectSetupMenuTests
    {
        private GameObject target;
        private int undoGroup;

        [SetUp]
        public void SetUp()
        {
            target = new GameObject("Setup test");
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            // Revert structural Undo records while their owners are still alive. Leaving
            // records for destroyed test objects can crash Unity's test-run cleanup.
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(undoGroup);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void EverySetupCanBeRepeatedWithoutDuplicatingComponentsOrCallbacks(
            [Values] GameObjectSetupMenu.SetupKind kind)
        {
            Assert.That(GameObjectSetupMenu.Attach(target, kind), Is.True);
            string first = Snapshot();
            Assert.That(GameObjectSetupMenu.Attach(target, kind), Is.True);
            Assert.That(Snapshot(), Is.EqualTo(first));
        }

        [Test]
        public void DialogueNPCWiresInteractionPlayerAndResolverAndCanBeUndone()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.DialogueNPC);
            Undo.CollapseUndoOperations(group);
            var npc = target.GetComponent<NPCDialogueBehaviour>();
            Assert.That(npc, Is.Not.Null);
            using var data = new SerializedObject(npc);
            Assert.That(data.FindProperty("dialogueEventPlayer").objectReferenceValue,
                Is.SameAs(target.GetComponent<DialogueEventPlayer>()));
            Assert.That(data.FindProperty("dialogueResolver").objectReferenceValue,
                Is.SameAs(target.GetComponent<DialogueTreeResolver>()));
            using var interaction = new SerializedObject(target.GetComponent<Interactable>());
            var calls = interaction.FindProperty("onInteractionSucceeded.m_PersistentCalls.m_Calls");
            Assert.That(calls.arraySize, Is.EqualTo(1));
            Assert.That(calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_Target").objectReferenceValue, Is.SameAs(npc));
            Assert.That(calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_MethodName").stringValue, Is.EqualTo("StartDialogue"));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(target.GetComponents<Component>().Length, Is.EqualTo(1));
        }

        [Test]
        public void HierarchyMenuAppliesToSelectionAsOneUndoAction()
        {
            var other = new GameObject("Second setup test");
            Object[] previous = Selection.objects;
            try
            {
                Selection.objects = new Object[] { target, other };
                Assert.That(EditorApplication.ExecuteMenuItem("GameObject/Quiet Static/Attach Setup/Interactions/Basic"), Is.True);
                Assert.That(target.GetComponent<Interactable>(), Is.Not.Null);
                Assert.That(other.GetComponent<Interactable>(), Is.Not.Null);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(target.GetComponent<Interactable>(), Is.Null);
                Assert.That(other.GetComponent<Interactable>(), Is.Null);
            }
            finally
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Selection.objects = previous;
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ExistingPromptCallbacksAndColliderArePreserved()
        {
            var interaction = target.AddComponent<Interactable>();
            var collider = target.AddComponent<SphereCollider>();
            collider.radius = 3f;
            using var data = new SerializedObject(interaction);
            data.FindProperty("displayName").stringValue = "Authored prompt";
            data.ApplyModifiedPropertiesWithoutUndo();
            GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.Dialogue);
            GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.Audio);
            Assert.That(interaction.DisplayName, Is.EqualTo("Authored prompt"));
            Assert.That(target.GetComponents<Collider>(), Is.EqualTo(new[] { collider }));
            data.Update();
            Assert.That(data.FindProperty("onInteractionSucceeded.m_PersistentCalls.m_Calls").arraySize, Is.EqualTo(2));
            Assert.That(target.GetComponent<AudioSource>().playOnAwake, Is.False);
        }

        [Test]
        public void DifferentInteractionTypesAreNotStacked()
        {
            target.AddComponent<HoldInteractable>();
            LogAssert.Expect(LogType.Warning, "Skipped Setup test: it already has a different interaction type.");
            Assert.That(GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.BasicInteraction), Is.False);
            Assert.That(target.GetComponent<Interactable>(), Is.Null);
        }

        [Test]
        public void MultipleMovementBehavioursRequireExplicitModes()
        {
            GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.WanderNPC);
            LogAssert.Expect(LogType.Warning, "Skipped Setup test: configure NPC modes before combining movement behaviours.");
            Assert.That(GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.FollowNPC), Is.False);
            Assert.That(target.GetComponent<NPCFollowBehaviour>(), Is.Null);
        }

        [Test]
        public void ExistingFixedNPCConversationIsNotReplacedByEmptyResolver()
        {
            var tree = ScriptableObject.CreateInstance<DialogueTree>();
            try
            {
                var npc = target.AddComponent<NPCDialogueBehaviour>();
                npc.SetDialogueTree(tree);
                GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.DialogueNPC);
                Assert.That(npc.DialogueTree, Is.SameAs(tree));
                Assert.That(target.GetComponent<DialogueTreeResolver>(), Is.Null);
            }
            finally { Object.DestroyImmediate(tree); }
        }

        private string Snapshot() => string.Join("\n", target.GetComponents<Component>()
            .Select(component => component.GetType().FullName + EditorJsonUtility.ToJson(component)));
    }
}
