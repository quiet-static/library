using QuietStatic.Toolkit.Characters;
using UnityEngine;
using UnityEngine.AI;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Moves an NPC toward the controller target while preserving a configurable distance.</summary>
    [RequireComponent(typeof(NPCController), typeof(NPCNavMeshMotor))]
    public class NPCFollowBehaviour : NPCBehaviour
    {
        [Tooltip("NavMesh motor used to approach the controller target.")]
        [SerializeField] private NPCNavMeshMotor motor;
        [Tooltip("Distance from the target at which the NPC stops approaching.")]
        [SerializeField, Min(0f)] private float followDistance = 2.5f;
        [Tooltip("Extra separation required before a stopped NPC begins following again.")]
        [SerializeField, Min(0f)] private float resumeDistanceBuffer = 0.5f;
        [Tooltip("Seconds between NavMesh destination updates while following.")]
        [SerializeField, Min(0.02f)] private float repathInterval = 0.15f;
        [Tooltip("Lead moving targets using their reported velocity to reduce trailing.")]
        [SerializeField] private bool useVelocityPrediction = true;
        [Tooltip("Minimum velocity-prediction time in seconds.")]
        [SerializeField, Min(0f)] private float minimumLookAhead = 0.1f;
        [Tooltip("Maximum velocity-prediction time in seconds.")]
        [SerializeField, Min(0f)] private float maximumLookAhead = 0.75f;
        [Tooltip("Radius used to project predicted positions onto the NavMesh.")]
        [SerializeField, Min(0.1f)] private float destinationSampleRadius = 2f;

        private VelocityReporter targetVelocity;
        private float repathTimer;
        private bool following;

        protected override void Awake()
        {
            base.Awake();
            if (motor == null)
                motor = GetComponent<NPCNavMeshMotor>();
        }

        private void OnEnable()
        {
            if (Controller != null)
                Controller.TargetChanged += HandleTargetChanged;
        }

        private void OnDisable()
        {
            if (Controller != null)
                Controller.TargetChanged -= HandleTargetChanged;
        }

        protected override void Start()
        {
            CacheVelocityReporter(Controller != null ? Controller.Target : null);
            base.Start();
        }

        private void Update()
        {
            if (!IsBehaviourActive || motor == null || Controller.Target == null)
                return;

            repathTimer -= Time.deltaTime;
            if (repathTimer > 0f)
                return;

            repathTimer = repathInterval;
            UpdateFollowDestination();
        }

        private void UpdateFollowDestination()
        {
            Transform target = Controller.Target;
            float distance = Vector3.Distance(transform.position, target.position);

            if (!following && distance > followDistance + resumeDistanceBuffer)
                following = true;
            else if (following && distance <= followDistance)
                following = false;

            if (!following)
            {
                motor.Stop();
                return;
            }

            Vector3 predictedTarget = GetPredictedTargetPosition(target);
            Vector3 away = transform.position - target.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f)
                away = -target.forward;

            Vector3 desiredPosition = predictedTarget + away.normalized * followDistance;
            motor.SetDestination(desiredPosition, destinationSampleRadius);
        }

        private Vector3 GetPredictedTargetPosition(Transform target)
        {
            if (!useVelocityPrediction || targetVelocity == null || motor.Agent == null)
                return target.position;

            float speed = Mathf.Max(motor.Agent.speed, 0.01f);
            float distance = Vector3.Distance(transform.position, target.position);
            float lookAhead = Mathf.Clamp(distance / speed, minimumLookAhead, maximumLookAhead);
            Vector3 predicted = target.position + targetVelocity.velocity * lookAhead;

            if (NavMesh.Raycast(target.position, predicted, out NavMeshHit hit, motor.Agent.areaMask))
                predicted = hit.position;

            return predicted;
        }

        private void HandleTargetChanged(Transform newTarget)
        {
            CacheVelocityReporter(newTarget);
            following = false;
            repathTimer = 0f;
        }

        private void CacheVelocityReporter(Transform target)
        {
            targetVelocity = target != null ? target.GetComponent<VelocityReporter>() : null;
        }

        protected override void OnBehaviourDeactivated()
        {
            following = false;
            motor?.Stop();
        }
    }
}
