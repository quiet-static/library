using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Directs a dedicated cinematic cutscene camera by jumping between predefined camera shots.
    /// </summary>
    /// <remarks>
    /// This component is intentionally small and timing-agnostic. It does not advance dialogue,
    /// run fades, wait between shots, or decide when a cutscene starts or ends.
    /// Other systems, such as a cutscene sequence runner or UnityEvents, should call
    /// <see cref="CutToShot"/>, <see cref="PlayShot"/>, <see cref="NextShot"/>, or
    /// <see cref="PreviousShot"/> when the camera needs to change.
    ///
    /// Each shot can either use an exact camera marker transform or calculate a camera
    /// position relative to a focus target. If a focus target is assigned, the director
    /// rotates the camera to look at that target plus the configured look-at offset.
    /// </remarks>
    public class CinematicCutsceneCameraDirector : MonoBehaviour
    {
        /// <summary>
        /// Defines one reusable cinematic camera shot.
        /// </summary>
        /// <remarks>
        /// A shot can be configured in two main ways:
        /// - Assign <see cref="cameraPositionMarker"/> to place the camera exactly at a marker.
        /// - Leave <see cref="cameraPositionMarker"/> empty and assign <see cref="focusTarget"/>
        ///   to calculate the camera position from <see cref="targetRelativeOffset"/>.
        ///
        /// If <see cref="focusTarget"/> is assigned, the camera will rotate to look at the target
        /// plus <see cref="lookAtOffset"/>. If no focus target is assigned but a camera marker is,
        /// the camera will use the marker's rotation.
        /// </remarks>
        [Serializable]
        public class CinematicShot
        {
            [Header("Shot Identity")]
            [Tooltip("Friendly name used to identify this shot in the Inspector and debug logs.")]
            public string shotName;

            [Header("Camera Placement")]
            [Tooltip("Optional target the camera should look at. Also used as the origin for Target Relative Offset when no camera marker is assigned.")]
            public Transform focusTarget;

            [Tooltip("Optional transform used as the exact camera position for this shot. If assigned, its position overrides Target Relative Offset.")]
            public Transform cameraPositionMarker;

            [Tooltip("Camera offset relative to the Focus Target. Used only when Camera Position Marker is not assigned.")]
            public Vector3 targetRelativeOffset = new Vector3(0f, 1.5f, -4f);

            [Tooltip("Extra world-space offset added to the Focus Target position when calculating the point the camera should look at.")]
            public Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);

            [Header("Lens")]
            [Tooltip("If true, this shot will override the cutscene camera's field of view.")]
            public bool changeFieldOfView = false;

            [Tooltip("Field of view to apply when Change Field Of View is enabled.")]
            [Range(10f, 100f)]
            public float fieldOfView = 45f;
        }

        [Header("Dependencies")]
        [Tooltip("Camera controlled by this director. If left empty, the script will try to find a Camera on this GameObject.")]
        [SerializeField] private Camera cutsceneCamera;

        [Tooltip("Optional idle-motion component on the same camera. Its base transform is refreshed after the director moves the camera.")]
        [SerializeField] private CutsceneCameraIdle idleMotion;

        [Header("Shots")]
        [Tooltip("Ordered list of cinematic shots. Other scripts reference shots by their index in this list.")]
        [SerializeField] private List<CinematicShot> shots = new List<CinematicShot>();

        [Header("Startup")]
        [Tooltip("If true, the director immediately cuts to the first shot when the scene starts.")]
        [SerializeField] private bool playFirstShotOnStart = false;

        /// <summary>
        /// Index of the currently active shot.
        /// </summary>
        /// <remarks>
        /// A value of -1 means no shot has been applied yet.
        /// </remarks>
        private int currentShotIndex = -1;

        /// <summary>
        /// Gets the index of the most recently applied shot.
        /// </summary>
        /// <value>
        /// The current shot index, or -1 if no shot has been applied yet.
        /// </value>
        public int CurrentShotIndex => currentShotIndex;

        /// <summary>
        /// Attempts to auto-fill camera-related dependencies when the component is added
        /// or reset in the Unity Inspector.
        /// </summary>
        private void Reset()
        {
            cutsceneCamera = GetComponent<Camera>();
            idleMotion = GetComponent<CutsceneCameraIdle>();
        }

        /// <summary>
        /// Ensures required local references are available before other scripts call into this director.
        /// </summary>
        private void Awake()
        {
            if (cutsceneCamera == null)
            {
                cutsceneCamera = GetComponent<Camera>();
            }

            if (idleMotion == null)
            {
                idleMotion = GetComponent<CutsceneCameraIdle>();
            }
        }

        /// <summary>
        /// Optionally applies the first configured shot when the scene starts.
        /// </summary>
        private void Start()
        {
            if (playFirstShotOnStart && shots.Count > 0)
            {
                CutToShot(0);
            }
        }

        /// <summary>
        /// Plays a shot by index.
        /// </summary>
        /// <remarks>
        /// This method is kept for compatibility with older cutscene sequence code.
        /// It currently behaves the same as <see cref="CutToShot"/>.
        /// </remarks>
        /// <param name="shotIndex">Index of the shot to apply from the configured shot list.</param>
        public void PlayShot(int shotIndex)
        {
            CutToShot(shotIndex);
        }

        /// <summary>
        /// Immediately moves and rotates the cutscene camera to the requested shot.
        /// </summary>
        /// <param name="shotIndex">Index of the shot to apply from the configured shot list.</param>
        public void CutToShot(int shotIndex)
        {
            if (!IsValidShotIndex(shotIndex))
            {
                GameLogger.Warning(
                    "CutToShot",
                    this,
                    $"Invalid cinematic shot index: {shotIndex}"
                );
                return;
            }

            CinematicShot shot = shots[shotIndex];

            if (!IsUsableShot(shot))
            {
                return;
            }

            currentShotIndex = shotIndex;
            ApplyShot(shot);

            GameLogger.Log(
                "CutToShot",
                this,
                $"Cut to cinematic shot {shotIndex}: {shot.shotName}"
            );
        }

        /// <summary>
        /// Cuts to the shot immediately after the current shot.
        /// </summary>
        /// <remarks>
        /// If no shot has been applied yet, this will attempt to cut to shot index 0.
        /// If the next index is outside the shot list, the request is ignored with a warning.
        /// </remarks>
        public void NextShot()
        {
            CutToShot(currentShotIndex + 1);
        }

        /// <summary>
        /// Cuts to the shot immediately before the current shot.
        /// </summary>
        /// <remarks>
        /// If the previous index is outside the shot list, the request is ignored with a warning.
        /// </remarks>
        public void PreviousShot()
        {
            CutToShot(currentShotIndex - 1);
        }

        /// <summary>
        /// Applies a validated shot to the director's transform and optional camera lens settings.
        /// </summary>
        /// <param name="shot">Shot data to apply.</param>
        private void ApplyShot(CinematicShot shot)
        {
            Vector3 targetPosition = GetShotCameraPosition(shot);
            Quaternion targetRotation = GetShotCameraRotation(shot, targetPosition);

            transform.position = targetPosition;
            transform.rotation = targetRotation;

            if (cutsceneCamera != null && shot.changeFieldOfView)
            {
                cutsceneCamera.fieldOfView = shot.fieldOfView;
            }

            RefreshIdleMotionBase();
        }

        /// <summary>
        /// Checks whether a shot has enough information to place or aim the camera.
        /// </summary>
        /// <param name="shot">Shot to validate.</param>
        /// <returns>
        /// True if the shot can be used; otherwise, false.
        /// </returns>
        private bool IsUsableShot(CinematicShot shot)
        {
            if (shot == null)
            {
                GameLogger.Warning(
                    "IsUsableShot",
                    this,
                    "Cinematic shot is null."
                );
                return false;
            }

            if (shot.focusTarget == null && shot.cameraPositionMarker == null)
            {
                GameLogger.Warning(
                    "IsUsableShot",
                    this,
                    $"Cinematic shot '{shot.shotName}' has no focus target or camera position marker."
                );

                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates the world-space camera position for a shot.
        /// </summary>
        /// <param name="shot">Shot whose position should be resolved.</param>
        /// <returns>
        /// The camera marker position when one is assigned; otherwise, the focus target
        /// position transformed by the shot's relative offset.
        /// </returns>
        private Vector3 GetShotCameraPosition(CinematicShot shot)
        {
            if (shot.cameraPositionMarker != null)
            {
                return shot.cameraPositionMarker.position;
            }

            return shot.focusTarget.TransformPoint(shot.targetRelativeOffset);
        }

        /// <summary>
        /// Calculates the world-space camera rotation for a shot.
        /// </summary>
        /// <param name="shot">Shot whose rotation should be resolved.</param>
        /// <param name="cameraPosition">Resolved camera position for this shot.</param>
        /// <returns>
        /// A rotation looking toward the focus target when available, the camera marker's
        /// rotation when only a marker is available, or the current transform rotation
        /// if no safe look direction can be calculated.
        /// </returns>
        private Quaternion GetShotCameraRotation(CinematicShot shot, Vector3 cameraPosition)
        {
            if (shot.focusTarget == null)
            {
                if (shot.cameraPositionMarker != null)
                {
                    return shot.cameraPositionMarker.rotation;
                }

                return transform.rotation;
            }

            Vector3 lookPoint = shot.focusTarget.position + shot.lookAtOffset;
            Vector3 lookDirection = lookPoint - cameraPosition;

            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return transform.rotation;
            }

            return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        /// <summary>
        /// Checks whether a shot index exists in the configured shot list.
        /// </summary>
        /// <param name="shotIndex">Index to validate.</param>
        /// <returns>
        /// True if the index is inside the shot list; otherwise, false.
        /// </returns>
        private bool IsValidShotIndex(int shotIndex)
        {
            return shotIndex >= 0 && shotIndex < shots.Count;
        }

        /// <summary>
        /// Refreshes the optional idle-motion component after the camera has been repositioned.
        /// </summary>
        /// <remarks>
        /// This prevents idle bobbing or sway scripts from pulling the camera back toward an old
        /// base transform after the director cuts to a new shot.
        /// </remarks>
        private void RefreshIdleMotionBase()
        {
            if (idleMotion != null)
            {
                idleMotion.RefreshBaseTransform();
            }
        }
    }
}
