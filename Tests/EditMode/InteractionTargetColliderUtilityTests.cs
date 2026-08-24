using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Interactions;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    /// <summary>Protects the Inspector's Interactor collider guidance.</summary>
    public sealed class InteractionTargetColliderUtilityTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SolidColliderOnTarget_IsValid()
        {
            Interactable target = CreateTarget("Solid Target");
            BoxCollider collider = target.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = false;

            Assert.That(
                InteractionTargetColliderUtility.HasRaycastCollider(target),
                Is.True);
        }

        [Test]
        public void InactiveChildTriggerCollider_IsValidAuthoringConfiguration()
        {
            Interactable target = CreateTarget("Trigger Target");
            var child = new GameObject("Interaction Volume");
            child.transform.SetParent(root.transform);
            BoxCollider collider = child.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            child.SetActive(false);

            Assert.That(
                InteractionTargetColliderUtility.HasRaycastCollider(target),
                Is.True);
        }

        [Test]
        public void ColliderOnlyOnParent_IsNotValidForChildTarget()
        {
            root = new GameObject("Parent Collider");
            root.AddComponent<BoxCollider>();
            var child = new GameObject("Child Target");
            child.transform.SetParent(root.transform);
            Interactable target = child.AddComponent<Interactable>();

            Assert.That(
                InteractionTargetColliderUtility.HasRaycastCollider(target),
                Is.False);
        }

        [Test]
        public void NestedTargetCollider_DoesNotBelongToParentTarget()
        {
            Interactable parentTarget = CreateTarget("Parent Target");
            var child = new GameObject("Nested Target");
            child.transform.SetParent(root.transform);
            Interactable childTarget = child.AddComponent<Interactable>();
            child.AddComponent<BoxCollider>();

            Assert.That(
                InteractionTargetColliderUtility.HasRaycastCollider(parentTarget),
                Is.False);
            Assert.That(
                InteractionTargetColliderUtility.HasRaycastCollider(childTarget),
                Is.True);
        }

        private Interactable CreateTarget(string name)
        {
            root = new GameObject(name);
            return root.AddComponent<Interactable>();
        }
    }
}
