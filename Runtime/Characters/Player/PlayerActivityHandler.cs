using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Cameras;
using QuietStatic.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace QuietStatic.Toolkit.Characters.Player
{
    /// <summary>
    /// Applies a cross-scene activity to player-owned movement, camera, and optional progress visuals.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Characters/Player Activity Handler")]
    public class PlayerActivityHandler : MonoBehaviour
    {
        [Header("Sequence")]
        [Tooltip("Activity lifecycle published by a world-scene interaction.")]
        [FormerlySerializedAs("channel")]
        [SerializeField] private PlayerActivityChannel activityChannel;

        [Header("Player")]
        [Tooltip("Controller disabled while the player is seated. Camera look remains available.")]
        [SerializeField] private PlayerController playerController;

        [Tooltip("Camera controller constrained around the activity's optional focus target.")]
        [SerializeField] private CameraController cameraController;

        [Header("Progress Visual")]
        [Tooltip("Optional visual scaled down as activity progress increases.")]
        [FormerlySerializedAs("foodVisual")]
        [SerializeField] private Transform progressVisual;

        [Tooltip("Fraction of the original scale remaining immediately before completion.")]
        [Range(0f, 1f)]
        [SerializeField] private float finalScale = 0.05f;

        [Tooltip("Optional state channel updated when the activity completes.")]
        [FormerlySerializedAs("handStateChannel")]
        [SerializeField] private ObjectStateChannel completionStateChannel;

        [Tooltip("State selected after the activity completes.")]
        [FormerlySerializedAs("emptyPlateState")]
        [SerializeField] private ObjectStateDefinition completedState;

        private Vector3 originalVisualScale;
        private bool ownsMovementLock;
        private bool ownsLookConstraint;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (cameraController == null)
            {
                cameraController = GetComponentInChildren<CameraController>(true);
            }

            if (progressVisual != null)
            {
                originalVisualScale = progressVisual.localScale;
            }
        }

        private void OnEnable()
        {
            if (activityChannel == null)
            {
                return;
            }

            activityChannel.ContextBegan += HandleBegan;
            activityChannel.ProgressChanged += HandleProgressChanged;
            activityChannel.Completed += HandleCompleted;
            activityChannel.Cancelled += HandleCancelled;
        }

        private void OnDisable()
        {
            if (activityChannel != null)
            {
                activityChannel.ContextBegan -= HandleBegan;
                activityChannel.ProgressChanged -= HandleProgressChanged;
                activityChannel.Completed -= HandleCompleted;
                activityChannel.Cancelled -= HandleCancelled;
            }

            RestoreMovement();
        }

        private void HandleBegan(PlayerActivityContext context)
        {
            if (progressVisual != null)
            {
                progressVisual.localScale = originalVisualScale;
            }

            playerController?.SetMovementEnabled(false);
            ownsMovementLock = playerController != null;

            if (playerController != null && context.PlayerAnchor != null)
            {
                SnapPlayerToAnchor(context.PlayerAnchor);
            }

            if (cameraController != null && context.CameraFocusTarget != null)
            {
                cameraController.BeginLookConstraint(
                    context.CameraFocusTarget,
                    context.HorizontalLookRange,
                    context.VerticalLookRange,
                    context.SnapCameraToFocus);
                ownsLookConstraint = true;
            }
        }

        private void SnapPlayerToAnchor(Transform playerAnchor)
        {
            Transform playerTransform = playerController.transform;
            CharacterController characterController =
                playerController.GetComponent<CharacterController>();
            bool restoreCharacterController =
                characterController != null && characterController.enabled;

            if (restoreCharacterController)
            {
                characterController.enabled = false;
            }

            playerTransform.SetPositionAndRotation(
                playerAnchor.position,
                playerAnchor.rotation);

            if (restoreCharacterController)
            {
                characterController.enabled = true;
            }

            Physics.SyncTransforms();
        }

        private void HandleProgressChanged(float progress)
        {
            if (progressVisual == null)
            {
                return;
            }

            float scale = Mathf.Lerp(1f, finalScale, Mathf.Clamp01(progress));
            progressVisual.localScale = originalVisualScale * scale;
        }

        private void HandleCompleted()
        {
            if (completionStateChannel != null && completedState != null)
            {
                completionStateChannel.ActivateState(completedState);
            }
            else if (progressVisual != null)
            {
                progressVisual.gameObject.SetActive(false);
            }

            RestoreMovement();
        }

        private void HandleCancelled()
        {
            if (progressVisual != null)
            {
                progressVisual.localScale = originalVisualScale;
            }

            RestoreMovement();
        }

        private void RestoreMovement()
        {
            if (ownsLookConstraint)
            {
                cameraController?.EndLookConstraint();
                ownsLookConstraint = false;
            }

            if (!ownsMovementLock)
            {
                return;
            }

            playerController.SetMovementEnabled(true);
            ownsMovementLock = false;
        }
    }
}
