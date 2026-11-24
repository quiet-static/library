using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class ColliderDialogueTriggerTests
    {
        private readonly List<Object> created = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
                Object.Destroy(created[index]);

            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChildColliderResolvesItsActorAndOnlyConsumesAnAcceptedStart()
        {
            FlagManager flags = Track(new GameObject("Flags")).AddComponent<FlagManager>();
            GameObject managerObject = Track(new GameObject("Dialogue manager"));
            managerObject.AddComponent<DialogueRunner>();
            var manager = managerObject.AddComponent<DialogueManager>();

            var inside = new Vector3(500f, 100f, 500f);
            Vector3 outside = inside + Vector3.right * 10f;
            GameObject actor = Track(new GameObject("Entering player"));
            actor.tag = "Player";
            actor.transform.position = outside;
            GameObject child = Track(new GameObject("Untagged player collider"));
            child.transform.SetParent(actor.transform, false);
            child.AddComponent<BoxCollider>();

            GameObject trigger = Track(new GameObject("Dialogue volume"));
            trigger.transform.position = inside;
            trigger.AddComponent<BoxCollider>().isTrigger = true;
            trigger.AddComponent<Rigidbody>().isKinematic = true;
            var resolver = trigger.AddComponent<DialogueTreeResolver>();
            var dialogue = trigger.AddComponent<DialogueEventPlayer>();

            DialogueTree fixedTree = CreateTree("fixed");
            DialogueTree contextualTree = CreateTree("contextual");
            var condition = Track(ScriptableObject.CreateInstance<ActorCondition>());
            condition.Actor = actor;
            var rule = new DialogueTreeResolver.DialogueRule();
            Set(rule, "tree", contextualTree);
            Set(rule, "contextConditions", new DialogueContextCondition[] { condition });
            Set(resolver, "rules", new[] { rule });
            dialogue.SetDefaultDialogue(fixedTree);
            Set(dialogue, "dialogueResolver", resolver);
            Set(dialogue, "startOnTriggerEnter", true);
            Set(dialogue, "startRequirement", new FlagRequirement(FlagRequirementMode.All, new[] { "ready" }));

            yield return MoveActor(actor, inside);
            Assert.That(dialogue.HasTriggered, Is.False);
            Assert.That(manager.IsDialogueActive, Is.False);

            yield return MoveActor(actor, outside);
            flags.SetFlag("ready");
            yield return MoveActor(actor, inside);
            Assert.That(manager.CurrentDialogueTree, Is.SameAs(contextualTree));
            Assert.That(dialogue.HasTriggered, Is.True);

            dialogue.StopDialogue();
            yield return MoveActor(actor, outside);
            yield return MoveActor(actor, inside);
            Assert.That(manager.IsDialogueActive, Is.False);
        }

        private DialogueTree CreateTree(string id)
        {
            DialogueTree tree = Track(ScriptableObject.CreateInstance<DialogueTree>());
            Set(tree, "nodes", new[] { new DialogueTree.Node { id = id, nextNodeIndex = -1 } });
            Set(tree, "startNodeIndex", 0);
            return tree;
        }

        private static IEnumerator MoveActor(GameObject actor, Vector3 position)
        {
            actor.transform.position = position;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        private T Track<T>(T instance) where T : Object
        {
            created.Add(instance);
            return instance;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class ActorCondition : DialogueContextCondition
        {
            public GameObject Actor { get; set; }

            public override bool IsMet(GameObject player, Interactor interactor) => player == Actor;
        }
    }
}
