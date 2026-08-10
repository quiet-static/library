using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Toolkit.Horror
{
    /// <summary>Music change applied when a tension state becomes active.</summary>
    public enum TensionMusicAction { KeepCurrent, Play, Stop }

    /// <summary>Configurable ambience states selected explicitly or from flags.</summary>
    [CreateAssetMenu(
        fileName = "HorrorTensionDefinition",
        menuName = "Quiet Static Toolkit/Horror/Tension Definition")]
    public sealed class HorrorTensionDefinition : ScriptableObject
    {
        /// <summary>One selectable ambience configuration and its flag-based activation rule.</summary>
        [Serializable]
        public sealed class State
        {
            [Tooltip("Stable state ID used by UnityEvents and runtime lookups.")]
            [SerializeField] private string id;
            [Tooltip("Higher-priority matching flag states win.")]
            [SerializeField] private int priority;
            [Tooltip("Flags that make this state eligible during automatic evaluation.")]
            [SerializeField] private FlagRequirement activationRequirement = new();

            [Header("Music")]
            [Tooltip("Whether entering this state keeps, replaces, or stops current music.")]
            [SerializeField] private TensionMusicAction musicAction = TensionMusicAction.KeepCurrent;
            [Tooltip("Track used when Music Action is Play.")]
            [SerializeField] private AudioClip music;
            [Tooltip("Use MusicManager's configured crossfade instead of changing immediately.")]
            [SerializeField] private bool fadeMusic = true;

            [Header("Entry SFX")]
            [Tooltip("One 2D clip selected randomly whenever this state is newly entered.")]
            [SerializeField] private AudioClip[] entrySfx;
            [Tooltip("Normalized volume used for the randomly selected entry sound.")]
            [Range(0f, 1f)]
            [SerializeField] private float entrySfxVolume = 1f;

            [Header("Overlay")]
            [Tooltip("Tint applied to the controller's optional fullscreen overlay image.")]
            [SerializeField] private Color overlayColor = Color.black;
            [Tooltip("Target opacity for the fullscreen tension overlay.")]
            [Range(0f, 1f)] [SerializeField] private float overlayAlpha;
            [Tooltip("Unscaled seconds used to blend from the current overlay opacity.")]
            [Min(0f)] [SerializeField] private float overlayTransitionDuration = 1f;

            /// <summary>Gets the normalized stable state ID.</summary>
            public string Id => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            /// <summary>Gets the authoring priority used to break matching-rule ties.</summary>
            public int Priority => priority;
            /// <summary>Gets the flag requirement used during automatic selection.</summary>
            public FlagRequirement ActivationRequirement => activationRequirement;
            /// <summary>Gets the music operation applied when this state is entered.</summary>
            public TensionMusicAction MusicAction => musicAction;
            /// <summary>Gets the music clip used by the Play operation.</summary>
            public AudioClip Music => music;
            /// <summary>Gets whether music changes use the manager's configured crossfade.</summary>
            public bool FadeMusic => fadeMusic;
            /// <summary>Gets the candidate entry sounds.</summary>
            public IReadOnlyList<AudioClip> EntrySfx => entrySfx ?? Array.Empty<AudioClip>();
            /// <summary>Gets the normalized entry-sound volume.</summary>
            public float EntrySfxVolume => entrySfxVolume;
            /// <summary>Gets the target fullscreen overlay tint.</summary>
            public Color OverlayColor => overlayColor;
            /// <summary>Gets the target fullscreen overlay opacity.</summary>
            public float OverlayAlpha => overlayAlpha;
            /// <summary>Gets the unscaled overlay blend duration in seconds.</summary>
            public float OverlayTransitionDuration => overlayTransitionDuration;

            /// <summary>Returns whether this state's configured flag rule is currently met.</summary>
            public bool MatchesFlags(FlagManager flags = null) =>
                activationRequirement != null &&
                activationRequirement.IsConfigured &&
                activationRequirement.IsMet(flags);
        }

        [Tooltip("Optional state used before any flag rule matches.")]
        [SerializeField] private string defaultStateId;
        [Tooltip("All selectable tension states. Matching states use the highest priority.")]
        [SerializeField] private State[] states;

        /// <summary>Gets the normalized fallback state ID.</summary>
        public string DefaultStateId => string.IsNullOrWhiteSpace(defaultStateId)
            ? string.Empty : defaultStateId.Trim();
        /// <summary>Gets all configured tension states.</summary>
        public IReadOnlyList<State> States => states ?? Array.Empty<State>();

        /// <summary>Finds a tension state by exact stable ID.</summary>
        public State FindState(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId) || states == null) return null;
            string normalized = stateId.Trim();
            foreach (State state in states)
                if (state != null && state.Id == normalized) return state;
            return null;
        }

        /// <summary>Returns the highest-priority matching state or configured default.</summary>
        public State SelectState(FlagManager flags = null)
        {
            State selected = null;
            if (states != null)
            {
                foreach (State state in states)
                {
                    if (state != null && state.MatchesFlags(flags) &&
                        (selected == null || state.Priority > selected.Priority))
                        selected = state;
                }
            }
            return selected ?? FindState(DefaultStateId);
        }
    }
}
