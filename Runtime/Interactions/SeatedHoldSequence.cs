using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Adapts a world-space hold interaction into a cross-scene seated sequence.
    /// The couch owns when eating is available; the player scene owns its effects.
    /// </summary>
    [RequireComponent(typeof(HoldInteractable))]
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Seated Hold Sequence")]
    public sealed class SeatedHoldSequence : MonoBehaviour
    {
        [Tooltip("Hold interaction enabled after the player sits. Defaults to this object.")]
        [SerializeField] private HoldInteractable holdInteractable;

        [Tooltip("Channel received by the persistent player-scene eating handler.")]
        [SerializeField] private EatingSequenceChannel channel;

        [Tooltip("World-space position and facing direction used while the player is seated.")]
        [SerializeField] private Transform playerAnchor;

        [Header("Input")]
        [Tooltip("Require the player to keep this hold under the crosshair. Disable this to read held interaction input for the entire seated sequence without needing a collider target.")]
        [SerializeField] private bool requireColliderFocus = true;

        [Tooltip("UI channel used for the prompt and progress meter when collider focus is not required.")]
        [SerializeField] private InteractionUIChannel interactionUIChannel;

        [Header("Camera Focus")]
        [Tooltip("Optional object the seated camera initially faces, such as a television.")]
        [SerializeField] private Transform cameraFocusTarget;

        [Tooltip("Maximum horizontal look angle to either side of the focus direction.")]
        [Range(0f, 180f)]
        [SerializeField] private float horizontalLookRange = 20f;

        [Tooltip("Maximum vertical look angle above or below the focus direction.")]
        [Range(0f, 89f)]
        [SerializeField] private float verticalLookRange = 12f;

        [Tooltip("Immediately face the focus target when sitting instead of rotating from the current view.")]
        [SerializeField] private bool snapCameraToFocus = true;

        private bool isSeated;
        private bool isDirectHoldActive;

        /// <summary>
        /// Gets whether eating must remain under the crosshair. When false, the
        /// seated sequence owns hold input and its collider is optional.
        /// </summary>
        public bool RequiresColliderFocus => requireColliderFocus;

        private void Reset()
        {
            holdInteractable = GetComponent<HoldInteractable>();
            playerAnchor = transform.Find("Player");
        }

        private void Awake()
        {
            if (holdInteractable == null)
            {
                holdInteractable = GetComponent<HoldInteractable>();
            }

            if (playerAnchor == null)
            {
                playerAnchor = transform.Find("Player");
            }
        }

        private void OnEnable()
        {
            holdInteractable.ProgressChanged += HandleProgressChanged;
            holdInteractable.Completed += HandleCompleted;
        }

        private void OnDisable()
        {
            holdInteractable.ProgressChanged -= HandleProgressChanged;
            holdInteractable.Completed -= HandleCompleted;

            if (isSeated)
            {
                channel?.Cancel();
                isSeated = false;
            }

            holdInteractable.SetInteractorTargetingEnabled(true);
            StopDirectHold(false);
        }

        private void Update()
        {
            if (!isSeated || requireColliderFocus)
            {
                return;
            }

            bool isHeld = GameInputManager.Instance != null &&
                GameInputManager.Instance.InteractHeld;

            if (!isHeld)
            {
                if (isDirectHoldActive)
                {
                    StopDirectHold(true);
                }

                return;
            }

            if (!isDirectHoldActive)
            {
                isDirectHoldActive = true;
                interactionUIChannel?.HidePrompt();
            }

            if (!holdInteractable.Advance(Time.deltaTime))
            {
                StopDirectHold(true);
                return;
            }

            if (holdInteractable.IsComplete)
            {
                StopDirectHold(false);
            }
        }

        private void LateUpdate()
        {
            if (!isSeated || requireColliderFocus)
            {
                return;
            }

            if (isDirectHoldActive)
            {
                interactionUIChannel?.HidePrompt();
                interactionUIChannel?.ShowProgress(
                    holdInteractable.ProgressName,
                    holdInteractable.Progress);
            }
            else
            {
                interactionUIChannel?.HideProgress();
                interactionUIChannel?.ShowPrompt(holdInteractable.HoverPrompt);
            }
        }

        /// <summary>Locks the player into the seated activity and enables eating.</summary>
        public void BeginSitting()
        {
            if (isSeated)
            {
                return;
            }

            isSeated = true;
            holdInteractable.ResetProgress();
            holdInteractable.SetEnabled(true);
            holdInteractable.SetInteractorTargetingEnabled(requireColliderFocus);
            channel?.Begin(
                playerAnchor,
                cameraFocusTarget,
                horizontalLookRange,
                verticalLookRange,
                snapCameraToFocus);

            if (!requireColliderFocus)
            {
                interactionUIChannel?.ShowPrompt(holdInteractable.HoverPrompt);
            }
        }

        /// <summary>Ends sitting early and restores player control.</summary>
        public void CancelSitting()
        {
            if (!isSeated)
            {
                return;
            }

            isSeated = false;
            holdInteractable.SetEnabled(false);
            holdInteractable.Cancel();
            holdInteractable.SetInteractorTargetingEnabled(true);
            StopDirectHold(false);
            channel?.Cancel();
        }

        private void HandleProgressChanged(float progress) =>
            channel?.ReportProgress(progress);

        private void HandleCompleted()
        {
            isSeated = false;
            holdInteractable.SetInteractorTargetingEnabled(true);
            StopDirectHold(false);
            channel?.Complete();
        }

        private void StopDirectHold(bool restorePrompt)
        {
            if (requireColliderFocus && !isDirectHoldActive)
            {
                return;
            }

            if (isDirectHoldActive)
            {
                holdInteractable.Cancel();
                isDirectHoldActive = false;
            }

            interactionUIChannel?.HideProgress();

            if (restorePrompt && isSeated && !requireColliderFocus)
            {
                interactionUIChannel?.ShowPrompt(holdInteractable.HoverPrompt);
            }
            else
            {
                interactionUIChannel?.HidePrompt();
            }
        }
    }
}
