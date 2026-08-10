using System;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Defines a world interaction with optional flag requirements, progression effects,
    /// and Inspector-configured success or failure callbacks.
    /// </summary>
    /// <remarks>
    /// The component owns interaction rules, not input or UI. An <see cref="Interactor"/>
    /// attempts the action, global events notify cross-scene listeners, and serialized
    /// UnityEvents drive local behavior such as animation or audio.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Interactable")]
    public class Interactable : MonoBehaviour, IInteractionTarget
    {
        /// <summary>Raised after any interactable completes successfully.</summary>
        public static event Action<Interactable, Interactor> OnInteractionSucceeded;

        /// <summary>Raised after an enabled interactable fails its requirements.</summary>
        public static event Action<Interactable, Interactor> OnInteractionFailed;

        /// <summary>Raised when an interactable's runtime enabled state changes.</summary>
        public static event Action<Interactable, bool> OnInteractionEnabledChanged;

        [Header("Interaction Display")]
        [Tooltip("Name or prompt shown to the player.")]
        [SerializeField] private string displayName = "Interact";

        [Header("Requirements")]
        [Tooltip("Optional flags required before this interaction succeeds.")]
        [SerializeField] private FlagRequirement requirement;

        [Header("Success Behavior")]
        [Tooltip("Disable further interaction attempts after the first successful interaction.")]
        [SerializeField] private bool disableAfterSuccess;

        [Tooltip("Flags set when the interaction succeeds.")]
        [FlagId]
        [SerializeField] private string[] flagsToSetOnSuccess;

        [Tooltip("Scene-local callbacks invoked after success flags and the global success event.")]
        [SerializeField] private UnityEvent onInteractionSucceeded;

        [Header("Failure Behavior")]
        [Tooltip("Flags set when the interaction fails.")]
        [FlagId]
        [SerializeField] private string[] flagsToSetOnFailure;

        [Tooltip("Scene-local callbacks invoked after failure flags and the global failure event.")]
        [SerializeField] private UnityEvent onInteractionFailed;

        /// <summary>Gets the player-facing interaction label.</summary>
        public string DisplayName => displayName;

        /// <inheritdoc />
        public Transform InteractionTransform => transform;

        /// <summary>Gets whether this component currently accepts interaction attempts.</summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>Checks the runtime enabled state and configured flag requirement.</summary>
        /// <returns>True when an interaction attempt would succeed.</returns>
        public bool CanInteract()
        {
            return IsEnabled && (requirement == null || requirement.IsMet());
        }

        /// <inheritdoc />
        public bool IsInteractionAvailable(Interactor interactor)
        {
            return IsEnabled;
        }

        /// <summary>Attempts the interaction and invokes the matching event path.</summary>
        /// <param name="interactor">Optional actor that initiated the attempt.</param>
        /// <returns>True when the success path ran; otherwise false.</returns>
        public bool TryInteract(Interactor interactor = null)
        {
            GameLogger.Log(
                "TryInteract",
                this,
                $"TryInteract called on {gameObject.name} at frame {Time.frameCount}"
            );

            if (!IsEnabled)
            {
                return false;
            }

            if (!CanInteract())
            {
                HandleFailedInteraction(interactor);
                return false;
            }

            HandleSuccessfulInteraction(interactor);
            return true;
        }

        /// <summary>Changes whether this object accepts attempts and notifies global listeners.</summary>
        /// <param name="isEnabled">New interaction state.</param>
        public void SetEnabled(bool isEnabled)
        {
            if (IsEnabled == isEnabled)
            {
                return;
            }

            IsEnabled = isEnabled;
            OnInteractionEnabledChanged?.Invoke(this, IsEnabled);
        }

        private void HandleSuccessfulInteraction(Interactor interactor)
        {
            SetFlags(flagsToSetOnSuccess);
            OnInteractionSucceeded?.Invoke(this, interactor);
            onInteractionSucceeded?.Invoke();

            if (disableAfterSuccess)
            {
                SetEnabled(false);
            }
        }

        private void HandleFailedInteraction(Interactor interactor)
        {
            SetFlags(flagsToSetOnFailure);
            OnInteractionFailed?.Invoke(this, interactor);
            onInteractionFailed?.Invoke();
        }

        private static void SetFlags(string[] flagIds)
        {
            if (FlagManager.Instance == null || flagIds == null)
            {
                return;
            }

            foreach (string flagId in flagIds)
            {
                if (!string.IsNullOrWhiteSpace(flagId))
                {
                    FlagManager.Instance.SetFlag(flagId);
                }
            }
        }
    }
}
