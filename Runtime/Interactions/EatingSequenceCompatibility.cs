using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Compatibility alias for the former eating-specific activity context.</summary>
    [Obsolete("Use PlayerActivityContext.")]
    public readonly struct EatingSequenceContext
    {
        private readonly PlayerActivityContext context;

        public EatingSequenceContext(
            Transform playerAnchor,
            Transform cameraFocusTarget,
            float horizontalLookRange,
            float verticalLookRange,
            bool snapCameraToFocus)
        {
            context = new PlayerActivityContext(
                playerAnchor,
                cameraFocusTarget,
                horizontalLookRange,
                verticalLookRange,
                snapCameraToFocus);
        }

        private EatingSequenceContext(PlayerActivityContext value) => context = value;

        public Transform PlayerAnchor => context.PlayerAnchor;
        public Transform CameraFocusTarget => context.CameraFocusTarget;
        public float HorizontalLookRange => context.HorizontalLookRange;
        public float VerticalLookRange => context.VerticalLookRange;
        public bool SnapCameraToFocus => context.SnapCameraToFocus;

        public static implicit operator PlayerActivityContext(EatingSequenceContext value) => value.context;
        public static implicit operator EatingSequenceContext(PlayerActivityContext value) => new(value);
    }

    /// <summary>Compatibility adapter for assets created with the eating-specific channel name.</summary>
    [Obsolete("Use PlayerActivityChannel.")]
    public sealed class EatingSequenceChannel : PlayerActivityChannel { }

    /// <summary>Compatibility adapter for scenes using the former seated sequence component.</summary>
    [Obsolete("Use HoldActivitySequence.")]
    [RequireComponent(typeof(HoldInteractable))]
    public sealed class SeatedHoldSequence : HoldActivitySequence { }
}
