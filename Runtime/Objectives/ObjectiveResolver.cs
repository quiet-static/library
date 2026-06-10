using System;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Flags;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Objectives
{
    /// <summary>
    /// Resolves and displays the current objective by checking a prioritized list of
    /// flag-based objective entries.
    /// </summary>
    /// <remarks>
    /// Objectives should be ordered from lowest priority to highest priority in the
    /// Inspector. When <see cref="RefreshObjective"/> runs, the resolver searches the
    /// list from the end toward the beginning and selects the first objective whose
    /// <see cref="FlagRequirement"/> is met.
    ///
    /// This makes it easy to define broad early-game objectives first, then place more
    /// specific late-game or high-priority objectives later in the list.
    /// </remarks>
    public class ObjectiveResolver : MonoBehaviour
    {
        /// <summary>
        /// Defines one possible objective and the flag requirement that makes it active.
        /// </summary>
        [Serializable]
        public class ObjectiveEntry
        {
            [Header("Identity")]
            [Tooltip("Optional identifier for this objective entry. Useful for organization, debugging, or designer notes.")]
            /// <summary>
            /// Optional identifier for this objective entry.
            /// </summary>
            public string id;

            [Header("Requirement")]
            [Tooltip("Flag condition that must be met for this objective to become active.")]
            /// <summary>
            /// Flag requirement that determines whether this objective can be selected.
            /// </summary>
            public FlagRequirement requirement;

            [Header("Display Text")]
            [Tooltip("Text shown to the player when this objective is selected.")]
            [TextArea]
            /// <summary>
            /// Objective text displayed when this entry is selected.
            /// </summary>
            public string objectiveText;
        }

        /// <summary>
        /// UnityEvent wrapper for objective text changes.
        /// </summary>
        /// <remarks>
        /// The string argument contains the newly selected objective text.
        /// </remarks>
        [Serializable]
        public class StringUnityEvent : UnityEvent<string> { }

        [Header("UI")]
        [Tooltip("TextMeshPro label that displays the currently selected objective. If unassigned, objectives still resolve and events still fire.")]
        /// <summary>
        /// Optional TextMeshPro label used to display the current objective.
        /// </summary>
        [SerializeField] private TMP_Text objectiveLabel;

        [Header("Objectives")]
        [Tooltip("Objective entries ordered from lowest priority to highest priority. The resolver checks this list from bottom to top.")]
        /// <summary>
        /// Ordered list of objective entries.
        /// </summary>
        [SerializeField] private ObjectiveEntry[] objectives;

        [Tooltip("Objective text used when no objective entry has a met requirement.")]
        /// <summary>
        /// Objective text used when no configured objective requirement is met.
        /// </summary>
        [SerializeField] private string fallbackObjective = "";

        [Header("Events")]
        [Tooltip("Invoked whenever the resolved objective text changes.")]
        /// <summary>
        /// Inspector event invoked when the objective changes.
        /// </summary>
        [SerializeField] private StringUnityEvent onObjectiveChanged;

        /// <summary>
        /// Gets the currently resolved objective text.
        /// </summary>
        public string CurrentObjective { get; private set; }

        /// <summary>
        /// Attempts to populate the objective label reference when the component is added
        /// or reset in the Unity Inspector.
        /// </summary>
        private void Reset()
        {
            objectiveLabel = GetComponentInChildren<TMP_Text>();
        }

        /// <summary>
        /// Subscribes to global flag changes and resolves the starting objective.
        /// </summary>
        private void OnEnable()
        {
            FlagSet.OnFlagSet += HandleFlagSet;
            RefreshObjective();
        }

        /// <summary>
        /// Unsubscribes from global flag changes to avoid callbacks after this component
        /// is disabled or destroyed.
        /// </summary>
        private void OnDisable()
        {
            FlagSet.OnFlagSet -= HandleFlagSet;
        }

        /// <summary>
        /// Refreshes the active objective after a flag is set.
        /// </summary>
        /// <param name="flagId">
        /// The flag that was set. This resolver does not need the specific value because
        /// it re-checks every configured objective requirement.
        /// </param>
        private void HandleFlagSet(string flagId)
        {
            RefreshObjective();
        }

        /// <summary>
        /// Re-evaluates all objective entries and applies the highest-priority objective
        /// whose requirement is currently met.
        /// </summary>
        /// <remarks>
        /// The objective list is searched from last to first, so entries later in the
        /// Inspector override earlier entries when multiple requirements are valid.
        /// </remarks>
        public void RefreshObjective()
        {
            string selected = fallbackObjective;

            if (objectives != null)
            {
                for (int i = objectives.Length - 1; i >= 0; i--)
                {
                    ObjectiveEntry entry = objectives[i];

                    if (CanUseObjective(entry))
                    {
                        selected = entry.objectiveText;
                        break;
                    }
                }
            }

            SetObjective(selected);
        }

        /// <summary>
        /// Sets the current objective text directly and notifies listeners if it changed.
        /// </summary>
        /// <param name="objective">
        /// The objective text to display. A null value is treated as an empty string.
        /// </param>
        public void SetObjective(string objective)
        {
            objective ??= string.Empty;

            if (CurrentObjective == objective)
            {
                return;
            }

            CurrentObjective = objective;
            UpdateObjectiveLabel();
            NotifyObjectiveChanged();
        }

        /// <summary>
        /// Determines whether an objective entry is valid and currently selectable.
        /// </summary>
        /// <param name="entry">The objective entry to check.</param>
        /// <returns>
        /// True if the entry exists, has a requirement, and its requirement is met;
        /// otherwise, false.
        /// </returns>
        private bool CanUseObjective(ObjectiveEntry entry)
        {
            return entry != null &&
                   entry.requirement != null &&
                   entry.requirement.IsMet();
        }

        /// <summary>
        /// Updates the assigned UI label with the current objective text.
        /// </summary>
        private void UpdateObjectiveLabel()
        {
            if (objectiveLabel != null)
            {
                objectiveLabel.text = CurrentObjective;
            }
        }

        /// <summary>
        /// Sends objective change notifications through the Inspector UnityEvent and
        /// the toolkit-level global event hub.
        /// </summary>
        private void NotifyObjectiveChanged()
        {
            onObjectiveChanged?.Invoke(CurrentObjective);
            ToolkitEvents.RaiseObjectiveChanged(CurrentObjective);
        }
    }
}
