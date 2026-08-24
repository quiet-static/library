using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Objectives;
using QuietStatic.Toolkit.Saving;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Narrative
{
    /// <summary>Owns runtime progression through one story sequence definition.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Narrative/Story Sequence Runner")]
    public sealed class StorySequenceRunner : MonoBehaviour, ISaveParticipant
    {
        [Serializable]
        public sealed class StageUnityEvent : UnityEvent<string> { }

        [Serializable]
        private sealed class SaveData
        {
            public string currentStageId;
            public bool currentStageCompleted;
            public List<string> completedStageIds = new();
        }

        [Header("Definition")]
        [Tooltip("Reusable story-stage graph controlled by this runner.")]
        [SerializeField] private StorySequenceDefinition sequence;

        [Tooltip("Optional map used by stage scene-connection IDs.")]
        [SerializeField] private SceneFlowMap sceneFlowMap;

        [Tooltip("Required channel used to submit stage scene transitions.")]
        [RequiredCommandChannel]
        [SerializeField] private SceneFlowRequestChannel requestChannel;

        [Header("Startup")]
        [Tooltip("Begin at the definition's starting stage during Start.")]
        [SerializeField] private bool startOnStart = true;

        [Tooltip("Evaluate configured completion requirements whenever flags change.")]
        [SerializeField] private bool autoAdvanceFromFlags = true;

        [Header("Events")]
        [Tooltip("Invoked with the stable stage ID after entry actions are applied.")]
        [SerializeField] private StageUnityEvent onStageEntered;
        [Tooltip("Invoked with the stable stage ID after completion actions are applied.")]
        [SerializeField] private StageUnityEvent onStageCompleted;
        [Tooltip("Invoked when a completed stage has no valid next-stage link.")]
        [SerializeField] private UnityEvent onSequenceCompleted;

        private readonly HashSet<string> completedStageIds = new(StringComparer.Ordinal);
        private bool applyingStageChange;

        public StorySequenceDefinition.Stage CurrentStage { get; private set; }
        public bool IsCurrentStageCompleted { get; private set; }
        public IReadOnlyCollection<string> CompletedStageIds => completedStageIds;
        public string SaveId => $"quietstatic.story-sequence.{sequence?.Id ?? string.Empty}";

        private FlagManager observedFlags;

        private void OnEnable()
        {
            observedFlags = FlagManager.Instance;
            if (observedFlags != null)
            {
                observedFlags.FlagsChanged += EvaluateProgress;
            }
        }

        private void OnDisable()
        {
            if (observedFlags != null)
            {
                observedFlags.FlagsChanged -= EvaluateProgress;
                observedFlags = null;
            }
        }

        private void Start()
        {
            if (startOnStart && CurrentStage == null)
            {
                StartSequence();
            }
        }

        /// <summary>Starts from the definition's configured first stage.</summary>
        public void StartSequence()
        {
            completedStageIds.Clear();
            CurrentStage = null;
            IsCurrentStageCompleted = false;
            TryEnterStage(sequence?.StartingStageId);
        }

        /// <summary>Attempts to enter an explicitly named stage.</summary>
        public bool TryEnterStage(string stageId)
        {
            StorySequenceDefinition.Stage stage = sequence?.FindStage(stageId);
            if (stage == null ||
                (stage.EntryRequirement != null &&
                 !stage.EntryRequirement.IsMet()))
            {
                return false;
            }

            CurrentStage = stage;
            IsCurrentStageCompleted = completedStageIds.Contains(stage.Id);
            if (IsCurrentStageCompleted)
            {
                return true;
            }

            applyingStageChange = true;
            SetFlags(stage.FlagsToSetOnEnter);
            if (stage.Objective != null)
            {
                ObjectiveManager.Instance?.ActivateObjective(stage.Objective);
            }

            RequestSceneConnection(stage.SceneConnectionId);
            onStageEntered?.Invoke(stage.Id);
            applyingStageChange = false;
            EvaluateProgress();
            return true;
        }

        /// <summary>Explicitly completes the current stage and advances when possible.</summary>
        public void CompleteCurrentStage()
        {
            if (CurrentStage == null || IsCurrentStageCompleted)
            {
                return;
            }

            StorySequenceDefinition.Stage completed = CurrentStage;
            applyingStageChange = true;
            completedStageIds.Add(completed.Id);
            IsCurrentStageCompleted = true;
            if (completed.Objective != null &&
                ObjectiveManager.Instance?.ActiveObjective == completed.Objective)
            {
                ObjectiveManager.Instance.CompleteActiveObjective();
            }
            SetFlags(completed.FlagsToSetOnComplete);
            onStageCompleted?.Invoke(completed.Id);

            if (string.IsNullOrWhiteSpace(completed.NextStageId))
            {
                onSequenceCompleted?.Invoke();
                applyingStageChange = false;
                return;
            }

            applyingStageChange = false;
            TryEnterStage(completed.NextStageId);
        }

        /// <summary>Evaluates automatic completion and pending next-stage entry.</summary>
        public void EvaluateProgress()
        {
            if (applyingStageChange || CurrentStage == null)
            {
                return;
            }

            if (IsCurrentStageCompleted)
            {
                TryEnterStage(CurrentStage.NextStageId);
                return;
            }

            FlagRequirement requirement = CurrentStage.CompletionRequirement;
            if (autoAdvanceFromFlags &&
                requirement != null &&
                requirement.IsConfigured &&
                requirement.IsMet())
            {
                CompleteCurrentStage();
            }
        }

        public string CaptureSaveState()
        {
            return JsonUtility.ToJson(new SaveData
            {
                currentStageId = CurrentStage?.Id ?? string.Empty,
                currentStageCompleted = IsCurrentStageCompleted,
                completedStageIds = completedStageIds.OrderBy(id => id).ToList(),
            });
        }

        public void RestoreSaveState(string json)
        {
            SaveData data = string.IsNullOrWhiteSpace(json)
                ? new SaveData()
                : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            completedStageIds.Clear();
            foreach (string id in data.completedStageIds ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(id)) completedStageIds.Add(id.Trim());
            }
            CurrentStage = sequence?.FindStage(data.currentStageId);
            IsCurrentStageCompleted = data.currentStageCompleted && CurrentStage != null;
            if (CurrentStage != null && !IsCurrentStageCompleted && CurrentStage.Objective != null)
            {
                ObjectiveManager.Instance?.ActivateObjective(CurrentStage.Objective);
            }
        }

        private void RequestSceneConnection(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId) || sceneFlowMap == null ||
                !sceneFlowMap.TryCreateRequest(connectionId, out SceneTransitionRequest request))
            {
                return;
            }
            if (requestChannel == null || !requestChannel.RequestTransition(request))
            {
                GameLogger.Warning(
                    nameof(RequestSceneConnection),
                    this,
                    "Story sequence scene transition requires an active request-channel receiver.");
            }
        }

        private static void SetFlags(IEnumerable<string> flagIds)
        {
            if (FlagManager.Instance == null || flagIds == null) return;
            foreach (string id in flagIds)
            {
                if (!string.IsNullOrWhiteSpace(id)) FlagManager.Instance.SetFlag(id.Trim());
            }
        }
    }
}
