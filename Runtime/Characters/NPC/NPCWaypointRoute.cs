using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Stores an ordered, scene-owned collection of NPC waypoints.</summary>
    /// <remarks>
    /// Waypoint positions belong to a scene, so this route is a MonoBehaviour rather than a
    /// ScriptableObject. Multiple NPC route behaviours may safely share one route because each
    /// behaviour owns its own traversal state.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NPCWaypointRoute : MonoBehaviour
    {
        [Tooltip("Waypoints in authored traversal order. Null entries are ignored by route behaviours.")]
        [SerializeField] private NPCWaypoint[] waypoints = Array.Empty<NPCWaypoint>();

        /// <summary>Gets the number of authored entries, including any null entries.</summary>
        public int Count => waypoints?.Length ?? 0;

        /// <summary>Gets the authored waypoint collection as a read-only list.</summary>
        public IReadOnlyList<NPCWaypoint> Waypoints => waypoints ?? Array.Empty<NPCWaypoint>();

        /// <summary>Gets a waypoint by authored array index, or null when the index is invalid.</summary>
        public NPCWaypoint GetWaypoint(int index)
        {
            return waypoints != null && index >= 0 && index < waypoints.Length
                ? waypoints[index]
                : null;
        }

        /// <summary>
        /// Rebuilds the route from child NPCWaypoint components in hierarchy order.
        /// This is available from the component context menu for quick scene authoring.
        /// </summary>
        [ContextMenu("Refresh Waypoints From Children")]
        public void RefreshWaypointsFromChildren()
        {
            waypoints = GetComponentsInChildren<NPCWaypoint>(true);
        }

        private void Reset()
        {
            RefreshWaypointsFromChildren();
        }

        private void OnDrawGizmosSelected()
        {
            if (waypoints == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.9f);
            NPCWaypoint previous = null;
            foreach (NPCWaypoint waypoint in waypoints)
            {
                if (waypoint == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(waypoint.transform.position, 0.08f);
                if (previous != null)
                {
                    Gizmos.DrawLine(previous.transform.position, waypoint.transform.position);
                }

                previous = waypoint;
            }
        }
    }
}
