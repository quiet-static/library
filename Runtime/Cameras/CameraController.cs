using QuietStatic.Input;
using UnityEngine;

namespace QuietStatic.Toolkit.Cameras
{
    /// <summary>
    /// Controls a camera that can operate in either third-person orbit mode
    /// or first-person player view mode.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The transform the camera should follow and orbit around, usually the active player character.")]
        [SerializeField] private Transform target;

        [Tooltip("A MonoBehaviour on this or another GameObject that implements ILookInputSource and provides look input.")]
        [SerializeField] private MonoBehaviour inputSourceBehaviour;

        [Header("Sensitivity")]
        [Tooltip("Multiplier applied to look input before updating the camera yaw and pitch.")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Tooltip("Lowest vertical pitch angle the camera can rotate to, in degrees.")]
        [SerializeField] private float minVerticalAngle = -30f;

        [Tooltip("Highest vertical pitch angle the camera can rotate to, in degrees.")]
        [SerializeField] private float maxVerticalAngle = 70f;

        [Header("Third Person")]
        [Tooltip("How far behind the target the camera should sit.")]
        [SerializeField] private float distance = 5f;

        [Tooltip("Vertical offset added to the target position for third-person orbiting.")]
        [SerializeField] private float heightOffset = 1.5f;

        [Header("Third Person Smoothing")]
        [Tooltip("How quickly the camera position catches up to the desired follow position.")]
        [SerializeField] private float followSmoothTime = 0.001f;

        [Tooltip("How quickly the camera rotation catches up to the desired rotation.")]
        [SerializeField] private float rotationSmoothTime = 8f;

        [Header("First Person")]
        [Tooltip("Whether the camera should use first-person behavior.")]
        [SerializeField] private bool isFirstPerson;

        [Tooltip("Camera position anchor, usually placed at the player's eye level.")]
        [SerializeField] private Transform firstPersonAnchor;

        [Tooltip("The player body rotated horizontally with first-person camera yaw.")]
        [SerializeField] private Transform playerBody;

        /// <summary>
        /// Cached source of player look input.
        /// </summary>
        private ILookInputSource inputSource;

        /// <summary>
        /// Horizontal camera rotation in degrees.
        /// </summary>
        private float yaw;

        /// <summary>
        /// Vertical camera rotation in degrees.
        /// </summary>
        private float pitch;

        /// <summary>
        /// Velocity used by SmoothDamp for third-person camera movement.
        /// </summary>
        private Vector3 currentVelocity;

        /// <summary>
        /// Gets whether the camera is currently operating in first-person mode.
        /// </summary>
        public bool IsFirstPerson => isFirstPerson;

        private void Awake()
        {
            inputSource = inputSourceBehaviour as ILookInputSource;

            if (inputSourceBehaviour != null && inputSource == null)
            {
                GameLogger.Warning(
                    nameof(Awake),
                    this,
                    $"{nameof(CameraController)} requires a component that implements {nameof(ILookInputSource)}."
                );

                enabled = false;
                return;
            }

            SetAnglesFromRotation(transform.rotation);
        }

        private void LateUpdate()
        {
            if (target == null || inputSource == null)
            {
                return;
            }

            HandleRotation(inputSource.Look);
            HandleFollow();
        }

        /// <summary>
        /// Sets the transform this camera follows.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Sets the third-person follow distance.
        /// </summary>
        public void SetDistance(float newDistance)
        {
            distance = Mathf.Max(0f, newDistance);
        }

        /// <summary>
        /// Sets the first-person camera anchor.
        /// </summary>
        public void SetFirstPersonAnchor(Transform newAnchor)
        {
            firstPersonAnchor = newAnchor;
        }

        /// <summary>
        /// Sets the transform rotated by first-person horizontal look input.
        /// </summary>
        public void SetPlayerBody(Transform newPlayerBody)
        {
            playerBody = newPlayerBody;
        }

        /// <summary>
        /// Changes between first-person and third-person camera behavior.
        /// </summary>
        public void SetFirstPersonMode(bool enabled)
        {
            isFirstPerson = enabled;
            currentVelocity = Vector3.zero;

            // Prevent visible movement interpolation when changing modes.
            HandleFollow();
        }

        /// <summary>
        /// Immediately snaps the camera to its current desired position and rotation.
        /// Useful after loading a scene, swapping characters, or changing camera mode.
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);

            if (isFirstPerson)
            {
                if (firstPersonAnchor == null)
                {
                    return;
                }

                transform.SetPositionAndRotation(
                    firstPersonAnchor.position,
                    targetRotation
                );

                return;
            }

            Vector3 focusPoint = target.position + Vector3.up * heightOffset;
            Vector3 desiredPosition = focusPoint - targetRotation * Vector3.forward * distance;

            transform.SetPositionAndRotation(desiredPosition, targetRotation);
            currentVelocity = Vector3.zero;
        }

        /// <summary>
        /// Rotates the camera's internal yaw and pitch values so it faces a target.
        /// </summary>
        /// <remarks>
        /// This only changes the internal camera angles. The normal camera update
        /// continues to handle movement and rotation afterward.
        /// </remarks>
        public void FaceTarget(Transform focusTarget)
        {
            if (focusTarget == null)
            {
                GameLogger.Warning(
                    nameof(FaceTarget),
                    this,
                    $"{nameof(CameraController)} cannot face a null target."
                );

                return;
            }

            if (target == null)
            {
                GameLogger.Warning(
                    nameof(FaceTarget),
                    this,
                    $"{nameof(CameraController)} has no follow target."
                );

                return;
            }

            Vector3 lookOrigin = GetCurrentLookOrigin();
            Vector3 directionToFocus = focusTarget.position - lookOrigin;

            if (directionToFocus.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Vector3 flatDirection = new Vector3(
                directionToFocus.x,
                0f,
                directionToFocus.z
            );

            // Do not overwrite yaw if the target is directly above or below.
            if (flatDirection.sqrMagnitude > 0.001f)
            {
                yaw = Mathf.Atan2(
                    flatDirection.x,
                    flatDirection.z
                ) * Mathf.Rad2Deg;
            }

            pitch = -Mathf.Atan2(
                directionToFocus.y,
                flatDirection.magnitude
            ) * Mathf.Rad2Deg;

            pitch = Mathf.Clamp(
                pitch,
                minVerticalAngle,
                maxVerticalAngle
            );
        }

        /// <summary>
        /// Immediately turns and positions the camera to face a target.
        /// </summary>
        /// <remarks>
        /// Use this for cutscenes or scripted moments where you do not want
        /// third-person rotation smoothing to delay the camera turn.
        /// </remarks>
        public void SnapFaceTarget(Transform focusTarget)
        {
            FaceTarget(focusTarget);
            SnapToTarget();
        }

        private void HandleRotation(Vector2 lookInput)
        {
            yaw += lookInput.x * mouseSensitivity;
            pitch -= lookInput.y * mouseSensitivity;

            pitch = Mathf.Clamp(
                pitch,
                minVerticalAngle,
                maxVerticalAngle
            );
        }

        private void HandleFollow()
        {
            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);

            if (isFirstPerson)
            {
                HandleFirstPersonFollow(targetRotation);
                return;
            }

            HandleThirdPersonFollow(targetRotation);
        }

        private void HandleFirstPersonFollow(Quaternion targetRotation)
        {
            if (firstPersonAnchor == null)
            {
                GameLogger.Warning(
                    nameof(HandleFirstPersonFollow),
                    this,
                    $"{nameof(CameraController)} is in first-person mode but has no first-person anchor assigned."
                );

                return;
            }

            // FPS camera movement should be immediate, not smoothed.
            transform.SetPositionAndRotation(
                firstPersonAnchor.position,
                targetRotation
            );

            // The body follows yaw only. Pitch stays on the camera.
            if (playerBody != null)
            {
                playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private void HandleThirdPersonFollow(Quaternion targetRotation)
        {
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

        /// <summary>
        /// Gets the world position from which FaceTarget should calculate its direction.
        /// </summary>
        private Vector3 GetCurrentLookOrigin()
        {
            if (isFirstPerson && firstPersonAnchor != null)
            {
                return firstPersonAnchor.position;
            }

            return target.position + Vector3.up * heightOffset;
        }

        /// <summary>
        /// Updates yaw and pitch from a world rotation.
        /// </summary>
        private void SetAnglesFromRotation(Quaternion rotation)
        {
            Vector3 eulerAngles = rotation.eulerAngles;

            yaw = eulerAngles.y;
            pitch = eulerAngles.x;

            if (pitch > 180f)
            {
                pitch -= 360f;
            }

            pitch = Mathf.Clamp(
                pitch,
                minVerticalAngle,
                maxVerticalAngle
            );
        }
    }
}