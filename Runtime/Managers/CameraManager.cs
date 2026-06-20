using QuietStatic.Toolkit.Cameras;
using QuietStatic.Toolkit.Core;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Global helper for controlling the active <see cref="CameraController"/>.
    /// </summary>
    /// <remarks>
    /// This class intentionally does not know about characters, dialogue, cutscenes,
    /// or any specific gameplay system. Other systems can call its public methods
    /// to change camera focus, temporarily override its target, or restore the
    /// previous camera state.
    /// </remarks>
    public class CameraManager : ToolkitSingleton<CameraManager>
    {
        [Header("References")]
        [Tooltip("Camera controller managed by this component. If left empty, one is searched for at runtime.")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("Unity Camera component controlled when enabling or disabling the main view. If left empty, it is found on the CameraController object.")]
        [SerializeField] private Camera mainCamera;

        /// <summary>
        /// Most recently assigned follow target.
        /// </summary>
        private Transform activeTarget;

        /// <summary>
        /// Last camera target saved before a temporary focus override.
        /// </summary>
        private Transform savedTarget;

        /// <summary>
        /// Last camera distance saved before a temporary focus override.
        /// </summary>
        private float savedDistance;

        /// <summary>
        /// Current distance tracked by this manager.
        /// </summary>
        private float currentDistance;

        /// <summary>
        /// Gets the controlled camera controller.
        /// </summary>
        public CameraController Controller => cameraController;

        /// <summary>
        /// Gets the currently tracked follow target.
        /// </summary>
        public Transform ActiveTarget => activeTarget;

        private void Awake()
        {
            base.Awake();

            ResolveReferences();
        }

        /// <summary>
        /// Finds required camera references if they were not assigned in the Inspector.
        /// </summary>
        private void ResolveReferences()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>();
            }

            if (cameraController == null)
            {
                GameLogger.Warning(
                    nameof(ResolveReferences),
                    this,
                    $"No {nameof(CameraController)} was found."
                );

                return;
            }

            if (mainCamera == null)
            {
                mainCamera = cameraController.GetComponent<Camera>();
            }
        }

        /// <summary>
        /// Enables the Unity Camera component used for rendering gameplay.
        /// </summary>
        public void EnableMainCamera()
        {
            ResolveReferences();

            if (mainCamera != null)
            {
                mainCamera.enabled = true;
            }
        }

        /// <summary>
        /// Disables the Unity Camera component used for rendering gameplay.
        /// </summary>
        public void DisableMainCamera()
        {
            ResolveReferences();

            if (mainCamera != null)
            {
                mainCamera.enabled = false;
            }
        }

        /// <summary>
        /// Changes the transform followed by the active camera.
        /// </summary>
        /// <param name="newTarget">Transform the camera should follow.</param>
        /// <param name="snapImmediately">
        /// Whether the camera should immediately move to its new desired pose.
        /// </param>
        public void SetCameraTarget(
            Transform newTarget,
            bool snapImmediately = false
        )
        {
            if (!HasController())
            {
                return;
            }

            activeTarget = newTarget;
            cameraController.SetTarget(newTarget);

            if (snapImmediately)
            {
                cameraController.SnapToTarget();
            }
        }

        /// <summary>
        /// Changes the camera follow distance.
        /// </summary>
        /// <param name="newDistance">New desired camera distance.</param>
        public void SetCameraDistance(float newDistance)
        {
            if (!HasController())
            {
                return;
            }

            currentDistance = Mathf.Max(0f, newDistance);
            cameraController.SetDistance(currentDistance);
        }

        /// <summary>
        /// Rotates the camera toward a world-space focus target while keeping
        /// its current follow target unchanged.
        /// </summary>
        /// <param name="focusTarget">Transform the camera should look toward.</param>
        /// <param name="snapImmediately">
        /// Whether the camera should immediately rotate to face the target.
        /// </param>
        public void FaceTarget(
            Transform focusTarget,
            bool snapImmediately = false
        )
        {
            if (!HasController() || focusTarget == null)
            {
                return;
            }

            if (snapImmediately)
            {
                cameraController.SnapFaceTarget(focusTarget);
                return;
            }

            cameraController.FaceTarget(focusTarget);
        }

        /// <summary>
        /// Saves the current camera state so it can later be restored with
        /// <see cref="RestoreSavedCameraState"/>.
        /// </summary>
        public void SaveCurrentCameraState()
        {
            savedTarget = activeTarget;
            savedDistance = currentDistance;
        }

        /// <summary>
        /// Saves the current camera state, optionally changes its distance,
        /// and rotates it toward a target.
        /// </summary>
        /// <param name="focusTarget">Transform the camera should face.</param>
        /// <param name="focusDistance">
        /// Distance to use during this focus mode. Pass a negative value to leave
        /// the current distance unchanged.
        /// </param>
        /// <param name="snapImmediately">
        /// Whether the camera should immediately turn toward the focus target.
        /// </param>
        public void EnterFocusMode(
            Transform focusTarget,
            float focusDistance = -1f,
            bool snapImmediately = false
        )
        {
            SaveCurrentCameraState();

            if (focusDistance >= 0f)
            {
                SetCameraDistance(focusDistance);
            }

            FaceTarget(focusTarget, snapImmediately);
        }

        /// <summary>
        /// Restores the target and distance saved before entering focus mode.
        /// </summary>
        /// <param name="snapImmediately">
        /// Whether the camera should immediately move to the restored pose.
        /// </param>
        public void RestoreSavedCameraState(bool snapImmediately = false)
        {
            if (!HasController())
            {
                return;
            }

            SetCameraTarget(savedTarget, false);
            SetCameraDistance(savedDistance);

            if (snapImmediately)
            {
                cameraController.SnapToTarget();
            }

            savedTarget = null;
        }

        /// <summary>
        /// Changes whether the managed controller uses first-person behavior.
        /// </summary>
        public void SetFirstPersonMode(bool enabled)
        {
            if (!HasController())
            {
                return;
            }

            cameraController.SetFirstPersonMode(enabled);
        }

        /// <summary>
        /// Checks that a managed camera controller is available.
        /// </summary>
        private bool HasController()
        {
            ResolveReferences();

            if (cameraController != null)
            {
                return true;
            }

            GameLogger.Warning(
                nameof(CameraManager),
                this,
                $"{nameof(CameraManager)} cannot perform this action because no {nameof(CameraController)} is available."
            );

            return false;
        }
    }
}