using UnityEngine;

namespace QuietStatic.Toolkit.Cameras
{
    /// <summary>
    /// Temporarily takes control of the camera and smoothly rotates it toward
    /// a world-space target.
    /// </summary>
    public class CameraFocusController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Normal player camera controller disabled during scripted focus.")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("Optional first-person anchor whose position the camera follows.")]
        [SerializeField] private Transform positionAnchor;

        [Header("Focus")]
        [Tooltip("How quickly the camera rotates toward its focus target.")]
        [SerializeField] private float rotationSpeed = 5f;

        private Transform focusTarget;
        private bool isFocusing;

        public bool IsFocusing => isFocusing;

        private void Awake()
        {
            if (cameraController == null)
            {
                cameraController = GetComponent<CameraController>();
            }
        }

        private void LateUpdate()
        {
            if (!isFocusing || focusTarget == null)
            {
                return;
            }

            if (positionAnchor != null)
            {
                transform.position = positionAnchor.position;
            }

            Vector3 direction = focusTarget.position - transform.position;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Begins smoothly focusing the camera on a target.
        /// </summary>
        public void BeginFocus(Transform newFocusTarget)
        {
            if (newFocusTarget == null)
            {
                return;
            }

            focusTarget = newFocusTarget;
            isFocusing = true;

            if (cameraController != null)
            {
                cameraController.SetCameraControlEnabled(false);
            }
        }

        /// <summary>
        /// Stops scripted focus and returns control to the normal camera.
        /// </summary>
        public void EndFocus()
        {
            if (!isFocusing)
            {
                return;
            }

            isFocusing = false;
            focusTarget = null;

            if (cameraController != null)
            {
                cameraController.SyncAnglesFromCurrentRotation();
                cameraController.SetCameraControlEnabled(true);
            }
        }

        /// <summary>
        /// Immediately faces the current focus target.
        /// </summary>
        public void SnapToFocus()
        {
            if (focusTarget == null)
            {
                return;
            }

            Vector3 direction = focusTarget.position - transform.position;

            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );
        }
    }
}