using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Applies one dropdown-selected camera shot from a parameterless UnityEvent callback.
    /// </summary>
    /// <remarks>
    /// Use this focused scene handler when an arbitrary UnityEvent needs to change a cinematic
    /// shot. It avoids serializing a fragile list index into the event argument.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Cinematics/Cutscene Camera Shot Trigger")]
    public sealed class CutsceneCameraShotTrigger : MonoBehaviour
    {
        [Tooltip("Camera director that owns the selectable shots.")]
        [SerializeField] private CinematicCutsceneCameraDirector cameraDirector;

        [Tooltip("Stable camera shot selected from the assigned director.")]
        [CinematicShotId(nameof(cameraDirector))]
        [SerializeField] private string cameraShotId;

        /// <summary>Gets the assigned camera director.</summary>
        public CinematicCutsceneCameraDirector CameraDirector => cameraDirector;

        /// <summary>Gets the stable ID of the selected shot.</summary>
        public string CameraShotId => cameraShotId;

        private void Reset()
        {
            cameraDirector = GetComponent<CinematicCutsceneCameraDirector>();
        }

        /// <summary>Immediately applies the selected camera shot.</summary>
        public void Run()
        {
            if (cameraDirector == null || string.IsNullOrWhiteSpace(cameraShotId))
            {
                GameLogger.Warning(
                    nameof(Run),
                    this,
                    "A camera director and camera shot must be assigned."
                );
                return;
            }

            cameraDirector.CutToShot(cameraShotId);
        }
    }
}
