using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Toolkit.Objectives
{
    /// <summary>
    /// Reusable definition for an objective that can be activated, completed, and saved.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Objective",
        menuName = "Quiet Static Toolkit/Objectives/Objective Definition")]
    public sealed class ObjectiveDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique ID used by runtime state and save files. Do not change it after shipping.")]
        [SerializeField] private string id;

        [Tooltip("Short player-facing objective title.")]
        [SerializeField] private string title;

        [Tooltip("Optional longer player-facing explanation of the objective.")]
        [TextArea(2, 6)]
        [SerializeField] private string description;

        [Header("Progression")]
        [Tooltip("Optional flag condition that automatically activates this objective. Database order determines priority when multiple conditions are met.")]
        [SerializeField] private FlagRequirement activationRequirement = new();

        [Tooltip("Optional flag condition that automatically completes this objective. None leaves completion to explicit commands.")]
        [SerializeField] private FlagRequirement completionRequirement = new();

        /// <summary>Gets the stable objective ID.</summary>
        public string Id => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();

        /// <summary>Gets the short player-facing title.</summary>
        public string Title => title ?? string.Empty;

        /// <summary>Gets the optional longer description.</summary>
        public string Description => description ?? string.Empty;

        /// <summary>Gets the optional automatic activation rule.</summary>
        public FlagRequirement ActivationRequirement => activationRequirement;

        /// <summary>Gets the optional automatic completion rule.</summary>
        public FlagRequirement CompletionRequirement => completionRequirement;

        /// <summary>
        /// Gets the preferred single-label text, using the description when supplied
        /// and falling back to the title.
        /// </summary>
        public string DisplayText =>
            string.IsNullOrWhiteSpace(Description) ? Title : Description;

        /// <summary>
        /// Returns whether this objective has a configured completion rule that is met.
        /// </summary>
        public bool IsCompletionMet(FlagManager flagManager = null)
        {
            return completionRequirement != null &&
                   completionRequirement.IsConfigured &&
                   completionRequirement.IsMet(flagManager);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            id = id?.Trim();
        }
#endif
    }
}
