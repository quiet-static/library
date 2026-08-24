using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Context used to position the player and constrain camera look during an activity.</summary>
    public readonly struct PlayerActivityContext
    {
        public PlayerActivityContext(
            Transform playerAnchor,
            Transform cameraFocusTarget,
            float horizontalLookRange,
            float verticalLookRange,
            bool snapCameraToFocus)
        {
            PlayerAnchor = playerAnchor;
            CameraFocusTarget = cameraFocusTarget;
            HorizontalLookRange = Mathf.Max(0f, horizontalLookRange);
            VerticalLookRange = Mathf.Max(0f, verticalLookRange);
            SnapCameraToFocus = snapCameraToFocus;
        }

        public Transform PlayerAnchor { get; }
        public Transform CameraFocusTarget { get; }
        public float HorizontalLookRange { get; }
        public float VerticalLookRange { get; }
        public bool SnapCameraToFocus { get; }
    }

    /// <summary>
    /// Carries a seated progress activity from a world-scene interaction to the
    /// persistent player scene without either scene directly referencing the other.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerActivityChannel",
        menuName = "Quiet Static Toolkit/Interactions/Player Activity Channel")]
    public class PlayerActivityChannel : ScriptableObject
    {
        /// <summary>Raised with the complete seated position and camera configuration.</summary>
        public event Action<PlayerActivityContext> ContextBegan;

        /// <summary>Raised with normalized activity progress.</summary>
        public event Action<float> ProgressChanged;

        /// <summary>Raised when the activity is complete.</summary>
        public event Action Completed;

        /// <summary>Raised if the activity ends without completion.</summary>
        public event Action Cancelled;

        /// <summary>Gets the interaction-button state published by the persistent player service.</summary>
        public bool InteractHeld { get; private set; }

        /// <summary>Publishes the current interaction-button state for the active world activity.</summary>
        public void SetInteractHeld(bool interactHeld) => InteractHeld = interactHeld;

        /// <summary>Begins the seated eating state without repositioning the player.</summary>
        public void Begin() => Begin(null);

        /// <summary>Begins the seated eating state at a world-space player anchor.</summary>
        /// <param name="playerAnchor">Position and facing direction used by the player.</param>
        public void Begin(Transform playerAnchor) => Begin(
            playerAnchor,
            null,
            0f,
            0f,
            false);

        /// <summary>Begins eating with an optional constrained camera focus.</summary>
        public void Begin(
            Transform playerAnchor,
            Transform cameraFocusTarget,
            float horizontalLookRange,
            float verticalLookRange,
            bool snapCameraToFocus = true)
        {
            var context = new PlayerActivityContext(
                playerAnchor,
                cameraFocusTarget,
                horizontalLookRange,
                verticalLookRange,
                snapCameraToFocus);
            ContextBegan?.Invoke(context);
        }

        /// <summary>Publishes normalized consumption progress.</summary>
        public void ReportProgress(float progress) =>
            ProgressChanged?.Invoke(Mathf.Clamp01(progress));

        /// <summary>Completes the seated eating state.</summary>
        public void Complete() => Completed?.Invoke();

        /// <summary>Cancels the seated eating state.</summary>
        public void Cancel()
        {
            InteractHeld = false;
            Cancelled?.Invoke();
        }
    }
}
