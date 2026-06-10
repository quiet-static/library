using QuietStatic.Input;
using UnityEngine;

namespace QuietStatic.Animation.Cameras
{
    /// <summary>
    /// Controls a third-person camera that follows a target transform and rotates around it
    /// using input from a component that implements <see cref="ILookInputSource"/>.
    /// </summary>
    /// <remarks>
    /// Attach this script to the camera object that should orbit around the player or active
    /// character. The camera reads look input every <see cref="LateUpdate"/>, updates its yaw
    /// and pitch values, then smoothly moves to a position behind the target.
    ///
    /// This script intentionally depends on the <see cref="ILookInputSource"/> interface instead
    /// of a concrete player input class. That keeps the camera reusable with different input
    /// providers, such as player controls, cutscene controls, or testing input.
    /// </remarks>
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The transform the camera should follow and orbit around, usually the active player character.")]
        [SerializeField] private Transform target;

        [Tooltip("A MonoBehaviour on this or another GameObject that implements ILookInputSource and provides look input.")]
        [SerializeField] private MonoBehaviour inputSourceBehaviour;

        [Header("Rotation")]
        [Tooltip("Multiplier applied to look input before updating the camera yaw and pitch.")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Tooltip("Lowest vertical pitch angle the camera can rotate to, in degrees. Negative values look downward.")]
        [SerializeField] private float minVerticalAngle = -30f;

        [Tooltip("Highest vertical pitch angle the camera can rotate to, in degrees. Positive values look upward.")]
        [SerializeField] private float maxVerticalAngle = 70f;

        [Header("Distance")]
        [Tooltip("How far behind the target the camera should sit.")]
        [SerializeField] private float distance = 5f;

        [Tooltip("Vertical offset added to the target position so the camera focuses around the upper body instead of the feet.")]
        [SerializeField] private float heightOffset = 1.5f;

        [Header("Smoothing")]
        [Tooltip("How quickly the camera position catches up to the desired follow position. Lower values feel snappier.")]
        [SerializeField] private float followSmoothTime = 0.05f;

        [Tooltip("How quickly the camera rotation catches up to the desired look rotation. Higher values feel snappier.")]
        [SerializeField] private float rotationSmoothTime = 8f;

        /// <summary>
        /// Cached input source used to read the current look input each frame.
        /// </summary>
        private ILookInputSource inputSource;

        /// <summary>
        /// Current horizontal camera angle, in degrees.
        /// </summary>
        private float yaw;

        /// <summary>
        /// Current vertical camera angle, in degrees.
        /// </summary>
        private float pitch;

        /// <summary>
        /// Velocity reference used by <see cref="Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float)"/>
        /// when smoothing the camera follow position.
        /// </summary>
        private Vector3 currentVelocity;

        /// <summary>
        /// Resolves the configured input source before the camera begins updating.
        /// </summary>
        /// <remarks>
        /// Unity cannot serialize interface fields directly in the Inspector, so this script stores
        /// the input provider as a <see cref="MonoBehaviour"/> and casts it to <see cref="ILookInputSource"/>
        /// at runtime. If the assigned behaviour does not implement the interface, the camera disables
        /// itself to avoid repeated null-reference errors.
        /// </remarks>
        private void Awake()
        {
            inputSource = inputSourceBehaviour as ILookInputSource;

            if (inputSourceBehaviour != null && inputSource == null)
            {
                Debug.LogError(
                    $"{nameof(CameraController)} requires a component that implements {nameof(ILookInputSource)}.",
                    this
                );
                enabled = false;
            }
        }

        /// <summary>
        /// Updates the camera after normal frame movement has completed.
        /// </summary>
        /// <remarks>
        /// Camera follow logic belongs in <see cref="LateUpdate"/> so the target has already finished
        /// moving for the frame. This helps reduce jitter when following player-controlled objects.
        /// </remarks>
        private void LateUpdate()
        {
            if (target == null || inputSource == null)
                return;

            HandleRotation(inputSource.Look);
            HandleFollow();
        }

        /// <summary>
        /// Sets the transform this camera should follow and orbit around.
        /// </summary>
        /// <param name="newTarget">
        /// The new transform to follow. Passing <c>null</c> clears the target and pauses camera follow updates
        /// until another target is assigned.
        /// </param>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Updates the stored yaw and pitch values using the current look input.
        /// </summary>
        /// <param name="lookInput">
        /// The current look input, where X controls horizontal rotation and Y controls vertical rotation.
        /// </param>
        /// <remarks>
        /// Pitch is clamped between <see cref="minVerticalAngle"/> and <see cref="maxVerticalAngle"/>
        /// so the camera cannot rotate too far above or below the target.
        /// </remarks>
        private void HandleRotation(Vector2 lookInput)
        {
            yaw += lookInput.x * mouseSensitivity;
            pitch -= lookInput.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }

        /// <summary>
        /// Smoothly moves and rotates the camera to its desired position around the current target.
        /// </summary>
        /// <remarks>
        /// The desired position is calculated by creating a focus point above the target, rotating a
        /// backwards offset by the current yaw and pitch, and placing the camera at that offset. Position
        /// smoothing uses <see cref="Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float)"/>, while
        /// rotation smoothing uses <see cref="Quaternion.Slerp(Quaternion, Quaternion, float)"/>.
        /// </remarks>
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
