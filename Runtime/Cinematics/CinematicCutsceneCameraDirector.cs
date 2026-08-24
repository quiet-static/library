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
            [Tooltip("Stable ID used by dropdown-backed shot references. Keep this value unchanged after other components reference it.")]
            /// <summary>Stable scene-local ID used to reference this shot.</summary>
            public string shotId;

            [Tooltip("Friendly name used to identify this shot in the Inspector and debug logs.")]
            /// <summary>Designer-facing label for this shot.</summary>
            public string shotName;

            [Header("Camera Placement")]
            [Tooltip("Optional target the camera should look at. Also used as the origin for Target Relative Offset when no camera marker is assigned.")]
            /// <summary>Optional transform the shot faces and uses as its relative origin.</summary>
            public Transform focusTarget;

            [Tooltip("Optional transform used as the exact camera position for this shot. If assigned, its position overrides Target Relative Offset.")]
            /// <summary>Optional transform supplying the exact camera pose position.</summary>
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
        [Tooltip("Cinematic shots addressable by required stable Shot IDs.")]
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

        /// <summary>Gets the stable ID of the most recently applied shot.</summary>
        /// <value>The current shot ID, or an empty string if no shot is active.</value>
        public string CurrentShotId => GetShotId(currentShotIndex);

        /// <summary>Gets the number of shots configured on this director.</summary>
        public int ShotCount => shots?.Count ?? 0;

        /// <summary>Gets the camera whose lens settings are controlled by this director.</summary>
        public Camera CutsceneCamera => cutsceneCamera != null
            ? cutsceneCamera
            : GetComponent<Camera>();

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
            if (playFirstShotOnStart && ShotCount > 0)
            {
                CutToShot(0);
            }
        }

        /// <summary>Plays a shot by stable ID.</summary>
        /// <param name="shotId">Stable ID configured on the requested shot.</param>
        public void PlayShot(string shotId)
        {
            CutToShot(shotId);
        }

        /// <summary>Immediately moves and rotates the cutscene camera to a shot by stable ID.</summary>
        /// <param name="shotId">Stable ID configured on the requested shot.</param>
        public void CutToShot(string shotId)
        {
            if (!TryGetShotIndex(shotId, out int shotIndex))
            {
                GameLogger.Warning(
                    nameof(CutToShot),
                    this,
                    $"Unknown cinematic shot ID: '{shotId}'"
                );
                return;
            }

            CutToShot(shotIndex);
        }

        /// <summary>
        /// Immediately moves and rotates the cutscene camera to the requested shot.
        /// </summary>
        /// <param name="shotIndex">Index of the shot to apply from the configured shot list.</param>
        public void CutToShot(int shotIndex)
        {
            if (!TryApplyShot(shotIndex, true))
            {
                return;
            }

            currentShotIndex = shotIndex;
            CinematicShot shot = shots[shotIndex];

            GameLogger.Log(
                "CutToShot",
                this,
                $"Cut to cinematic shot '{GetShotId(shotIndex)}' ({shot.shotName})"
            );
        }

        /// <summary>
        /// Applies a shot pose for editor preview without changing the current runtime shot.
        /// </summary>
        /// <param name="shotId">Stable ID configured on the requested shot.</param>
        /// <returns>True when the shot was found, usable, and applied.</returns>
        public bool PreviewShot(string shotId)
        {
            if (!TryGetShotIndex(shotId, out int shotIndex))
            {
                return false;
            }

            return PreviewShot(shotIndex);
        }

        /// <summary>
        /// Applies a shot pose for editor preview without changing the current runtime shot.
        /// </summary>
        /// <param name="shotIndex">Zero-based index into the configured shot list.</param>
        /// <returns>True when the shot is usable and was applied.</returns>
        public bool PreviewShot(int shotIndex)
        {
            return TryApplyShot(shotIndex, false);
        }

        /// <summary>Checks whether a configured shot has enough data to be applied.</summary>
        /// <param name="shotIndex">Zero-based index into the configured shot list.</param>
        /// <returns>True when the shot exists and has a focus target or camera marker.</returns>
        public bool IsShotUsable(int shotIndex)
        {
            return IsValidShotIndex(shotIndex) &&
                   IsUsableShot(shots[shotIndex], false);
        }

        /// <summary>Gets the explicit stable ID assigned to a configured shot.</summary>
        /// <param name="shotIndex">Zero-based index into the configured shot list.</param>
        /// <returns>The trimmed Shot ID, or an empty string when none is assigned.</returns>
        public string GetExplicitShotId(int shotIndex)
        {
            if (!IsValidShotIndex(shotIndex) || shots[shotIndex] == null)
            {
                return string.Empty;
            }

            return shots[shotIndex].shotId?.Trim() ?? string.Empty;
        }

        /// <summary>Gets the stable ID used to reference a configured shot.</summary>
        /// <param name="shotIndex">Zero-based index into the configured shot list.</param>
        /// <returns>
        /// The explicit Shot ID, or an empty string when the index or entry is invalid.
        /// </returns>
        public string GetShotId(int shotIndex)
        {
            if (!IsValidShotIndex(shotIndex))
            {
                return string.Empty;
            }

            return GetExplicitShotId(shotIndex);
        }

        /// <summary>Gets a designer-facing label for a configured shot.</summary>
        /// <param name="shotIndex">Zero-based index into the configured shot list.</param>
        /// <returns>A friendly shot name with a deterministic fallback.</returns>
        public string GetShotDisplayName(int shotIndex)
        {
            if (!IsValidShotIndex(shotIndex) || shots[shotIndex] == null)
            {
                return $"Shot {shotIndex + 1}";
            }

            CinematicShot shot = shots[shotIndex];
            if (!string.IsNullOrWhiteSpace(shot.shotName))
            {
                return shot.shotName.Trim();
            }

            string id = GetShotId(shotIndex);
            return !string.IsNullOrEmpty(id) ? id : $"Shot {shotIndex + 1}";
        }

        /// <summary>Resolves a stable shot ID to its current list index.</summary>
        /// <param name="shotId">Stable ID to find.</param>
        /// <param name="shotIndex">Receives the matching list index, or -1 when not found.</param>
        /// <returns>True when a matching shot exists.</returns>
        public bool TryGetShotIndex(string shotId, out int shotIndex)
        {
            shotIndex = -1;
            if (string.IsNullOrWhiteSpace(shotId))
            {
                return false;
            }

            string requestedId = shotId.Trim();
            for (int index = 0; index < ShotCount; index++)
            {
                if (string.Equals(
                        GetExplicitShotId(index),
                        requestedId,
                        StringComparison.Ordinal))
                {
                    shotIndex = index;
                    return true;
                }
            }

            return false;
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

            Camera camera = CutsceneCamera;
            if (camera != null && shot.changeFieldOfView)
            {
                camera.fieldOfView = shot.fieldOfView;
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
        private bool IsUsableShot(CinematicShot shot, bool logWarning = true)
        {
            if (shot == null)
            {
                if (logWarning)
                {
                    GameLogger.Warning(
                        "IsUsableShot",
                        this,
                        "Cinematic shot is null."
                    );
                }
                return false;
            }

            if (shot.focusTarget == null && shot.cameraPositionMarker == null)
            {
                if (logWarning)
                {
                    GameLogger.Warning(
                        "IsUsableShot",
                        this,
                        $"Cinematic shot '{shot.shotName}' has no focus target or camera position marker."
                    );
                }

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
            return shotIndex >= 0 && shotIndex < ShotCount;
        }

        private bool TryApplyShot(int shotIndex, bool logWarning)
        {
            if (!IsValidShotIndex(shotIndex))
            {
                if (logWarning)
                {
                    GameLogger.Warning(
                        "CutToShot",
                        this,
                        $"Invalid cinematic shot index: {shotIndex}"
                    );
                }
                return false;
            }

            CinematicShot shot = shots[shotIndex];
            if (!IsUsableShot(shot, logWarning))
            {
                return false;
            }

            ApplyShot(shot);
            return true;
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
            CutsceneCameraIdle currentIdleMotion = idleMotion != null
                ? idleMotion
                : GetComponent<CutsceneCameraIdle>();
            if (currentIdleMotion != null)
            {
                currentIdleMotion.RefreshBaseTransform();
            }
        }
    }
}
