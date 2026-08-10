using System;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace QuietStatic.Toolkit.Deductions
{
    /// <summary>Data-driven conclusion selected from the current gameplay flags.</summary>
    [MovedFrom(true, "QuietStatic.NightwatchTheatre.Deductions", null, "DeductionResultDefinition")]
    [CreateAssetMenu(
        fileName = "Deduction Result",
        menuName = "Quiet Static Toolkit/Deductions/Result Definition")]
    public sealed class DeductionResultDefinition : ScriptableObject
    {
        [Tooltip("Stable result ID used by game-specific ending logic and save data.")]
        [SerializeField] private string id;

        [Tooltip("Higher-priority matching results win. Use specific conclusions above fallbacks.")]
        [SerializeField] private int priority;

        [Tooltip("Player-facing ending title.")]
        [SerializeField] private string title;

        [Tooltip("Short player-facing conclusion text.")]
        [TextArea(2, 6)]
        [SerializeField] private string flavorText;

        [Tooltip("Optional explanation of the reasoning error or missed evidence.")]
        [TextArea(2, 6)]
        [SerializeField] private string reasoningHint;

        [Tooltip("Every configured requirement must pass for this result to match. Combine All and NotAny requirements for required and forbidden flags.")]
        [SerializeField] private FlagRequirement[] requirements;

        /// <summary>Gets the stable result identifier.</summary>
        public string Id => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        /// <summary>Gets the result priority.</summary>
        public int Priority => priority;

        /// <summary>Gets the player-facing title.</summary>
        public string Title => title ?? string.Empty;

        /// <summary>Gets the player-facing conclusion.</summary>
        public string FlavorText => flavorText ?? string.Empty;

        /// <summary>Gets the optional reasoning hint.</summary>
        public string ReasoningHint => reasoningHint ?? string.Empty;

        /// <summary>Returns whether every configured requirement matches current flags.</summary>
        public bool Matches(FlagManager flagManager = null)
        {
            flagManager ??= FlagManager.Instance;

            if (flagManager == null)
            {
                return false;
            }

            foreach (FlagRequirement requirement in requirements ?? Array.Empty<FlagRequirement>())
            {
                if (requirement != null && !requirement.IsMet(flagManager))
                {
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            id = id?.Trim();
        }
#endif
    }
}
