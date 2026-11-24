using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cameras;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Cinematics;
using UnityEngine.Events;
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

            runner.DialogueStarted += startedHandler;
            runner.NodeChanged += nodeChangedHandler;
            runner.DialogueEnded += endedHandler;

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
            manager.DialogueStarted += managerStartedHandler;
            manager.DialogueEnded += managerEndedHandler;

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

        [UnityTest]
        public IEnumerator DisabledEventPlayer_DoesNotAdoptALaterRunOfTheSameTree()
        {
            DialogueTree tree = Track(CreateTree(
                new DialogueTree.Node
                {
                    id = "only",
                    nextNodeIndex = -1,
                }));
            var managerObject = Track(new GameObject("Dialogue Manager"));
            managerObject.AddComponent<DialogueRunner>();
            DialogueManager manager =
                managerObject.AddComponent<DialogueManager>();
            var firstObject = Track(new GameObject("First Dialogue Player"));
            DialogueEventPlayer first =
                firstObject.AddComponent<DialogueEventPlayer>();
            var secondObject = Track(new GameObject("Second Dialogue Player"));
            DialogueEventPlayer second =
                secondObject.AddComponent<DialogueEventPlayer>();
            int firstEnded = 0;
            int secondEnded = 0;
            first.DialogueEnded += (_, _) => firstEnded++;
            second.DialogueEnded += (_, _) => secondEnded++;

            Assert.That(
                first.TryStartDialogue(tree, null, null),
                Is.True);
            first.enabled = false;
            Assert.That(manager.StopDialogue(), Is.True);

            Assert.That(first.IsRunning, Is.False);
            Assert.That(firstEnded, Is.EqualTo(1));
            Assert.That(
                second.TryStartDialogue(tree, null, null),
                Is.True);
            first.enabled = true;
            first.ChooseDialogueOption(0);
            first.AdvanceDialogue();
            first.StopDialogue();
            Assert.That(second.IsRunning, Is.True);
            Assert.That(manager.StopDialogue(), Is.True);

            Assert.That(firstEnded, Is.EqualTo(1));
            Assert.That(secondEnded, Is.EqualTo(1));
            Assert.That(first.IsRunning, Is.False);
            Assert.That(second.IsRunning, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ANodeCallbackCanEndAndReplaceItsSessionWithoutReplayingTheFirstNode()
        {
            DialogueTree tree = Track(CreateTree(new DialogueTree.Node { id = "start", nextNodeIndex = -1 }));
            var managerObject = Track(new GameObject("Dialogue Manager"));
            managerObject.AddComponent<DialogueRunner>();
            managerObject.AddComponent<DialogueManager>();
            DialogueEventPlayer first = Track(new GameObject("First")).AddComponent<DialogueEventPlayer>();
            DialogueEventPlayer replacement = Track(new GameObject("Replacement")).AddComponent<DialogueEventPlayer>();
            var observed = new List<string>();
            first.NodeChanged += node =>
            {
                observed.Add("first:" + node.id);
                Assert.That(first.CurrentNode, Is.SameAs(node));
                first.StopDialogue();
            };
            first.DialogueEnded += (_, _) =>
            {
                observed.Add("first ended");
                Assert.That(replacement.TryStartDialogue(tree, null, null), Is.True);
            };
            replacement.NodeChanged += node => observed.Add("replacement:" + node.id);

            Assert.That(first.TryStartDialogue(tree, null, null), Is.True);
            CollectionAssert.AreEqual(new[] { "first:start", "first ended", "replacement:start" }, observed);
            Assert.That(first.IsRunning, Is.False);
            Assert.That(replacement.IsRunning, Is.True);
            replacement.StopDialogue();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DialogueReleasesOnlyItsOwnCameraFocus()
        {
            DialogueTree tree = Track(CreateTree(new DialogueTree.Node { id = "start", nextNodeIndex = -1 }));
            var managerObject = Track(new GameObject("Dialogue Manager"));
            managerObject.AddComponent<DialogueRunner>();
            managerObject.AddComponent<DialogueManager>();
            var cameraObject = Track(new GameObject("Camera Manager"));
            cameraObject.AddComponent<CameraController>();
            var focus = cameraObject.AddComponent<CameraFocusController>();
            var camera = cameraObject.AddComponent<CameraManager>();
            var player = Track(new GameObject("Dialogue Owner")).AddComponent<DialogueEventPlayer>();
            Transform target = Track(new GameObject("Target")).transform;

            Assert.That(player.TryStartDialogue(tree, null, null), Is.True);
            focus.BeginFocus(target);
            player.StopDialogue();
            Assert.That(focus.IsFocusing, Is.True);
            focus.EndFocus();

            Assert.That(player.TryStartDialogue(tree, target, null), Is.True);
            Assert.That(focus.IsFocusing, Is.True);
            player.StopDialogue();
            Assert.That(focus.IsFocusing, Is.False);

            Assert.That(player.TryStartDialogue(tree, target, null), Is.True);
            camera.BeginFocus(target);
            player.StopDialogue();
            Assert.That(focus.IsFocusing, Is.True);
            focus.EndFocus();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CinematicCues_FollowOwnedSessionIncludingFirstNodeBranchesAndReplay()
        {
            DialogueTree tree = Track(CreateTree(
                new DialogueTree.Node { id = "start", choices = new[]
                {
                    new DialogueTree.Choice { text = "Left", nextNodeIndex = 1 },
                    new DialogueTree.Choice { text = "Right", nextNodeIndex = 2 }
                } },
                new DialogueTree.Node { id = "left", nextNodeIndex = -1 },
                new DialogueTree.Node { id = "right", nextNodeIndex = -1 }));
            var managerObject = Track(new GameObject("Dialogue Manager"));
            managerObject.AddComponent<DialogueRunner>();
            var manager = managerObject.AddComponent<DialogueManager>();
            var playerObject = Track(new GameObject("Cinematic Dialogue"));
            var player = playerObject.AddComponent<DialogueEventPlayer>();
            var other = Track(new GameObject("Other Dialogue")).AddComponent<DialogueEventPlayer>();
            var cueObject = Track(new GameObject("Cues"));
            cueObject.SetActive(false);
            var cues = cueObject.AddComponent<DialogueNodeCinematicCue>();
            var cameraObject = Track(new GameObject("Beat Camera"));
            cameraObject.AddComponent<Camera>();
            var director = cameraObject.AddComponent<CinematicCutsceneCameraDirector>();
            var shots = new List<CinematicCutsceneCameraDirector.CinematicShot>();
            var entered = new List<string>();
            var mappings = new List<DialogueNodeCinematicCue.Cue>();
            foreach (string id in new[] { "start", "left", "right" })
            {
                string captured = id;
                var action = new UnityEvent();
                action.AddListener(() => entered.Add(captured));
                var marker = Track(new GameObject(id + " marker")).transform;
                marker.position = new Vector3(shots.Count, 1, -3);
                shots.Add(new CinematicCutsceneCameraDirector.CinematicShot
                    { shotId = id, cameraPositionMarker = marker });
                mappings.Add(new DialogueNodeCinematicCue.Cue
                    { nodeId = id, cameraDirector = director, cameraShotId = id, onNodeEntered = action });
            }
            typeof(CinematicCutsceneCameraDirector).GetField("shots", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(director, shots);
            typeof(DialogueNodeCinematicCue).GetField("dialoguePlayer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(cues, player);
            typeof(DialogueNodeCinematicCue).GetField("cues", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(cues, mappings);
            cueObject.SetActive(true);

            Assert.That(player.TryStartDialogue(tree, null, null), Is.True);
            Assert.That(director.CurrentShotId, Is.EqualTo("start"));
            manager.ChooseDialogueOption(1);
            Assert.That(director.CurrentShotId, Is.EqualTo("right"));
            Assert.That(cameraObject.transform.position, Is.EqualTo(new Vector3(2, 1, -3)));
            manager.StopDialogue();
            CollectionAssert.AreEqual(new[] { "start", "right" }, entered);
            // Same asset on a different player must not activate this player's shots.
            Assert.That(other.TryStartDialogue(tree, null, null), Is.True);
            manager.ChooseDialogueOption(0);
            manager.StopDialogue();
            CollectionAssert.AreEqual(new[] { "start", "right" }, entered);
            Assert.That(director.CurrentShotId, Is.EqualTo("right"));
            Assert.That(player.TryStartDialogue(tree, null, null), Is.True);
            cueObject.SetActive(false);
            manager.ChooseDialogueOption(0);
            CollectionAssert.AreEqual(new[] { "start", "right", "start" }, entered);
            // Re-enabling catches up to the current node exactly once.
            cueObject.SetActive(true);
            CollectionAssert.AreEqual(new[] { "start", "right", "start", "left" }, entered);
            Assert.That(director.CurrentShotId, Is.EqualTo("left"));
            manager.StopDialogue();
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
