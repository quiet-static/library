using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Reusable, scene-independent description of a cinematic.</summary>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/Cinematics/Cinematic Definition")]
    public sealed class CinematicDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Beat
        {
            [Tooltip("Stable identifier used by scene event routes and editor tooling.")]
            public string id;

            [Tooltip("Optional designer-facing description of this beat.")]
            [TextArea(1, 3)] public string description;

            [Header("Independent Tracks")]
            [Tooltip("Optional camera shot ID resolved by the scene player.")]
            public string cameraShotId;

            [Tooltip("Character animation commands applied together when the beat starts.")]
            public List<CharacterAnimation> characterAnimations = new();

            [Tooltip("Optional activity ID resolved by the scene player. Activities may implement ICinematicWaitSource.")]
            public string activityId;

            [Header("Timing")]
            [Min(0f)] public float delayBeforeActivity;
            [Min(0f)] public float delayAfterActivity;
        }

        [Serializable]
        public sealed class CharacterAnimation
        {
            [Tooltip("Character ID resolved by the scene player.")]
            public string characterId;

            [Tooltip("Animator trigger to set. Leave empty when using a state name only.")]
            public string trigger;

            [Tooltip("Animator state to play. Leave empty when using a trigger only.")]
            public string stateName;

            [Min(0)] public int layer;
            [Range(0f, 1f)] public float normalizedStartTime;
        }

        [Tooltip("Stable ID used by databases and launch code.")]
        [SerializeField] private string id;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private List<Beat> beats = new();

        public string Id => id;
        public string Description => description;
        public IReadOnlyList<Beat> Beats => beats;
    }
}
