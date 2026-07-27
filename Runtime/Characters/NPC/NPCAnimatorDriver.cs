using UnityEngine;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Updates an Animator from NavMesh movement without coupling animation to movement logic.</summary>
    public class NPCAnimatorDriver : MonoBehaviour
    {
        [Tooltip("Animator receiving locomotion values.")]
        [SerializeField] private Animator animator;
        [Tooltip("Motor whose current NavMesh velocity drives animation.")]
        [SerializeField] private NPCNavMeshMotor motor;
        [Tooltip("Animator float parameter used for movement speed.")]
        [SerializeField] private string speedParameter = "Speed";
        [Tooltip("Divide velocity by the agent's configured maximum speed before updating the Animator.")]
        [SerializeField] private bool normalizeByAgentSpeed = true;
        [SerializeField, Min(0f)] private float damping = 0.1f;

        private int speedHash;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            motor = GetComponent<NPCNavMeshMotor>();
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (motor == null)
                motor = GetComponent<NPCNavMeshMotor>();
            speedHash = Animator.StringToHash(speedParameter);
        }

        private void Update()
        {
            if (animator == null || motor == null || motor.Agent == null)
                return;

            float speed = motor.Agent.velocity.magnitude;
            if (normalizeByAgentSpeed)
                speed /= Mathf.Max(motor.Agent.speed, 0.01f);

            animator.SetFloat(speedHash, speed, damping, Time.deltaTime);
        }
    }
}
