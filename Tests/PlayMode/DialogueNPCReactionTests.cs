using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Dialogue;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class DialogueNPCReactionTests
    {
        private readonly List<Object> created = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                Object.Destroy(created[index]);
            }

            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TriggerReactsToItsActorAndRestoresPreviousLookAndModeAfterDialogue()
        {
            DialogueManager manager = CreateManager();
            DialogueTree tree = CreateTree();
            var idleLook = Track(new GameObject("Original look target"));
            var registeredPlayer = Track(new GameObject("Registered player"));
            Track(new GameObject("Player Manager")).AddComponent<PlayerManager>().SetPlayer(registeredPlayer);
            var actor = Track(new GameObject("Entering actor"));
            Collider actorCollider = actor.AddComponent<BoxCollider>();
            NPCController npc = CreateNpc(out NPCLookAtBehaviour gaze, out NPCModeController modes);
            DialogueNPCReaction reaction = CreateReaction(npc, tree, out DialogueEventPlayer source);
            Set(reaction, "modeOnStart", "Talking");
            Set(source, "startOnTriggerEnter", true);
            Set(source, "requiredTag", string.Empty);

            var unrelatedPlayer = Track(new GameObject("Unrelated dialogue source")).AddComponent<DialogueEventPlayer>();
            unrelatedPlayer.SetDefaultDialogue(tree);
            yield return null;
            gaze.StartTemporaryLookAt(idleLook.transform, Vector3.up);

            Assert.That(unrelatedPlayer.TryStartDefaultDialogue(), Is.True);
            source.SendMessage("OnTriggerEnter", actorCollider);
            Assert.That(source.HasTriggered, Is.False);
            Assert.That(gaze.CurrentTarget, Is.SameAs(idleLook.transform));
            Assert.That(modes.CurrentModeName, Is.EqualTo("Patrol"));
            manager.StopDialogue();

            source.SendMessage("OnTriggerEnter", actorCollider);
            Assert.That(source.HasTriggered, Is.True);
            Assert.That(gaze.CurrentTarget, Is.SameAs(actor.transform));
            Assert.That(modes.CurrentModeName, Is.EqualTo("Talking"));

            manager.AdvanceDialogue();

            Assert.That(gaze.CurrentTarget, Is.SameAs(idleLook.transform));
            Assert.That(modes.CurrentModeName, Is.EqualTo("Patrol"));
        }

        [UnityTest]
        public IEnumerator EndingPreservesANewerLookAndDisablingReleasesOwnedState()
        {
            DialogueManager manager = CreateManager();
            DialogueTree tree = CreateTree();
            var actor = Track(new GameObject("Player"));
            var newerLook = Track(new GameObject("Later look target"));
            NPCController npc = CreateNpc(out NPCLookAtBehaviour gaze, out NPCModeController modes);
            DialogueNPCReaction reaction = CreateReaction(npc, tree, out DialogueEventPlayer source);
            Set(reaction, "modeOnStart", "Talking");
            Set(reaction, "modeOnEnd", "Exit");
            yield return null;

            Assert.That(source.TryStartDefaultDialogue(actor), Is.True);
            gaze.StartTemporaryLookAt(newerLook.transform);
            manager.StopDialogue();

            Assert.That(gaze.CurrentTarget, Is.SameAs(newerLook.transform));
            Assert.That(modes.CurrentModeName, Is.EqualTo("Exit"));

            gaze.StopLooking();
            modes.SetModeByName("Patrol");
            Assert.That(source.TryStartDefaultDialogue(actor), Is.True);
            Assert.That(gaze.IsBehaviourActive, Is.True);

            reaction.enabled = false;

            Assert.That(gaze.CurrentTarget, Is.Null);
            Assert.That(gaze.IsBehaviourActive, Is.False);
            Assert.That(modes.CurrentModeName, Is.EqualTo("Patrol"));
            manager.StopDialogue();
            Assert.That(modes.CurrentModeName, Is.EqualTo("Patrol"));
        }

        private DialogueManager CreateManager()
        {
            GameObject manager = Track(new GameObject("Dialogue Manager"));
            manager.AddComponent<DialogueRunner>();
            return manager.AddComponent<DialogueManager>();
        }

        private DialogueTree CreateTree()
        {
            DialogueTree tree = Track(ScriptableObject.CreateInstance<DialogueTree>());
            Set(tree, "nodes", new[] { new DialogueTree.Node { id = "start", nextNodeIndex = -1 } });
            Set(tree, "startNodeIndex", 0);
            return tree;
        }

        private NPCController CreateNpc(out NPCLookAtBehaviour gaze, out NPCModeController modes)
        {
            GameObject npc = Track(new GameObject("NPC"));
            var controller = npc.AddComponent<NPCController>();
            gaze = npc.AddComponent<NPCLookAtBehaviour>();
            modes = npc.AddComponent<NPCModeController>();
            Set(modes, "managedBehaviours", System.Array.Empty<NPCBehaviour>());
            Set(modes, "managedObjects", System.Array.Empty<GameObject>());
            Set(modes, "modes", new[] { Mode("Patrol"), Mode("Talking"), Mode("Exit") });
            return controller;
        }

        private static NPCModeController.NPCMode Mode(string name) => new()
        {
            modeName = name,
            enabledBehaviours = System.Array.Empty<NPCBehaviour>(),
            enabledObjects = System.Array.Empty<GameObject>()
        };

        private DialogueNPCReaction CreateReaction(NPCController npc, DialogueTree tree, out DialogueEventPlayer player)
        {
            GameObject source = Track(new GameObject("Dialogue Trigger"));
            player = source.AddComponent<DialogueEventPlayer>();
            player.SetDefaultDialogue(tree);
            var reaction = source.AddComponent<DialogueNPCReaction>();
            Set(reaction, "npc", npc);
            Set(reaction, "lookAtPlayer", true);
            return reaction;
        }

        private T Track<T>(T instance) where T : Object
        {
            created.Add(instance);
            return instance;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
