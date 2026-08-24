using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Shared NavMesh movement wrapper used by follow and wander behaviours.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public class NPCNavMeshMotor : MonoBehaviour
    {
        [Tooltip("NavMeshAgent that performs pathfinding and movement.")]
        [SerializeField] private NavMeshAgent agent;
        [Tooltip("Radius used when projecting requested positions onto the NavMesh.")]
        [Min(0f)]
        [SerializeField] private float placementSampleRadius = 3f;
        [Tooltip("Clear the current path when this motor is disabled.")]
        [SerializeField] private bool stopOnDisable = true;
        [Tooltip("Invoked when the motor transitions from stationary to moving.")]
        [SerializeField] private UnityEvent onStartedMoving;
        [Tooltip("Invoked when the motor transitions from moving to stationary.")]
        [SerializeField] private UnityEvent onStoppedMoving;

        private bool wasMoving;

        /// <summary>Gets the wrapped NavMesh agent.</summary>
        public NavMeshAgent Agent => agent;

        /// <summary>Gets whether the agent can currently accept NavMesh commands.</summary>
        public bool IsReady => agent != null && agent.enabled && agent.isOnNavMesh;

        /// <summary>Gets whether the agent currently has meaningful velocity.</summary>
        public bool IsMoving => IsReady && agent.velocity.sqrMagnitude > 0.01f;

        private void Reset() => agent = GetComponent<NavMeshAgent>();

        private void Awake()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
        }

        private void Start() => TryPlaceOnNavMesh();

        private void Update()
        {
            bool moving = IsMoving;
            if (moving == wasMoving)
                return;

            wasMoving = moving;
            if (moving) onStartedMoving?.Invoke();
            else onStoppedMoving?.Invoke();
        }

        private void OnDisable()
        {
            if (stopOnDisable)
                Stop();
        }

        /// <summary>Attempts to project and warp the agent onto a nearby NavMesh position.</summary>
        /// <returns>True when the agent is already placed or was placed successfully.</returns>
        public bool TryPlaceOnNavMesh()
        {
            if (agent == null || !agent.enabled)
                return false;

            if (agent.isOnNavMesh)
                return true;

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, placementSampleRadius, agent.areaMask))
                return false;

            return agent.Warp(hit.position);
        }

        /// <summary>Projects a world position onto the NavMesh and assigns it as the destination.</summary>
        /// <param name="worldPosition">Requested world-space destination.</param>
        /// <param name="sampleRadius">Radius used to find a nearby NavMesh position.</param>
        /// <returns>True when a valid destination was assigned.</returns>
        public bool SetDestination(Vector3 worldPosition, float sampleRadius = 2f)
        {
            return SetDestination(worldPosition, out _, sampleRadius);
        }

        /// <summary>
        /// Projects a world position onto the NavMesh, assigns it as the destination, and reports
        /// the exact sampled position used by the agent.
        /// </summary>
        /// <param name="worldPosition">Requested world-space destination.</param>
        /// <param name="resolvedDestination">
        /// Sampled NavMesh destination when the request succeeds; otherwise the requested position.
        /// </param>
        /// <param name="sampleRadius">Radius used to find a nearby NavMesh position.</param>
        /// <returns>True when a valid destination was assigned.</returns>
        public bool SetDestination(
            Vector3 worldPosition,
            out Vector3 resolvedDestination,
            float sampleRadius = 2f)
        {
            resolvedDestination = worldPosition;
            if (!TryPlaceOnNavMesh())
                return false;

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, sampleRadius, agent.areaMask))
                return false;

            if (!agent.SetDestination(hit.position))
            {
                return false;
            }

            resolvedDestination = hit.position;
            agent.isStopped = false;
            return true;
        }

        /// <summary>Immediately moves the NPC to a nearby valid NavMesh position.</summary>
        /// <param name="worldPosition">Requested world-space arrival position.</param>
        /// <param name="worldRotation">Rotation applied after the agent is moved.</param>
        /// <param name="sampleRadius">Radius used to find a nearby NavMesh position.</param>
        /// <returns>True when the NPC was transported successfully.</returns>
        public bool Warp(Vector3 worldPosition, Quaternion worldRotation, float sampleRadius = 2f)
        {
            if (agent == null || !agent.enabled ||
                !NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, sampleRadius, agent.areaMask))
            {
                return false;
            }

            bool transported = agent.Warp(hit.position);
            if (!transported)
            {
                return false;
            }

            transform.rotation = worldRotation;
            agent.ResetPath();
            agent.isStopped = false;
            return true;
        }

        /// <summary>Stops movement and clears the current path.</summary>
        public void Stop()
        {
            if (!IsReady)
                return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        /// <summary>Allows the agent to resume its current path when ready.</summary>
        public void Resume()
        {
            if (IsReady)
                agent.isStopped = false;
        }

        /// <summary>Changes whether the NavMesh agent rotates its transform automatically.</summary>
        /// <param name="enabledState">Whether automatic rotation should be enabled.</param>
        public void SetAutomaticRotation(bool enabledState)
        {
            if (agent != null)
                agent.updateRotation = enabledState;
        }
    }
}
