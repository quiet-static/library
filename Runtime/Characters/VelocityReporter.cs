using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Calculates and exposes this GameObject's current velocity based on its frame-to-frame
    /// position changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="VelocityReporter"/> is useful when an object does not already expose velocity
    /// through a physics component, or when another system needs an easy way to read movement
    /// information from a Transform.
    /// </para>
    /// <para>
    /// The component provides two velocity values:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description><see cref="rawVelocity"/>: the immediate frame-to-frame velocity.</description>
    /// </item>
    /// <item>
    /// <description><see cref="velocity"/>: a smoothed velocity that is less jittery.</description>
    /// </item>
    /// </list>
    /// <para>
    /// This can be reused by NPC follow behavior, look-ahead targeting, animation logic,
    /// camera prediction, or movement-based effects.
    /// </para>
    /// </remarks>
    public class VelocityReporter : MonoBehaviour
    {
        [Header("Smoothing")]
        [Tooltip("Controls how quickly the smoothed velocity catches up to the raw velocity. Lower values react faster; higher values smooth more strongly.")]
        [Min(0f)]
        public float smoothingTimeFactor = 0.5f;

        /// <summary>
        /// Stores the object's position from the previous frame.
        /// </summary>
        /// <remarks>
        /// This is used as the starting point for calculating how far the object moved
        /// during the current frame.
        /// </remarks>
        private Vector3 prevPos;

        /// <summary>
        /// Internal velocity reference used by <see cref="Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float)"/>.
        /// </summary>
        /// <remarks>
        /// Unity updates this value internally while smoothing. It should usually only be
        /// reset when the smoothing state itself needs to be cleared.
        /// </remarks>
        private Vector3 smoothingParamVel;

        /// <summary>
        /// Gets the direct frame-to-frame velocity of this GameObject.
        /// </summary>
        /// <remarks>
        /// This value is calculated by dividing the position delta by <see cref="Time.deltaTime"/>.
        /// It responds immediately, but it can be noisy when movement changes suddenly between frames.
        /// </remarks>
        public Vector3 rawVelocity { get; private set; }

        /// <summary>
        /// Gets the smoothed velocity of this GameObject.
        /// </summary>
        /// <remarks>
        /// This value gradually moves toward <see cref="rawVelocity"/> using
        /// <see cref="Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float)"/>.
        /// Use this when another system needs a more stable velocity value.
        /// </remarks>
        public Vector3 velocity { get; private set; }

        /// <summary>
        /// Captures the starting position so the first velocity calculation has a valid
        /// previous position to compare against.
        /// </summary>
        private void Start()
        {
            prevPos = transform.position;
        }

        /// <summary>
        /// Calculates raw velocity and smoothed velocity once per frame.
        /// </summary>
        /// <remarks>
        /// When <see cref="Time.deltaTime"/> is effectively zero, velocity values are reset
        /// to zero to avoid division by zero or invalid motion data.
        /// </remarks>
        private void Update()
        {
            if (!Mathf.Approximately(Time.deltaTime, 0f))
            {
                rawVelocity = CalculateRawVelocity();
                velocity = CalculateSmoothedVelocity(rawVelocity);
            }
            else
            {
                ResetVelocityValues();
            }

            prevPos = transform.position;
        }

        /// <summary>
        /// Calculates the direct velocity from the object's position delta this frame.
        /// </summary>
        /// <returns>
        /// The current frame-to-frame velocity in world units per second.
        /// </returns>
        private Vector3 CalculateRawVelocity()
        {
            return (transform.position - prevPos) / Time.deltaTime;
        }

        /// <summary>
        /// Smooths the reported velocity toward the latest raw velocity.
        /// </summary>
        /// <param name="targetVelocity">The raw velocity value to smooth toward.</param>
        /// <returns>
        /// A smoothed velocity value that reduces sudden spikes and jitter.
        /// </returns>
        private Vector3 CalculateSmoothedVelocity(Vector3 targetVelocity)
        {
            return Vector3.SmoothDamp(
                velocity,
                targetVelocity,
                ref smoothingParamVel,
                smoothingTimeFactor
            );
        }

        /// <summary>
        /// Clears both reported velocity values and the internal smoothing helper value.
        /// </summary>
        /// <remarks>
        /// This is used when velocity cannot be safely calculated, such as when
        /// <see cref="Time.deltaTime"/> is effectively zero.
        /// </remarks>
        private void ResetVelocityValues()
        {
            rawVelocity = Vector3.zero;
            velocity = Vector3.zero;
            smoothingParamVel = Vector3.zero;
        }
    }
}
