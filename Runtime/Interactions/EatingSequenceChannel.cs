using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Context used to position the player and constrain camera look while eating.</summary>
    public readonly struct EatingSequenceContext
    {
        public EatingSequenceContext(
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
    /// Carries a seated eating sequence from a world-scene interaction to the
    /// persistent player scene without either scene directly referencing the other.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EatingSequenceChannel",
        menuName = "Quiet Static Toolkit/Interactions/Eating Sequence Channel")]
    public sealed class EatingSequenceChannel : ScriptableObject
    {
        /// <summary>
        /// Raised when the player enters the seated eating state. The argument is the
        /// world-space anchor where the player should be positioned, or null when no
        /// repositioning is requested.
        /// </summary>
        public event Action<Transform> Began;

        /// <summary>Raised with the complete seated position and camera configuration.</summary>
        public event Action<EatingSequenceContext> ContextBegan;

        /// <summary>Raised with normalized eating progress.</summary>
        public event Action<float> ProgressChanged;

        /// <summary>Raised when all food has been consumed.</summary>
        public event Action Completed;

        /// <summary>Raised if the seated eating state ends without completion.</summary>
        public event Action Cancelled;

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
            var context = new EatingSequenceContext(
                playerAnchor,
                cameraFocusTarget,
                horizontalLookRange,
                verticalLookRange,
                snapCameraToFocus);
            Began?.Invoke(playerAnchor);
            ContextBegan?.Invoke(context);
        }

        /// <summary>Publishes normalized consumption progress.</summary>
        public void ReportProgress(float progress) =>
            ProgressChanged?.Invoke(Mathf.Clamp01(progress));

        /// <summary>Completes the seated eating state.</summary>
        public void Complete() => Completed?.Invoke();

        /// <summary>Cancels the seated eating state.</summary>
        public void Cancel() => Cancelled?.Invoke();
    }
}
