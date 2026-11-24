using System;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Interactions;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;
using Batch = QuietStatic.Toolkit.Editor.Interactions.ClutterBatchInteractionAuthoring;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ClutterBatchInteractionAuthoringTests
    {
        private GameObject root;
        private InteractionUIChannel channel;
        private int undoGroup;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Clutter batch test");
            channel = ScriptableObject.CreateInstance<InteractionUIChannel>();
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(undoGroup);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void FlavorPreviewIsReadOnlyAndApplyUsesOneUndoGroup()
        {
            GameObject notes = MakeProp("Notes");
            GameObject photo = MakeProp("Photograph");
            Collider photoCollider = photo.GetComponent<Collider>();
            Object.DestroyImmediate(notes.GetComponent<Collider>());
            Batch.Target[] targets =
            {
                new(notes) { Prompt = "Read notes", FlavorText = "A short list." },
                new(photo) { FlavorText = "A faded photograph." }
            };
            Assert.That(Batch.Preview(targets, Batch.Preset.Flavor, channel).Select(row => row.Status),
                Is.EqualTo(new[] { Batch.Status.Ready, Batch.Status.Ready }));
            Assert.That(root.GetComponentsInChildren<Interactable>(), Is.Empty);

            Assert.That(Batch.Apply(targets, Batch.Preset.Flavor, channel).Select(row => row.Status),
                Is.EqualTo(new[] { Batch.Status.Applied, Batch.Status.Applied }));
            Assert.That(notes.GetComponent<Interactable>().DisplayName, Is.EqualTo("Read notes"));
            Assert.That(notes.GetComponent<FlavorTextInteraction>().FlavorText, Is.EqualTo("A short list."));
            Assert.That(notes.GetComponent<FlavorTextInteraction>().Channel, Is.SameAs(channel));
            Assert.That(notes.GetComponent<Collider>().isTrigger, Is.True);
            Assert.That(photo.GetComponents<Collider>(), Is.EqualTo(new[] { photoCollider }));
            Assert.That(Batch.Preview(targets, Batch.Preset.Flavor, null).All(row => row.Status == Batch.Status.Unchanged), Is.True);

            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(root.GetComponentsInChildren<Interactable>(), Is.Empty);
            Assert.That(notes.GetComponent<Collider>(), Is.Null);
            Assert.That(photo.GetComponent<Collider>(), Is.SameAs(photoCollider));
        }

        [Test]
        public void ExplicitChildrenTakePrecedenceOverTheirGroupAndGameplayIsPreserved()
        {
            GameObject notes = MakeProp("Notes");
            GameObject projector = MakeProp("Projector");
            var gameplay = projector.AddComponent<Interactable>();
            Batch.Target[] targets =
            {
                new(root) { FlavorText = "A group of objects." },
                new(notes) { FlavorText = "A short list." },
                new(projector) { FlavorText = "A projector." }
            };

            Assert.That(Batch.Apply(targets, Batch.Preset.Flavor, channel).Select(row => row.Status),
                Is.EqualTo(new[] { Batch.Status.Skipped, Batch.Status.Applied, Batch.Status.Skipped }));
            Assert.That(root.GetComponent<Interactable>(), Is.Null);
            Assert.That(projector.GetComponent<Interactable>(), Is.SameAs(gameplay));
            Assert.That(projector.GetComponent<FlavorTextInteraction>(), Is.Null);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void NewFlavorAddsTargetableTriggerWithoutChangingDecorativeCollider(bool colliderEnabled, bool ignoreRaycast)
        {
            GameObject prop = MakeProp("Decorative clutter");
            int originalLayer = LayerMask.NameToLayer(ignoreRaycast ? "Ignore Raycast" : "Default");
            prop.layer = originalLayer;
            Collider authored = prop.GetComponent<Collider>();
            authored.enabled = colliderEnabled;
            var targets = new[] { new Batch.Target(prop) { FlavorText = "A discarded note." } };

            Batch.Row result = Batch.Apply(targets, Batch.Preset.Flavor, channel)[0];
            Assert.That(result.Status, Is.EqualTo(Batch.Status.Applied), result.Detail);
            Collider added = prop.GetComponentsInChildren<Collider>(true).Single(collider => collider != authored);
            int interactionLayer = LayerMask.NameToLayer("Interactables");
            if (interactionLayer < 0) interactionLayer = LayerMask.NameToLayer("Default");
            Assert.That(added.gameObject.layer, Is.EqualTo(interactionLayer));
            Assert.That(added.enabled && added.isTrigger, Is.True);
            Assert.That(prop.GetComponent<Collider>(), Is.SameAs(authored));
            Assert.That(prop.layer, Is.EqualTo(originalLayer));
            Assert.That(authored.enabled, Is.EqualTo(colliderEnabled));
            Assert.That(authored.isTrigger, Is.False);
            Assert.That(Batch.Preview(targets, Batch.Preset.Flavor, channel)[0].Status, Is.EqualTo(Batch.Status.Unchanged));
        }

        [Test]
        public void PickupAddsSolidGeometryAndLeavesExistingPhysicsSettingsIntact()
        {
            GameObject cup = MakeProp("Cup");
            Collider trigger = cup.GetComponent<Collider>();
            trigger.isTrigger = true;
            Rigidbody body = cup.AddComponent<Rigidbody>();
            body.mass = 2f;
            var targets = new[] { new Batch.Target(cup) };

            Assert.That(Batch.Apply(targets, Batch.Preset.Pickup, null)[0].Status, Is.EqualTo(Batch.Status.Applied));
            Assert.That(cup.GetComponent<Interactable>().IsGrabbable, Is.True);
            Assert.That(cup.GetComponent<Grabbable>(), Is.Not.Null);
            Assert.That(cup.GetComponent<Rigidbody>(), Is.SameAs(body));
            Assert.That(body.mass, Is.EqualTo(2f));
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(cup.GetComponentsInChildren<Collider>().Count(collider => !collider.isTrigger), Is.EqualTo(1));
        }

        [Test]
        public void ProjectPrefabApplyTargetsItsExplicitChildAndPreservesTheAssetGuid()
        {
            string path = $"Assets/ClutterBatchTest_{Guid.NewGuid():N}.prefab";
            MakeProp("Cup");
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                string originalGuid = AssetDatabase.AssetPathToGUID(path);
                GameObject child = prefab.transform.GetChild(0).gameObject;
                var targets = new[] { new Batch.Target(child) { Prompt = "Pick up cup" } };

                Batch.Row row = Batch.Apply(targets, Batch.Preset.Pickup, null)[0];
                Assert.That(row.Status, Is.EqualTo(Batch.Status.Applied), row.Detail);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(originalGuid));
                GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(reloaded.GetComponent<Interactable>(), Is.Null);
                GameObject editedChild = reloaded.transform.GetChild(0).gameObject;
                Assert.That(editedChild.GetComponent<Interactable>().DisplayName, Is.EqualTo("Pick up cup"));
                Assert.That(editedChild.GetComponent<Grabbable>(), Is.Not.Null);
                Assert.That(Batch.Preview(targets, Batch.Preset.Pickup, null)[0].Status, Is.EqualTo(Batch.Status.Unchanged));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        private GameObject MakeProp(string name)
        {
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = name;
            prop.transform.SetParent(root.transform);
            return prop;
        }
    }
}
