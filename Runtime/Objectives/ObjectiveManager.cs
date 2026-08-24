using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Saving;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Objectives
{
    /// <summary>
    /// Owns the active objective and completed-objective history.
    /// </summary>
    /// <remarks>
    /// Keep one instance in the persistent Systems scene. Scene objects should issue
    /// commands through <see cref="QuietStatic.ObjectiveHandler"/> or events.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Objectives/Objective Manager")]
    public sealed class ObjectiveManager :
        ToolkitSingleton<ObjectiveManager>,
        ISaveParticipant
    {
        private const string ParticipantId = "quietstatic.objectives";

        [Serializable]
        public sealed class ObjectiveUnityEvent : UnityEvent<ObjectiveDefinition>
        {
        }

        [Serializable]
        private sealed class ObjectiveSaveData
        {
            /// <summary>Stable ID of the active objective, if any.</summary>
            public string activeObjectiveId;

            /// <summary>Stable IDs of objectives completed before the save.</summary>
            public List<string> completedObjectiveIds = new();
        }

        [Header("Definitions")]
        [Tooltip("Database used to restore objective references from stable IDs.")]
        [SerializeField] private ObjectiveDatabase database;

        [Header("Lifecycle")]
        [Tooltip("When enabled, the highest-priority database objective whose activation requirement is met becomes active.")]
        [SerializeField] private bool autoActivateFromFlags = true;

        [Tooltip("When enabled, an active objective completes as soon as its configured flag requirement is met.")]
        [SerializeField] private bool autoCompleteFromFlags = true;

        [Tooltip("When enabled, a completed definition may be activated again.")]
        [SerializeField] private bool allowReactivateCompleted;

        [Header("Events")]
        [Tooltip("Invoked after an objective becomes active.")]
        [SerializeField] private ObjectiveUnityEvent onObjectiveActivated;

        [Tooltip("Invoked when listeners should refresh the active objective presentation.")]
        [SerializeField] private ObjectiveUnityEvent onObjectiveUpdated;

        [Tooltip("Invoked after an objective is marked complete.")]
        [SerializeField] private ObjectiveUnityEvent onObjectiveCompleted;

        [Tooltip("Invoked after an active objective is cleared without completion.")]
        [SerializeField] private ObjectiveUnityEvent onObjectiveCleared;

        private readonly HashSet<string> completedObjectiveIds =
            new(StringComparer.Ordinal);

        private bool isRefreshingFromFlags;
        private FlagManager observedFlags;

        /// <summary>Raised after any objective lifecycle transition.</summary>
        public event Action ObjectiveLifecycleChanged;

        /// <summary>Gets the currently active objective, if any.</summary>
        public ObjectiveDefinition ActiveObjective { get; private set; }

        /// <summary>Gets completed stable objective IDs.</summary>
        public IReadOnlyCollection<string> CompletedObjectiveIds =>
            completedObjectiveIds;

        /// <summary>Gets the configured objective database.</summary>
        public ObjectiveDatabase Database => database;

        /// <inheritdoc />
        public string SaveId => ParticipantId;

        private void OnEnable()
        {
            if (Instance == this)
            {
                ObserveActiveFlagManager();
            }
        }

        private void OnDisable()
        {
            if (observedFlags != null)
            {
                observedFlags.FlagsChanged -= RefreshObjectivesFromFlags;
                observedFlags = null;
            }
        }

        private void Start()
        {
            if (Instance == this)
            {
                ObserveActiveFlagManager();
                RefreshObjectivesFromFlags();
            }
        }

        private void ObserveActiveFlagManager()
        {
            FlagManager activeFlags = FlagManager.Instance;
            if (observedFlags == activeFlags)
            {
                return;
            }

            if (observedFlags != null)
            {
                observedFlags.FlagsChanged -= RefreshObjectivesFromFlags;
            }

            observedFlags = activeFlags;
            if (observedFlags != null)
            {
                observedFlags.FlagsChanged += RefreshObjectivesFromFlags;
            }
        }

        /// <summary>Returns whether the supplied objective has been completed.</summary>
        public bool HasCompleted(ObjectiveDefinition objective)
        {
            return objective != null && HasCompleted(objective.Id);
        }

        /// <summary>Returns whether an objective ID has been completed.</summary>
        public bool HasCompleted(string objectiveId)
        {
            return !string.IsNullOrWhiteSpace(objectiveId) &&
                   completedObjectiveIds.Contains(objectiveId.Trim());
        }

        /// <summary>
        /// Makes a definition active. Completed objectives are rejected unless replay is enabled.
        /// </summary>
        public bool ActivateObjective(ObjectiveDefinition objective)
        {
            if (!CanActivate(objective))
            {
                return false;
            }

            if (ActiveObjective == objective)
            {
                return false;
            }

            if (ActiveObjective != null)
            {
                ObjectiveDefinition previous = ActiveObjective;
                ActiveObjective = null;
                RaiseCleared(previous);
            }

            ActiveObjective = objective;
            onObjectiveActivated?.Invoke(objective);
            ObjectiveLifecycleChanged?.Invoke();
            EvaluateActiveObjectiveCompletion();
            return true;
        }

        /// <summary>Marks the active objective complete.</summary>
        public bool CompleteActiveObjective()
        {
            return CompleteObjective(ActiveObjective);
        }

        /// <summary>Marks a definition complete and deactivates it when necessary.</summary>
        public bool CompleteObjective(ObjectiveDefinition objective)
        {
            if (objective == null || string.IsNullOrWhiteSpace(objective.Id))
            {
                return false;
            }

            string objectiveId = objective.Id;

            if (!completedObjectiveIds.Add(objectiveId))
            {
                return false;
            }

            if (ActiveObjective == objective)
            {
                ActiveObjective = null;
            }

            onObjectiveCompleted?.Invoke(objective);
            ObjectiveLifecycleChanged?.Invoke();
            return true;
        }

        /// <summary>Clears the active objective without marking it complete.</summary>
        public bool ClearActiveObjective()
        {
            if (ActiveObjective == null)
            {
                return false;
            }

            ObjectiveDefinition previous = ActiveObjective;
            ActiveObjective = null;
            RaiseCleared(previous);
            return true;
        }

        /// <summary>Notifies listeners to refresh the current objective.</summary>
        public void RefreshActiveObjective()
        {
            if (ActiveObjective == null)
            {
                return;
            }

            onObjectiveUpdated?.Invoke(ActiveObjective);
            ObjectiveLifecycleChanged?.Invoke();
        }

        /// <summary>
        /// Completes the active objective when its configured flag rule is met.
        /// </summary>
        public void EvaluateActiveObjectiveCompletion()
        {
            EvaluateActiveObjectiveCompletion(FlagManager.Instance);
        }

        /// <summary>
        /// Completes the active objective against an explicit flag manager.
        /// </summary>
        /// <param name="flagManager">Flag state used to evaluate the completion rule.</param>
        public void EvaluateActiveObjectiveCompletion(FlagManager flagManager)
        {
            if (autoCompleteFromFlags &&
                ActiveObjective != null &&
                ActiveObjective.IsCompletionMet(flagManager))
            {
                CompleteActiveObjective();
            }
        }

        /// <summary>
        /// Completes the current objective when appropriate, then selects the
        /// highest-priority incomplete database objective whose activation rule is met.
        /// </summary>
        public void RefreshObjectivesFromFlags()
        {
            RefreshObjectivesFromFlags(FlagManager.Instance);
        }

        /// <summary>
        /// Refreshes automatic objective lifecycle against an explicit flag manager.
        /// </summary>
        /// <param name="flagManager">Flag state used for activation and completion rules.</param>
        public void RefreshObjectivesFromFlags(FlagManager flagManager)
        {
            if (isRefreshingFromFlags)
            {
                return;
            }

            isRefreshingFromFlags = true;

            try
            {
                EvaluateActiveObjectiveCompletion(flagManager);

                if (!autoActivateFromFlags || database == null)
                {
                    return;
                }

                ObjectiveDefinition selected =
                    FindHighestPriorityObjective(flagManager);

                if (selected == ActiveObjective)
                {
                    return;
                }

                if (selected == null &&
                    ActiveObjective != null &&
                    (ActiveObjective.ActivationRequirement == null ||
                     !ActiveObjective.ActivationRequirement.IsConfigured))
                {
                    return;
                }

                if (selected != null)
                {
                    ActivateObjective(selected);
                }
                else
                {
                    ClearActiveObjective();
                }
            }
            finally
            {
                isRefreshingFromFlags = false;
            }
        }

        /// <inheritdoc />
        public string CaptureSaveState()
        {
            var saveData = new ObjectiveSaveData
            {
                activeObjectiveId = ActiveObjective?.Id ?? string.Empty,
                completedObjectiveIds =
                    completedObjectiveIds.OrderBy(id => id).ToList()
            };

            return JsonUtility.ToJson(saveData);
        }

        /// <inheritdoc />
        public void RestoreSaveState(string json)
        {
            ObjectiveDefinition previous = ActiveObjective;
            ObjectiveSaveData saveData = string.IsNullOrWhiteSpace(json)
                ? new ObjectiveSaveData()
                : JsonUtility.FromJson<ObjectiveSaveData>(json);

            ActiveObjective = null;
            completedObjectiveIds.Clear();

            if (saveData?.completedObjectiveIds != null)
            {
                foreach (string objectiveId in saveData.completedObjectiveIds)
                {
                    if (!string.IsNullOrWhiteSpace(objectiveId))
                    {
                        completedObjectiveIds.Add(objectiveId.Trim());
                    }
                }
            }

            if (database != null && saveData != null)
            {
                ObjectiveDefinition restored =
                    database.FindById(saveData.activeObjectiveId);

                if (restored != null && !HasCompleted(restored))
                {
                    ActiveObjective = restored;
                }
            }

            if (previous != null && previous != ActiveObjective)
            {
                onObjectiveCleared?.Invoke(previous);
            }

            if (ActiveObjective != null)
            {
                onObjectiveUpdated?.Invoke(ActiveObjective);
            }

            ObjectiveLifecycleChanged?.Invoke();
            RefreshObjectivesFromFlags();
        }

        private ObjectiveDefinition FindHighestPriorityObjective(
            FlagManager flagManager)
        {
            IReadOnlyList<ObjectiveDefinition> objectives = database.Objectives;

            for (int index = objectives.Count - 1; index >= 0; index--)
            {
                ObjectiveDefinition objective = objectives[index];

                if (objective == null ||
                    objective.ActivationRequirement == null ||
                    !objective.ActivationRequirement.IsConfigured ||
                    !objective.ActivationRequirement.IsMet(flagManager) ||
                    (!allowReactivateCompleted && HasCompleted(objective)) ||
                    objective.IsCompletionMet(flagManager))
                {
                    continue;
                }

                return objective;
            }

            return null;
        }

        private bool CanActivate(ObjectiveDefinition objective)
        {
            if (objective == null || string.IsNullOrWhiteSpace(objective.Id))
            {
                return false;
            }

            return allowReactivateCompleted || !HasCompleted(objective);
        }

        private void RaiseCleared(ObjectiveDefinition objective)
        {
            onObjectiveCleared?.Invoke(objective);
            ObjectiveLifecycleChanged?.Invoke();
        }
    }
}
