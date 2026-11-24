using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Input;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class GrabbablePlayModeTests
    {
        private static readonly Vector3 TestOrigin = new(400f, 200f, 400f);
        private readonly List<GameObject> createdObjects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.Destroy(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator InteractPicksUpThroughTheRaycastAndDropRestoresPhysics()
        {
            Interactor player = CreatePlayer(out Transform hold);
            Grabbable grabbable = CreateProp("Plate", out Interactable interactable, out Rigidbody body);
            Transform originalParent = CreateObject("Prop Parent", TestOrigin).transform;
            grabbable.transform.SetParent(originalParent, true);
            body.useGravity = true;
            Vector3 holdOffset = new(0.2f, -0.3f, 0.15f);
            Vector3 holdRotation = new(10f, 30f, 45f);
            SetField(grabbable, "holdPositionOffset", holdOffset);
            SetField(grabbable, "holdRotationOffset", holdRotation);
            bool succeeded = false;
            interactable.InteractionSucceeded += (_, actor) => succeeded = actor == player;

            Physics.SyncTransforms();
            Assert.That(player.TryInteract(), Is.True);
            Assert.That(succeeded, Is.True);
            Assert.That(player.HeldGrabbable, Is.SameAs(grabbable));
            Assert.That(grabbable.transform.parent, Is.SameAs(hold));
            Assert.That(grabbable.transform.localPosition, Is.EqualTo(holdOffset));
            Assert.That(Quaternion.Angle(grabbable.transform.localRotation,
                Quaternion.Euler(holdRotation)), Is.LessThan(0.001f));
            Assert.That(body.isKinematic, Is.True);

            Vector3 dropPosition = grabbable.transform.position;
            player.HandleDropInput();

            Assert.That(player.HeldGrabbable, Is.Null);
            Assert.That(grabbable.IsHeld, Is.False);
            Assert.That(grabbable.IsThrown, Is.False);
            Assert.That(grabbable.transform.parent, Is.SameAs(originalParent));
            Assert.That(Vector3.Distance(grabbable.transform.position, dropPosition),
                Is.LessThan(0.001f));
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.detectCollisions, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DialogueConditionTracksPickupAndDropWithAttachedOrDetachedInteractor()
        {
            Interactor interactor = CreatePlayer(out _);
            GameObject playerRoot = CreateObject("Player Root", TestOrigin);
            interactor.transform.SetParent(playerRoot.transform, true);
            CreateProp("Book", out Interactable interactable, out _);
            var condition = ScriptableObject.CreateInstance<PlayerHoldingObjectDialogueCondition>();

            try
            {
                Assert.That(condition.IsMet(playerRoot, null), Is.False);
                Assert.That(interactable.TryInteract(interactor), Is.True);
                Assert.That(condition.IsMet(playerRoot, null), Is.True);

                interactor.transform.SetParent(null, true);
                Assert.That(condition.IsMet(playerRoot, interactor), Is.True);

                interactor.HandleDropInput();
                Assert.That(condition.IsMet(playerRoot, interactor), Is.False);

                SetField(condition, "requireHeldObject", false);
                Assert.That(condition.IsMet(playerRoot, interactor), Is.True);
                Assert.That(condition.IsMet(playerRoot, null), Is.False);
            }
            finally
            {
                Object.Destroy(condition);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ChargeUsesExistingProgressBarAndResetsWhenCanceledOrDisabled()
        {
            InteractionUIManager ui = CreateProgressUI(out Slider progress);
            Interactor player = CreatePlayer(out _, ui);
            Grabbable grabbable = CreateProp("Cup", out Interactable interactable, out _);
            SetField(grabbable, "chargeDuration", 2f);

            Assert.That(interactable.TryInteract(player), Is.True);
            player.HandleThrowPressed();

            Assert.That(grabbable.IsCharging, Is.True);
            Assert.That(progress.gameObject.activeSelf, Is.True);
            Assert.That(progress.value, Is.Zero);

            grabbable.AdvanceCharge(0.5f);
            player.HandleThrowPressed();
            yield return null;
            Assert.That(progress.value, Is.InRange(0.25f, 0.99f));

            grabbable.AdvanceCharge(10f);
            yield return null;
            Assert.That(progress.value, Is.EqualTo(1f));

            player.CancelThrowCharge();
            player.HandleThrowReleased();
            Assert.That(grabbable.IsCharging, Is.False);
            Assert.That(grabbable.IsHeld, Is.True);
            Assert.That(progress.gameObject.activeSelf, Is.False);
            Assert.That(progress.value, Is.Zero);

            player.HandleThrowPressed();
            interactable.IsGrabbable = false;
            player.HandleThrowReleased();
            Assert.That(player.HeldGrabbable, Is.Null);
            Assert.That(grabbable.IsHeld, Is.False);
            Assert.That(grabbable.IsThrown, Is.False);
            Assert.That(progress.gameObject.activeSelf, Is.False);
            Assert.That(progress.value, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SwitchingHeldObjectsCancelsChargeAndDestroyingReplacementClearsProgress()
        {
            InteractionUIManager ui = CreateProgressUI(out Slider progress);
            Interactor player = CreatePlayer(out _, ui);
            Grabbable original = CreateProp("Cup", out Interactable originalInteraction, out _);
            Grabbable replacement = CreateProp("Book", out Interactable replacementInteraction, out _);

            Assert.That(originalInteraction.TryInteract(player), Is.True);
            player.HandleThrowPressed();
            Assert.That(original.IsCharging, Is.True);

            Assert.That(replacementInteraction.TryInteract(player), Is.True);
            player.HandleThrowReleased();
            Assert.That(original.IsCharging, Is.False);
            Assert.That(original.IsHeld, Is.False);
            Assert.That(original.IsThrown, Is.False);
            Assert.That(player.HeldGrabbable, Is.SameAs(replacement));
            Assert.That(progress.gameObject.activeSelf, Is.False);

            player.HandleThrowPressed();
            Assert.That(progress.gameObject.activeSelf, Is.True);
            Object.Destroy(replacement.gameObject);
            yield return null;

            Assert.That(player.HeldGrabbable, Is.Null);
            Assert.That(progress.gameObject.activeSelf, Is.False);
            Assert.That(progress.value, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReleasingChargeAppliesInterpolatedImpulseOnlyOnce()
        {
            Interactor player = CreatePlayer(out _);
            Grabbable grabbable = CreateProp("Book", out Interactable interactable, out Rigidbody body);
            SetField(grabbable, "minThrowForce", 2f);
            SetField(grabbable, "maxThrowForce", 8f);
            SetField(grabbable, "chargeDuration", 2f);

            Assert.That(interactable.TryInteract(player), Is.True);
            player.HandleThrowReleased();
            Assert.That(grabbable.IsHeld, Is.True);

            player.HandleThrowPressed();
            grabbable.AdvanceCharge(1f);
            player.HandleThrowReleased();
            player.HandleThrowReleased();

            Assert.That(grabbable.IsThrown, Is.True);
            Assert.That(player.HeldGrabbable, Is.Null);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.z, Is.EqualTo(5f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator DisablingInputReaderDuringAThrowPressCancelsCharge()
        {
            InteractionUIManager ui = CreateProgressUI(out Slider progress);
            Interactor player = CreatePlayer(out _, ui);
            Grabbable grabbable = CreateProp("Cup", out Interactable interactable, out _);
            GameObject readerOwner = CreateObject("Input reader", TestOrigin);
            readerOwner.SetActive(false);
            PlayerInputReader reader = readerOwner.AddComponent<PlayerInputReader>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputSettings settings = InputSystem.settings;
            InputSettings.UpdateMode previousUpdateMode = settings.updateMode;
            InputSettings.BackgroundBehavior previousBackgroundBehavior = settings.backgroundBehavior;
            Gamepad gamepad = null;

            try
            {
                settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                gamepad = InputSystem.AddDevice<Gamepad>();
                InputActionMap playerActions = actions.AddActionMap("Player");
                playerActions.devices = new InputDevice[] { gamepad };
                playerActions.AddAction("Move", InputActionType.Value);
                playerActions.AddAction("Look", InputActionType.Value);
                playerActions.AddAction("Jump", InputActionType.Button);
                playerActions.AddAction("Sprint", InputActionType.Button);
                playerActions.AddAction("Interact", InputActionType.Button);
                playerActions.AddAction("Throw", InputActionType.Button, "<Gamepad>/buttonSouth");
                SetField(reader, "inputActions", actions);
                SetField(reader, "grabbableInteractor", player);
                readerOwner.SetActive(true);

                Assert.That(interactable.TryInteract(player), Is.True);
                InputSystem.QueueStateEvent(gamepad, new GamepadState(GamepadButton.South));
                for (int frame = 0; frame < 5 && !grabbable.IsCharging; frame++)
                {
                    yield return null;
                }
                Assert.That(grabbable.IsCharging, Is.True);
                Assert.That(progress.gameObject.activeSelf, Is.True);

                reader.enabled = false;
                Assert.That(grabbable.IsCharging, Is.False);
                Assert.That(progress.gameObject.activeSelf, Is.False);

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                reader.enabled = true;
                yield return null;
                Assert.That(player.HeldGrabbable, Is.SameAs(grabbable));
                Assert.That(grabbable.IsThrown, Is.False);
            }
            finally
            {
                reader.enabled = false;
                if (gamepad != null)
                {
                    InputSystem.RemoveDevice(gamepad);
                }
                settings.backgroundBehavior = previousBackgroundBehavior;
                settings.updateMode = previousUpdateMode;
                Object.Destroy(actions);
            }
        }

        [UnityTest]
        public IEnumerator DeliberateThrowsReportOnlyTheirFirstImpactAndCanBeRepeated()
        {
            Interactor player = CreatePlayer(out Transform hold);
            Grabbable grabbable = CreateProp("Plate", out Interactable interactable, out Rigidbody body);
            body.constraints = RigidbodyConstraints.FreezeRotation;
            SetField(interactable, "displayName", "Porcelain Plate");
            SetField(grabbable, "useChargeThrow", false);
            Collider frontWall = CreateWall("Window", 2f);
            CreateWall("Rear wall", -0.5f);
            Collider playerCollider = CreateWall("Player collider", 0.55f);
            playerCollider.transform.SetParent(player.transform, true);
            var impacts = new List<ThrowImpactEvent>();
            grabbable.ThrowImpacted += impacts.Add;

            Assert.That(interactable.TryInteract(player), Is.True);
            Physics.SyncTransforms();
            player.HandleDropInput();
            body.linearVelocity = Vector3.forward * 3f;
            yield return WaitForContact(body, 1.75f, forward: true);
            Assert.That(impacts, Is.Empty);

            Assert.That(interactable.TryInteract(player), Is.True);
            Assert.That(grabbable.transform.parent, Is.SameAs(hold));
            player.HandleThrowPressed();
            Assert.That(player.HeldGrabbable, Is.Null);
            yield return WaitForImpact(impacts, 1);
            Assert.That(impacts, Has.Count.EqualTo(1));
            ThrowImpactEvent impact = impacts[0];
            Assert.That(impact.Grabbable, Is.SameAs(grabbable));
            Assert.That(impact.Interactable, Is.SameAs(interactable));
            Assert.That(impact.ObjectIdentifier, Is.EqualTo("Porcelain Plate"));
            Assert.That(impact.Thrower, Is.SameAs(player.gameObject));
            Assert.That(impact.HitObject, Is.SameAs(frontWall.gameObject));
            Assert.That(impact.HitCollider, Is.SameAs(frontWall));
            Assert.That(impact.RelativeVelocity.magnitude, Is.GreaterThan(0f));

            playerCollider.enabled = false;
            body.linearVelocity = Vector3.back * 8f;
            yield return WaitForContact(body, -0.05f, forward: false);
            Assert.That(impacts, Has.Count.EqualTo(1));

            Assert.That(interactable.TryInteract(player), Is.True);
            player.HandleThrowPressed();
            yield return WaitForImpact(impacts, 2);
            Assert.That(impacts, Has.Count.EqualTo(2));
        }

        private static IEnumerator WaitForImpact(List<ThrowImpactEvent> impacts, int expectedCount)
        {
            for (int step = 0; step < 60 && impacts.Count < expectedCount; step++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator WaitForContact(Rigidbody body, float offset, bool forward)
        {
            float target = TestOrigin.z + offset;
            for (int step = 0; step < 60; step++)
            {
                yield return new WaitForFixedUpdate();
                bool reachedWall = forward ? body.position.z >= target : body.position.z <= target;
                if (reachedWall && Mathf.Abs(body.linearVelocity.z) < 0.1f)
                {
                    yield break;
                }
            }

            Assert.Fail($"Body did not stop at z={target} (actual z={body.position.z}).");
        }

        private Interactor CreatePlayer(out Transform hold, InteractionUIManager ui = null)
        {
            GameObject player = CreateObject("Player", TestOrigin);
            player.SetActive(false);
            Camera camera = player.AddComponent<Camera>();
            camera.enabled = false;
            camera.nearClipPlane = 0.01f;
            hold = new GameObject("Hold Point").transform;
            hold.SetParent(player.transform, false);
            hold.localPosition = new Vector3(0f, 0f, 0.45f);

            Interactor interactor = player.AddComponent<Interactor>();
            SetField(interactor, "interactionCamera", camera);
            SetField(interactor, "holdTransform", hold);
            SetField(interactor, "holderRoot", player);
            SetField(interactor, "interactionUI", ui);
            player.SetActive(true);
            return interactor;
        }

        private Grabbable CreateProp(string name, out Interactable interactable, out Rigidbody body)
        {
            GameObject prop = CreateObject(name, TestOrigin + Vector3.forward * 1.5f);
            body = prop.AddComponent<Rigidbody>();
            body.useGravity = false;
            prop.AddComponent<BoxCollider>().size = Vector3.one * 0.3f;
            interactable = prop.AddComponent<Interactable>();
            Grabbable grabbable = prop.AddComponent<Grabbable>();
            interactable.IsGrabbable = true;
            return grabbable;
        }

        private Collider CreateWall(string name, float z)
        {
            GameObject wall = CreateObject(name, TestOrigin + Vector3.forward * z);
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(5f, 5f, 0.1f);
            return collider;
        }

        private InteractionUIManager CreateProgressUI(out Slider progress)
        {
            GameObject root = CreateObject("Interaction UI", Vector3.zero);
            root.SetActive(false);
            var progressObject = new GameObject("Progress", typeof(RectTransform), typeof(Slider));
            progressObject.transform.SetParent(root.transform, false);
            progress = progressObject.GetComponent<Slider>();

            InteractionUIManager ui = root.AddComponent<InteractionUIManager>();
            SetField(ui, "progressRoot", progressObject);
            SetField(ui, "progressSlider", progress);
            root.SetActive(true);
            return ui;
        }

        private GameObject CreateObject(string name, Vector3 position)
        {
            GameObject gameObject = new(name);
            gameObject.transform.position = position;
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
