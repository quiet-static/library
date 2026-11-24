using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Characters.Player;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class ContextualDialogueInteractionTests
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
        public IEnumerator DialogueActionChecksLiveActorAndOnlyConsumesASuccessfulStart()
        {
            FlagManager flags = Track(new GameObject("Flags")).AddComponent<FlagManager>();
            PlayerManager playerManager = Track(new GameObject("Players")).AddComponent<PlayerManager>();
            GameObject registeredPlayer = Track(new GameObject("Registered player"));
            registeredPlayer.AddComponent<MovementStateController>();
            playerManager.SetPlayer(registeredPlayer);

            GameObject actor = Track(new GameObject("Active actor"));
            MovementStateController movement = actor.AddComponent<MovementStateController>();
            movement.SetState(EntityState.MovementState.Falling);
            Interactor interactor = Track(new GameObject("Camera interactor")).AddComponent<Interactor>();

            DialogueTree tree = Track(ScriptableObject.CreateInstance<DialogueTree>());
            Set(tree, "nodes", new[] { new DialogueTree.Node { id = "first", nextNodeIndex = -1 } });
            Set(tree, "startNodeIndex", 0);
            DialogueContextCondition condition = Track(ScriptableObject.CreateInstance<FallingCondition>());
            var rule = new DialogueTreeResolver.DialogueRule();
            Set(rule, "tree", tree);
            Set(rule, "contextConditions", new[] { condition });

            GameObject npcObject = Track(new GameObject("NPC"));
            var resolver = npcObject.AddComponent<DialogueTreeResolver>();
            Set(resolver, "rules", new[] { rule });
            var npc = npcObject.AddComponent<NPCDialogueBehaviour>();
            npc.SetDialogueEventPlayer(npcObject.AddComponent<DialogueEventPlayer>());
            Set(npc, "dialogueResolver", resolver);
            var interaction = npcObject.AddComponent<Interactable>();
            Set(interaction, "dialogueAction", npc);
            Set(interaction, "requirement", new FlagRequirement(FlagRequirementMode.All, new[] { "can-talk" }));
            Set(interaction, "disableAfterSuccess", true);
            Set(interaction, "flagsToSetOnSuccess", new[] { "conversation-started" });
            int successes = 0;
            interaction.InteractionSucceeded += (_, _) => successes++;

            Set(interactor, "holderRoot", actor);
            Assert.That(interaction.IsInteractionAvailable(interactor), Is.False);
            flags.SetFlag("can-talk");
            Set(interactor, "holderRoot", null);
            Assert.That(interaction.IsInteractionAvailable(interactor), Is.False);
            Set(interactor, "holderRoot", actor);
            Assert.That(interaction.IsInteractionAvailable(interactor), Is.True);

            movement.SetState(EntityState.MovementState.Idle);
            Assert.That(interaction.TryInteract(interactor), Is.False);
            movement.SetState(EntityState.MovementState.Falling);
            Assert.That(interaction.TryInteract(interactor), Is.False, "A missing manager rejects the resolved tree.");
            Assert.That(interaction.IsEnabled, Is.True);
            Assert.That(flags.HasFlag("conversation-started"), Is.False);
            Assert.That(successes, Is.Zero);

            GameObject managerObject = Track(new GameObject("Dialogue manager"));
            managerObject.AddComponent<DialogueRunner>();
            var manager = managerObject.AddComponent<DialogueManager>();

            Assert.That(interaction.TryInteract(interactor), Is.True);
            Assert.That(manager.CurrentDialogueTree, Is.SameAs(tree));
            Assert.That(npc.IsInDialogue, Is.True);
            Assert.That(interaction.IsEnabled, Is.False);
            Assert.That(flags.HasFlag("conversation-started"), Is.True);
            Assert.That(successes, Is.EqualTo(1));
            manager.StopDialogue();
            Assert.That(npc.IsInDialogue, Is.False);
            yield return null;
        }

        private T Track<T>(T instance) where T : Object
        {
            created.Add(instance);
            return instance;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private sealed class FallingCondition : DialogueContextCondition
        {
            public override bool IsMet(GameObject player, Interactor interactor) =>
                interactor != null && player.TryGetComponent(out MovementStateController movement) &&
                movement.CurrentState == EntityState.MovementState.Falling;
        }
    }
}
