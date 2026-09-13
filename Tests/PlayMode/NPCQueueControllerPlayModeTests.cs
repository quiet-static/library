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
