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
    public sealed class NPCCrowdControllerPlayModeTests
    {
        private readonly List<GameObject> createdObjects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.Destroy(createdObjects[index]);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisableDuringStagger_ResumesRemainingMembersWhenStopsAreDisabled()
        {
            NPCWaypointRoute route = CreateRoute();
            NPCWaypointRouteBehaviour first = CreateRouteBehaviour("First NPC", route);
            NPCWaypointRouteBehaviour second = CreateRouteBehaviour("Second NPC", route);
            NPCCrowdController crowd = CreateCrowd(first, second);
            int allMembersStartedCount = 0;
            GetField<UnityEvent>(crowd, "onAllMembersStarted")
                .AddListener(() => allMembersStartedCount++);

            yield return null;
            crowd.StartCrowd();

            Assert.That(first.IsBehaviourActive, Is.True);
            Assert.That(second.IsBehaviourActive, Is.False);
            Assert.That(crowd.IsRunning, Is.True);

            crowd.enabled = false;
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(first.IsBehaviourActive, Is.True);
            Assert.That(second.IsBehaviourActive, Is.False);
            Assert.That(crowd.IsRunning, Is.True);

            crowd.enabled = true;
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(first.IsBehaviourActive, Is.True);
            Assert.That(second.IsBehaviourActive, Is.True);
            Assert.That(crowd.IsRunning, Is.True);
            Assert.That(allMembersStartedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LastMemberCallbackDisablesCrowd_AllStartedEventWaitsForReenable()
        {
            NPCWaypointRoute route = CreateRoute();
            NPCWaypointRouteBehaviour first = CreateRouteBehaviour("First NPC", route);
            NPCWaypointRouteBehaviour second = CreateRouteBehaviour("Second NPC", route);
            NPCCrowdController crowd = CreateCrowd(first, second);
            int allMembersStartedCount = 0;
            GetField<UnityEvent>(crowd, "onAllMembersStarted")
                .AddListener(() => allMembersStartedCount++);
            second.RouteStarted += () => crowd.enabled = false;

            yield return null;
            crowd.StartCrowd();
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(crowd.enabled, Is.False);
            Assert.That(crowd.IsRunning, Is.True);
            Assert.That(first.IsBehaviourActive, Is.True);
            Assert.That(second.IsBehaviourActive, Is.True);
            Assert.That(allMembersStartedCount, Is.Zero);

            crowd.enabled = true;
            yield return null;

            Assert.That(allMembersStartedCount, Is.EqualTo(1));
        }

        private NPCCrowdController CreateCrowd(
            NPCWaypointRouteBehaviour first,
            NPCWaypointRouteBehaviour second)
        {
            GameObject crowdObject = Track(new GameObject("NPC Crowd"));
            NPCCrowdController crowd = crowdObject.AddComponent<NPCCrowdController>();
            SetField(crowd, "startOnStart", false);
            SetField(crowd, "restartRoutesWhenStarted", false);
            SetField(crowd, "initialDelay", 0f);
            SetField(crowd, "staggerInterval", 0.1f);
            SetField(crowd, "maximumStaggerJitter", 0f);
            SetField(crowd, "stopMembersOnDisable", false);
            SetField(crowd, "members", new[]
            {
                CreateMember(first),
                CreateMember(second),
            });
            return crowd;
        }

        private NPCWaypointRouteBehaviour CreateRouteBehaviour(
            string objectName,
            NPCWaypointRoute route)
        {
            GameObject actor = Track(new GameObject(objectName));
            actor.AddComponent<NPCController>();
            actor.AddComponent<NPCNavMeshMotor>();
            NPCWaypointRouteBehaviour behaviour =
                actor.AddComponent<NPCWaypointRouteBehaviour>();
            SetField(behaviour, "activeOnStart", false, typeof(NPCBehaviour));
            behaviour.SetRoute(route);
            return behaviour;
        }

        private NPCWaypointRoute CreateRoute()
        {
            GameObject routeObject = Track(new GameObject("Crowd Route"));
            NPCWaypointRoute route = routeObject.AddComponent<NPCWaypointRoute>();
            GameObject waypointObject = Track(new GameObject("Crowd Waypoint"));
            waypointObject.transform.position = Vector3.forward * 5f;
            NPCWaypoint waypoint = waypointObject.AddComponent<NPCWaypoint>();
            SetField(route, "waypoints", new[] { waypoint });
            return route;
        }

        private static NPCCrowdController.CrowdMember CreateMember(
            NPCWaypointRouteBehaviour routeBehaviour)
        {
            var member = new NPCCrowdController.CrowdMember();
            SetField(member, "routeBehaviour", routeBehaviour);
            return member;
        }

        private GameObject Track(GameObject value)
        {
            createdObjects.Add(value);
            return value;
        }

        private static T GetField<T>(object target, string name) where T : class
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            return field.GetValue(target) as T;
        }

        private static void SetField(
            object target,
            string name,
            object value,
            System.Type declaringType = null)
        {
            FieldInfo field = (declaringType ?? target.GetType()).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
