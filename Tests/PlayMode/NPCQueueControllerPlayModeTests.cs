using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class NPCQueueControllerPlayModeTests
    {
        private GameObject host;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StaggeredArrivals_RespectSpacingPauseAndRestart()
        {
            NPCQueueController queue = CreateStaggeredQueue(out NPCQueueMember[] customers);
            var enteredAt = new float[] { -1f, -1f, -1f };
            for (int index = 0; index < customers.Length; index++)
            {
                int capturedIndex = index;
                GetField<UnityEvent>(customers[index], "onEntering")
                    .AddListener(() => enteredAt[capturedIndex] = Time.time);
            }
            host.SetActive(true);
            queue.BeginQueue();
            Assert.That(customers[0].gameObject.activeSelf, Is.True);
            Assert.That(customers[1].gameObject.activeSelf, Is.False);
            queue.PauseQueue();
            yield return new WaitForSeconds(0.4f);
            Assert.That(customers[1].gameObject.activeSelf, Is.False);
            queue.ResumeQueue();
            yield return new WaitForSeconds(0.1f);
            Assert.That(customers[1].gameObject.activeSelf, Is.False, "Pause must preserve the arrival delay.");
            float deadline = Time.time + 2f;
            while (!customers[2].gameObject.activeSelf && Time.time < deadline)
                yield return null;
            Assert.That(customers[2].gameObject.activeSelf, Is.True);
            Assert.That(enteredAt[1] - enteredAt[0], Is.GreaterThanOrEqualTo(0.65f));
            Assert.That(enteredAt[2] - enteredAt[1], Is.GreaterThanOrEqualTo(0.28f));

            queue.CancelQueue();
            queue.BeginQueue();
            Assert.That(customers[0].gameObject.activeSelf, Is.True, "Restart should not inherit the old cooldown.");
            Assert.That(customers[1].gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator FastCheckout_WaitsForNextArrivalAndPlacesItAtEntrance()
        {
            NPCQueueController queue = CreateStaggeredQueue(out NPCQueueMember[] customers);
            Transform entrance = GetField<Transform>(queue, "entryPoint");
            SetField(queue, "servicePoint", entrance);
            SetField(queue, "exitPoint", entrance);
            host.SetActive(true);
            queue.BeginQueue();
            Assert.That(queue.CompleteService(), Is.True);
            Assert.That(customers[0].State, Is.EqualTo(NPCQueueMemberState.Completed));
            Assert.That(customers[1].gameObject.activeSelf, Is.False);
            yield return new WaitForSeconds(0.1f);
            Assert.That(customers[1].gameObject.activeSelf, Is.False);
            float deadline = Time.time + 2f;
            while (!customers[1].gameObject.activeSelf && Time.time < deadline)
                yield return null;
            Assert.That(customers[1].State, Is.EqualTo(NPCQueueMemberState.ReadyForService));
            Assert.That(customers[1].transform.position, Is.EqualTo(entrance.position));
        }

        private NPCQueueController CreateStaggeredQueue(out NPCQueueMember[] customers)
        {
            host = new GameObject("Staggered Customer Queue");
            host.SetActive(false);
            NPCQueueController queue = host.AddComponent<NPCQueueController>();
            SetField(queue, "arrivalInterval", 0.3f);
            SetField(queue, "maximumActiveMembers", 3);
            SetField(queue, "useDirectMovementFallback", true);
            SetField(queue, "entryPoint", CreateTarget("Entrance", Vector3.zero));
            SetField(queue, "servicePoint", CreateTarget("Register", Vector3.right * 2f));
            SetField(queue, "waitingPoints", new[]
            {
                CreateTarget("Line 1", new Vector3(2f, 0f, 1f)),
                CreateTarget("Line 2", new Vector3(2f, 0f, 2f)),
            });
            customers = new NPCQueueMember[3];
            for (int index = 0; index < customers.Length; index++)
            {
                var actor = CreateTarget($"Customer {index}", new Vector3(-10f, 0f, index)).gameObject;
                customers[index] = actor.AddComponent<NPCQueueMember>();
                actor.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            }
            SetField(queue, "initialMembers", customers);
            return queue;
        }

        [UnityTest]
        public IEnumerator ConcurrentCustomers_FinishTheirOwnBrowseWaitBeforeOrderedService()
        {
            host = new GameObject("Concurrent Customer Queue");
            host.SetActive(false);
            NPCQueueController queue = host.AddComponent<NPCQueueController>();
            SetField(queue, "maximumActiveMembers", 3);
            SetField(queue, "useDirectMovementFallback", true);
            SetField(queue, "fallbackMovementSpeed", 20f);
            SetField(queue, "servicePoint", CreateTarget("Register", new Vector3(4f, 0f, 0f)));
            SetField(queue, "exitPoint", CreateTarget("Exit", new Vector3(6f, 0f, 0f)));
            SetField(queue, "waitingPoints", new[]
            {
                CreateTarget("Line 1", new Vector3(4f, 0f, 1f)),
                CreateTarget("Line 2", new Vector3(4f, 0f, 2f)),
            });

            var customers = new NPCQueueMember[3];
            var stops = new Transform[3];
            var arrivals = new float[] { -1f, -1f, -1f };
            var arrivalCounts = new int[3];
            var departedStop = new bool[3];
            var durations = new[] { 3f, 5f, 4f };
            for (int index = 0; index < customers.Length; index++)
            {
                int customerIndex = index;
                Vector3 position = new Vector3(0f, 0f, index);
                GameObject actor = CreateTarget($"Customer {index}", position).gameObject;
                customers[index] = actor.AddComponent<NPCQueueMember>();
                actor.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                stops[index] = CreateTarget($"Browse Stop {index}", position);
                NPCWaypoint waypoint = stops[index].gameObject.AddComponent<NPCWaypoint>();
                SetField(waypoint, "minimumWaitDuration", durations[index]);
                SetField(waypoint, "maximumWaitDuration", durations[index]);
                SetField(waypoint, "destinationJitterRadius", 0f);
                GetField<UnityEvent<NPCController>>(waypoint, "onReached").AddListener(_ =>
                {
                    arrivals[customerIndex] = Time.time;
                    arrivalCounts[customerIndex]++;
                });
                SetField(customers[index], "preServiceWaypoints", new[] { stops[index] });
            }

            SetField(queue, "initialMembers", customers);
            var serviceOrder = new List<int>();
            queue.MemberReadyForService += (_, index) => serviceOrder.Add(index);
            host.SetActive(true);
            queue.BeginQueue();
            foreach (NPCQueueMember customer in customers)
            {
                Assert.That(customer.gameObject.activeInHierarchy, Is.True);
                Assert.That(customer.State, Is.EqualTo(NPCQueueMemberState.Entering));
            }

            float deadline = Time.time + 9f;
            while (queue.IsRunning && Time.time < deadline)
            {
                for (int index = 0; index < customers.Length; index++)
                {
                    if (arrivals[index] < 0f || departedStop[index])
                    {
                        continue;
                    }

                    float elapsed = Time.time - arrivals[index];
                    bool moved = Vector3.Distance(customers[index].transform.position,
                        stops[index].position) > 0.01f;
                    if (moved)
                    {
                        Assert.That(elapsed, Is.GreaterThanOrEqualTo(durations[index] - 0.08f),
                            $"Customer {index} left its browsing stop too early.");
                        Assert.That(elapsed, Is.LessThan(durations[index] + 0.8f),
                            $"Customer {index}'s wait restarted when the line advanced.");
                        departedStop[index] = true;
                    }
                }

                if (queue.CurrentState == NPCQueueMemberState.ReadyForService)
                {
                    Assert.That(queue.BeginService(), Is.True);
                    Assert.That(queue.CompleteService(), Is.True);
                }
                yield return null;
            }

            Assert.That(queue.IsRunning, Is.False, "Every customer should finish and leave.");
            Assert.That(serviceOrder, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(arrivalCounts, Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(departedStop, Is.All.True);
            foreach (NPCQueueMember customer in customers)
            {
                Assert.That(customer.State, Is.EqualTo(NPCQueueMemberState.Completed));
            }
        }

        private Transform CreateTarget(string name, Vector3 position)
        {
            var target = new GameObject(name);
            target.transform.SetParent(host.transform);
            target.transform.position = position;
            return target.transform;
        }

        private static T GetField<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
