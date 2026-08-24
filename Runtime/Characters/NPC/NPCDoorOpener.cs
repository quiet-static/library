using UnityEngine;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Result of checking the next short NPC movement segment for an openable door.</summary>
    public enum NPCDoorTraversalStatus
    {
        Clear,
        Waiting,
        Blocked
    }

    /// <summary>Detects explicitly marked doors immediately ahead and requests safe passage.</summary>
    /// <remarks>
    /// This component does not move the NPC. A queue, route, or other movement owner calls
    /// <see cref="EvaluatePath"/> and holds movement while it returns
    /// <see cref="NPCDoorTraversalStatus.Waiting"/>.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/NPC/NPC Door Opener")]
    [DisallowMultipleComponent]
    public sealed class NPCDoorOpener : MonoBehaviour
    {
        private const int MaximumProbeHits = 16;

        [Header("Movement")]
        [Tooltip("Optional motor whose current steering target defines the next NavMesh path segment.")]
        [SerializeField] private NPCNavMeshMotor motor;

        [Header("Probe")]
        [Tooltip("Optional world-space origin. When omitted, the NPC transform plus Vertical Offset is used.")]
        [SerializeField] private Transform probeOrigin;

        [Min(0f)]
        [Tooltip("Height above the NPC pivot used when Probe Origin is omitted.")]
        [SerializeField] private float verticalOffset = 0.8f;

        [Min(0.05f)]
        [Tooltip("Radius of the short sphere cast used to find a door in the movement corridor.")]
        [SerializeField] private float probeRadius = 0.3f;

        [Min(0.1f)]
        [Tooltip("Maximum distance ahead at which the NPC requests a door to open.")]
        [SerializeField] private float probeDistance = 2.5f;

        [Tooltip("Physics layers that may contain NPC Path Door colliders.")]
        [SerializeField] private LayerMask doorLayers = ~0;

        private readonly RaycastHit[] probeHits = new RaycastHit[MaximumProbeHits];
        private NPCPathDoor pendingDoor;

        /// <summary>Gets the nearest marked door detected by the most recent evaluation.</summary>
        public NPCPathDoor DetectedDoor { get; private set; }

        private void Reset()
        {
            motor = GetComponent<NPCNavMeshMotor>();
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<NPCNavMeshMotor>();
            }
        }

        /// <summary>
        /// Checks the short corridor toward a movement destination and requests the nearest
        /// marked door to open when necessary.
        /// </summary>
        /// <param name="destination">Current movement destination in world space.</param>
        /// <returns>Whether movement may continue, should wait, or is blocked by a locked door.</returns>
        public NPCDoorTraversalStatus EvaluatePath(Vector3 destination)
        {
            DetectedDoor = null;
            if (pendingDoor != null)
            {
                DetectedDoor = pendingDoor;
                NPCDoorTraversalStatus pendingStatus = ToTraversalStatus(
                    pendingDoor.RequestPassage());
                if (pendingStatus != NPCDoorTraversalStatus.Waiting)
                {
                    pendingDoor = null;
                }

                return pendingStatus;
            }

            Vector3 origin = probeOrigin != null
                ? probeOrigin.position
                : transform.position + Vector3.up * verticalOffset;
            Vector3 pathTarget = ResolveProbeTarget(destination);
            DetectedDoor = FindNearestDoor(origin, pathTarget);
            if (DetectedDoor == null &&
                (pathTarget - destination).sqrMagnitude > 0.001f)
            {
                // A closed carving door can make the NavMesh steer elsewhere. The authored
                // destination corridor is the fallback that lets the NPC request that door.
                DetectedDoor = FindNearestDoor(origin, destination);
            }

            if (DetectedDoor == null)
            {
                return NPCDoorTraversalStatus.Clear;
            }

            NPCDoorTraversalStatus status = ToTraversalStatus(DetectedDoor.RequestPassage());
            if (status == NPCDoorTraversalStatus.Waiting)
            {
                pendingDoor = DetectedDoor;
            }

            return status;
        }

        /// <summary>Forgets a door wait when the owning movement operation is cancelled.</summary>
        public void ClearPendingDoor()
        {
            pendingDoor = null;
            DetectedDoor = null;
        }

        private NPCPathDoor FindNearestDoor(Vector3 origin, Vector3 target)
        {
            Vector3 direction = target - origin;
            direction.y = 0f;
            float destinationDistance = direction.magnitude;
            if (destinationDistance <= 0.001f)
            {
                return null;
            }

            direction /= destinationDistance;
            float castDistance = Mathf.Min(probeDistance, destinationDistance);
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
                direction,
                probeHits,
                castDistance,
                doorLayers,
                QueryTriggerInteraction.Ignore);

            NPCPathDoor nearestDoor = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = probeHits[index].collider;
                if (collider == null || IsOwnedCollider(collider.transform))
                {
                    continue;
                }

                NPCPathDoor door = collider.GetComponentInParent<NPCPathDoor>();
                if (door == null || probeHits[index].distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = probeHits[index].distance;
                nearestDoor = door;
            }

            return nearestDoor;
        }

        private static NPCDoorTraversalStatus ToTraversalStatus(NPCPathDoorState state)
        {
            switch (state)
            {
                case NPCPathDoorState.Open:
                    return NPCDoorTraversalStatus.Clear;
                case NPCPathDoorState.Opening:
                    return NPCDoorTraversalStatus.Waiting;
                default:
                    return NPCDoorTraversalStatus.Blocked;
            }
        }

        private bool IsOwnedCollider(Transform candidate)
        {
            return candidate == transform || candidate.IsChildOf(transform);
        }

        private Vector3 ResolveProbeTarget(Vector3 destination)
        {
            UnityEngine.AI.NavMeshAgent agent = motor != null ? motor.Agent : null;
            if (agent == null ||
                !agent.enabled ||
                !agent.isOnNavMesh ||
                agent.pathPending ||
                !agent.hasPath)
            {
                return destination;
            }

            Vector3 steeringOffset = agent.steeringTarget - transform.position;
            steeringOffset.y = 0f;
            return steeringOffset.sqrMagnitude > 0.001f
                ? agent.steeringTarget
                : destination;
        }

        private void OnValidate()
        {
            verticalOffset = Mathf.Max(0f, verticalOffset);
            probeRadius = Mathf.Max(0.05f, probeRadius);
            probeDistance = Mathf.Max(0.1f, probeDistance);
        }

        private void OnDisable()
        {
            ClearPendingDoor();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = probeOrigin != null
                ? probeOrigin.position
                : transform.position + Vector3.up * verticalOffset;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin + transform.forward * probeDistance, probeRadius);
            Gizmos.DrawLine(origin, origin + transform.forward * probeDistance);
        }
    }
}
