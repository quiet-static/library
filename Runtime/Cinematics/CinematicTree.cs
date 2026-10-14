using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>A linear, reusable sequence of cinematic text beats. Array order defines playback.</summary>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/Cinematics/Cinematic Tree")]
    public sealed class CinematicTree : ScriptableObject
    {
        [Serializable]
        public sealed class Beat
        {
            [Tooltip("Stable ID used by scene camera cues. Reordering never changes this ID.")]
            public string id;
            [Tooltip("Text shown at the bottom of the screen. Supports TMP rich text.")]
            [TextArea(2, 6)] public string text;
            [Tooltip("Characters revealed per second. Zero reveals the entire beat immediately.")]
            [Min(0)] public float charactersPerSecond = 28;
            [Tooltip("Seconds to hold after the complete text is visible in automatic playback.")]
            [Min(0)] public float holdSeconds = 2;
        }

        [SerializeField, TextArea(1, 4)] private string description;
        [SerializeField] private List<Beat> beats = new();
        public string Description => description;
        public IReadOnlyList<Beat> Beats => beats;

        /// <summary>Validates identities and timing before a sequence starts or is imported.</summary>
        public bool TryValidate(out string error)
        {
            if (beats == null || beats.Count == 0) { error = "Add at least one text beat."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < beats.Count; i++)
            {
                Beat beat = beats[i];
                if (beat == null || string.IsNullOrWhiteSpace(beat.id) || !ids.Add(beat.id))
                { error = $"Beat {i + 1} needs a unique, non-empty ID."; return false; }
                if (string.IsNullOrWhiteSpace(beat.text))
                { error = $"Beat '{beat.id}' needs text."; return false; }
                if (!ValidTime(beat.charactersPerSecond) || !ValidTime(beat.holdSeconds))
                { error = $"Beat '{beat.id}' has invalid timing."; return false; }
            }
            error = null;
            return true;
        }

        private static bool ValidTime(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
