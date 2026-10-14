using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Shows lightweight environmental flavor text when an Interactable succeeds.</summary>
    /// <remarks>
    /// This component deliberately contains no flags, dialogue nodes, choices, or story state.
    /// Use it for optional observations attached to clutter and environmental props.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Flavor Text Interaction")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Interactable))]
    public sealed class FlavorTextInteraction : MonoBehaviour
    {
        [Tooltip("Cross-scene channel consumed by the persistent interaction UI.")]
        [SerializeField] private InteractionUIChannel channel;

        [Tooltip("Short optional observation shown after inspecting this object.")]
        [TextArea(2, 6)]
        [SerializeField] private string flavorText;

        [Tooltip("Use a custom display duration instead of the UI listener's default.")]
        [SerializeField] private bool useCustomDuration;

        [Tooltip("Seconds the observation remains visible when custom duration is enabled.")]
        [Min(0.01f)]
        [SerializeField] private float messageDuration = 3f;

        private Interactable interaction;

        /// <summary>Gets the authored observation.</summary>
        public string FlavorText => flavorText ?? string.Empty;

        /// <summary>Gets the configured UI channel.</summary>
        public InteractionUIChannel Channel => channel;

        private void Awake() => interaction = GetComponent<Interactable>();

        private void OnEnable()
        {
            if (interaction == null) interaction = GetComponent<Interactable>();
            if (interaction != null) interaction.InteractionSucceeded += HandleInteraction;
        }

        private void OnDisable()
        {
            if (interaction != null) interaction.InteractionSucceeded -= HandleInteraction;
        }

        /// <summary>Shows the current flavor text. Useful for previews and UnityEvents.</summary>
        public void ShowFlavorText()
        {
            if (channel == null || string.IsNullOrWhiteSpace(flavorText)) return;
            if (useCustomDuration) channel.ShowMessageForSeconds(flavorText, messageDuration);
            else channel.ShowMessage(flavorText);
        }

        private void HandleInteraction(Interactable _, Interactor __) => ShowFlavorText();
    }
}
