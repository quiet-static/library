using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Selects interaction feedback from ordered flag requirements and publishes it
    /// through an <see cref="InteractionUIChannel"/>.
    /// </summary>
    /// <remarks>
    /// Wire <see cref="ShowMessage"/> to an Interactable success or failure UnityEvent.
    /// The first configured rule whose requirement is met wins. This component owns
    /// conditional presentation only; the Interactable continues to own whether the
    /// interaction itself succeeds.
    /// </remarks>
    [AddComponentMenu(
        "Quiet Static Toolkit/Interactions/Conditional Interaction Message")]
    public sealed class ConditionalInteractionMessage : MonoBehaviour
    {
        /// <summary>One ordered flag condition and the message it selects.</summary>
        [Serializable]
        public sealed class MessageRule
        {
            [Tooltip("Optional authoring label explaining when this message is used.")]
            [SerializeField] private string label;

            [Tooltip("Flag condition that selects this message. Rules are checked from top to bottom.")]
            [SerializeField] private FlagRequirement requirement = new();

            [Tooltip("Interaction UI message sent when this rule is the first match.")]
            [TextArea(2, 5)]
            [SerializeField] private string message;

            /// <summary>Gets the optional authoring label.</summary>
            public string Label => label ?? string.Empty;

            /// <summary>Gets the flag requirement evaluated for this rule.</summary>
            public FlagRequirement Requirement => requirement;

            /// <summary>Gets the player-facing message.</summary>
            public string Message => message ?? string.Empty;
        }

        [Header("Output")]
        [Tooltip("Cross-scene channel consumed by the persistent interaction UI.")]
        [SerializeField] private InteractionUIChannel channel;

        [Header("Conditional Messages")]
        [Tooltip("Ordered messages. The first non-empty message whose requirement is met is used.")]
        [SerializeField] private MessageRule[] rules;

        [Tooltip("Message used when no configured rule matches. Leave empty to send nothing.")]
        [TextArea(2, 5)]
        [SerializeField] private string defaultMessage;

        [Header("Timing")]
        [Tooltip("Use a custom display duration instead of the UI listener's default message duration.")]
        [SerializeField] private bool useCustomDuration;

        [Tooltip("Seconds a custom-duration message remains visible.")]
        [Min(0.01f)]
        [SerializeField] private float messageDuration = 3f;

        /// <summary>Gets the configured ordered rules.</summary>
        public IReadOnlyList<MessageRule> Rules =>
            rules ?? Array.Empty<MessageRule>();

        /// <summary>Gets the fallback text used when no rule matches.</summary>
        public string DefaultMessage => defaultMessage ?? string.Empty;

        /// <summary>
        /// Resolves and publishes the currently applicable message. This parameterless
        /// method is suitable for Interactable UnityEvents.
        /// </summary>
        public void ShowMessage()
        {
            if (channel == null || !TryResolveMessage(out string message))
            {
                return;
            }

            if (useCustomDuration)
            {
                channel.ShowMessageForSeconds(message, messageDuration);
            }
            else
            {
                channel.ShowMessage(message);
            }
        }

        /// <summary>Finds the first message matching the current flag state.</summary>
        /// <param name="message">Resolved message, or an empty string when none applies.</param>
        /// <returns>True when a non-empty rule or fallback message was found.</returns>
        public bool TryResolveMessage(out string message)
        {
            if (rules != null)
            {
                foreach (MessageRule rule in rules)
                {
                    if (rule == null ||
                        string.IsNullOrWhiteSpace(rule.Message) ||
                        rule.Requirement == null ||
                        !rule.Requirement.IsMet())
                    {
                        continue;
                    }

                    message = rule.Message;
                    return true;
                }
            }

            message = defaultMessage ?? string.Empty;
            return !string.IsNullOrWhiteSpace(message);
        }
    }
}
