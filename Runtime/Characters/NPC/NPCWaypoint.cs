using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Describes one scene-owned stop on an authored NPC route.</summary>
    /// <remarks>
    /// The Transform supplies the destination. The remaining fields describe what an NPC
    /// should do after reaching it, without coupling the waypoint to a particular character.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NPCWaypoint : MonoBehaviour
    {
        /// <summary>Determines how an NPC is oriented while it waits at this waypoint.</summary>
        public enum FacingMode
        {
            None,
            WaypointForward,
            Target
        }

        [Header("Arrival")]
        [Tooltip("Planar distance from the resolved destination that counts as reaching this waypoint.")]
        [Min(0.01f)]
        [SerializeField] private float arrivalDistance = 0.35f;

        [Tooltip("Random horizontal offset around this waypoint used to keep multiple NPCs from stacking on one point.")]
        [Min(0f)]
        [SerializeField] private float destinationJitterRadius = 0.25f;

        [Header("Wait")]
        [Tooltip("Minimum time an NPC waits after reaching this waypoint.")]
        [Min(0f)]
        [SerializeField] private float minimumWaitDuration = 1f;

        [Tooltip("Maximum time an NPC waits after reaching this waypoint.")]
        [Min(0f)]
        [SerializeField] private float maximumWaitDuration = 3f;

        [Header("Facing")]
        [Tooltip("How an NPC should face while waiting at this waypoint.")]
        [SerializeField] private FacingMode facingMode;

        [Tooltip("Target faced when Facing Mode is Target. The waypoint's forward direction is used by Waypoint Forward.")]
        [SerializeField] private Transform facingTarget;

        [Tooltip("Maximum degrees per second used to turn toward the configured facing direction. Zero turns immediately.")]
        [Min(0f)]
        [SerializeField] private float facingTurnSpeed = 360f;

        [Tooltip("Remaining yaw angle considered close enough to finish facing this waypoint.")]
        [Range(0.1f, 20f)]
        [SerializeField] private float facingTolerance = 2f;

        [Header("Animation")]
        [Tooltip("Optional Animator trigger fired on the arriving NPC through its NPCAnimationTrigger component.")]
        [SerializeField] private string animatorTrigger;

        [Header("Events")]
        [Tooltip("Invoked after an NPC reaches this waypoint. The arriving NPC is supplied as the event argument.")]
        [SerializeField] private UnityEvent<NPCController> onReached = new();

        /// <summary>Gets the planar distance that counts as reaching this waypoint.</summary>
        public float ArrivalDistance => Mathf.Max(0.01f, arrivalDistance);

        /// <summary>Gets the random horizontal destination offset radius.</summary>
        public float DestinationJitterRadius => Mathf.Max(0f, destinationJitterRadius);

        /// <summary>Gets the minimum configured wait duration.</summary>
        public float MinimumWaitDuration => Mathf.Max(0f, minimumWaitDuration);

        /// <summary>Gets the normalized maximum configured wait duration.</summary>
        public float MaximumWaitDuration => Mathf.Max(MinimumWaitDuration, maximumWaitDuration);

        /// <summary>Gets the configured facing policy.</summary>
        public FacingMode WaypointFacingMode => facingMode;

        /// <summary>Gets the maximum facing turn speed in degrees per second.</summary>
        public float FacingTurnSpeed => Mathf.Max(0f, facingTurnSpeed);

        /// <summary>Gets the yaw tolerance used to finish facing.</summary>
        public float FacingTolerance => Mathf.Max(0.1f, facingTolerance);

        /// <summary>Gets the normalized optional Animator trigger name.</summary>
        public string AnimatorTrigger => string.IsNullOrWhiteSpace(animatorTrigger)
            ? string.Empty
            : animatorTrigger.Trim();

        /// <summary>Chooses a wait duration inside this waypoint's configured range.</summary>
        public float GetWaitDuration()
        {
            float minimum = MinimumWaitDuration;
            float maximum = MaximumWaitDuration;
            return maximum <= minimum
                ? minimum
                : Random.Range(minimum, maximum);
        }

        /// <summary>
        /// Resolves the horizontal direction an NPC should face at this waypoint.
        /// </summary>
        /// <param name="actorPosition">Current world position of the NPC.</param>
        /// <param name="direction">Normalized horizontal facing direction when available.</param>
        /// <returns>True when this waypoint has a valid facing direction.</returns>
        public bool TryGetFacingDirection(Vector3 actorPosition, out Vector3 direction)
        {
            switch (facingMode)
            {
                case FacingMode.WaypointForward:
                    direction = transform.forward;
                    break;

                case FacingMode.Target when facingTarget != null:
                    direction = facingTarget.position - actorPosition;
                    break;

                default:
                    direction = Vector3.zero;
                    return false;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.zero;
                return false;
            }

            direction.Normalize();
            return true;
        }

        /// <summary>Invokes this waypoint's arrival callbacks for an NPC route runner.</summary>
        /// <param name="npc">NPC that reached the waypoint.</param>
        public void NotifyReached(NPCController npc)
        {
            onReached?.Invoke(npc);
        }

        private void OnValidate()
        {
            arrivalDistance = Mathf.Max(0.01f, arrivalDistance);
            destinationJitterRadius = Mathf.Max(0f, destinationJitterRadius);
            minimumWaitDuration = Mathf.Max(0f, minimumWaitDuration);
            maximumWaitDuration = Mathf.Max(minimumWaitDuration, maximumWaitDuration);
            facingTurnSpeed = Mathf.Max(0f, facingTurnSpeed);
            facingTolerance = Mathf.Clamp(facingTolerance, 0.1f, 20f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, ArrivalDistance);

            if (DestinationJitterRadius > 0f)
            {
                Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, DestinationJitterRadius);
            }
        }
    }
}
