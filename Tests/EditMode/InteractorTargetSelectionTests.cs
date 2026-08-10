using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cameras;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class TestProjectInteractionTarget :
        MonoBehaviour,
        IInteractionTarget,
        IInteractionFocusReceiver
    {
        public string DisplayName => "Project Interaction";
        public Transform InteractionTransform => transform;
        public bool IsAvailable { get; set; } = true;
        public bool WasFocused { get; private set; }
        public bool WasInteracted { get; private set; }

        public bool IsInteractionAvailable(Interactor interactor)
        {
            return IsAvailable;
        }

        public bool TryInteract(Interactor interactor)
        {
            WasInteracted = true;
            return true;
        }

        public void SetInteractionFocused(bool focused)
        {
            WasFocused = focused;
        }
    }

    public class InteractorTargetSelectionTests
    {
        private GameObject cameraObject;
        private GameObject targetObject;
        private Interactor interactor;
        private Interactable normalInteraction;
        private ActivatedProgressInteractable progressInteraction;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Interaction Camera");
            cameraObject.tag = "MainCamera";
            Camera interactionCamera = cameraObject.AddComponent<Camera>();
            interactor = cameraObject.AddComponent<Interactor>();
            AssignCamera(interactor, interactionCamera);

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "Staged Interaction";
            targetObject.transform.position = Vector3.forward;
            normalInteraction = targetObject.AddComponent<Interactable>();
            progressInteraction =
                targetObject.AddComponent<ActivatedProgressInteractable>();

            Physics.SyncTransforms();
        }

        private static void AssignCamera(
            Interactor target,
            Camera interactionCamera)
        {
            typeof(Interactor)
                .GetField(
                    "interactionCamera",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                .SetValue(target, interactionCamera);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void AvailableProgressInteractionTakesPrecedence()
        {
            interactor.RefreshTarget();

            Assert.That(interactor.CurrentProgressTarget,
                Is.SameAs(progressInteraction));
            Assert.That(interactor.CurrentTarget, Is.Null);
        }

        [Test]
        public void RunningProgressInteractionContinuesToBlockNormalInteraction()
        {
            Assert.That(progressInteraction.TryActivate(), Is.True);

            interactor.RefreshTarget();

            Assert.That(interactor.CurrentProgressTarget,
                Is.SameAs(progressInteraction));
            Assert.That(interactor.CurrentTarget, Is.Null);
        }

        [Test]
        public void CompletedProgressInteractionYieldsToNormalInteraction()
        {
            Assert.That(progressInteraction.TryActivate(), Is.True);
            progressInteraction.SetProgress(1f);

            interactor.RefreshTarget();

            Assert.That(interactor.CurrentProgressTarget, Is.Null);
            Assert.That(interactor.CurrentTarget, Is.SameAs(normalInteraction));
        }

        [Test]
        public void DisabledProgressInteractionYieldsToNormalInteraction()
        {
            progressInteraction.SetEnabled(false);

            interactor.RefreshTarget();

            Assert.That(interactor.CurrentProgressTarget, Is.Null);
            Assert.That(interactor.CurrentTarget, Is.SameAs(normalInteraction));
        }
    }

    public class HoldInteractorTargetSelectionTests
    {
        private GameObject cameraObject;
        private GameObject targetObject;
        private Interactor interactor;
        private HoldInteractable holdInteraction;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Interaction Camera");
            cameraObject.tag = "MainCamera";
            Camera interactionCamera = cameraObject.AddComponent<Camera>();
            interactor = cameraObject.AddComponent<Interactor>();
            typeof(Interactor)
                .GetField(
                    "interactionCamera",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(interactor, interactionCamera);

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.transform.position = Vector3.forward;
            holdInteraction = targetObject.AddComponent<HoldInteractable>();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void ExternallyOwnedHoldIsNotAcquiredByCrosshairInteractor()
        {
            holdInteraction.SetInteractorTargetingEnabled(false);

            interactor.RefreshTarget();

            Assert.That(interactor.CurrentHoldTarget, Is.Null);
        }

        [Test]
        public void HoldLifecycleEventsRunOncePerPressAndRelease()
        {
            int began = 0;
            int ended = 0;
            holdInteraction.HoldBegan += () => began++;
            holdInteraction.HoldEnded += () => ended++;
            holdInteraction.SetEnabled(true);

            holdInteraction.Advance(0.1f);
            holdInteraction.Advance(0.1f);
            holdInteraction.Cancel();
            holdInteraction.Cancel();

            Assert.That(began, Is.EqualTo(1));
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(holdInteraction.IsBeingHeld, Is.False);
        }
    }

    public class CameraLookConstraintTests
    {
        [Test]
        public void LookInputIsClampedAroundFocusDirection()
        {
            GameObject target = new("Target");
            GameObject focus = new("Focus");
            GameObject cameraObject = new("Camera");
            focus.transform.position = new Vector3(0f, 1.5f, 10f);
            CameraController controller =
                cameraObject.AddComponent<CameraController>();
            SetPrivateField(controller, "target", target.transform);

            controller.BeginLookConstraint(focus.transform, 10f, 5f, false);
            float centerYaw = GetPrivateFloat(controller, "constrainedYawCenter");
            float centerPitch = GetPrivateFloat(controller, "constrainedPitchCenter");
            typeof(CameraController)
                .GetMethod(
                    "HandleRotation",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { new Vector2(1000f, 1000f) });
            float yaw = GetPrivateFloat(controller, "yaw");
            float pitch = GetPrivateFloat(controller, "pitch");

            Assert.That(
                Mathf.Abs(Mathf.DeltaAngle(centerYaw, yaw)),
                Is.LessThanOrEqualTo(10.001f));
            Assert.That(
                Mathf.Abs(pitch - centerPitch),
                Is.LessThanOrEqualTo(5.001f));
            Assert.That(controller.IsLookConstrained, Is.True);

            controller.EndLookConstraint();
            Assert.That(controller.IsLookConstrained, Is.False);

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(focus);
            Object.DestroyImmediate(target);
        }

        private static void SetPrivateField(
            object target,
            string name,
            object value) =>
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private static float GetPrivateFloat(object target, string name) =>
            (float)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
    }

    public class NestedInteractorTargetSelectionTests
    {
        private GameObject cameraObject;
        private GameObject containerObject;
        private GameObject otherObject;
        private Interactor interactor;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Interaction Camera");
            cameraObject.tag = "MainCamera";
            Camera interactionCamera = cameraObject.AddComponent<Camera>();
            interactor = cameraObject.AddComponent<Interactor>();
            AssignCamera(interactor, interactionCamera);
        }

        private static void AssignCamera(
            Interactor target,
            Camera interactionCamera)
        {
            typeof(Interactor)
                .GetField(
                    "interactionCamera",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                .SetValue(target, interactionCamera);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(otherObject);
            Object.DestroyImmediate(containerObject);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void NestedInteractableTakesPrecedenceOverItsContainer()
        {
            containerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            containerObject.name = "Fridge";
            containerObject.transform.position = Vector3.forward;
            containerObject.AddComponent<Interactable>();

            GameObject nestedObject =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            nestedObject.name = "Food";
            nestedObject.transform.SetParent(containerObject.transform, false);
            nestedObject.transform.localScale = Vector3.one * 0.25f;
            Interactable foodInteraction =
                nestedObject.AddComponent<Interactable>();

            Physics.SyncTransforms();
            interactor.RefreshTarget();

            Assert.That(interactor.CurrentTarget,
                Is.SameAs(foodInteraction));
        }

        [Test]
        public void UnrelatedInteractableBehindContainerDoesNotTakePrecedence()
        {
            containerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            containerObject.name = "Fridge";
            containerObject.transform.position = Vector3.forward;
            Interactable fridgeInteraction =
                containerObject.AddComponent<Interactable>();

            otherObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            otherObject.name = "Unrelated Object";
            otherObject.transform.position = Vector3.forward * 1.75f;
            otherObject.transform.localScale = Vector3.one * 0.25f;
            otherObject.AddComponent<Interactable>();

            Physics.SyncTransforms();
            interactor.RefreshTarget();

            Assert.That(interactor.CurrentTarget,
                Is.SameAs(fridgeInteraction));
        }

        [Test]
        public void NonInteractableColliderStillBlocksObjectsBehindIt()
        {
            containerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            containerObject.name = "Blocking Wall";
            containerObject.transform.position = Vector3.forward * 0.5f;
            containerObject.transform.localScale =
                new Vector3(1f, 1f, 0.1f);

            otherObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            otherObject.name = "Hidden Interaction";
            otherObject.transform.position = Vector3.forward;
            otherObject.transform.localScale = Vector3.one * 0.25f;
            otherObject.AddComponent<Interactable>();

            Physics.SyncTransforms();
            interactor.RefreshTarget();

            Assert.That(interactor.CurrentTarget, Is.Null);
        }

        [Test]
        public void ProjectOwnedInterfaceTargetCanBeSelectedAndInteracted()
        {
            containerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            containerObject.name = "Project Interaction";
            containerObject.transform.position = Vector3.forward;
            TestProjectInteractionTarget projectTarget =
                containerObject.AddComponent<TestProjectInteractionTarget>();

            Physics.SyncTransforms();
            interactor.RefreshTarget();

            Assert.That(interactor.CurrentInteractionTarget,
                Is.SameAs(projectTarget));
            Assert.That(interactor.CurrentTarget, Is.Null);
            Assert.That(projectTarget.WasFocused, Is.True);
            Assert.That(interactor.TryInteract(), Is.True);
            Assert.That(projectTarget.WasInteracted, Is.True);
        }

        [Test]
        public void IgnoredThirdPersonRootDoesNotOccludeTarget()
        {
            containerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            containerObject.name = "Player Body";
            containerObject.transform.position = Vector3.forward * 0.5f;
            containerObject.transform.localScale =
                new Vector3(0.25f, 0.25f, 0.25f);

            otherObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            otherObject.name = "Interaction Behind Player";
            otherObject.transform.position = Vector3.forward * 1.5f;
            Interactable targetInteraction =
                otherObject.AddComponent<Interactable>();

            AssignPrivateField(
                interactor,
                "ignoredRoot",
                containerObject.transform
            );

            Physics.SyncTransforms();
            interactor.RefreshTarget();

            Assert.That(interactor.CurrentTarget,
                Is.SameAs(targetInteraction));
        }

        [Test]
        public void InteractionOriginReachRejectsDistantAimedTarget()
        {
            containerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            containerObject.name = "Distant Interaction";
            containerObject.transform.position = Vector3.forward * 2f;
            containerObject.AddComponent<Interactable>();

            AssignPrivateField(
                interactor,
                "interactionOrigin",
                cameraObject.transform
            );
            AssignPrivateField(
                interactor,
                "requireInteractionOriginInRange",
                true
            );
            AssignPrivateField(
                interactor,
                "maximumInteractionOriginDistance",
                0.75f
            );

            Physics.SyncTransforms();
            interactor.RefreshTarget();

            Assert.That(interactor.CurrentInteractionTarget, Is.Null);
        }

        private static void AssignPrivateField(
            Interactor target,
            string fieldName,
            object value)
        {
            typeof(Interactor)
                .GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                .SetValue(target, value);
        }
    }
}
