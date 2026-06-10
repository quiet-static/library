using UnityEngine;

namespace QuietStatic.Toolkit.Utilities
{
    /// <summary>
    /// Rotates this GameObject so it faces a target transform over time.
    /// </summary>
    /// <remarks>
    /// This component is useful for simple NPC facing, props that should turn toward
    /// the player, cutscene characters, cameras, or other objects that need to smoothly
    /// look at a target without requiring a full animation or AI system.
    ///
    /// By default, rotation is limited to the Y axis so the object stays upright. Disable
    /// <see cref="onlyYaw"/> if the object should pitch up or down toward the target too.
    /// </remarks>
    public class FaceTarget : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform this object should rotate toward. If this is empty, no rotation is applied.")]
        [SerializeField] private Transform target;

        [Header("Rotation Settings")]
        [Tooltip("How quickly this object turns toward the target. Higher values rotate faster.")]
        [Min(0f)]
        [SerializeField] private float turnSpeed = 8f;

        [Tooltip("If true, only rotates around the Y axis so the object remains upright. If false, the object can pitch up or down toward the target.")]
        [SerializeField] private bool onlyYaw = true;

        /// <summary>
        /// Assigns the target this object should face.
        /// </summary>
        /// <param name="newTarget">
        /// The new transform to face. Pass <c>null</c> to stop facing a target.
        /// </param>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Rotates this object toward the current target each frame.
        /// </summary>
        private void Update()
        {
            FaceCurrentTarget();
        }

        /// <summary>
        /// Calculates the direction to the target and smoothly rotates toward it.
        /// </summary>
        private void FaceCurrentTarget()
        {
            if (target == null)
            {
                return;
            }

            Vector3 direction = GetDirectionToTarget();

            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                turnSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Gets the direction from this object to the target, optionally flattened to the Y axis.
        /// </summary>
        /// <returns>
        /// A world-space direction vector pointing from this object toward the target.
        /// </returns>
        private Vector3 GetDirectionToTarget()
        {
            Vector3 direction = target.position - transform.position;

            if (onlyYaw)
            {
                direction.y = 0f;
            }

            return direction;
        }
    }
}
