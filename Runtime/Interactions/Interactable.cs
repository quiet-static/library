using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Represents a reusable world object that can be interacted with by an <see cref="Interactor"/>.
    /// </summary>
    /// <remarks>
    /// This component is intentionally generic so it can be reused for doors, pickups, switches,
    /// readable objects, puzzle props, and other interaction targets.
    ///
    /// Interaction success is determined by the optional <see cref="FlagRequirement"/>. When the
    /// requirement is met, this component sets success flags, invokes success events, and can
    /// optionally disable itself so it cannot be used again. When the requirement is not met, it
    /// sets failure flags and invokes failure events instead.
    /// </remarks>
    public class Interactable : MonoBehaviour
    {
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
        /// <remarks>
        /// This is separate from the GameObject active state. A disabled interactable can remain
        /// visible in the scene while rejecting interaction attempts.
        /// </remarks>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>
        /// Checks whether this interactable is currently available and its flag requirement is met.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the interactable is enabled and has no unmet requirement;
        /// otherwise, <c>false</c>.
        /// </returns>
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
        /// Optional interactor that initiated the interaction. This parameter is currently unused,
        /// but is kept so future interaction logic can react to who performed the interaction.
        /// </param>
        /// <returns>
        /// <c>true</c> if the interaction succeeded; <c>false</c> if the interactable was disabled
        /// or its requirement was not met.
        /// </returns>
        public bool TryInteract(Interactor interactor = null)
        {
            if (!IsEnabled)
            {
                return false;
            }

            if (!CanInteract())
            {
                HandleFailedInteraction();
                return false;
            }

            HandleSuccessfulInteraction();
            return true;
        }

        /// <summary>
        /// Enables or disables this interactable without changing the GameObject active state.
        /// </summary>
        /// <param name="isEnabled">
        /// Whether future interaction attempts should be accepted.
        /// </param>
        public void SetEnabled(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        /// <summary>
        /// Applies all success effects for this interaction.
        /// </summary>
        private void HandleSuccessfulInteraction()
        {
            SetFlags(flagsToSetOnSuccess);
            onInteractionSucceeded?.Invoke();

            if (disableAfterSuccess)
            {
                IsEnabled = false;
            }
        }

        /// <summary>
        /// Applies all failure effects for this interaction.
        /// </summary>
        private void HandleFailedInteraction()
        {
            SetFlags(flagsToSetOnFailure);
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
                FlagSet.Instance.SetFlag(flagId);
            }
        }
    }
}
