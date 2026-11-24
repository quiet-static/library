using System;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Dialogue;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Batch = QuietStatic.Toolkit.Editor.Characters.NPC.NPCPerformanceAuthoring;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NPCPerformanceAuthoringTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder("Assets", $"__NPCPerformance_{Guid.NewGuid():N}"));
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void ExternalDialogueReactionsPreviewTheirOwnNpcControllersAndApplyWithUndo()
        {
            AnimatorController compatible = CreateController("With wave", "wave");
            var overrides = new AnimatorOverrideController(compatible);
            try
            {
                DialogueNPCReaction first = MakeReaction("Sam", overrides);
                DialogueNPCReaction second = MakeReaction("Dan", CreateController("No wave", "other"));
                Component[] targets = { first, second };
                var patch = new Batch.Patch(Batch.Field.ReactionEndAnimation, "wave");

                var preview = Batch.Preview(targets, patch);
                Assert.That(preview[0].Status, Is.EqualTo(Batch.Status.Ready));
                Assert.That(preview[1].Status, Is.EqualTo(Batch.Status.Skipped));
                Assert.That(ReadEndAnimation(first), Is.EqualTo("idle"));

                var applied = Batch.Apply(targets, patch);
                Assert.That(applied[0].Status, Is.EqualTo(Batch.Status.Applied));
                Assert.That(applied[1].Status, Is.EqualTo(Batch.Status.Skipped));
                Assert.That(ReadEndAnimation(first), Is.EqualTo("wave"));
                Assert.That(ReadEndAnimation(second), Is.EqualTo("idle"));

                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(ReadEndAnimation(first), Is.EqualTo("idle"));
            }
            finally { Object.DestroyImmediate(overrides); }
        }

        [Test]
        public void StartingModeIsMatchedByNameInEachNpcRatherThanByListPosition()
        {
            NPCController first = new GameObject("First").AddComponent<NPCController>();
            NPCController second = new GameObject("Second").AddComponent<NPCController>();
            NPCModeController firstModes = MakeModes(first, "Idle", "Talk");
            NPCModeController secondModes = MakeModes(second, "Idle", "Walk", "Talk");
            var patch = new Batch.Patch(Batch.Field.StartupMode, "talk");

            var applied = Batch.Apply(new Component[] { first, second }, patch);

            Assert.That(applied[0].Status, Is.EqualTo(Batch.Status.Applied));
            Assert.That(applied[1].Status, Is.EqualTo(Batch.Status.Applied));
            using var firstData = new SerializedObject(firstModes);
            using var secondData = new SerializedObject(secondModes);
            Assert.That(firstData.FindProperty("startingMode").intValue, Is.EqualTo(1));
            Assert.That(secondData.FindProperty("startingMode").intValue, Is.EqualTo(2));
        }

        private AnimatorController CreateController(string name, string trigger)
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath($"{folder}/{name}.controller");
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            return controller;
        }

        private static DialogueNPCReaction MakeReaction(string name, RuntimeAnimatorController animations)
        {
            var npc = new GameObject(name).AddComponent<NPCController>();
            var animator = npc.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = animations;
            npc.gameObject.AddComponent<NPCAnimationTrigger>();
            var reaction = new GameObject(name + " Dialogue Volume").AddComponent<DialogueNPCReaction>();
            using var data = new SerializedObject(reaction);
            data.FindProperty("npc").objectReferenceValue = npc;
            data.FindProperty("animationOnEnd").stringValue = "idle";
            data.ApplyModifiedPropertiesWithoutUndo();
            return reaction;
        }

        private static string ReadEndAnimation(DialogueNPCReaction reaction)
        {
            using var data = new SerializedObject(reaction);
            return data.FindProperty("animationOnEnd").stringValue;
        }

        private static NPCModeController MakeModes(NPCController npc, params string[] names)
        {
            var mode = npc.gameObject.AddComponent<NPCModeController>();
            using var data = new SerializedObject(mode);
            SerializedProperty modes = data.FindProperty("modes");
            modes.arraySize = names.Length;
            for (int index = 0; index < names.Length; index++)
                modes.GetArrayElementAtIndex(index).FindPropertyRelative("modeName").stringValue = names[index];
            data.ApplyModifiedPropertiesWithoutUndo();
            return mode;
        }
    }
}
