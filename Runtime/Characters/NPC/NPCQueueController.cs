using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Moves an ordered group of NPCs through waiting positions, service, and departure.</summary>
    /// <remarks>
    /// The queue owns spatial progression only. Game-specific dialogue, transactions, objectives,
    /// and flags should subscribe to its events and call <see cref="BeginService"/> or
    /// <see cref="CompleteService"/> when their own work advances.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NPCQueueController : MonoBehaviour
    {
        [Serializable]
        public sealed class MemberEvent : UnityEvent<NPCQueueMember> { }

        [Serializable]
        public sealed class MovementFailureEvent : UnityEvent<NPCQueueMember, Transform> { }

        private sealed class Motion
        {
            public NPCQueueMember Member;
            public readonly List<Transform> Targets = new List<Transform>();
            public int TargetIndex;
            public NPCQueueMemberState FinalState;
            public bool UseFallback;
            public bool WaitingForDoor;
            public int DestinationRequestedFrame;
            public Vector3 ResolvedDestination;
            public bool HasResolvedDestination;
        }

        [Header("Queue Layout")]
        [Tooltip("Position where newly activated NPCs enter the queue.")]
        [SerializeField] private Transform entryPoint;

        [Tooltip("Position occupied by the NPC currently ready to be served.")]
        [SerializeField] private Transform servicePoint;

        [Tooltip("Position reached before a completed NPC is hidden or released.")]
        [SerializeField] private Transform exitPoint;

        [Tooltip("Ordered line positions behind the service point, nearest first.")]
        [SerializeField] private Transform[] waitingPoints = Array.Empty<Transform>();

        [Header("Members")]
        [Tooltip("NPCs served in this order. Runtime enqueueing is also supported.")]
        [SerializeField] private NPCQueueMember[] initialMembers = Array.Empty<NPCQueueMember>();

        [Min(1)]
        [Tooltip("Maximum simultaneously visible members. Waiting-point capacity is also respected.")]
        [SerializeField] private int maximumActiveMembers = 1;

        [Tooltip("Begin processing the configured members when this component starts.")]
        [SerializeField] private bool beginOnStart;

        [Tooltip("Hide configured members until they enter, and hide each member after departure.")]
        [SerializeField] private bool manageMemberVisibility = true;

        [Header("Movement")]
        [Min(0.05f)]
        [Tooltip("Horizontal distance considered close enough to a queue destination.")]
        [SerializeField] private float arrivalDistance = 0.35f;

        [Min(0.1f)]
        [Tooltip("Movement speed used when a NavMesh motor cannot accept the destination.")]
        [SerializeField] private float fallbackMovementSpeed = 1.8f;

        [Tooltip("Move directly toward a destination when NavMesh routing fails. Keep disabled when NPCs must remain on a baked NavMesh.")]
        [SerializeField] private bool useDirectMovementFallback;

        [Min(0f)]
        [Tooltip("Fallback turning speed in degrees per second.")]
        [SerializeField] private float fallbackTurnSpeed = 540f;

        [Header("Events")]
        [Tooltip("Raised when the front member reaches the service point.")]
        [SerializeField] private MemberEvent onMemberReadyForService = new MemberEvent();

        [Tooltip("Raised when game-specific logic accepts the front member for service.")]
        [SerializeField] private MemberEvent onServiceStarted = new MemberEvent();

        [Tooltip("Raised after a completed member reaches the exit.")]
        [SerializeField] private MemberEvent onMemberDeparted = new MemberEvent();

        [Tooltip("Raised after every queued member has departed.")]
        [SerializeField] private UnityEvent onQueueCompleted = new UnityEvent();

        [Tooltip("Raised when a queue member cannot obtain a usable path or encounters a locked path door.")]
        [SerializeField] private MovementFailureEvent onMovementFailed = new MovementFailureEvent();

        private readonly List<NPCQueueMember> members = new List<NPCQueueMember>();
        private readonly List<NPCQueueMember> activeMembers = new List<NPCQueueMember>();
        private readonly List<Motion> motions = new List<Motion>();
        private int currentIndex;
        private int nextActivationIndex;
        private bool initialized;
        private bool completionRaised;
        private bool resumeAfterEnable;

        /// <summary>Raised when the front member reaches service.</summary>
        public event Action<NPCQueueMember, int> MemberReadyForService;

        /// <summary>Raised when service begins for the front member.</summary>
        public event Action<NPCQueueMember, int> ServiceStarted;

        /// <summary>Raised when one member finishes leaving.</summary>
        public event Action<NPCQueueMember, int> MemberDeparted;

        /// <summary>Raised when no queued members remain.</summary>
        public event Action QueueCompleted;

        /// <summary>Raised when an NPC cannot be routed to its current destination.</summary>
        public event Action<NPCQueueMember, Transform> MovementFailed;

        /// <summary>Gets the zero-based index of the front member in the complete queue.</summary>
        public int CurrentIndex => currentIndex;

        /// <summary>Gets the front member, or null after the queue finishes.</summary>
        public NPCQueueMember CurrentMember =>
            currentIndex >= 0 && currentIndex < members.Count ? members[currentIndex] : null;

        /// <summary>Gets the front member's current state.</summary>
        public NPCQueueMemberState CurrentState =>
            CurrentMember != null ? CurrentMember.State : NPCQueueMemberState.Completed;

        /// <summary>Gets whether queue processing has begun and members remain.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Gets whether an active queue is temporarily holding all movement.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>Gets the configured and runtime-enqueued members in service order.</summary>
        public IReadOnlyList<NPCQueueMember> Members => members;

        private void Awake()
        {
            InitializeMembers();
        }

        private void Start()
        {
            if (beginOnStart)
            {
                BeginQueue();
            }
        }

        private void OnEnable()
        {
            if (!resumeAfterEnable)
            {
                return;
            }

            resumeAfterEnable = false;
            ResumeQueue();
        }

        private void OnDisable()
        {
            resumeAfterEnable = IsRunning && !IsPaused;
            PauseQueue();
        }

        private void Update()
        {
            if (IsPaused)
            {
                return;
            }

            for (int index = motions.Count - 1; index >= 0; index--)
            {
                if (index >= motions.Count)
                {
                    continue;
                }
                UpdateMotion(motions[index]);
            }
        }

        /// <summary>Resets and begins processing members from the start of the queue.</summary>
        public void BeginQueue()
        {
            InitializeMembers();
            if (IsRunning)
            {
                return;
            }
            if (!isActiveAndEnabled)
            {
                GameLogger.Warning(
                    $"Cannot begin NPC queue '{name}' while its controller is disabled.",
                    this);
                return;
            }

            ResetQueueInternal(true);
            if (members.Count == 0)
            {
                CompleteQueue();
                return;
            }

            IsRunning = true;
            FillActivePositions();
        }

        /// <summary>Returns the queue and all configured members to their inactive state.</summary>
        /// <param name="hideMembers">Whether queue-managed member objects should be hidden.</param>
        public void ResetQueue(bool hideMembers = true)
        {
            InitializeMembers();
            ResetQueueInternal(hideMembers);
        }

        /// <summary>Temporarily stops every active member without discarding route progress.</summary>
        public void PauseQueue()
        {
            if (!IsRunning || IsPaused)
            {
                return;
            }

            IsPaused = true;
            StopMotors();
        }

        /// <summary>Continues every route segment held by <see cref="PauseQueue"/>.</summary>
        public void ResumeQueue()
        {
            if (!IsRunning || !IsPaused)
            {
                return;
            }

            IsPaused = false;
            for (int index = motions.Count - 1; index >= 0; index--)
            {
                if (index >= motions.Count)
                {
                    continue;
                }
                BeginMotionSegment(motions[index]);
            }
        }

        /// <summary>Marks the ready front member as actively being served.</summary>
        /// <returns>True when service began.</returns>
        public bool BeginService()
        {
            NPCQueueMember member = CurrentMember;
            if (!IsRunning || IsPaused || member == null ||
                member.State != NPCQueueMemberState.ReadyForService)
            {
                return false;
            }

            member.ApplyState(NPCQueueMemberState.InService);
            ServiceStarted?.Invoke(member, currentIndex);
            onServiceStarted?.Invoke(member);
            return true;
        }

        /// <summary>Sends the front member to the exit and advances the line after departure.</summary>
        /// <returns>True when departure began.</returns>
        public bool CompleteService()
        {
            NPCQueueMember member = CurrentMember;
            if (!IsRunning || IsPaused || member == null ||
                (member.State != NPCQueueMemberState.InService &&
                 member.State != NPCQueueMemberState.ReadyForService))
            {
                return false;
            }

            member.ApplyState(NPCQueueMemberState.Leaving);
            ScheduleMotion(member, EnumerateTarget(exitPoint), NPCQueueMemberState.Completed);
            return true;
        }

        /// <summary>Adds a member to the end of the runtime queue.</summary>
        /// <returns>True when the member was valid and not already queued.</returns>
        public bool Enqueue(NPCQueueMember member)
        {
            InitializeMembers();
            if (member == null || members.Contains(member))
            {
                return false;
            }

            members.Add(member);
            if (manageMemberVisibility)
            {
                member.gameObject.SetActive(false);
            }

            if (IsRunning)
            {
                FillActivePositions();
            }

            return true;
        }

        /// <summary>Stops movement and optionally hides all members without raising completion.</summary>
        public void CancelQueue(bool hideMembers = true)
        {
            IsRunning = false;
            IsPaused = false;
            StopAllMotion();
            completionRaised = false;
            foreach (NPCQueueMember member in members)
            {
                if (member == null)
                {
                    continue;
                }

                member.ApplyState(NPCQueueMemberState.Inactive, false);
                if (hideMembers && manageMemberVisibility)
                {
                    member.gameObject.SetActive(false);
                }
            }

            activeMembers.Clear();
        }

        /// <summary>Reconstructs logical state without replaying queue or member lifecycle callbacks.</summary>
        /// <param name="memberIndex">Index of the member at the front of the restored queue.</param>
        /// <param name="state">Logical state restored for that member.</param>
        public void RestoreAt(int memberIndex, NPCQueueMemberState state)
        {
            InitializeMembers();
            ResetQueueInternal(true);
            currentIndex = Mathf.Clamp(memberIndex, 0, members.Count);
            nextActivationIndex = currentIndex;

            if (currentIndex >= members.Count || state == NPCQueueMemberState.Completed)
            {
                currentIndex = members.Count;
                nextActivationIndex = members.Count;
                completionRaised = true;
                IsRunning = false;
                return;
            }

            IsRunning = true;
            NPCQueueMember member = ActivateMember(currentIndex);
            nextActivationIndex = currentIndex + 1;

            switch (state)
            {
                case NPCQueueMemberState.Entering:
                    Place(member, entryPoint);
                    member.ApplyState(NPCQueueMemberState.Entering, false);
                    ScheduleServiceApproach(member);
                    break;
                case NPCQueueMemberState.ReadyForService:
                case NPCQueueMemberState.InService:
                    Place(member, servicePoint);
                    member.ApplyState(state, false);
                    break;
                case NPCQueueMemberState.Leaving:
                    Place(member, servicePoint);
                    member.ApplyState(NPCQueueMemberState.Leaving, false);
                    ScheduleMotion(member, EnumerateTarget(exitPoint), NPCQueueMemberState.Completed);
                    break;
                default:
                    Place(member, servicePoint);
                    member.ApplyState(NPCQueueMemberState.ReadyForService, false);
                    break;
            }

            RestoreWaitingMembers();
        }

        private void InitializeMembers()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            members.Clear();
            foreach (NPCQueueMember member in initialMembers ?? Array.Empty<NPCQueueMember>())
            {
                if (member != null && !members.Contains(member))
                {
                    members.Add(member);
                }
            }

            if (manageMemberVisibility)
            {
                foreach (NPCQueueMember member in members)
                {
                    member.gameObject.SetActive(false);
                }
            }
        }

        private void ResetQueueInternal(bool hideMembers)
        {
            StopAllMotion();
            IsRunning = false;
            IsPaused = false;
            completionRaised = false;
            resumeAfterEnable = false;
            currentIndex = 0;
            nextActivationIndex = 0;
            activeMembers.Clear();
            foreach (NPCQueueMember member in members)
            {
                if (member == null)
                {
                    continue;
                }

                member.ApplyState(NPCQueueMemberState.Inactive, false);
                if (hideMembers && manageMemberVisibility)
                {
                    member.gameObject.SetActive(false);
                }
            }
        }

        private void FillActivePositions()
        {
            int capacity = Mathf.Max(1, Mathf.Min(
                maximumActiveMembers,
                1 + CountWaitingPoints()));

            while (activeMembers.Count < capacity && nextActivationIndex < members.Count)
            {
                int memberIndex = nextActivationIndex++;
                NPCQueueMember member = ActivateMember(memberIndex);
                if (member == null)
                {
                    continue;
                }

                Place(member, entryPoint);
                member.ApplyState(NPCQueueMemberState.Entering);
                if (memberIndex == currentIndex)
                {
                    ScheduleServiceApproach(member);
                }
                else
                {
                    int waitingIndex = activeMembers.IndexOf(member) - 1;
                    ScheduleWaitingApproach(member, waitingIndex);
                }
            }
        }

        private void FillWaitingPositions()
        {
            FillActivePositions();
            for (int index = 1; index < activeMembers.Count; index++)
            {
                ScheduleWaitingMotion(activeMembers[index], index - 1);
            }
        }

        private NPCQueueMember ActivateMember(int index)
        {
            if (index < 0 || index >= members.Count)
            {
                return null;
            }

            NPCQueueMember member = members[index];
            if (member == null)
            {
                return null;
            }

            member.gameObject.SetActive(true);
            if (!activeMembers.Contains(member))
            {
                activeMembers.Add(member);
            }

            return member;
        }

        private void ScheduleServiceApproach(NPCQueueMember member)
        {
            var targets = new List<Transform>();
            if (member.State != NPCQueueMemberState.Waiting)
            {
                foreach (Transform waypoint in member.PreServiceWaypoints)
                {
                    if (waypoint != null)
                    {
                        targets.Add(waypoint);
                    }
                }
            }

            if (servicePoint != null)
            {
                targets.Add(servicePoint);
            }

            ScheduleMotion(member, targets, NPCQueueMemberState.ReadyForService);
        }

        private void ScheduleWaitingMotion(NPCQueueMember member, int waitingIndex)
        {
            Transform waitingPoint = GetWaitingPoint(waitingIndex);
            if (waitingPoint == null)
            {
                return;
            }

            ScheduleMotion(
                member,
                EnumerateTarget(waitingPoint),
                NPCQueueMemberState.Waiting);
        }

        private void ScheduleWaitingApproach(NPCQueueMember member, int waitingIndex)
        {
            Transform waitingPoint = GetWaitingPoint(waitingIndex);
            if (waitingPoint == null)
            {
                return;
            }

            var targets = new List<Transform>();
            foreach (Transform waypoint in member.PreServiceWaypoints)
            {
                if (waypoint != null)
                {
                    targets.Add(waypoint);
                }
            }
            targets.Add(waitingPoint);
            ScheduleMotion(member, targets, NPCQueueMemberState.Waiting);
        }

        private void ScheduleMotion(
            NPCQueueMember member,
            IEnumerable<Transform> targets,
            NPCQueueMemberState finalState)
        {
            RemoveMotion(member);
            var motion = new Motion
            {
                Member = member,
                FinalState = finalState,
            };
            foreach (Transform target in targets)
            {
                if (target != null)
                {
                    motion.Targets.Add(target);
                }
            }

            if (motion.Targets.Count == 0)
            {
                CompleteMotion(motion);
                return;
            }

            motions.Add(motion);
            BeginMotionSegment(motion);
        }

        private void BeginMotionSegment(Motion motion)
        {
            Transform target = motion.Targets[motion.TargetIndex];
            if (!PrepareDoorPassage(motion, target))
            {
                return;
            }

            motion.HasResolvedDestination = false;
            motion.ResolvedDestination = target.position;
            NPCNavMeshMotor motor = motion.Member.Motor;
            Vector3 resolvedDestination = target.position;
            bool accepted = motor != null && motor.SetDestination(
                target.position,
                out resolvedDestination);
            motion.ResolvedDestination = resolvedDestination;
            motion.HasResolvedDestination = accepted;
            motion.DestinationRequestedFrame = Time.frameCount;
            motion.UseFallback = !accepted && useDirectMovementFallback;
            if (motion.UseFallback)
            {
                motor?.Stop();
            }
            if (!accepted && !useDirectMovementFallback)
            {
                FailMotion(motion, target);
                return;
            }

            if (HasReached(
                    motion.Member.transform,
                    GetArrivalPosition(motion, target)))
            {
                CompleteMotionSegment(motion, motion.Member.transform, target);
            }
        }

        private void UpdateMotion(Motion motion)
        {
            if (motion.Member == null || motion.TargetIndex >= motion.Targets.Count)
            {
                motions.Remove(motion);
                return;
            }

            Transform actor = motion.Member.transform;
            Transform target = motion.Targets[motion.TargetIndex];
            bool wasWaitingForDoor = motion.WaitingForDoor;
            if (!PrepareDoorPassage(motion, target))
            {
                return;
            }

            if (wasWaitingForDoor)
            {
                // Stopping for a door clears the NavMesh path. Rebuild it only after the
                // animation and obstacle-carving grace period have both elapsed.
                BeginMotionSegment(motion);
                return;
            }

            if (motion.UseFallback)
            {
                MoveWithoutNavMesh(actor, target);
            }
            else if (HasPathFailed(motion))
            {
                if (useDirectMovementFallback)
                {
                    motion.Member.Motor?.Stop();
                    motion.UseFallback = true;
                }
                else
                {
                    FailMotion(motion, target);
                }
                return;
            }

            if (!HasReached(actor, GetArrivalPosition(motion, target)))
            {
                return;
            }

            CompleteMotionSegment(motion, actor, target);
        }

        private void CompleteMotionSegment(Motion motion, Transform actor, Transform target)
        {
            NPCNavMeshMotor motor = motion.Member.Motor;
            bool useResolvedDestination =
                !motion.UseFallback && motion.HasResolvedDestination;
            Vector3 completionPosition = useResolvedDestination
                ? motion.ResolvedDestination
                : target.position;
            bool warped = useResolvedDestination &&
                          motor != null &&
                          motor.Warp(
                              completionPosition,
                              target.rotation,
                              Mathf.Max(0.05f, arrivalDistance));
            if (!warped)
            {
                actor.SetPositionAndRotation(completionPosition, target.rotation);
            }
            motor?.Stop();
            motion.TargetIndex++;
            if (motion.TargetIndex < motion.Targets.Count)
            {
                BeginMotionSegment(motion);
                return;
            }

            motions.Remove(motion);
            CompleteMotion(motion);
        }

        private static bool HasPathFailed(Motion motion)
        {
            NPCNavMeshMotor motor = motion.Member.Motor;
            UnityEngine.AI.NavMeshAgent agent = motor != null ? motor.Agent : null;
            if (agent == null ||
                Time.frameCount <= motion.DestinationRequestedFrame ||
                !agent.enabled ||
                !agent.isOnNavMesh ||
                agent.pathPending)
            {
                return false;
            }

            if (!agent.hasPath ||
                agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
            {
                return true;
            }

            if (agent.pathStatus != UnityEngine.AI.NavMeshPathStatus.PathPartial)
            {
                return false;
            }

            // A partial path may still be the valid approach to a closed carving door.
            // Walk to its endpoint before deciding that fallback or failure is required.
            return agent.remainingDistance <= agent.stoppingDistance + 0.05f;
        }

        private bool PrepareDoorPassage(Motion motion, Transform target)
        {
            NPCDoorOpener opener = motion.Member != null
                ? motion.Member.DoorOpener
                : null;
            if (opener == null)
            {
                motion.WaitingForDoor = false;
                return true;
            }

            NPCDoorTraversalStatus status = opener.EvaluatePath(target.position);
            if (status == NPCDoorTraversalStatus.Clear)
            {
                motion.WaitingForDoor = false;
                return true;
            }

            motion.Member.Motor?.Stop();
            motion.UseFallback = false;
            motion.WaitingForDoor = status == NPCDoorTraversalStatus.Waiting;
            if (status == NPCDoorTraversalStatus.Blocked)
            {
                FailMotion(motion, target);
            }

            return false;
        }

        private void FailMotion(Motion motion, Transform target)
        {
            NPCQueueMember member = motion.Member;
            IsRunning = false;
            IsPaused = false;
            StopAllMotion();
            MovementFailed?.Invoke(member, target);
            GameLogger.SafeExecute(
                "raising NPC queue movement-failed callbacks",
                () => onMovementFailed?.Invoke(member, target),
                this);
        }

        private void CompleteMotion(Motion motion)
        {
            NPCQueueMember member = motion.Member;
            member.ApplyState(motion.FinalState);
            if (motion.FinalState == NPCQueueMemberState.ReadyForService)
            {
                MemberReadyForService?.Invoke(member, currentIndex);
                onMemberReadyForService?.Invoke(member);
            }
            else if (motion.FinalState == NPCQueueMemberState.Completed)
            {
                CompleteDeparture(member);
            }
        }

        private void CompleteDeparture(NPCQueueMember member)
        {
            int departedIndex = currentIndex;
            activeMembers.Remove(member);
            if (manageMemberVisibility)
            {
                member.gameObject.SetActive(false);
            }

            MemberDeparted?.Invoke(member, departedIndex);
            onMemberDeparted?.Invoke(member);
            currentIndex++;

            if (!IsRunning)
            {
                return;
            }

            if (currentIndex >= members.Count)
            {
                CompleteQueue();
                return;
            }

            NPCQueueMember next = activeMembers.Count > 0
                ? activeMembers[0]
                : ActivateMember(currentIndex);
            if (nextActivationIndex <= currentIndex)
            {
                nextActivationIndex = currentIndex + 1;
            }

            if (next.State != NPCQueueMemberState.Waiting)
            {
                next.ApplyState(NPCQueueMemberState.Entering);
            }
            ScheduleServiceApproach(next);
            FillWaitingPositions();
        }

        private void CompleteQueue()
        {
            if (completionRaised)
            {
                return;
            }

            completionRaised = true;
            IsRunning = false;
            IsPaused = false;
            StopAllMotion();
            QueueCompleted?.Invoke();
            GameLogger.SafeInvoke("raising NPC queue completion callbacks", onQueueCompleted, this);
        }

        private void StopAllMotion()
        {
            StopMotors();
            foreach (Motion motion in motions)
            {
                motion.Member?.DoorOpener?.ClearPendingDoor();
            }
            motions.Clear();
        }

        private void StopMotors()
        {
            foreach (Motion motion in motions)
            {
                motion.Member?.Motor?.Stop();
            }
        }

        private void RemoveMotion(NPCQueueMember member)
        {
            for (int index = motions.Count - 1; index >= 0; index--)
            {
                if (motions[index].Member == member)
                {
                    motions[index].Member?.Motor?.Stop();
                    motions[index].Member?.DoorOpener?.ClearPendingDoor();
                    motions.RemoveAt(index);
                }
            }
        }

        private void Place(NPCQueueMember member, Transform target)
        {
            if (member == null || target == null)
            {
                return;
            }

            NPCNavMeshMotor motor = member.Motor;
            if (motor != null && motor.Warp(target.position, target.rotation))
            {
                motor.Stop();
                return;
            }

            member.transform.SetPositionAndRotation(target.position, target.rotation);
        }

        private void RestoreWaitingMembers()
        {
            int capacity = Mathf.Max(1, Mathf.Min(maximumActiveMembers, 1 + CountWaitingPoints()));
            for (int offset = 1; offset < capacity; offset++)
            {
                int memberIndex = currentIndex + offset;
                if (memberIndex >= members.Count)
                {
                    break;
                }

                Transform waitingPoint = GetWaitingPoint(offset - 1);
                NPCQueueMember member = ActivateMember(memberIndex);
                Place(member, waitingPoint);
                member.ApplyState(NPCQueueMemberState.Waiting, false);
                nextActivationIndex = memberIndex + 1;
            }
        }

        private int CountWaitingPoints()
        {
            int count = 0;
            foreach (Transform waitingPoint in waitingPoints ?? Array.Empty<Transform>())
            {
                if (waitingPoint != null)
                {
                    count++;
                }
            }
            return count;
        }

        private Transform GetWaitingPoint(int index)
        {
            if (index < 0)
            {
                return null;
            }

            int current = 0;
            foreach (Transform waitingPoint in waitingPoints ?? Array.Empty<Transform>())
            {
                if (waitingPoint == null)
                {
                    continue;
                }
                if (current == index)
                {
                    return waitingPoint;
                }
                current++;
            }
            return null;
        }

        private void MoveWithoutNavMesh(Transform actor, Transform target)
        {
            Vector3 direction = target.position - actor.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                actor.rotation = Quaternion.RotateTowards(
                    actor.rotation,
                    rotation,
                    fallbackTurnSpeed * Time.deltaTime);
            }

            actor.position = Vector3.MoveTowards(
                actor.position,
                target.position,
                fallbackMovementSpeed * Time.deltaTime);
        }

        private static Vector3 GetArrivalPosition(Motion motion, Transform target)
        {
            return !motion.UseFallback && motion.HasResolvedDestination
                ? motion.ResolvedDestination
                : target.position;
        }

        private bool HasReached(Transform actor, Transform target)
        {
            return target != null && HasReached(actor, target.position);
        }

        private bool HasReached(Transform actor, Vector3 targetPosition)
        {
            Vector3 offset = actor.position - targetPosition;
            offset.y = 0f;
            return offset.sqrMagnitude <= arrivalDistance * arrivalDistance;
        }

        private static IEnumerable<Transform> EnumerateTarget(Transform target)
        {
            if (target != null)
            {
                yield return target;
            }
        }
    }
}
