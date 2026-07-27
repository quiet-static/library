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
        [SerializeField] private float placementSampleRadius = 3f;
        [Tooltip("Clear the current path when this motor is disabled.")]
        [SerializeField] private bool stopOnDisable = true;
        [Tooltip("Invoked when the motor transitions from stationary to moving.")]
        [SerializeField] private UnityEvent onStartedMoving;
        [Tooltip("Invoked when the motor transitions from moving to stationary.")]
        [SerializeField] private UnityEvent onStoppedMoving;

        private bool wasMoving;

        public NavMeshAgent Agent => agent;
        public bool IsReady => agent != null && agent.enabled && agent.isOnNavMesh;
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

        public bool SetDestination(Vector3 worldPosition, float sampleRadius = 2f)
        {
            if (!TryPlaceOnNavMesh())
                return false;

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, sampleRadius, agent.areaMask))
                return false;

            agent.isStopped = false;
            return agent.SetDestination(hit.position);
        }

        public void Stop()
        {
            if (!IsReady)
                return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        public void Resume()
        {
            if (IsReady)
                agent.isStopped = false;
        }

        public void SetAutomaticRotation(bool enabledState)
        {
            if (agent != null)
                agent.updateRotation = enabledState;
        }
    }
}
