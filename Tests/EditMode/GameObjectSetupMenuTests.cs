using System;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Setup;
using QuietStatic.Toolkit.Editor.Tooling;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
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
            var interactable = target.GetComponent<Interactable>();
            Assert.That(interactable.DialogueAction, Is.SameAs(npc));
            using var interaction = new SerializedObject(interactable);
            var calls = interaction.FindProperty("onInteractionSucceeded.m_PersistentCalls.m_Calls");
            Assert.That(calls.arraySize, Is.Zero);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(target.GetComponents<Component>().Length, Is.EqualTo(1));
        }

        [Test]
        public void DialogueNPCPreservesUnityEventWiringOnExistingInteractions()
        {
            var interactable = target.AddComponent<Interactable>();
            GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.DialogueNPC);

            Assert.That(interactable.DialogueAction, Is.Null);
            using var interaction = new SerializedObject(interactable);
            var calls = interaction.FindProperty("onInteractionSucceeded.m_PersistentCalls.m_Calls");
            Assert.That(calls.arraySize, Is.EqualTo(1));
            Assert.That(calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_Target").objectReferenceValue,
                Is.SameAs(target.GetComponent<NPCDialogueBehaviour>()));
            Assert.That(calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_MethodName").stringValue,
                Is.EqualTo("StartDialogue"));
        }

        [Test]
        public void DialogueTriggerAddsStandaloneVolumeAndCanBeUndone()
        {
            target.layer = 2;
            var solidCollider = target.AddComponent<SphereCollider>();
            Assert.That(GameObjectSetupMenu.ApplyToTargets(new[] { target }, GameObjectSetupMenu.SetupKind.DialogueTrigger),
                Is.EqualTo(1));

            var player = target.GetComponent<DialogueEventPlayer>();
            using var data = new SerializedObject(player);
            Assert.That(data.FindProperty("startOnTriggerEnter").boolValue, Is.True);
            Assert.That(data.FindProperty("requiredTag").stringValue, Is.EqualTo("Player"));
            Assert.That(data.FindProperty("triggerOnce").boolValue, Is.True);
            Assert.That(target.GetComponent<BoxCollider>().isTrigger, Is.True);
            Assert.That(solidCollider.isTrigger, Is.False);
            Assert.That(target.GetComponent<DialogueTreeResolver>(), Is.Null);
            Assert.That(target.GetComponent<Interactable>(), Is.Null);
            Assert.That(target.GetComponent<InteractionHighlighter>(), Is.Null);
            Assert.That(target.layer, Is.EqualTo(2));

            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(target.GetComponent<DialogueEventPlayer>(), Is.Null);
            Assert.That(target.GetComponents<Collider>(), Is.EqualTo(new[] { solidCollider }));
        }

        [Test]
        public void DialogueTriggerPreservesAnAuthoredPlayerAndVolume()
        {
            var player = target.AddComponent<DialogueEventPlayer>();
            target.AddComponent<BoxCollider>().isTrigger = true;
            var tree = ScriptableObject.CreateInstance<DialogueTree>();
            try
            {
                using var data = new SerializedObject(player);
                data.FindProperty("dialogueTree").objectReferenceValue = tree;
                data.FindProperty("requiredTag").stringValue = "Untagged";
                data.FindProperty("triggerOnce").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
                string authored = Snapshot();

                Assert.That(GameObjectSetupMenu.Attach(target, GameObjectSetupMenu.SetupKind.DialogueTrigger), Is.True);
                Assert.That(Snapshot(), Is.EqualTo(authored));
            }
            finally { Object.DestroyImmediate(tree); }
        }

        [Test]
        public void DialogueTriggerSkipsVolumesWithinAManualInteractionHierarchy()
        {
            target.AddComponent<Interactable>();
            var volume = new GameObject("Dialogue volume");
            volume.transform.SetParent(target.transform);
            LogAssert.Expect(LogType.Warning,
                "Skipped Dialogue volume: put the Dialogue Trigger on a separate object outside the interaction target's hierarchy.");
            Assert.That(GameObjectSetupMenu.Attach(volume, GameObjectSetupMenu.SetupKind.DialogueTrigger), Is.False);
            Assert.That(volume.GetComponent<Collider>(), Is.Null);
            Assert.That(volume.GetComponent<DialogueEventPlayer>(), Is.Null);
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
        public void InspectSceneContentMenuSwitchesAnAlreadyOpenWorkspace()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Opening an EditorWindow requires a graphics device.");

            const string tabKey = "QuietStatic.ContentWorkspace.SelectedTab";
            string previousTab = EditorPrefs.GetString(tabKey);
            Object[] previousSelection = Selection.objects;
            var window = EditorWindow.GetWindow<ContentWorkspaceWindow>("Content Workspace");
            try
            {
                window.CreateGUI();
                ContentWorkspaceWindow.Open("dialogue");
                var heading = window.rootVisualElement.Q<Label>("workspace-view-heading");
                Assert.That(heading.text, Is.EqualTo("Dialogue"));

                var storyMenu = window.rootVisualElement.Query<ToolbarMenu>().ToList()
                    .Single(menu => menu.text == "Story");
                Assert.That(storyMenu.menu.MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                    Does.Contain("Cinematic Trees"));
                ContentWorkspaceWindow.Open("cinematic-trees");
                Assert.That(heading.text, Is.EqualTo("Cinematic Trees"));

                Selection.activeGameObject = target;
                Assert.That(EditorApplication.ExecuteMenuItem("GameObject/Quiet Static/Inspect Scene Content"), Is.True);
                Assert.That(heading.text, Is.EqualTo("Scene Content"));
            }
            finally
            {
                window.Close();
                Selection.objects = previousSelection;
                if (string.IsNullOrEmpty(previousTab)) EditorPrefs.DeleteKey(tabKey);
                else EditorPrefs.SetString(tabKey, previousTab);
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
