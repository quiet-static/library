using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Adds configurable idle position and rotation motion to a cinematic camera.
    /// </summary>
    /// <remarks>
    /// Motion is applied around a cached local pose. Call
    /// <see cref="RefreshBaseTransform"/> after a camera director moves the camera
    /// so idle motion continues around the new shot.
    /// </remarks>
    public class CutsceneCameraIdle : MonoBehaviour
    {
        [Header("Idle Toggle")]
        [Tooltip("Whether idle position and rotation offsets are applied.")]
        [SerializeField] private bool idleEnabled = true;

        [Header("Position Idle")]
        [Tooltip("Whether local-position idle motion is enabled.")]
        [SerializeField] private bool usePositionIdle = true;

        [Tooltip("Maximum local-position offset applied on each axis.")]
        [SerializeField]
        private Vector3 positionAmplitude = new Vector3(0.04f, 0.03f, 0f);

        [Tooltip("How quickly the position noise changes.")]
        [Min(0f)]
        [SerializeField] private float positionFrequency = 0.35f;

        [Header("Rotation Idle")]
        [Tooltip("Whether local-rotation idle motion is enabled.")]
        [SerializeField] private bool useRotationIdle = true;

        [Tooltip("Maximum local-rotation offset, in degrees, on each axis.")]
        [SerializeField]
        private Vector3 rotationAmplitude = new Vector3(0.4f, 0.4f, 0.2f);

        [Tooltip("How quickly the rotation noise changes.")]
        [Min(0f)]
        [SerializeField] private float rotationFrequency = 0.25f;

        [Header("Smoothing")]
        [Tooltip("How quickly the camera eases toward each generated offset.")]
        [Min(0f)]
        [SerializeField] private float smoothing = 8f;

        [HideInInspector]
        [SerializeField] private float speed = -1f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private Vector3 currentPositionOffset;
        private Vector3 currentRotationOffset;
        private float positionSeed;
        private float rotationSeed;

        private void Awake()
        {
            RefreshBaseTransform();
            positionSeed = Random.Range(0f, 1000f);
            rotationSeed = Random.Range(1000f, 2000f);
        }

        private void LateUpdate()
        {
            if (!idleEnabled)
            {
                ReturnToBaseTransform();
                return;
            }

            float currentTime = Time.time;
            Vector3 targetPositionOffset = Vector3.zero;
            Vector3 targetRotationOffset = Vector3.zero;

            if (usePositionIdle)
            {
                targetPositionOffset = GetNoiseOffset(
                    positionSeed,
                    currentTime * ResolveFrequency(positionFrequency),
                    positionAmplitude
                );
            }

            if (useRotationIdle)
            {
                targetRotationOffset = GetNoiseOffset(
                    rotationSeed,
                    currentTime * ResolveFrequency(rotationFrequency),
                    rotationAmplitude
                );
            }

            currentPositionOffset = Vector3.Lerp(
                currentPositionOffset,
                targetPositionOffset,
                Time.deltaTime * smoothing
            );
            currentRotationOffset = Vector3.Lerp(
                currentRotationOffset,
                targetRotationOffset,
                Time.deltaTime * smoothing
            );

            ApplyOffsets();
        }

        /// <summary>
        /// Updates the local pose around which idle motion is applied.
        /// </summary>
        public void RefreshBaseTransform()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            currentPositionOffset = Vector3.zero;
            currentRotationOffset = Vector3.zero;
        }

        /// <summary>
        /// Enables or disables idle motion.
        /// </summary>
        public void SetIdleEnabled(bool enabled)
        {
            idleEnabled = enabled;
        }

        /// <summary>
        /// Immediately removes all idle offsets.
        /// </summary>
        public void ResetIdle()
        {
            currentPositionOffset = Vector3.zero;
            currentRotationOffset = Vector3.zero;
            ApplyOffsets();
        }

        private void ReturnToBaseTransform()
        {
            currentPositionOffset = Vector3.Lerp(
                currentPositionOffset,
                Vector3.zero,
                Time.deltaTime * smoothing
            );
            currentRotationOffset = Vector3.Lerp(
                currentRotationOffset,
                Vector3.zero,
                Time.deltaTime * smoothing
            );

            ApplyOffsets();
        }

        private void ApplyOffsets()
        {
            transform.localPosition =
                baseLocalPosition + currentPositionOffset;
            transform.localRotation =
                baseLocalRotation * Quaternion.Euler(currentRotationOffset);
        }

        private static Vector3 GetNoiseOffset(
            float seed,
            float time,
            Vector3 amplitude)
        {
            return new Vector3(
                NoiseCentered(seed, time) * amplitude.x,
                NoiseCentered(seed + 17f, time) * amplitude.y,
                NoiseCentered(seed + 31f, time) * amplitude.z
            );
        }

        private static float NoiseCentered(float seed, float time)
        {
            return Mathf.PerlinNoise(seed, time) * 2f - 1f;
        }

        private float ResolveFrequency(float configuredFrequency)
        {
            return speed >= 0f ? speed : configuredFrequency;
        }
    }
}
