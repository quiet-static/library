using System;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Interactions;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class GrabbableAuthoringTests
    {
        [Test]
        public void EnablingAddsOneComponentAndSupportsUndoWithoutDiscardingSettings()
        {
            GameObject owner = new("Grabbable authoring test");
            Interactable interactable = owner.AddComponent<Interactable>();
            owner.AddComponent<Rigidbody>();

            try
            {
                Assert.That(interactable.IsGrabbable, Is.False);
                Assert.That(owner.GetComponent<Grabbable>() == null, Is.True);

                SetInspectorToggle(interactable, true);
                Assert.That(interactable.IsGrabbable, Is.True);
                Assert.That(owner.GetComponents<Grabbable>(), Has.Length.EqualTo(1));

                Undo.PerformUndo();
                Assert.That(interactable.IsGrabbable, Is.False);
                Assert.That(owner.GetComponent<Grabbable>() == null, Is.True);

                Undo.PerformRedo();
                Assert.That(interactable.IsGrabbable, Is.True);
                Grabbable grabbable = owner.GetComponent<Grabbable>();
                Assert.That(grabbable, Is.Not.Null);
                using var settings = new SerializedObject(grabbable);
                settings.FindProperty("holdPositionOffset").vector3Value = Vector3.right;
                settings.ApplyModifiedPropertiesWithoutUndo();

                SetInspectorToggle(interactable, false);
                Assert.That(interactable.IsGrabbable, Is.False);
                Assert.That(owner.GetComponent<Grabbable>(), Is.SameAs(grabbable));

                SetInspectorToggle(interactable, true);
                Assert.That(owner.GetComponents<Grabbable>(), Has.Length.EqualTo(1));
                settings.Update();
                Assert.That(settings.FindProperty("holdPositionOffset").vector3Value,
                    Is.EqualTo(Vector3.right));
            }
            finally
            {
                Undo.ClearUndo(interactable);
                if (owner.TryGetComponent(out Grabbable grabbable))
                {
                    Undo.ClearUndo(grabbable);
                }

                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void EnablingOnPrefabInstanceCreatesOverrideAndPrefabContentsCanSaveIt()
        {
            string assetPath = $"Assets/GrabbableTest_{Guid.NewGuid():N}.prefab";
            GameObject owner = new("Grabbable prefab test");
            owner.AddComponent<Interactable>();
            owner.AddComponent<Rigidbody>();
            GameObject instance = null;
            GameObject contents = null;

            try
            {
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(owner, assetPath);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                Interactable instanceInteractable = instance.GetComponent<Interactable>();
                SetInspectorToggle(instanceInteractable, true);

                Assert.That(PrefabUtility.IsAddedComponentOverride(instance.GetComponent<Grabbable>()),
                    Is.True);
                using (var serializedInstance = new SerializedObject(instanceInteractable))
                {
                    Assert.That(serializedInstance.FindProperty("isGrabbable").prefabOverride,
                        Is.True);
                }
                Assert.That(asset.GetComponent<Interactable>().IsGrabbable, Is.False);
                Assert.That(asset.GetComponent<Grabbable>() == null, Is.True);

                contents = PrefabUtility.LoadPrefabContents(assetPath);
                SetInspectorToggle(contents.GetComponent<Interactable>(), true);
                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                Assert.That(asset.GetComponent<Interactable>().IsGrabbable, Is.True);
                Assert.That(asset.GetComponents<Grabbable>(), Has.Length.EqualTo(1));
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(owner);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static void SetInspectorToggle(Interactable interactable, bool enabled)
        {
            using var serialized = new SerializedObject(interactable);
            serialized.FindProperty("isGrabbable").boolValue = enabled;
            InteractableEditor.ApplyGrabbableModification(serialized);
        }
    }
}
