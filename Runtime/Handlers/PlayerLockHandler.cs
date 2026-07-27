using QuietStatic.Toolkit.Cameras;
using UnityEngine;

namespace QuietStatic.Toolkit.Characters.Player
{
    /// <summary>
    /// UnityEvent-facing bridge that locks or restores player movement and camera look together.
    /// </summary>
    /// <remarks>
    /// Place this in the persistent Player scene and invoke it from dialogue or cutscene lifecycle
    /// events. Assign both references explicitly so locking remains deterministic across scenes.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Handlers/Dialogue Player Lock Handler")]
    public class DialoguePlayerLockHandler : MonoBehaviour
    {
        [Tooltip("Player controller whose movement input is enabled or disabled.")]
        [SerializeField] private PlayerController playerController;
        [Tooltip("Camera controller whose look input is enabled or disabled.")]
        [SerializeField] private CameraController cameraController;

        /// <summary>Disables player movement and camera look.</summary>
        public void LockPlayer()
        {
            playerController.SetMovementEnabled(false);
            cameraController.SetLookEnabled(false);
        }

        /// <summary>Restores player movement and camera look.</summary>
        public void UnlockPlayer()
        {
            playerController.SetMovementEnabled(true);
            cameraController.SetLookEnabled(true);
        }
    }
}
