using UnityEngine;
using UnityEngine.AI;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Lets a background NPC roam randomly or among assigned patrol points.</summary>
    [RequireComponent(typeof(NPCController), typeof(NPCNavMeshMotor))]
    public class NPCWanderBehaviour : NPCBehaviour
    {
        public enum WanderMode { RandomRadius, PatrolPoints }

        [Tooltip("NavMesh motor used to reach generated or authored destinations.")]
        [SerializeField] private NPCNavMeshMotor motor;
        [Tooltip("Choose random points within a radius or visit authored patrol points.")]
        [SerializeField] private WanderMode wanderMode = WanderMode.RandomRadius;
        [Tooltip("Center of random wandering. Uses this NPC's initial position when omitted.")]
        [SerializeField] private Transform wanderOrigin;
        [SerializeField, Min(0.1f)] private float wanderRadius = 5f;
        [Tooltip("Ordered destinations used in Patrol Points mode.")]
        [SerializeField] private Transform[] patrolPoints;
        [Tooltip("Choose a random patrol destination instead of advancing in array order.")]
        [SerializeField] private bool randomizePatrolOrder;
        [SerializeField, Min(0f)] private float minimumWait = 1f;
        [SerializeField, Min(0f)] private float maximumWait = 3f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.3f;
        [SerializeField, Min(0.1f)] private float sampleRadius = 2f;

        private Vector3 startPosition;
        private float waitTimer;
        private int patrolIndex;
        private bool hasDestination;

        protected override void Awake()
        {
            base.Awake();
            if (motor == null)
                motor = GetComponent<NPCNavMeshMotor>();
            startPosition = transform.position;
        }

        private void Update()
        {
            if (!IsBehaviourActive || motor == null || !motor.IsReady)
                return;

            if (hasDestination && HasArrived())
            {
                hasDestination = false;
                motor.Stop();
                waitTimer = Random.Range(minimumWait, Mathf.Max(minimumWait, maximumWait));
            }

            if (hasDestination)
                return;

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
                ChooseDestination();
        }

        private bool HasArrived()
        {
            NavMeshAgent agent = motor.Agent;
            return !agent.pathPending && agent.remainingDistance <= Mathf.Max(arrivalDistance, agent.stoppingDistance);
        }

        private void ChooseDestination()
        {
            Vector3 destination;
            if (wanderMode == WanderMode.PatrolPoints && patrolPoints != null && patrolPoints.Length > 0)
            {
                int index = randomizePatrolOrder ? Random.Range(0, patrolPoints.Length) : patrolIndex;
                Transform point = patrolPoints[index];
                if (!randomizePatrolOrder)
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                if (point == null)
                    return;
                destination = point.position;
            }
            else
            {
                Vector3 origin = wanderOrigin != null ? wanderOrigin.position : startPosition;
                Vector2 circle = Random.insideUnitCircle * wanderRadius;
                destination = origin + new Vector3(circle.x, 0f, circle.y);
            }

            hasDestination = motor.SetDestination(destination, sampleRadius);
            if (!hasDestination)
                waitTimer = 0.5f;
        }

        protected override void OnBehaviourDeactivated()
        {
            hasDestination = false;
            motor?.Stop();
        }
    }
}
