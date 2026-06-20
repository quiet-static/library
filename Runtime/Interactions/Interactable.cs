using System;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Represents a reusable world object that can be interacted with by an <see cref="Interactor"/>.
    /// </summary>
    /// <remarks>
    /// This component supports both Inspector-assigned UnityEvents and static C# events.
    ///
    /// Use UnityEvents for scene-local behavior such as animations, sounds, or enabling objects.
    /// Use the static C# events for systems that may live in separate loaded scenes, such as
    /// objective managers, dialogue systems, analytics, or global progression managers.
    /// </remarks>
    public class Interactable : MonoBehaviour
    {
        /// <summary>
        /// Raised whenever any interactable successfully completes an interaction.
        /// The first parameter is the interactable that was used.
        /// The second parameter is the interactor that initiated the action, if available.
        /// </summary>
        public static event Action<Interactable, Interactor> OnInteractionSucceeded;

        /// <summary>
        /// Raised whenever any interactable receives an interaction attempt that fails because
        /// its requirements were not met.
        /// The first parameter is the interactable that was attempted.
        /// The second parameter is the interactor that initiated the action, if available.
        /// </summary>
        public static event Action<Interactable, Interactor> OnInteractionFailed;

        /// <summary>
        /// Raised whenever an interactable's enabled state changes.
        /// The first parameter is the interactable whose state changed.
        /// The second parameter is its new enabled state.
        /// </summary>
        public static event Action<Interactable, bool> OnInteractionEnabledChanged;

        [Header("Interaction Display")]
        [Tooltip("Name or prompt shown to the player when this object can be interacted with.")]
        [SerializeField] private string displayName = "Interact";

        [Header("Requirements")]
        [Tooltip("Optional flag requirement that must be met before the interaction succeeds. If left null, the interaction is always allowed while enabled.")]
        [SerializeField] private FlagRequirement requirement;

        [Header("Success Behavior")]
        [Tooltip("If true, this interactable disables itself after a successful interaction.")]
        [SerializeField] private bool disableAfterSuccess;

        [Tooltip("Flags to set when the interaction succeeds.")]
        [SerializeField] private string[] flagsToSetOnSuccess;

        [Tooltip("Events invoked after the interaction succeeds and success flags are set.")]
        [SerializeField] private UnityEvent onInteractionSucceeded;

        [Header("Failure Behavior")]
        [Tooltip("Flags to set when the player attempts the interaction but the requirement is not met.")]
        [SerializeField] private string[] flagsToSetOnFailure;

        [Tooltip("Events invoked after the interaction fails and failure flags are set.")]
        [SerializeField] private UnityEvent onInteractionFailed;

        /// <summary>
        /// Gets the player-facing display name or prompt for this interactable.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Gets whether this interactable is currently allowed to process interaction attempts.
        /// </summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>
        /// Checks whether this interactable is currently available and its flag requirement is met.
        /// </summary>
        public bool CanInteract()
        {
            if (!IsEnabled)
            {
                return false;
            }

            return requirement == null || requirement.IsMet();
        }

        /// <summary>
        /// Attempts to interact with this object.
        /// </summary>
        /// <param name="interactor">
        /// Optional interactor that initiated the interaction.
        /// </param>
        /// <returns>
        /// <c>true</c> if the interaction succeeded; otherwise <c>false</c>.
        /// </returns>
        public bool TryInteract(Interactor interactor = null)
        {
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

        /// <summary>
        /// Enables or disables this interactable without changing the GameObject active state.
        /// </summary>
        /// <param name="isEnabled">Whether future interaction attempts should be accepted.</param>
        public void SetEnabled(bool isEnabled)
        {
            if (IsEnabled == isEnabled)
            {
                return;
            }

            IsEnabled = isEnabled;
            OnInteractionEnabledChanged?.Invoke(this, IsEnabled);
        }

        /// <summary>
        /// Applies all success effects for this interaction.
        /// </summary>
        private void HandleSuccessfulInteraction(Interactor interactor)
        {
            SetFlags(flagsToSetOnSuccess);

            // Global C# listeners first, then local Inspector listeners.
            OnInteractionSucceeded?.Invoke(this, interactor);
            onInteractionSucceeded?.Invoke();

            if (disableAfterSuccess)
            {
                SetEnabled(false);
            }
        }

        /// <summary>
        /// Applies all failure effects for this interaction.
        /// </summary>
        private void HandleFailedInteraction(Interactor interactor)
        {
            SetFlags(flagsToSetOnFailure);

            // Global C# listeners first, then local Inspector listeners.
            OnInteractionFailed?.Invoke(this, interactor);
            onInteractionFailed?.Invoke();
        }

        /// <summary>
        /// Sets each valid flag id through the active <see cref="FlagSet"/> singleton.
        /// </summary>
        /// <param name="flagIds">The flag ids to set. Null arrays are ignored.</param>
        private static void SetFlags(string[] flagIds)
        {
            if (FlagSet.Instance == null || flagIds == null)
            {
                return;
            }

            foreach (string flagId in flagIds)
            {
                if (string.IsNullOrWhiteSpace(flagId))
                {
                    continue;
                }

                FlagSet.Instance.SetFlag(flagId);
            }
        }
    }
}