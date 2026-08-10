using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Cameras;
using QuietStatic.Toolkit.Utilities;
using UnityEngine;

namespace QuietStatic.Toolkit.Characters.Player
{
    /// <summary>
    /// Applies a cross-scene eating sequence to player-owned movement and held-food visuals.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Characters/Player Eating Sequence Handler")]
    public sealed class PlayerEatingSequenceHandler : MonoBehaviour
    {
        [Header("Sequence")]
        [Tooltip("Eating sequence published by world-scene furniture.")]
        [SerializeField] private EatingSequenceChannel channel;

        [Header("Player")]
        [Tooltip("Controller disabled while the player is seated. Camera look remains available.")]
        [SerializeField] private PlayerController playerController;

        [Tooltip("Camera controller constrained around the activity's optional focus target.")]
        [SerializeField] private CameraController cameraController;

        [Header("Held Food")]
        [Tooltip("Food visual that shrinks as eating progresses.")]
        [SerializeField] private Transform foodVisual;

        [Tooltip("Fraction of the original scale remaining immediately before completion.")]
        [Range(0f, 1f)]
        [SerializeField] private float finalScale = 0.05f;

        [Tooltip("Player-hand state channel used to remove the dish and food after eating.")]
        [SerializeField] private ObjectStateChannel handStateChannel;

        [Tooltip("Empty-hand state selected after the eating meter completes.")]
        [SerializeField] private ObjectStateDefinition emptyPlateState;

        private Vector3 originalFoodScale;
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

            if (foodVisual != null)
            {
                originalFoodScale = foodVisual.localScale;
            }
        }

        private void OnEnable()
        {
            if (channel == null)
            {
                return;
            }

            channel.ContextBegan += HandleBegan;
            channel.ProgressChanged += HandleProgressChanged;
            channel.Completed += HandleCompleted;
            channel.Cancelled += HandleCancelled;
        }

        private void OnDisable()
        {
            if (channel != null)
            {
                channel.ContextBegan -= HandleBegan;
                channel.ProgressChanged -= HandleProgressChanged;
                channel.Completed -= HandleCompleted;
                channel.Cancelled -= HandleCancelled;
            }

            RestoreMovement();
        }

        private void HandleBegan(EatingSequenceContext context)
        {
            if (foodVisual != null)
            {
                foodVisual.localScale = originalFoodScale;
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
            if (foodVisual == null)
            {
                return;
            }

            float scale = Mathf.Lerp(1f, finalScale, Mathf.Clamp01(progress));
            foodVisual.localScale = originalFoodScale * scale;
        }

        private void HandleCompleted()
        {
            if (handStateChannel != null && emptyPlateState != null)
            {
                handStateChannel.ActivateState(emptyPlateState);
            }
            else if (foodVisual != null)
            {
                foodVisual.gameObject.SetActive(false);
            }

            RestoreMovement();
        }

        private void HandleCancelled()
        {
            if (foodVisual != null)
            {
                foodVisual.localScale = originalFoodScale;
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
