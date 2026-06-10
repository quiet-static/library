using System.Collections;
using UnityEngine;

namespace QuietStatic.Toolkit.CameraTools
{
    /// <summary>
    /// Moves a camera transform between predefined camera poses.
    /// </summary>
    /// <remarks>
    /// This component is useful for cutscenes, static camera transitions, inspection views,
    /// jumpscare setups, or any moment where the active camera needs to snap or smoothly move
    /// to a specific transform in the scene.
    ///
    /// A camera pose is represented by a normal <see cref="Transform"/>. The target camera will
    /// copy that transform's world position and world rotation either instantly through
    /// <see cref="CutTo"/> or over time through <see cref="MoveTo"/>.
    /// </remarks>
    public class CameraPoseDirector : MonoBehaviour
    {
        [Header("Camera Reference")]
        [Tooltip("The camera transform that will be moved by this director. If left empty, Reset will try to assign the main camera.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Movement Settings")]
        [Tooltip("How long, in seconds, the camera should take to smoothly move to a new pose when using MoveTo.")]
        [Min(0f)]
        [SerializeField] private float moveDuration = 0.5f;

        /// <summary>
        /// The currently running smooth camera movement coroutine.
        /// </summary>
        /// <remarks>
        /// This is tracked so a new move can cancel the previous move instead of allowing multiple
        /// routines to fight over the same camera transform.
        /// </remarks>
        private Coroutine moveRoutine;

        /// <summary>
        /// Attempts to automatically assign the scene's main camera when this component is added
        /// or reset in the Unity Inspector.
        /// </summary>
        /// <remarks>
        /// This only runs in editor-driven reset situations. Runtime setup is still validated by
        /// the public methods, which safely return if no camera transform has been assigned.
        /// </remarks>
        private void Reset()
        {
            if (UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }
        }

        /// <summary>
        /// Instantly snaps the controlled camera to the supplied pose.
        /// </summary>
        /// <param name="pose">
        /// The transform whose world position and world rotation should be copied by the camera.
        /// </param>
        /// <remarks>
        /// Use this for hard camera cuts, such as switching to a new cutscene angle immediately.
        /// If either the controlled camera or the supplied pose is missing, the method safely does nothing.
        /// </remarks>
        public void CutTo(Transform pose)
        {
            if (cameraTransform == null || pose == null)
            {
                return;
            }

            cameraTransform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>
        /// Smoothly moves the controlled camera to the supplied pose over <see cref="moveDuration"/> seconds.
        /// </summary>
        /// <param name="pose">
        /// The transform whose world position and world rotation should become the camera's final pose.
        /// </param>
        /// <remarks>
        /// If another move is already in progress, it is stopped before the new move begins.
        /// This prevents overlapping camera movement coroutines from competing for control.
        /// If either the controlled camera or the supplied pose is missing, the method safely does nothing.
        /// </remarks>
        public void MoveTo(Transform pose)
        {
            if (cameraTransform == null || pose == null)
            {
                return;
            }

            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
            }

            moveRoutine = StartCoroutine(MoveRoutine(pose));
        }

        /// <summary>
        /// Interpolates the controlled camera from its current position and rotation to the target pose.
        /// </summary>
        /// <param name="pose">
        /// The transform that provides the desired final camera position and rotation.
        /// </param>
        /// <returns>
        /// An enumerator used by Unity's coroutine system to spread the movement across multiple frames.
        /// </returns>
        /// <remarks>
        /// The movement uses <see cref="Mathf.SmoothStep(float, float, float)"/> for eased timing,
        /// <see cref="Vector3.Lerp(Vector3, Vector3, float)"/> for position, and
        /// <see cref="Quaternion.Slerp(Quaternion, Quaternion, float)"/> for rotation.
        /// Once complete, the camera is set exactly to the target pose to avoid small interpolation offsets.
        /// </remarks>
        private IEnumerator MoveRoutine(Transform pose)
        {
            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;

                float t = moveDuration <= 0f
                    ? 1f
                    : Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);

                cameraTransform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, pose.position, t),
                    Quaternion.Slerp(startRotation, pose.rotation, t)
                );

                yield return null;
            }

            cameraTransform.SetPositionAndRotation(pose.position, pose.rotation);
            moveRoutine = null;
        }
    }
}
