using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NPCDoorTraversalTests
    {
        private const string OpenTrigger = "Open";
        private const string CloseTrigger = "Close";

        private readonly List<GameObject> createdObjects = new();
        private readonly List<AnimatorController> createdControllers = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(createdObjects[index]);
            }

            for (int index = createdControllers.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(createdControllers[index]);
            }

            createdObjects.Clear();
            createdControllers.Clear();
        }

        [Test]
        public void BinaryUnlock_IdempotentActivationPreservesLegacyToggle()
        {
            DoorFixture fixture = CreateDoor(Vector3.forward, 0f);
            int transitionCount = 0;
            fixture.Unlock.StateChanged += _ => transitionCount++;

            Assert.That(fixture.Unlock.Activate(), Is.True);
            Assert.That(fixture.Unlock.Activate(), Is.True);
            Assert.That(fixture.Unlock.IsActivated, Is.True);
            Assert.That(transitionCount, Is.EqualTo(1));

            fixture.Unlock.UnlockInteraction();

            Assert.That(fixture.Unlock.IsActivated, Is.False);
            Assert.That(transitionCount, Is.EqualTo(2));

            fixture.Unlock.UnlockInteraction();

            Assert.That(fixture.Unlock.IsActivated, Is.True);
            Assert.That(transitionCount, Is.EqualTo(3));
            Assert.That(fixture.Unlock.SetActivated(true), Is.True);
            Assert.That(transitionCount, Is.EqualTo(3));
            Assert.That(fixture.Unlock.Deactivate(), Is.True);
            Assert.That(fixture.Unlock.IsActivated, Is.False);
            Assert.That(transitionCount, Is.EqualTo(4));
        }

        [Test]
        public void PathDoor_RepeatedPassageRequestsOpenOnceAndWaitForClearance()
        {
            DoorFixture fixture = CreateDoor(Vector3.forward, 10f);
            int transitionCount = 0;
            fixture.Unlock.StateChanged += _ => transitionCount++;

            NPCPathDoorState firstState = fixture.PathDoor.RequestPassage();
            NPCPathDoorState secondState = fixture.PathDoor.RequestPassage();

            Assert.That(firstState, Is.EqualTo(NPCPathDoorState.Opening));
            Assert.That(secondState, Is.EqualTo(NPCPathDoorState.Opening));
            Assert.That(fixture.PathDoor.CurrentState, Is.EqualTo(NPCPathDoorState.Opening));
            Assert.That(fixture.PathDoor.IsPassable, Is.False);
            Assert.That(fixture.Unlock.IsActivated, Is.True);
            Assert.That(transitionCount, Is.EqualTo(1));
        }

        [Test]
        public void DoorOpener_DoorAheadRequestsPassageAndWaits()
        {
            NPCDoorOpener opener = CreateOpener(Vector3.zero);
            DoorFixture fixture = CreateDoor(new Vector3(0f, 0.8f, 1.5f), 10f);
            int transitionCount = 0;
            fixture.Unlock.StateChanged += _ => transitionCount++;
            Physics.SyncTransforms();

            NPCDoorTraversalStatus firstStatus =
                opener.EvaluatePath(Vector3.forward * 5f);
            NPCDoorTraversalStatus secondStatus =
                opener.EvaluatePath(Vector3.forward * 5f);

            Assert.That(firstStatus, Is.EqualTo(NPCDoorTraversalStatus.Waiting));
            Assert.That(secondStatus, Is.EqualTo(NPCDoorTraversalStatus.Waiting));
            Assert.That(opener.DetectedDoor, Is.SameAs(fixture.PathDoor));
            Assert.That(fixture.Unlock.IsActivated, Is.True);
            Assert.That(transitionCount, Is.EqualTo(1));
        }

        [Test]
        public void DoorOpener_AlreadyOpenDoorIsClearAndDoesNotToggleClosed()
        {
            NPCDoorOpener opener = CreateOpener(Vector3.zero);
            DoorFixture fixture = CreateDoor(new Vector3(0f, 0.8f, 1.5f), 0f);
            Assert.That(fixture.Unlock.Activate(), Is.True);
            Physics.SyncTransforms();

            NPCDoorTraversalStatus status =
                opener.EvaluatePath(Vector3.forward * 5f);

            Assert.That(status, Is.EqualTo(NPCDoorTraversalStatus.Clear));
            Assert.That(opener.DetectedDoor, Is.SameAs(fixture.PathDoor));
            Assert.That(fixture.PathDoor.IsPassable, Is.True);
            Assert.That(fixture.Unlock.IsActivated, Is.True);
        }

        [Test]
        public void DoorOpener_LockedDoorAheadBlocksWithoutActivating()
        {
            NPCDoorOpener opener = CreateOpener(Vector3.zero);
            DoorFixture fixture = CreateDoor(new Vector3(0f, 0.8f, 1.5f), 0f);
            fixture.Interaction.SetEnabled(false);
            Physics.SyncTransforms();

            NPCDoorTraversalStatus status =
                opener.EvaluatePath(Vector3.forward * 5f);

            Assert.That(status, Is.EqualTo(NPCDoorTraversalStatus.Blocked));
            Assert.That(opener.DetectedDoor, Is.SameAs(fixture.PathDoor));
            Assert.That(fixture.PathDoor.CurrentState, Is.EqualTo(NPCPathDoorState.Locked));
            Assert.That(fixture.Unlock.IsActivated, Is.False);
        }

        [Test]
        public void DoorOpener_DoorBehindTravelDirectionIsIgnored()
        {
            NPCDoorOpener opener = CreateOpener(Vector3.zero);
            DoorFixture fixture = CreateDoor(new Vector3(0f, 0.8f, -1.5f), 0f);
            Physics.SyncTransforms();

            NPCDoorTraversalStatus status =
                opener.EvaluatePath(Vector3.forward * 5f);

            Assert.That(status, Is.EqualTo(NPCDoorTraversalStatus.Clear));
            Assert.That(opener.DetectedDoor, Is.Null);
            Assert.That(fixture.Unlock.IsActivated, Is.False);
        }

        [Test]
        public void DoorOpener_OffsetProbeOriginUsesOriginToTargetDirection()
        {
            NPCDoorOpener opener = CreateOpener(Vector3.zero);
            Transform probeOrigin = CreateObject("Offset Probe Origin").transform;
            probeOrigin.position = new Vector3(5f, 0.8f, 0f);
            var serializedOpener = new SerializedObject(opener);
            serializedOpener.FindProperty("probeOrigin").objectReferenceValue = probeOrigin;
            serializedOpener.ApplyModifiedPropertiesWithoutUndo();

            DoorFixture fixture = CreateDoor(new Vector3(5f, 0.8f, 1.5f), 0f);
            Physics.SyncTransforms();

            opener.EvaluatePath(new Vector3(5f, 0.8f, 5f));

            Assert.That(opener.DetectedDoor, Is.SameAs(fixture.PathDoor));
        }

        [Test]
        public void QueueFallback_DoorOpeningHoldsActorWithoutEnablingFallback()
        {
            QueueFixture queueFixture = CreateQueue(Vector3.forward * 5f);
            DoorFixture doorFixture =
                CreateDoor(new Vector3(0f, 0.8f, 1.5f), 10f);
            Physics.SyncTransforms();
            Vector3 entryPosition = queueFixture.Member.transform.position;

            queueFixture.Queue.BeginQueue();

            object motion = GetOnlyMotion(queueFixture.Queue);
            Assert.That(queueFixture.Queue.IsRunning, Is.True);
            Assert.That(queueFixture.Member.transform.position, Is.EqualTo(entryPosition));
            Assert.That(GetMotionFlag(motion, "WaitingForDoor"), Is.True);
            Assert.That(GetMotionFlag(motion, "UseFallback"), Is.False);
            Assert.That(doorFixture.Unlock.IsActivated, Is.True);
        }

        [Test]
        public void QueueFallback_LockedDoorRaisesMovementFailureAndStopsQueue()
        {
            QueueFixture queueFixture = CreateQueue(Vector3.forward * 5f);
            DoorFixture doorFixture =
                CreateDoor(new Vector3(0f, 0.8f, 1.5f), 0f);
            doorFixture.Interaction.SetEnabled(false);
            NPCQueueMember failedMember = null;
            Transform failedDestination = null;
            int failureCount = 0;
            queueFixture.Queue.MovementFailed += (member, destination) =>
            {
                failedMember = member;
                failedDestination = destination;
                failureCount++;
            };
            Physics.SyncTransforms();

            queueFixture.Queue.BeginQueue();

            Assert.That(queueFixture.Queue.IsRunning, Is.False);
            Assert.That(failureCount, Is.EqualTo(1));
            Assert.That(failedMember, Is.SameAs(queueFixture.Member));
            Assert.That(failedDestination, Is.SameAs(queueFixture.ServicePoint));
            Assert.That(GetMotions(queueFixture.Queue), Is.Empty);
            Assert.That(doorFixture.Unlock.IsActivated, Is.False);
        }

        [Test]
        public void Queue_NearDestinationStillChecksLockedDoorBeforeArrival()
        {
            QueueFixture queueFixture = CreateQueue(Vector3.forward * 1.5f);
            var serializedQueue = new SerializedObject(queueFixture.Queue);
            serializedQueue.FindProperty("arrivalDistance").floatValue = 2f;
            serializedQueue.ApplyModifiedPropertiesWithoutUndo();
            DoorFixture doorFixture =
                CreateDoor(new Vector3(0f, 0.8f, 0.75f), 0f);
            doorFixture.Interaction.SetEnabled(false);
            int failureCount = 0;
            queueFixture.Queue.MovementFailed += (_, _) => failureCount++;
            Physics.SyncTransforms();

            queueFixture.Queue.BeginQueue();

            Assert.That(queueFixture.Queue.IsRunning, Is.False);
            Assert.That(failureCount, Is.EqualTo(1));
            Assert.That(queueFixture.Member.State, Is.Not.EqualTo(
                NPCQueueMemberState.ReadyForService));
            Assert.That(doorFixture.PathDoor.CurrentState,
                Is.EqualTo(NPCPathDoorState.Locked));
            Assert.That(doorFixture.Unlock.IsActivated, Is.False);
        }

        [Test]
        public void WaypointRoute_LockedDoorHoldsDestinationAttempt()
        {
            GameObject actor = CreateObject("Route NPC");
            actor.AddComponent<NPCController>();
            actor.AddComponent<NPCNavMeshMotor>();
            NPCDoorOpener opener = actor.AddComponent<NPCDoorOpener>();
            NPCWaypointRouteBehaviour behaviour =
                actor.AddComponent<NPCWaypointRouteBehaviour>();

            GameObject waypointObject = CreateObject("Route Destination");
            waypointObject.transform.position = Vector3.forward * 5f;
            NPCWaypoint waypoint = waypointObject.AddComponent<NPCWaypoint>();
            GameObject routeObject = CreateObject("Route");
            NPCWaypointRoute route = routeObject.AddComponent<NPCWaypointRoute>();
            var serializedRoute = new SerializedObject(route);
            SerializedProperty waypoints = serializedRoute.FindProperty("waypoints");
            waypoints.arraySize = 1;
            waypoints.GetArrayElementAtIndex(0).objectReferenceValue = waypoint;
            serializedRoute.ApplyModifiedPropertiesWithoutUndo();
            behaviour.SetRoute(route);

            DoorFixture doorFixture =
                CreateDoor(new Vector3(0f, 0.8f, 1.5f), 0f);
            doorFixture.Interaction.SetEnabled(false);
            Physics.SyncTransforms();

            InvokePrivate(behaviour, "TryBeginCurrentDestination");

            Assert.That(behaviour.DoorOpener, Is.SameAs(opener));
            Assert.That(opener.DetectedDoor, Is.SameAs(doorFixture.PathDoor));
            Assert.That(GetPrivateField<bool>(behaviour, "hasDestination"), Is.False);
            Assert.That(GetPrivateField<float>(behaviour, "retryTimer"), Is.GreaterThan(0f));
        }

        private NPCDoorOpener CreateOpener(Vector3 position)
        {
            GameObject actor = CreateObject("NPC Door Opener");
            actor.transform.position = position;
            return actor.AddComponent<NPCDoorOpener>();
        }

        private QueueFixture CreateQueue(Vector3 servicePosition)
        {
            GameObject queueObject = CreateObject("NPC Queue");
            NPCQueueController queue = queueObject.AddComponent<NPCQueueController>();
            Transform entryPoint = CreateObject("Queue Entry").transform;
            entryPoint.position = Vector3.zero;
            Transform servicePoint = CreateObject("Queue Service").transform;
            servicePoint.position = servicePosition;

            GameObject actor = CreateObject("Queue Member");
            actor.transform.position = entryPoint.position;
            NPCQueueMember member = actor.AddComponent<NPCQueueMember>();
            NPCDoorOpener opener = actor.AddComponent<NPCDoorOpener>();
            var serializedMember = new SerializedObject(member);
            serializedMember.FindProperty("doorOpener").objectReferenceValue = opener;
            serializedMember.ApplyModifiedPropertiesWithoutUndo();

            var serializedQueue = new SerializedObject(queue);
            serializedQueue.FindProperty("entryPoint").objectReferenceValue = entryPoint;
            serializedQueue.FindProperty("servicePoint").objectReferenceValue = servicePoint;
            serializedQueue.FindProperty("manageMemberVisibility").boolValue = false;
            serializedQueue.FindProperty("useDirectMovementFallback").boolValue = true;
            serializedQueue.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(queue.Enqueue(member), Is.True);
            return new QueueFixture(queue, member, servicePoint);
        }

        private DoorFixture CreateDoor(Vector3 position, float clearanceDelay)
        {
            GameObject doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdObjects.Add(doorObject);
            doorObject.name = "NPC Path Door";
            doorObject.transform.position = position;
            doorObject.transform.localScale = new Vector3(1.5f, 2f, 0.2f);

            Interactable interaction = doorObject.AddComponent<Interactable>();
            Animator animator = doorObject.AddComponent<Animator>();
            var controller = new AnimatorController();
            controller.AddLayer("Base Layer");
            controller.AddParameter(OpenTrigger, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CloseTrigger, AnimatorControllerParameterType.Trigger);
            animator.runtimeAnimatorController = controller;
            createdControllers.Add(controller);

            InteractableUnlock unlock = doorObject.AddComponent<InteractableUnlock>();
            var serializedUnlock = new SerializedObject(unlock);
            serializedUnlock.FindProperty("animator").objectReferenceValue = animator;
            serializedUnlock.FindProperty("animationOnTrigger").stringValue = OpenTrigger;
            serializedUnlock.FindProperty("isBinary").boolValue = true;
            serializedUnlock.FindProperty("animationOffTrigger").stringValue = CloseTrigger;
            serializedUnlock.ApplyModifiedPropertiesWithoutUndo();

            NPCPathDoor pathDoor = doorObject.AddComponent<NPCPathDoor>();
            var serializedDoor = new SerializedObject(pathDoor);
            serializedDoor.FindProperty("clearanceDelay").floatValue = clearanceDelay;
            serializedDoor.ApplyModifiedPropertiesWithoutUndo();

            return new DoorFixture(interaction, unlock, pathDoor);
        }

        private GameObject CreateObject(string objectName)
        {
            var created = new GameObject(objectName);
            createdObjects.Add(created);
            return created;
        }

        private static object GetOnlyMotion(NPCQueueController queue)
        {
            IList motions = GetMotions(queue);
            Assert.That(motions, Has.Count.EqualTo(1));
            return motions[0];
        }

        private static IList GetMotions(NPCQueueController queue)
        {
            FieldInfo field = typeof(NPCQueueController).GetField(
                "motions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(queue) as IList;
        }

        private static bool GetMotionFlag(object motion, string fieldName)
        {
            FieldInfo field = motion.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(motion);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private readonly struct DoorFixture
        {
            public DoorFixture(
                Interactable interaction,
                InteractableUnlock unlock,
                NPCPathDoor pathDoor)
            {
                Interaction = interaction;
                Unlock = unlock;
                PathDoor = pathDoor;
            }

            public Interactable Interaction { get; }
            public InteractableUnlock Unlock { get; }
            public NPCPathDoor PathDoor { get; }
        }

        private readonly struct QueueFixture
        {
            public QueueFixture(
                NPCQueueController queue,
                NPCQueueMember member,
                Transform servicePoint)
            {
                Queue = queue;
                Member = member;
                ServicePoint = servicePoint;
            }

            public NPCQueueController Queue { get; }
            public NPCQueueMember Member { get; }
            public Transform ServicePoint { get; }
        }
    }
}
