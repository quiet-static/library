using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NPCQueueControllerTests
    {
        private GameObject queueObject;
        private GameObject memberObject;
        private NPCQueueController queue;
        private NPCQueueMember member;

        [SetUp]
        public void SetUp()
        {
            queueObject = new GameObject("NPC Queue");
            queue = queueObject.AddComponent<NPCQueueController>();

            memberObject = new GameObject("Queue Member");
            member = memberObject.AddComponent<NPCQueueMember>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(queueObject);
            Object.DestroyImmediate(memberObject);
        }

        [Test]
        public void EmptyQueue_CompletesExactlyOnceForTheRun()
        {
            int completions = 0;
            queue.QueueCompleted += () => completions++;

            queue.BeginQueue();

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(queue.IsRunning, Is.False);
            Assert.That(queue.CurrentMember, Is.Null);
        }

        [Test]
        public void Queue_WaitsForExplicitServiceAndIgnoresDuplicateProgression()
        {
            int readyCallbacks = 0;
            int departureCallbacks = 0;
            int completions = 0;
            queue.MemberReadyForService += (_, _) => readyCallbacks++;
            queue.MemberDeparted += (_, _) => departureCallbacks++;
            queue.QueueCompleted += () => completions++;
            Assert.That(queue.Enqueue(member), Is.True);
            Assert.That(queue.Enqueue(member), Is.False);

            queue.BeginQueue();
            queue.BeginQueue();

            Assert.That(readyCallbacks, Is.EqualTo(1));
            Assert.That(queue.CurrentMember, Is.SameAs(member));
            Assert.That(queue.CurrentState, Is.EqualTo(NPCQueueMemberState.ReadyForService));
            Assert.That(queue.BeginService(), Is.True);
            Assert.That(queue.BeginService(), Is.False);
            Assert.That(queue.CompleteService(), Is.True);
            Assert.That(queue.CompleteService(), Is.False);
            Assert.That(departureCallbacks, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.Completed));
        }

        [Test]
        public void CancelQueue_DoesNotReportDepartureOrCompletion()
        {
            int departures = 0;
            int completions = 0;
            queue.MemberDeparted += (_, _) => departures++;
            queue.QueueCompleted += () => completions++;
            queue.Enqueue(member);
            queue.BeginQueue();

            queue.CancelQueue();

            Assert.That(queue.IsRunning, Is.False);
            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.Inactive));
            Assert.That(departures, Is.Zero);
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void CancelQueue_WhenVisibilityIsExternallyManaged_LeavesMemberActive()
        {
            SetField(queue, "manageMemberVisibility", false);
            Assert.That(queue.Enqueue(member), Is.True);
            queue.BeginQueue();

            queue.CancelQueue();

            Assert.That(member.gameObject.activeSelf, Is.True);
            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.Inactive));
        }

        [Test]
        public void RestoreReadyState_SuppressesPastLifecycleCallbacks()
        {
            int memberReadyCallbacks = 0;
            int queueReadyCallbacks = 0;
            UnityEvent readyEvent = GetField<UnityEvent>(member, "onReadyForService");
            readyEvent.AddListener(() => memberReadyCallbacks++);
            queue.MemberReadyForService += (_, _) => queueReadyCallbacks++;
            queue.Enqueue(member);

            queue.RestoreAt(0, NPCQueueMemberState.ReadyForService);

            Assert.That(queue.IsRunning, Is.True);
            Assert.That(queue.CurrentMember, Is.SameAs(member));
            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.ReadyForService));
            Assert.That(memberReadyCallbacks, Is.Zero);
            Assert.That(queueReadyCallbacks, Is.Zero);
        }

        [Test]
        public void RestoreCompletedState_EstablishesTerminalQueueIndices()
        {
            int completions = 0;
            queue.QueueCompleted += () => completions++;
            queue.Enqueue(member);

            queue.RestoreAt(0, NPCQueueMemberState.Completed);

            Assert.That(queue.IsRunning, Is.False);
            Assert.That(queue.CurrentIndex, Is.EqualTo(queue.Members.Count));
            Assert.That(queue.CurrentMember, Is.Null);
            Assert.That(queue.CurrentState, Is.EqualTo(NPCQueueMemberState.Completed));
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void NavMeshFailure_StopsQueueAndReportsMemberAndDestination()
        {
            var destinationObject = new GameObject("Service Point");
            try
            {
                destinationObject.transform.position = Vector3.right * 5f;
                SetField(queue, "servicePoint", destinationObject.transform);
                queue.Enqueue(member);
                NPCQueueMember failedMember = null;
                Transform failedDestination = null;
                queue.MovementFailed += (candidate, destination) =>
                {
                    failedMember = candidate;
                    failedDestination = destination;
                };

                queue.BeginQueue();

                Assert.That(queue.IsRunning, Is.False);
                Assert.That(failedMember, Is.SameAs(member));
                Assert.That(failedDestination, Is.SameAs(destinationObject.transform));
            }
            finally
            {
                Object.DestroyImmediate(destinationObject);
            }
        }

        [Test]
        public void NearDestination_StillRequiresUsableNavMeshDestination()
        {
            var destinationObject = new GameObject("Nearby Service Point");
            try
            {
                destinationObject.transform.position = Vector3.right * 0.1f;
                SetField(queue, "servicePoint", destinationObject.transform);
                queue.Enqueue(member);
                int failureCount = 0;
                queue.MovementFailed += (_, _) => failureCount++;

                queue.BeginQueue();

                Assert.That(queue.IsRunning, Is.False);
                Assert.That(failureCount, Is.EqualTo(1));
                Assert.That(member.State, Is.Not.EqualTo(
                    NPCQueueMemberState.ReadyForService));
            }
            finally
            {
                Object.DestroyImmediate(destinationObject);
            }
        }

        [Test]
        public void NavMeshCompletion_UsesResolvedEndpointInsteadOfRawTarget()
        {
            var destinationObject = new GameObject("Raw Destination");
            try
            {
                destinationObject.transform.position = Vector3.right * 5f;
                Vector3 resolvedDestination = Vector3.right;
                System.Type motionType = typeof(NPCQueueController).GetNestedType(
                    "Motion",
                    BindingFlags.NonPublic);
                Assert.That(motionType, Is.Not.Null);
                object motion = System.Activator.CreateInstance(motionType);
                SetMotionField(motion, "Member", member);
                SetMotionField(
                    motion,
                    "FinalState",
                    NPCQueueMemberState.ReadyForService);
                SetMotionField(motion, "UseFallback", false);
                SetMotionField(motion, "HasResolvedDestination", true);
                SetMotionField(motion, "ResolvedDestination", resolvedDestination);
                var targets = GetMotionField<System.Collections.IList>(motion, "Targets");
                targets.Add(destinationObject.transform);
                member.Motor.Agent.enabled = false;

                MethodInfo completeSegment = typeof(NPCQueueController).GetMethod(
                    "CompleteMotionSegment",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(completeSegment, Is.Not.Null);
                completeSegment.Invoke(
                    queue,
                    new[] { motion, member.transform, destinationObject.transform });

                Assert.That(member.transform.position, Is.EqualTo(resolvedDestination));
                Assert.That(member.transform.position,
                    Is.Not.EqualTo(destinationObject.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(destinationObject);
            }
        }

        [Test]
        public void PreServiceWaypoint_WaitsWithinConfiguredRangeAndRaisesArrivalOnce()
        {
            NPCWaypoint waypoint = CreateWaypoint("Browse", 3f, 5f);
            SetField(member, "preServiceWaypoints", new[] { waypoint.transform });
            SetField(queue, "useDirectMovementFallback", true);
            int arrivals = 0;
            NPCController arrivingController = null;
            GetField<UnityEvent<NPCController>>(waypoint, "onReached").AddListener(npc =>
            {
                arrivals++;
                arrivingController = npc;
            });
            queue.Enqueue(member);
            queue.BeginQueue();

            object motion = FindMotion(member);
            float duration = GetMotionField<float>(motion, "WaitRemaining");
            Assert.That(duration, Is.InRange(3f, 5f));
            Assert.That(arrivingController, Is.SameAs(member.Controller));
            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.Entering));
            InvokeQueue("UpdateWaypointWait", motion, duration - 0.25f);
            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.Entering));
            InvokeQueue("UpdateWaypointWait", motion, 0.25f);

            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.ReadyForService));
            Assert.That(arrivals, Is.EqualTo(1));
        }

        [Test]
        public void Departure_PreservesEveryBrowsingCustomerAndNewlyAdmittedRoute()
        {
            NPCQueueMember second = CreateMember("Second");
            NPCQueueMember third = CreateMember("Third");
            NPCQueueMember fourth = CreateMember("Fourth");
            NPCWaypoint waypoint = CreateWaypoint("Browse", 4f, 4f);
            Transform firstWaitingPoint = CreateTarget("First Waiting", Vector3.right * 10f);
            Transform secondWaitingPoint = CreateTarget("Second Waiting", Vector3.right * 20f);
            SetField(queue, "waitingPoints", new[] { firstWaitingPoint, secondWaitingPoint });
            SetField(queue, "maximumActiveMembers", 3);
            SetField(queue, "useDirectMovementFallback", true);
            foreach (NPCQueueMember shopper in new[] { second, third, fourth })
            {
                SetField(shopper, "preServiceWaypoints", new[] { waypoint.transform });
            }
            int arrivals = 0;
            GetField<UnityEvent<NPCController>>(waypoint, "onReached").AddListener(_ => arrivals++);
            foreach (NPCQueueMember shopper in new[] { member, second, third, fourth })
            {
                queue.Enqueue(shopper);
            }
            queue.BeginQueue();

            object secondMotion = FindMotion(second);
            object thirdMotion = FindMotion(third);
            InvokeQueue("UpdateWaypointWait", secondMotion, 1f);
            InvokeQueue("UpdateWaypointWait", thirdMotion, 2f);
            Assert.That(fourth.gameObject.activeSelf, Is.False);
            Assert.That(queue.CompleteService(), Is.True);

            Assert.That(queue.CurrentMember, Is.SameAs(second));
            Assert.That(FindMotion(second), Is.SameAs(secondMotion));
            Assert.That(GetMotionField<float>(secondMotion, "WaitRemaining"), Is.EqualTo(3f));
            Assert.That(GetMotionField<NPCQueueMemberState>(secondMotion, "FinalState"),
                Is.EqualTo(NPCQueueMemberState.ReadyForService));
            Assert.That(FindMotion(third), Is.SameAs(thirdMotion));
            Assert.That(GetMotionField<float>(thirdMotion, "WaitRemaining"), Is.EqualTo(2f));
            Assert.That(GetMotionField<IList>(thirdMotion, "Targets")[1], Is.SameAs(firstWaitingPoint));
            Assert.That(GetMotionField<bool>(FindMotion(fourth), "IsWaitingAtWaypoint"), Is.True);
            Assert.That(arrivals, Is.EqualTo(3));

            InvokeQueue("UpdateWaypointWait", secondMotion, 3f);
            Assert.That(second.State, Is.EqualTo(NPCQueueMemberState.ReadyForService));
            Assert.That(third.State, Is.EqualTo(NPCQueueMemberState.Entering));
            Assert.That(arrivals, Is.EqualTo(3));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WaypointWait_PauseResumeAndCancelPreserveRotationOwnership(bool originalAutomaticRotation)
        {
            NPCWaypoint waypoint = CreateWaypoint("Browse", 4f, 4f);
            waypoint.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            SetField(waypoint, "facingMode", NPCWaypoint.FacingMode.WaypointForward);
            SetField(member, "preServiceWaypoints", new[] { waypoint.transform });
            SetField(queue, "useDirectMovementFallback", true);
            member.Motor.Agent.updateRotation = originalAutomaticRotation;
            int arrivals = 0;
            GetField<UnityEvent<NPCController>>(waypoint, "onReached").AddListener(_ => arrivals++);
            queue.Enqueue(member);
            queue.BeginQueue();
            object motion = FindMotion(member);
            InvokeQueue("UpdateWaypointWait", motion, 1f);

            queue.PauseQueue();
            Assert.That(member.Motor.Agent.updateRotation, Is.EqualTo(originalAutomaticRotation));
            InvokeQueue("Update");
            Assert.That(GetMotionField<float>(motion, "WaitRemaining"), Is.EqualTo(3f));
            queue.ResumeQueue();
            Assert.That(GetMotionField<bool>(motion, "IsWaitingAtWaypoint"), Is.True);
            Assert.That(member.Motor.Agent.updateRotation, Is.False);
            Assert.That(GetMotionField<float>(motion, "WaitRemaining"), Is.EqualTo(3f));
            Assert.That(arrivals, Is.EqualTo(1));

            queue.CancelQueue(false);
            Assert.That(member.Motor.Agent.updateRotation, Is.EqualTo(originalAutomaticRotation));
            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.Inactive));
            Assert.That(GetField<IList>(queue, "motions").Count, Is.Zero);
        }

        [Test]
        public void PlainTransformsAndServiceWaypoints_DoNotIntroduceBrowsingWaits()
        {
            Transform plainWaypoint = CreateTarget("Pass Through", Vector3.zero);
            NPCWaypoint serviceWaypoint = CreateWaypoint("Service", 10f, 10f);
            SetField(member, "preServiceWaypoints", new[] { plainWaypoint });
            SetField(queue, "servicePoint", serviceWaypoint.transform);
            SetField(queue, "useDirectMovementFallback", true);
            int serviceWaypointArrivals = 0;
            GetField<UnityEvent<NPCController>>(serviceWaypoint, "onReached")
                .AddListener(_ => serviceWaypointArrivals++);
            queue.Enqueue(member);

            queue.BeginQueue();

            Assert.That(member.State, Is.EqualTo(NPCQueueMemberState.ReadyForService));
            Assert.That(serviceWaypointArrivals, Is.Zero);
        }

        [Test]
        public void WaypointArrival_StopsLocomotionBeforeArrivalCallbacksWithoutDelayedIdle()
        {
            NPCWaypoint waypoint = CreateWaypoint("Browse", 4f, 4f);
            SetField(member, "preServiceWaypoints", new[] { waypoint.transform });
            SetField(queue, "useDirectMovementFallback", true);
            int stopCallbacks = 0;
            var stopped = new UnityEvent();
            stopped.AddListener(() => stopCallbacks++);
            SetField(member.Motor, "onStoppedMoving", stopped);
            int stopsAtArrival = -1;
            GetField<UnityEvent<NPCController>>(waypoint, "onReached")
                .AddListener(_ => stopsAtArrival = stopCallbacks);
            queue.Enqueue(member);
            SetField(member.Motor, "wasMoving", true);

            queue.BeginQueue();
            typeof(NPCNavMeshMotor).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(member.Motor, null);

            Assert.That(stopsAtArrival, Is.EqualTo(1));
            Assert.That(stopCallbacks, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ArrivalCallback_PausesOtherCustomersImmediately(bool resumeFromPause)
        {
            NPCQueueMember second = CreateMember("Second");
            NPCWaypoint firstWaypoint = CreateWaypoint("First Browse", 4f, 4f);
            NPCWaypoint secondWaypoint = CreateWaypoint("Second Browse", 4f, 4f);
            firstWaypoint.transform.position = Vector3.right * 5f;
            secondWaypoint.transform.position = Vector3.left * 5f;
            SetField(member, "preServiceWaypoints", new[] { firstWaypoint.transform });
            SetField(second, "preServiceWaypoints", new[] { secondWaypoint.transform });
            SetField(queue, "waitingPoints", new[] { CreateTarget("Waiting", Vector3.forward * 5f) });
            SetField(queue, "maximumActiveMembers", 2);
            SetField(queue, "useDirectMovementFallback", true);
            GetField<UnityEvent<NPCController>>(secondWaypoint, "onReached")
                .AddListener(_ => queue.PauseQueue());
            queue.Enqueue(member);
            queue.Enqueue(second);
            queue.BeginQueue();
            object firstMotion = FindMotion(member);
            if (resumeFromPause)
            {
                queue.PauseQueue();
            }
            member.transform.position = firstWaypoint.transform.position;
            second.transform.position = secondWaypoint.transform.position;

            if (resumeFromPause)
            {
                queue.ResumeQueue();
            }
            else
            {
                InvokeQueue("Update");
            }

            Assert.That(queue.IsPaused, Is.True);
            Assert.That(GetMotionField<bool>(FindMotion(second), "IsWaitingAtWaypoint"), Is.True);
            Assert.That(GetMotionField<bool>(firstMotion, "IsWaitingAtWaypoint"), Is.False);
        }

        private Transform CreateTarget(string targetName, Vector3 position)
        {
            var target = new GameObject(targetName);
            target.transform.SetParent(queueObject.transform);
            target.transform.position = position;
            return target.transform;
        }

        private NPCWaypoint CreateWaypoint(string waypointName, float minimum, float maximum)
        {
            NPCWaypoint waypoint = CreateTarget(waypointName, Vector3.zero).gameObject.AddComponent<NPCWaypoint>();
            SetField(waypoint, "minimumWaitDuration", minimum);
            SetField(waypoint, "maximumWaitDuration", maximum);
            SetField(waypoint, "destinationJitterRadius", 0f);
            return waypoint;
        }

        private NPCQueueMember CreateMember(string memberName)
        {
            return CreateTarget(memberName, Vector3.zero).gameObject.AddComponent<NPCQueueMember>();
        }

        private object FindMotion(NPCQueueMember shopper)
        {
            foreach (object motion in GetField<IList>(queue, "motions"))
            {
                if (GetMotionField<NPCQueueMember>(motion, "Member") == shopper)
                {
                    return motion;
                }
            }
            Assert.Fail($"No motion for {shopper.name}.");
            return null;
        }

        private void InvokeQueue(string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(NPCQueueController).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing queue method '{methodName}'.");
            method.Invoke(queue, arguments);
        }

        private static T GetField<T>(object target, string name) where T : class
        {
            return GetFieldInfo(target, name).GetValue(target) as T;
        }

        private static void SetField(object target, string name, object value)
        {
            GetFieldInfo(target, name).SetValue(target, value);
        }

        private static T GetMotionField<T>(object motion, string name)
        {
            FieldInfo field = motion.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing motion field '{name}'.");
            return (T)field.GetValue(motion);
        }

        private static void SetMotionField(object motion, string name, object value)
        {
            FieldInfo field = motion.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing motion field '{name}'.");
            field.SetValue(motion, value);
        }

        private static FieldInfo GetFieldInfo(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            return field;
        }
    }
}
