using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Objectives;
using UnityEngine;

namespace QuietStatic.Toolkit.Narrative
{
    /// <summary>Reusable ordered definition of high-level story progression.</summary>
    [CreateAssetMenu(
        fileName = "StorySequence",
        menuName = "Quiet Static Toolkit/Narrative/Story Sequence")]
    public sealed class StorySequenceDefinition : ScriptableObject
    {
        /// <summary>One stable story stage and its progression policy.</summary>
        [Serializable]
        public sealed class Stage
        {
            [Tooltip("Stable unique ID used by links and save data.")]
            [SerializeField] private string id;

            [Tooltip("Short author-facing label for this stage.")]
            [SerializeField] private string title;

            [Tooltip("Optional author notes describing the intended story beat.")]
            [TextArea(2, 6)]
            [SerializeField] private string description;

            [Tooltip("Flags required before this stage can begin. None always permits entry.")]
            [SerializeField] private FlagRequirement entryRequirement = new();

            [Tooltip("Configured flags that automatically complete this stage. None requires an explicit completion call.")]
            [SerializeField] private FlagRequirement completionRequirement = new();

            [Tooltip("Optional objective activated when this stage begins.")]
            [SerializeField] private ObjectiveDefinition objective;

            [Tooltip("Flags set after this stage successfully begins.")]
            [FlagId]
            [SerializeField] private string[] flagsToSetOnEnter;

            [Tooltip("Flags set when this stage completes.")]
            [FlagId]
            [SerializeField] private string[] flagsToSetOnComplete;

            [Tooltip("Stage entered after completion. Leave empty to end the sequence.")]
            [SerializeField] private string nextStageId;

            [Tooltip("Optional SceneFlowMap connection requested after stage entry.")]
            [SerializeField] private string sceneConnectionId;

            /// <summary>Gets the normalized stable stage ID.</summary>
            public string Id => Normalize(id);
            /// <summary>Gets the short author-facing stage title.</summary>
            public string Title => title ?? string.Empty;
            /// <summary>Gets the optional author notes for this story beat.</summary>
            public string Description => description ?? string.Empty;
            /// <summary>Gets the requirement that permits stage entry.</summary>
            public FlagRequirement EntryRequirement => entryRequirement;
            /// <summary>Gets the requirement that automatically completes the stage.</summary>
            public FlagRequirement CompletionRequirement => completionRequirement;
            /// <summary>Gets the objective activated when the stage begins.</summary>
            public ObjectiveDefinition Objective => objective;
            /// <summary>Gets the flag IDs set when the stage begins.</summary>
            public IReadOnlyList<string> FlagsToSetOnEnter => flagsToSetOnEnter ?? Array.Empty<string>();
            /// <summary>Gets the flag IDs set when the stage completes.</summary>
            public IReadOnlyList<string> FlagsToSetOnComplete => flagsToSetOnComplete ?? Array.Empty<string>();
            /// <summary>Gets the normalized ID of the next stage.</summary>
            public string NextStageId => Normalize(nextStageId);
            /// <summary>Gets the scene-flow connection requested on entry.</summary>
            public string SceneConnectionId => Normalize(sceneConnectionId);
        }

        [Header("Identity")]
        [Tooltip("Stable sequence ID used to namespace save data.")]
        [SerializeField] private string id;

        [Tooltip("First stage entered when the sequence starts without restored state.")]
        [SerializeField] private string startingStageId;

        [Header("Stages")]
        [Tooltip("All stages available to this sequence. Links use stable IDs rather than array positions.")]
        [SerializeField] private Stage[] stages;

        /// <summary>Gets the normalized stable sequence ID.</summary>
        public string Id => Normalize(id);
        /// <summary>Gets the normalized starting-stage ID.</summary>
        public string StartingStageId => Normalize(startingStageId);
        /// <summary>Gets all configured stages in authoring order.</summary>
        public IReadOnlyList<Stage> Stages => stages ?? Array.Empty<Stage>();

        /// <summary>Finds a stage using exact stable identity.</summary>
        public Stage FindStage(string stageId)
        {
            string normalized = Normalize(stageId);
            if (string.IsNullOrEmpty(normalized) || stages == null)
            {
                return null;
            }

            foreach (Stage stage in stages)
            {
                if (stage != null && string.Equals(
                        stage.Id, normalized, StringComparison.Ordinal))
                {
                    return stage;
                }
            }

            return null;
        }

        /// <summary>Gets the configured starting stage, if valid.</summary>
        public Stage GetStartingStage() => FindStage(StartingStageId);

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
