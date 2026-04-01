using QuietStatic.Input;
using UnityEngine;

namespace QuietStatic.Systems.Cameras
{
    /// <summary>
    /// A third-person camera that follows a target and rotates using a look input source.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;

        [Tooltip("Assign a component that implements ILookInputSource.")]
        [SerializeField] private MonoBehaviour inputSourceBehaviour;

        [Header("Rotation")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float minVerticalAngle = -30f;
        [SerializeField] private float maxVerticalAngle = 70f;

        [Header("Distance")]
        [SerializeField] private float distance = 5f;
        [SerializeField] private float heightOffset = 1.5f;

        [Header("Smoothing")]
        [SerializeField] private float followSmoothTime = 0.05f;
        [SerializeField] private float rotationSmoothTime = 8f;

        private ILookInputSource inputSource;

        private float yaw;
        private float pitch;

        private Vector3 currentVelocity;

        private void Awake()
        {
            inputSource = inputSourceBehaviour as ILookInputSource;

            if (inputSourceBehaviour != null && inputSource == null)
            {
                Debug.LogError(
                    $"{nameof(ThirdPersonCamera)} requires a component that implements {nameof(ILookInputSource)}.",
                    this
                );
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (target == null || inputSource == null)
                return;

            HandleRotation(inputSource.Look);
            HandleFollow();
        }

        /// <summary>
        /// Sets the target this camera should follow.
        /// </summary>
        /// <param name="newTarget">The transform to follow.</param>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Updates yaw and pitch from look input.
        /// </summary>
        /// <param name="lookInput">The current look input.</param>
        private void HandleRotation(Vector2 lookInput)
        {
            yaw += lookInput.x * mouseSensitivity;
            pitch -= lookInput.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }

        /// <summary>
        /// Smoothly moves and rotates the camera around the current target.
        /// </summary>
        private void HandleFollow()
        {
            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focusPoint = target.position + Vector3.up * heightOffset;
            Vector3 desiredPosition = focusPoint - targetRotation * Vector3.forward * distance;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                followSmoothTime
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSmoothTime * Time.deltaTime
            );
        }
    }
}