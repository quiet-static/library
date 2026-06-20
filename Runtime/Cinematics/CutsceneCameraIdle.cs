using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Adds subtle idle motion to a cutscene camera by gently offsetting its local
    /// position and rotation over time.
    /// </summary>
    /// <remarks>
    /// This component is useful for making static cinematic shots feel slightly more alive.
    /// It works in local space, so it can be placed directly on a camera or on a child
    /// transform beneath a camera rig.
    ///
    /// Call <see cref="RefreshBaseTransform"/> after another system moves or rotates the
    /// camera so the idle motion starts from the new camera pose instead of snapping back
    /// to the original pose captured during <see cref="Awake"/>.
    /// </remarks>
    public class CutsceneCameraIdle : MonoBehaviour
    {
        [Header("Idle Rotation")]
        [Tooltip("Maximum local rotation offset, in degrees, applied on each axis during the idle motion.")]
        [SerializeField] private Vector3 rotationAmplitude = new Vector3(1f, 1f, 0f);

        [Header("Idle Position")]
        [Tooltip("Maximum local position offset applied on each axis during the idle motion.")]
        [SerializeField] private Vector3 positionAmplitude = new Vector3(0.02f, 0.02f, 0f);

        [Header("Timing")]
        [Tooltip("Controls how quickly the idle camera motion cycles. Higher values create faster movement.")]
        [Min(0f)]
        [SerializeField] private float speed = 0.5f;

        /// <summary>
        /// The local position used as the center point for the idle movement.
        /// </summary>
        private Vector3 startPosition;

        /// <summary>
        /// The local rotation used as the base rotation for the idle movement.
        /// </summary>
        private Quaternion startRotation;

        /// <summary>
        /// Captures the camera's initial local pose before idle motion begins.
        /// </summary>
        private void Awake()
        {
            RefreshBaseTransform();
        }

        /// <summary>
        /// Applies a small looping local position and rotation offset every frame.
        /// </summary>
        private void Update()
        {
            ApplyIdleMotion();
        }

        /// <summary>
        /// Refreshes the base local position and rotation used by the idle motion.
        /// </summary>
        /// <remarks>
        /// Use this after a camera director, cutscene system, or animation changes the
        /// camera's transform. Without refreshing, the idle motion will continue to orbit
        /// around the old stored pose.
        /// </remarks>
        public void RefreshBaseTransform()
        {
            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
        }

        /// <summary>
        /// Calculates the current idle offset and applies it to the transform.
        /// </summary>
        private void ApplyIdleMotion()
        {
            Vector3 offset = GetIdleOffset();

            transform.localPosition = startPosition + Vector3.Scale(offset, positionAmplitude);
            transform.localRotation = startRotation * Quaternion.Euler(Vector3.Scale(offset, rotationAmplitude));
        }

        /// <summary>
        /// Builds a looping offset value from several sine/cosine waves.
        /// </summary>
        /// <returns>
        /// A normalized offset vector that is scaled by the configured position and
        /// rotation amplitudes before being applied.
        /// </returns>
        private Vector3 GetIdleOffset()
        {
            float time = Time.time * speed;

            return new Vector3(
                Mathf.Sin(time),
                Mathf.Cos(time * 0.7f),
                Mathf.Sin(time * 0.4f)
            );
        }
    }
}
