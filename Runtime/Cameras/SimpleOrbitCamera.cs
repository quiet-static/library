using UnityEngine;

namespace QuietStatic.Toolkit.CameraTools
{
    /// <summary>
    /// Provides a lightweight third-person orbit camera that can follow a target,
    /// rotate around that target from external look input, and avoid clipping through geometry.
    /// </summary>
    /// <remarks>
    /// This camera does not read input directly. Instead, another script should pass look input
    /// into <see cref="AddLookInput"/>. This keeps the camera reusable across different input systems,
    /// character controllers, and cutscene/gameplay setups.
    /// </remarks>
    public class SimpleOrbitCamera : MonoBehaviour
    {
        /// <summary>
        /// The transform the camera should orbit around.
        /// </summary>
        [Header("Target")]
        [Tooltip("The transform the camera should follow and orbit around. Usually this is the player or the active character.")]
        [SerializeField] private Transform target;

        /// <summary>
        /// The local offset from the target position used as the camera's focus point.
        /// </summary>
        [Tooltip("Offset from the target position used as the orbit focus point. Raise Y to make the camera look around the character's upper body/head instead of their feet.")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

        /// <summary>
        /// The desired distance between the camera and the focus point when unobstructed.
        /// </summary>
        [Header("Orbit Distance")]
        [Tooltip("How far the camera should sit from the target focus point when nothing is blocking the view.")]
        [Min(0.1f)]
        [SerializeField] private float distance = 4f;

        /// <summary>
        /// Controls how quickly look input rotates the camera.
        /// </summary>
        [Header("Rotation")]
        [Tooltip("How quickly the camera rotates when look input is added. Higher values make the camera turn faster.")]
        [Min(0f)]
        [SerializeField] private float sensitivity = 120f;

        /// <summary>
        /// The lowest vertical camera angle allowed, in degrees.
        /// </summary>
        [Tooltip("Lowest vertical orbit angle allowed, in degrees. Negative values let the camera look downward from above the target.")]
        [SerializeField] private float minPitch = -35f;

        /// <summary>
        /// The highest vertical camera angle allowed, in degrees.
        /// </summary>
        [Tooltip("Highest vertical orbit angle allowed, in degrees. Lower this if the camera can look too far upward or flip into awkward angles.")]
        [SerializeField] private float maxPitch = 70f;

        /// <summary>
        /// The smoothing time used when moving the camera toward its desired position.
        /// </summary>
        [Header("Smoothing")]
        [Tooltip("How quickly the camera position catches up to the desired orbit position. Lower values feel snappier; higher values feel smoother.")]
        [Min(0f)]
        [SerializeField] private float smoothTime = 0.05f;

        /// <summary>
        /// Layers that the camera should treat as solid when preventing camera clipping.
        /// </summary>
        [Header("Collision")]
        [Tooltip("Layers that can block the camera. The camera will move closer to the target when these layers are between the focus point and desired camera position.")]
        [SerializeField] private LayerMask collisionMask = ~0;

        /// <summary>
        /// Radius used for the sphere cast that checks whether the camera path is blocked.
        /// </summary>
        [Tooltip("Radius of the collision check used to keep the camera from clipping through walls or props.")]
        [Min(0f)]
        [SerializeField] private float collisionRadius = 0.2f;

        /// <summary>
        /// Current horizontal orbit angle, in degrees.
        /// </summary>
        private float yaw;

        /// <summary>
        /// Current vertical orbit angle, in degrees.
        /// </summary>
        private float pitch;

        /// <summary>
        /// Velocity reference used by <see cref="Vector3.SmoothDamp"/> when smoothing camera movement.
        /// </summary>
        private Vector3 currentVelocity;

        /// <summary>
        /// Look input accumulated since the previous camera update.
        /// </summary>
        private Vector2 pendingLookInput;

        /// <summary>
        /// Changes the transform that this camera follows and orbits around.
        /// </summary>
        /// <param name="newTarget">The new target transform. Passing null leaves the camera without a follow target until another target is assigned.</param>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Adds look input to be consumed during the next <see cref="LateUpdate"/>.
        /// </summary>
        /// <param name="lookInput">
        /// The look input delta. X rotates the camera horizontally, and Y rotates it vertically.
        /// </param>
        /// <remarks>
        /// This method accumulates input so multiple systems can add look movement before the camera updates.
        /// The accumulated value is cleared after it is applied.
        /// </remarks>
        public void AddLookInput(Vector2 lookInput)
        {
            pendingLookInput += lookInput;
        }

        /// <summary>
        /// Updates camera rotation from pending look input, calculates the desired orbit position,
        /// applies collision correction, and moves the camera after the target has updated.
        /// </summary>
        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            UpdateOrbitAngles();
            UpdateCameraTransform();
        }

        /// <summary>
        /// Rotates the camera to face a specific focus target while continuing to orbit around the current follow target.
        /// </summary>
        /// <param name="focusTarget">The transform the camera should face.</param>
        /// <remarks>
        /// This is useful for dialogue, cutscenes, jumpscares, or interaction moments where the camera
        /// should quickly look toward a specific object or character.
        /// </remarks>
        public void FaceTarget(Transform focusTarget)
        {
            if (target == null || focusTarget == null)
            {
                return;
            }

            Vector3 direction = focusTarget.position - GetFocusPoint();

            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            Vector3 euler = lookRotation.eulerAngles;

            yaw = euler.y;
            pitch = NormalizePitch(euler.x);
        }

        /// <summary>
        /// Applies pending look input to the current yaw and pitch values, then clamps the pitch.
        /// </summary>
        private void UpdateOrbitAngles()
        {
            yaw += pendingLookInput.x * sensitivity * Time.deltaTime;
            pitch -= pendingLookInput.y * sensitivity * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            pendingLookInput = Vector2.zero;
        }

        /// <summary>
        /// Calculates the desired camera position and rotation, corrects for collision, then applies the transform update.
        /// </summary>
        private void UpdateCameraTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focusPoint = GetFocusPoint();
            Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;

            desiredPosition = CorrectPositionForCollision(focusPoint, desiredPosition);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                smoothTime
            );

            transform.rotation = rotation;
        }

        /// <summary>
        /// Gets the point the camera orbits around.
        /// </summary>
        /// <returns>The target position with <see cref="targetOffset"/> applied.</returns>
        private Vector3 GetFocusPoint()
        {
            return target.position + targetOffset;
        }

        /// <summary>
        /// Moves the desired camera position closer to the focus point if something blocks the camera path.
        /// </summary>
        /// <param name="focusPoint">The point the camera is looking at and orbiting around.</param>
        /// <param name="desiredPosition">The ideal camera position before collision correction.</param>
        /// <returns>The corrected camera position.</returns>
        private Vector3 CorrectPositionForCollision(Vector3 focusPoint, Vector3 desiredPosition)
        {
            Vector3 focusToCamera = desiredPosition - focusPoint;
            Vector3 direction = focusToCamera.normalized;

            if (Physics.SphereCast(
                    focusPoint,
                    collisionRadius,
                    direction,
                    out RaycastHit hit,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                float correctedDistance = Mathf.Max(0.1f, hit.distance - collisionRadius);
                return focusPoint + direction * correctedDistance;
            }

            return desiredPosition;
        }

        /// <summary>
        /// Converts a Unity Euler pitch value from the 0-360 range into a signed -180 to 180 range.
        /// </summary>
        /// <param name="value">The pitch value to normalize.</param>
        /// <returns>The normalized pitch value.</returns>
        private static float NormalizePitch(float value)
        {
            if (value > 180f)
            {
                value -= 360f;
            }

            return value;
        }
    }
}
