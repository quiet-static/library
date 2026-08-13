using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class DialogueRunnerIntegrationTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();
        private Action<DialogueRunner> startedHandler;
        private Action<DialogueRunner, DialogueTree.Node> nodeChangedHandler;
        private Action<DialogueRunner> endedHandler;
        private Action<UnityEngine.Object, Transform> managerStartedHandler;
        private Action<UnityEngine.Object> managerEndedHandler;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DialogueRunner.OnDialogueStarted -= startedHandler;
            DialogueRunner.OnNodeChanged -= nodeChangedHandler;
            DialogueRunner.OnDialogueEnded -= endedHandler;
            DialogueManager.OnDialogueStarted -= managerStartedHandler;
            DialogueManager.OnDialogueEnded -= managerEndedHandler;
            startedHandler = null;
            nodeChangedHandler = null;
            endedHandler = null;
            managerStartedHandler = null;
            managerEndedHandler = null;

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.Destroy(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Choice_TraversesTreeSetsFlagsAndRaisesLifecycleEvents()
        {
            var managerObject = Track(new GameObject("Flag Manager"));
            FlagManager flagManager = managerObject.AddComponent<FlagManager>();

            DialogueTree tree = Track(CreateTree(
                new DialogueTree.Node
                {
                    id = "start",
                    flagsToSetOnEnter = new[] { "met_npc" },
                    choices = new[]
                    {
                        new DialogueTree.Choice
                        {
                            text = "Continue",
                            nextNodeIndex = 1,
                            flagsToSet = new[] { "accepted" }
                        }
                    }
                },
                new DialogueTree.Node
                {
                    id = "end",
                    nextNodeIndex = -1,
                    flagsToSetOnEnter = new[] { "reached_end" }
                }));

            var runnerObject = Track(new GameObject("Dialogue Runner"));
            DialogueRunner runner = runnerObject.AddComponent<DialogueRunner>();
            runner.SetTree(tree);

            var lifecycle = new List<string>();
            startedHandler = _ => lifecycle.Add("started");
            nodeChangedHandler = (_, node) => lifecycle.Add($"node:{node.id}");
            endedHandler = _ => lifecycle.Add("ended");

            DialogueRunner.OnDialogueStarted += startedHandler;
            DialogueRunner.OnNodeChanged += nodeChangedHandler;
            DialogueRunner.OnDialogueEnded += endedHandler;

            runner.StartDialogue();
            runner.Choose(0);
            runner.Advance();

            CollectionAssert.AreEqual(
                new[] { "started", "node:start", "node:end", "ended" },
                lifecycle);
            Assert.That(flagManager.HasAll(new[] { "met_npc", "accepted", "reached_end" }), Is.True);
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.CurrentNode, Is.Null);

            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingFlagManager_ClearsSingletonForTheNextSceneInstance()
        {
            var firstObject = Track(new GameObject("First Flag Manager"));
            FlagManager first = firstObject.AddComponent<FlagManager>();
            Assert.That(FlagManager.Instance, Is.SameAs(first));

            UnityEngine.Object.Destroy(firstObject);
            yield return null;

            Assert.That(FlagManager.Instance, Is.Null);

            var secondObject = Track(new GameObject("Second Flag Manager"));
            FlagManager second = secondObject.AddComponent<FlagManager>();
            Assert.That(FlagManager.Instance, Is.SameAs(second));
        }

        [UnityTest]
        public IEnumerator Manager_DerivesActiveTreeAndStateFromItsRunner()
        {
            DialogueTree tree = Track(CreateTree(
                new DialogueTree.Node
                {
                    id = "only",
                    nextNodeIndex = -1
                }));
            var managerObject = Track(new GameObject("Dialogue Manager"));
            DialogueRunner runner = managerObject.AddComponent<DialogueRunner>();
            DialogueManager manager = managerObject.AddComponent<DialogueManager>();
            var lifecycle = new List<string>();

            managerStartedHandler = (startedTree, _) =>
                lifecycle.Add($"started:{startedTree.name}");
            managerEndedHandler = endedTree =>
                lifecycle.Add($"ended:{endedTree.name}");
            DialogueManager.OnDialogueStarted += managerStartedHandler;
            DialogueManager.OnDialogueEnded += managerEndedHandler;

            Assert.That(manager.StartDialogue(tree), Is.True);
            Assert.That(manager.IsDialogueActive, Is.True);
            Assert.That(manager.CurrentDialogueTree, Is.SameAs(tree));
            Assert.That(runner.IsRunning, Is.True);

            manager.AdvanceDialogue();

            Assert.That(manager.IsDialogueActive, Is.False);
            Assert.That(manager.CurrentDialogueTree, Is.Null);
            Assert.That(runner.IsRunning, Is.False);
            CollectionAssert.AreEqual(
                new[] { $"started:{tree.name}", $"ended:{tree.name}" },
                lifecycle);

            yield return null;
        }

        private static DialogueTree CreateTree(params DialogueTree.Node[] nodes)
        {
            DialogueTree tree = ScriptableObject.CreateInstance<DialogueTree>();
            SetPrivateField(tree, "nodes", nodes);
            SetPrivateField(tree, "startNodeIndex", 0);
            return tree;
        }

        private T Track<T>(T created) where T : UnityEngine.Object
        {
            createdObjects.Add(created);
            return created;
        }

        private static void SetPrivateField<T>(DialogueTree tree, string fieldName, T value)
        {
            FieldInfo field = typeof(DialogueTree).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                throw new MissingFieldException(typeof(DialogueTree).FullName, fieldName);
            }

            field.SetValue(tree, value);
        }
    }
}
