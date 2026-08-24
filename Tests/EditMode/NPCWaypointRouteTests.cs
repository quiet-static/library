using System.Collections.Generic;
using NUnit.Framework;
using QuietStatic.Toolkit.Characters.NPC;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NPCWaypointRouteTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(createdObjects[index]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void Waypoint_NormalizesWaitRangeAndResolvesForwardFacing()
        {
            NPCWaypoint waypoint = CreateWaypoint("Facing Point");
            waypoint.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var serialized = new SerializedObject(waypoint);
            serialized.FindProperty("minimumWaitDuration").floatValue = 3f;
            serialized.FindProperty("maximumWaitDuration").floatValue = 1f;
            serialized.FindProperty("facingMode").enumValueIndex =
                (int)NPCWaypoint.FacingMode.WaypointForward;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(waypoint.MinimumWaitDuration, Is.EqualTo(3f));
            Assert.That(waypoint.MaximumWaitDuration, Is.EqualTo(3f));
            Assert.That(waypoint.GetWaitDuration(), Is.EqualTo(3f));
            Assert.That(
                waypoint.TryGetFacingDirection(Vector3.zero, out Vector3 direction),
                Is.True);
            Assert.That(Vector3.Dot(direction, Vector3.right), Is.GreaterThan(0.999f));
        }

        [Test]
        public void Route_RefreshesWaypointsInHierarchyOrder()
        {
            GameObject routeObject = CreateObject("Route");
            NPCWaypointRoute route = routeObject.AddComponent<NPCWaypointRoute>();
            NPCWaypoint first = CreateWaypoint("First", routeObject.transform);
            NPCWaypoint second = CreateWaypoint("Second", routeObject.transform);

            route.RefreshWaypointsFromChildren();

            Assert.That(route.Count, Is.EqualTo(2));
            Assert.That(route.GetWaypoint(0), Is.SameAs(first));
            Assert.That(route.GetWaypoint(1), Is.SameAs(second));
            Assert.That(route.GetWaypoint(-1), Is.Null);
            Assert.That(route.GetWaypoint(2), Is.Null);
        }

        [Test]
        public void RouteBehaviour_LoopAndPingPongAdvanceAsConfigured()
        {
            NPCWaypointRoute route = CreateRoute(
                CreateWaypoint("Point 0"),
                CreateWaypoint("Point 1"),
                CreateWaypoint("Point 2"));
            NPCWaypointRouteBehaviour behaviour = CreateBehaviour(route);

            behaviour.SetTraversalMode(NPCWaypointTraversalMode.Loop);
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(0));
            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(1));
            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(2));
            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(0));

            behaviour.SetTraversalMode(NPCWaypointTraversalMode.PingPong);
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(0));
            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(1));
            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(2));
            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(1));
            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(0));
        }

        [Test]
        public void RouteBehaviour_OnceSkipsNullEntriesAndCompletesOnce()
        {
            NPCWaypoint first = CreateWaypoint("First");
            NPCWaypoint last = CreateWaypoint("Last");
            NPCWaypointRoute route = CreateRoute(first, null, last);
            NPCWaypointRouteBehaviour behaviour = CreateBehaviour(route);
            int completionCount = 0;
            behaviour.RouteCompleted += () => completionCount++;

            behaviour.SetTraversalMode(NPCWaypointTraversalMode.Once);
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(0));

            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(2));
            Assert.That(behaviour.IsRouteComplete, Is.False);

            behaviour.SkipCurrentWaypoint();
            Assert.That(behaviour.IsRouteComplete, Is.True);
            Assert.That(completionCount, Is.EqualTo(1));

            behaviour.SkipCurrentWaypoint();
            Assert.That(completionCount, Is.EqualTo(1));
        }

        [Test]
        public void RouteBehaviour_RandomTraversalDoesNotRepeatWhenAlternativesExist()
        {
            NPCWaypointRoute route = CreateRoute(
                CreateWaypoint("Point 0"),
                CreateWaypoint("Point 1"),
                CreateWaypoint("Point 2"));
            NPCWaypointRouteBehaviour behaviour = CreateBehaviour(route);
            behaviour.SetTraversalMode(NPCWaypointTraversalMode.Random);

            for (int attempt = 0; attempt < 25; attempt++)
            {
                int previous = behaviour.CurrentWaypointIndex;
                behaviour.SkipCurrentWaypoint();
                Assert.That(behaviour.CurrentWaypointIndex, Is.InRange(0, 2));
                Assert.That(behaviour.CurrentWaypointIndex, Is.Not.EqualTo(previous));
            }
        }

        [Test]
        public void RouteBehaviour_StartingOnNullEntrySelectsNextValidWaypoint()
        {
            NPCWaypointRoute route = CreateRoute(
                CreateWaypoint("Point 0"),
                null,
                CreateWaypoint("Point 2"));
            NPCWaypointRouteBehaviour behaviour = CreateBehaviour(route);

            behaviour.SetStartingWaypointIndex(1);
            behaviour.RestartRoute();

            Assert.That(behaviour.CurrentWaypointIndex, Is.EqualTo(2));
        }

        private NPCWaypointRouteBehaviour CreateBehaviour(NPCWaypointRoute route)
        {
            GameObject actor = CreateObject("Ambient NPC");
            actor.AddComponent<NPCController>();
            actor.AddComponent<NPCNavMeshMotor>();
            NPCWaypointRouteBehaviour behaviour = actor.AddComponent<NPCWaypointRouteBehaviour>();
            behaviour.SetRoute(route);
            return behaviour;
        }

        private NPCWaypointRoute CreateRoute(params NPCWaypoint[] waypoints)
        {
            GameObject routeObject = CreateObject("Authored Route");
            NPCWaypointRoute route = routeObject.AddComponent<NPCWaypointRoute>();
            var serialized = new SerializedObject(route);
            SerializedProperty points = serialized.FindProperty("waypoints");
            points.arraySize = waypoints.Length;
            for (int index = 0; index < waypoints.Length; index++)
            {
                points.GetArrayElementAtIndex(index).objectReferenceValue = waypoints[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return route;
        }

        private NPCWaypoint CreateWaypoint(string objectName, Transform parent = null)
        {
            GameObject waypointObject = CreateObject(objectName);
            waypointObject.transform.SetParent(parent);
            return waypointObject.AddComponent<NPCWaypoint>();
        }

        private GameObject CreateObject(string objectName)
        {
            var created = new GameObject(objectName);
            createdObjects.Add(created);
            return created;
        }
    }
}
