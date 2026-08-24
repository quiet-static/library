using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Traversal policies available to an authored NPC waypoint route.</summary>
    public enum NPCWaypointTraversalMode
    {
        Once,
        Loop,
        PingPong,
        Random
    }

    /// <summary>Moves one NPC through a scene-owned waypoint route.</summary>
    /// <remarks>
    /// This behavior owns route traversal only. <see cref="NPCNavMeshMotor"/> owns movement,
    /// <see cref="NPCAnimatorDriver"/> owns locomotion presentation, and
    /// <see cref="NPCAnimationTrigger"/> receives optional waypoint animation cues. An optional
    /// <see cref="NPCDoorOpener"/> can hold each movement segment until marked doors are passable.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPCController), typeof(NPCNavMeshMotor))]
    public sealed class NPCWaypointRouteBehaviour : NPCBehaviour
    {
        /// <summary>Inspector event carrying the waypoint reached by this NPC.</summary>
        [Serializable]
        public sealed class WaypointEvent : UnityEvent<NPCWaypoint> { }

        [Header("Route")]
        [Tooltip("Scene-owned ordered route traversed by this NPC.")]
        [SerializeField] private NPCWaypointRoute route;

        [Tooltip("How the NPC advances after reaching the end of the authored route.")]
        [SerializeField] private NPCWaypointTraversalMode traversalMode = NPCWaypointTraversalMode.Loop;

        [Tooltip("Authored route index used when the route begins. Invalid and null entries resolve to the nearest valid waypoint.")]
        [Min(0)]
        [SerializeField] private int startingWaypointIndex;

        [Tooltip("Choose any valid waypoint when the route begins instead of using Starting Waypoint Index.")]
        [SerializeField] private bool randomizeStartingWaypoint;

        [Header("Movement")]
        [Tooltip("NavMesh motor used to reach each waypoint.")]
        [SerializeField] private NPCNavMeshMotor motor;

        [Tooltip("Optional local door adapter that requests passage through marked doors along the route.")]
        [SerializeField] private NPCDoorOpener doorOpener;

        [Tooltip("Radius used to project requested waypoint destinations onto the NavMesh.")]
        [Min(0.1f)]
        [SerializeField] private float destinationSampleRadius = 2f;

        [Tooltip("Delay before retrying when the motor or a waypoint path is temporarily unavailable.")]
        [Min(0.02f)]
        [SerializeField] private float destinationRetryDelay = 0.5f;

        [Header("Animation")]
        [Tooltip("Optional adapter that receives animation triggers configured on reached waypoints.")]
        [SerializeField] private NPCAnimationTrigger animationTriggerPlayer;

        [Header("Events")]
        [Tooltip("Invoked when a valid route starts or restarts.")]
        [SerializeField] private UnityEvent onRouteStarted = new();

        [Tooltip("Invoked after the NPC reaches a waypoint and its configured animation trigger is requested.")]
        [SerializeField] private WaypointEvent onWaypointReached = new();

        [Tooltip("Invoked when a route using Once reaches its final valid waypoint.")]
        [SerializeField] private UnityEvent onRouteCompleted = new();

        private readonly List<int> validWaypointIndices = new();
        private int routeCursor = -1;
        private int traversalDirection = 1;
        private bool routePrepared;
        private bool routeStartedRaised;
        private bool hasDestination;
        private bool hasRequestedDestination;
        private bool isWaiting;
        private bool isFacing;
        private bool restoreAutomaticRotation;
        private bool previousAutomaticRotation;
        private float waitTimer;
        private float retryTimer;
        private Vector3 requestedDestination;
        private Vector3 resolvedDestination;

        /// <summary>Raised when a valid route starts or restarts.</summary>
        public event Action RouteStarted;

        /// <summary>Raised when this NPC reaches a waypoint.</summary>
        public event Action<NPCWaypoint> WaypointReached;

        /// <summary>Raised when a Once route reaches its final valid waypoint.</summary>
        public event Action RouteCompleted;

        /// <summary>Gets the currently assigned route.</summary>
        public NPCWaypointRoute Route => route;

        /// <summary>Gets the optional adapter used to request passage through marked doors.</summary>
        public NPCDoorOpener DoorOpener => doorOpener;

        /// <summary>Gets the current traversal policy.</summary>
        public NPCWaypointTraversalMode TraversalMode => traversalMode;

        /// <summary>Gets the current authored waypoint index, or -1 when no valid route exists.</summary>
        public int CurrentWaypointIndex => routeCursor >= 0 && routeCursor < validWaypointIndices.Count
            ? validWaypointIndices[routeCursor]
            : -1;

        /// <summary>Gets the waypoint this NPC is moving toward or waiting at.</summary>
        public NPCWaypoint CurrentWaypoint => route?.GetWaypoint(CurrentWaypointIndex);

        /// <summary>Gets whether the NPC is dwelling at its current waypoint.</summary>
        public bool IsWaitingAtWaypoint => isWaiting;

        /// <summary>Gets whether a Once route has reached its final waypoint.</summary>
        public bool IsRouteComplete { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (motor == null)
            {
                motor = GetComponent<NPCNavMeshMotor>();
            }

            if (doorOpener == null)
            {
                doorOpener = GetComponent<NPCDoorOpener>();
            }

            if (animationTriggerPlayer == null)
            {
                animationTriggerPlayer = GetComponent<NPCAnimationTrigger>();
            }
        }

        private void Reset()
        {
            motor = GetComponent<NPCNavMeshMotor>();
            doorOpener = GetComponent<NPCDoorOpener>();
            animationTriggerPlayer = GetComponent<NPCAnimationTrigger>();
        }

        private void OnEnable()
        {
            if (IsBehaviourActive && isWaiting)
            {
                BeginFacing(CurrentWaypoint);
            }
        }

        private void OnDisable()
        {
            hasDestination = false;
            motor?.Stop();
            doorOpener?.ClearPendingDoor();
            EndFacing();
        }

        private void Update()
        {
            if (!IsBehaviourActive || IsRouteComplete)
            {
                return;
            }

            if (!EnsurePreparedRoute())
            {
                return;
            }

            if (isWaiting)
            {
                UpdateWaypointWait();
                return;
            }

            if (hasDestination)
            {
                UpdateDestinationProgress();
                return;
            }

            retryTimer -= Time.deltaTime;
            if (retryTimer <= 0f)
            {
                TryBeginCurrentDestination();
            }
        }

        /// <summary>Assigns a different scene route and resets traversal without changing active state.</summary>
        /// <param name="newRoute">Route to assign, or null to clear the current route.</param>
        public void SetRoute(NPCWaypointRoute newRoute)
        {
            CancelCurrentSegment();
            route = newRoute;
            ResetTraversalState();
            EnsurePreparedRoute();

            if (IsBehaviourActive)
            {
                RaiseRouteStartedIfNeeded();
            }
        }

        /// <summary>Changes traversal policy and restarts the route from its configured start.</summary>
        /// <param name="mode">New traversal policy.</param>
        public void SetTraversalMode(NPCWaypointTraversalMode mode)
        {
            traversalMode = mode;
            RestartRoute();
        }

        /// <summary>Changes the authored starting index used by future route restarts.</summary>
        /// <param name="index">Authored route index, clamped when the route restarts.</param>
        public void SetStartingWaypointIndex(int index)
        {
            startingWaypointIndex = Mathf.Max(0, index);
        }

        /// <summary>Starts or resumes traversal. A completed Once route is restarted.</summary>
        public void StartRoute()
        {
            if (IsRouteComplete)
            {
                RestartRoute();
                return;
            }

            if (!EnsurePreparedRoute())
            {
                return;
            }

            SetBehaviourActive(true);
            RaiseRouteStartedIfNeeded();
        }

        /// <summary>Pauses traversal and clears the active NavMesh path.</summary>
        public void StopRoute()
        {
            SetBehaviourActive(false);
        }

        /// <summary>Resets traversal to its configured start and activates the route.</summary>
        public void RestartRoute()
        {
            CancelCurrentSegment();
            ResetTraversalState();
            if (!EnsurePreparedRoute())
            {
                return;
            }

            SetBehaviourActive(true);
            RaiseRouteStartedIfNeeded();
        }

        /// <summary>
        /// Advances without treating the current waypoint as reached. Scene logic may use this
        /// to bypass a temporarily unsuitable or intentionally omitted stop.
        /// </summary>
        public void SkipCurrentWaypoint()
        {
            if (IsRouteComplete || !EnsurePreparedRoute())
            {
                return;
            }

            CancelCurrentSegment();
            AdvanceRoute();
        }

        protected override void OnBehaviourActivated()
        {
            if (!EnsurePreparedRoute())
            {
                return;
            }

            retryTimer = 0f;
            if (isWaiting)
            {
                BeginFacing(CurrentWaypoint);
            }

            RaiseRouteStartedIfNeeded();
        }

        protected override void OnBehaviourDeactivated()
        {
            hasDestination = false;
            motor?.Stop();
            doorOpener?.ClearPendingDoor();
            EndFacing();
        }

        private bool EnsurePreparedRoute()
        {
            if (routePrepared)
            {
                return validWaypointIndices.Count > 0;
            }

            routePrepared = true;
            validWaypointIndices.Clear();
            if (route != null)
            {
                for (int index = 0; index < route.Count; index++)
                {
                    if (route.GetWaypoint(index) != null)
                    {
                        validWaypointIndices.Add(index);
                    }
                }
            }

            if (validWaypointIndices.Count == 0)
            {
                routeCursor = -1;
                return false;
            }

            routeCursor = randomizeStartingWaypoint
                ? UnityEngine.Random.Range(0, validWaypointIndices.Count)
                : ResolveStartingCursor();
            traversalDirection = 1;
            ResetDestinationState();
            return true;
        }

        private int ResolveStartingCursor()
        {
            int clampedStart = route != null && route.Count > 0
                ? Mathf.Clamp(startingWaypointIndex, 0, route.Count - 1)
                : 0;

            for (int cursor = 0; cursor < validWaypointIndices.Count; cursor++)
            {
                if (validWaypointIndices[cursor] >= clampedStart)
                {
                    return cursor;
                }
            }

            return validWaypointIndices.Count - 1;
        }

        private void TryBeginCurrentDestination()
        {
            NPCWaypoint waypoint = CurrentWaypoint;
            if (waypoint == null || motor == null)
            {
                retryTimer = Mathf.Max(0.02f, destinationRetryDelay);
                return;
            }

            if (!hasRequestedDestination)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * waypoint.DestinationJitterRadius;
                requestedDestination = waypoint.transform.position + new Vector3(jitter.x, 0f, jitter.y);
                hasRequestedDestination = true;
            }

            if (!PrepareDoorPassage())
            {
                return;
            }

            if (!motor.SetDestination(
                    requestedDestination,
                    out resolvedDestination,
                    destinationSampleRadius))
            {
                retryTimer = Mathf.Max(0.02f, destinationRetryDelay);
                return;
            }

            hasDestination = true;
        }

        private void UpdateDestinationProgress()
        {
            if (!PrepareDoorPassage())
            {
                return;
            }

            if (motor == null || !motor.IsReady || motor.Agent == null)
            {
                FailCurrentDestination();
                return;
            }

            NavMeshAgent agent = motor.Agent;
            if (agent.pathPending)
            {
                return;
            }

            NPCWaypoint waypoint = CurrentWaypoint;
            if (waypoint == null)
            {
                SkipCurrentWaypoint();
                return;
            }

            Vector3 offset = transform.position - resolvedDestination;
            offset.y = 0f;
            float arrivalDistance = Mathf.Max(waypoint.ArrivalDistance, agent.stoppingDistance);
            if (offset.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                ReachCurrentWaypoint(waypoint);
                return;
            }

            bool pathEndedShort = agent.remainingDistance <= agent.stoppingDistance + 0.05f;
            if (!agent.hasPath ||
                agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                (agent.pathStatus == NavMeshPathStatus.PathPartial && pathEndedShort))
            {
                FailCurrentDestination();
            }
        }

        private void FailCurrentDestination()
        {
            hasDestination = false;
            motor?.Stop();
            retryTimer = Mathf.Max(0.02f, destinationRetryDelay);
        }

        private bool PrepareDoorPassage()
        {
            if (doorOpener == null)
            {
                return true;
            }

            NPCDoorTraversalStatus status = doorOpener.EvaluatePath(requestedDestination);
            if (status == NPCDoorTraversalStatus.Clear)
            {
                return true;
            }

            hasDestination = false;
            motor?.Stop();
            retryTimer = status == NPCDoorTraversalStatus.Blocked
                ? Mathf.Max(0.02f, destinationRetryDelay)
                : 0f;
            return false;
        }

        private void ReachCurrentWaypoint(NPCWaypoint waypoint)
        {
            hasDestination = false;
            motor?.Stop();

            // Establish the next stable state before callbacks run. Arrival listeners may
            // deliberately stop, skip, restart, or replace the route.
            waitTimer = waypoint.GetWaitDuration();
            isWaiting = true;
            BeginFacing(waypoint);

            string trigger = waypoint.AnimatorTrigger;
            if (!string.IsNullOrEmpty(trigger))
            {
                animationTriggerPlayer?.SetTrigger(trigger);
            }

            waypoint.NotifyReached(Controller);
            WaypointReached?.Invoke(waypoint);
            onWaypointReached?.Invoke(waypoint);
        }

        private void UpdateWaypointWait()
        {
            NPCWaypoint waypoint = CurrentWaypoint;
            if (waypoint == null)
            {
                isWaiting = false;
                AdvanceRoute();
                return;
            }

            waitTimer -= Time.deltaTime;
            bool facingComplete = UpdateFacing(waypoint);
            if (waitTimer > 0f || !facingComplete)
            {
                return;
            }

            isWaiting = false;
            EndFacing();
            AdvanceRoute();
        }

        private void BeginFacing(NPCWaypoint waypoint)
        {
            if (waypoint == null ||
                !waypoint.TryGetFacingDirection(transform.position, out _))
            {
                EndFacing();
                return;
            }

            if (!restoreAutomaticRotation && motor?.Agent != null)
            {
                previousAutomaticRotation = motor.Agent.updateRotation;
                restoreAutomaticRotation = true;
            }

            motor?.SetAutomaticRotation(false);
            isFacing = true;
        }

        private bool UpdateFacing(NPCWaypoint waypoint)
        {
            if (!isFacing)
            {
                return true;
            }

            if (!waypoint.TryGetFacingDirection(transform.position, out Vector3 direction))
            {
                EndFacing();
                return true;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            float angle = Quaternion.Angle(transform.rotation, targetRotation);
            if (waypoint.FacingTurnSpeed <= 0f)
            {
                transform.rotation = targetRotation;
                EndFacing();
                return true;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                waypoint.FacingTurnSpeed * Time.deltaTime);

            if (angle <= waypoint.FacingTolerance ||
                Quaternion.Angle(transform.rotation, targetRotation) <= waypoint.FacingTolerance)
            {
                transform.rotation = targetRotation;
                EndFacing();
                return true;
            }

            return false;
        }

        private void EndFacing()
        {
            isFacing = false;
            if (restoreAutomaticRotation)
            {
                motor?.SetAutomaticRotation(previousAutomaticRotation);
                restoreAutomaticRotation = false;
            }
        }

        private void AdvanceRoute()
        {
            ResetDestinationState();
            if (validWaypointIndices.Count == 0)
            {
                return;
            }

            switch (traversalMode)
            {
                case NPCWaypointTraversalMode.Once:
                    if (routeCursor >= validWaypointIndices.Count - 1)
                    {
                        CompleteRoute();
                    }
                    else
                    {
                        routeCursor++;
                    }
                    break;

                case NPCWaypointTraversalMode.Loop:
                    routeCursor = (routeCursor + 1) % validWaypointIndices.Count;
                    break;

                case NPCWaypointTraversalMode.PingPong:
                    AdvancePingPong();
                    break;

                case NPCWaypointTraversalMode.Random:
                    AdvanceRandom();
                    break;
            }
        }

        private void AdvancePingPong()
        {
            if (validWaypointIndices.Count <= 1)
            {
                routeCursor = 0;
                return;
            }

            int nextCursor = routeCursor + traversalDirection;
            if (nextCursor < 0 || nextCursor >= validWaypointIndices.Count)
            {
                traversalDirection *= -1;
                nextCursor = routeCursor + traversalDirection;
            }

            routeCursor = nextCursor;
        }

        private void AdvanceRandom()
        {
            if (validWaypointIndices.Count <= 1)
            {
                routeCursor = 0;
                return;
            }

            int choice = UnityEngine.Random.Range(0, validWaypointIndices.Count - 1);
            if (choice >= routeCursor)
            {
                choice++;
            }

            routeCursor = choice;
        }

        private void CompleteRoute()
        {
            IsRouteComplete = true;
            isWaiting = false;
            CancelCurrentSegment();
            RouteCompleted?.Invoke();
            onRouteCompleted?.Invoke();
        }

        private void ResetTraversalState()
        {
            routePrepared = false;
            routeStartedRaised = false;
            IsRouteComplete = false;
            routeCursor = -1;
            traversalDirection = 1;
            waitTimer = 0f;
            retryTimer = 0f;
            isWaiting = false;
            validWaypointIndices.Clear();
            ResetDestinationState();
        }

        private void ResetDestinationState()
        {
            doorOpener?.ClearPendingDoor();
            hasDestination = false;
            hasRequestedDestination = false;
            requestedDestination = Vector3.zero;
            resolvedDestination = Vector3.zero;
            retryTimer = 0f;
        }

        private void CancelCurrentSegment()
        {
            hasDestination = false;
            isWaiting = false;
            motor?.Stop();
            EndFacing();
            ResetDestinationState();
        }

        private void RaiseRouteStartedIfNeeded()
        {
            if (routeStartedRaised || validWaypointIndices.Count == 0)
            {
                return;
            }

            routeStartedRaised = true;
            RouteStarted?.Invoke();
            onRouteStarted?.Invoke();
        }

        private void OnValidate()
        {
            startingWaypointIndex = Mathf.Max(0, startingWaypointIndex);
            destinationSampleRadius = Mathf.Max(0.1f, destinationSampleRadius);
            destinationRetryDelay = Mathf.Max(0.02f, destinationRetryDelay);
        }
    }
}
