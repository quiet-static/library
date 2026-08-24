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
