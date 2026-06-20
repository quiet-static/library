using System;
using System.Collections;
using System.Collections.Generic;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic
{
    /// <summary>
    /// Coordinates the generic lifecycle of a cutscene.
    /// </summary>
    /// <remarks>
    /// This manager does not load scenes, change game states, set flags, spawn players,
    /// or decide what happens after a cutscene.
    ///
    /// Instead, it:
    /// - Tracks whether a cutscene is active
    /// - Optionally fades to black before beginning and ending
    /// - Raises C# events for global listeners
    /// - Raises UnityEvents for scene-local behavior
    ///
    /// Game-specific systems should subscribe to these events and handle transitions,
    /// scene loading, flags, dialogue, player spawning, and progression rules.
    /// </remarks>
    public class CutsceneManager : ToolkitSingleton<CutsceneManager>
    {
        /// <summary>
        /// Raised when any cutscene is ready to begin, after the optional fade-to-black.
        /// </summary>
        public static event Action<string> OnCutsceneStarted;

        /// <summary>
        /// Raised when any cutscene is ending, after the optional fade-to-black.
        /// </summary>
        /// <remarks>
        /// At this point, <see cref="IsPlayingCutscene"/> has already been set to false,
        /// so listeners may safely begin another cutscene if needed.
        /// </remarks>
        public static event Action<string> OnCutsceneEnded;

        /// <summary>
        /// Raised whenever the active cutscene state changes.
        /// First parameter is the cutscene id; second parameter is whether it is active.
        /// </summary>
        public static event Action<string, bool> OnCutsceneStateChanged;

        [Serializable]
        public class StringEvent : UnityEvent<string>
        {
        }

        [Header("Fade")]
        [Tooltip("Optional screen fader used before cutscenes begin and end.")]
        [SerializeField] private ScreenFader fader;

        [Tooltip("Whether this manager should fade to black before beginning and ending cutscenes.")]
        [SerializeField] private bool useScreenFade = true;

        [Tooltip("How long to wait after requesting a fade to black before continuing.")]
        [Min(0f)]
        [SerializeField] private float fadeWaitDuration = 1f;

        [Header("Replay Rules")]
        [Tooltip("When enabled, cutscene IDs that have already completed cannot be played again until cleared manually.")]
        [SerializeField] private bool preventReplayByDefault = true;

        [Header("Unity Events")]
        [Tooltip("Invoked after the optional fade-to-black when a cutscene begins.")]
        [SerializeField] private StringEvent onCutsceneStarted;

        [Tooltip("Invoked after the optional fade-to-black when a cutscene ends.")]
        [SerializeField] private StringEvent onCutsceneEnded;

        /// <summary>
        /// Gets the currently active cutscene identifier.
        /// Empty when no cutscene is active.
        /// </summary>
        public string CurrentCutsceneId { get; private set; }

        /// <summary>
        /// Gets whether a cutscene is currently active.
        /// </summary>
        public bool IsPlayingCutscene { get; private set; }

        /// <summary>
        /// Stores completed cutscene IDs when replay prevention is enabled.
        /// </summary>
        private readonly HashSet<string> playedCutsceneIds = new();

        /// <summary>
        /// Prevents duplicate end requests while the ending coroutine is active.
        /// </summary>
        private bool isEndingCutscene;

        private void Reset()
        {
            fader = FindAnyObjectByType<ScreenFader>();
        }

        /// <summary>
        /// Begins a cutscene using the provided identifier.
        /// </summary>
        /// <param name="cutsceneId">
        /// A non-empty identifier for the cutscene, such as "Intro", "HallwayScare",
        /// or "PrincipalOfficeEnding".
        /// </param>
        public void Play(string cutsceneId)
        {
            Play(cutsceneId, preventReplayByDefault);
        }

        /// <summary>
        /// Begins a cutscene using the provided identifier.
        /// </summary>
        /// <param name="cutsceneId">Identifier for the cutscene.</param>
        /// <param name="preventReplay">
        /// Whether this specific cutscene should be blocked after it has completed once.
        /// </param>
        public void Play(string cutsceneId, bool preventReplay)
        {
            if (string.IsNullOrWhiteSpace(cutsceneId))
            {
                GameLogger.Warning(
                    "Play",
                    this,
                    $"{nameof(CutsceneManager)} cannot play a cutscene with an empty ID."
                );
                return;
            }

            if (IsPlayingCutscene)
            {
                GameLogger.Warning(
                    "Play",
                    this,
                    $"{nameof(CutsceneManager)} cannot start '{cutsceneId}' because " +
                    $"'{CurrentCutsceneId}' is already active."
                );
                return;
            }

            if (preventReplay && playedCutsceneIds.Contains(cutsceneId))
            {
                return;
            }

            StartCoroutine(BeginCutsceneRoutine(cutsceneId, preventReplay));
        }

        /// <summary>
        /// Ends the currently active cutscene.
        /// </summary>
        public void EndCurrentCutscene()
        {
            if (!IsPlayingCutscene || isEndingCutscene)
            {
                return;
            }

            StartCoroutine(EndCutsceneRoutine(CurrentCutsceneId));
        }

        /// <summary>
        /// Returns whether a cutscene ID has previously completed.
        /// </summary>
        public bool HasCutscenePlayed(string cutsceneId)
        {
            return !string.IsNullOrWhiteSpace(cutsceneId) &&
                   playedCutsceneIds.Contains(cutsceneId);
        }

        /// <summary>
        /// Allows a previously completed cutscene to be played again.
        /// </summary>
        public void MarkCutsceneAsUnplayed(string cutsceneId)
        {
            if (string.IsNullOrWhiteSpace(cutsceneId))
            {
                return;
            }

            playedCutsceneIds.Remove(cutsceneId);
        }

        /// <summary>
        /// Clears the replay history for every cutscene.
        /// Useful when starting a new game or resetting progression.
        /// </summary>
        public void ClearPlayedCutscenes()
        {
            playedCutsceneIds.Clear();
        }

        /// <summary>
        /// Runs the opening fade and announces that the cutscene should begin.
        /// </summary>
        private IEnumerator BeginCutsceneRoutine(
            string cutsceneId,
            bool preventReplay
        )
        {
            IsPlayingCutscene = true;
            isEndingCutscene = false;
            CurrentCutsceneId = cutsceneId;

            OnCutsceneStateChanged?.Invoke(cutsceneId, true);

            yield return FadeToBlackRoutine();

            if (preventReplay)
            {
                playedCutsceneIds.Add(cutsceneId);
            }

            OnCutsceneStarted?.Invoke(cutsceneId);
            onCutsceneStarted?.Invoke(cutsceneId);
        }

        /// <summary>
        /// Runs the ending fade, announces that the cutscene ended, and clears state.
        /// </summary>
        private IEnumerator EndCutsceneRoutine(string cutsceneId)
        {
            isEndingCutscene = true;

            yield return FadeToBlackRoutine();

            // Clear internal state before events fire.
            // This lets listeners safely start a chained cutscene.
            IsPlayingCutscene = false;
            isEndingCutscene = false;
            CurrentCutsceneId = string.Empty;

            OnCutsceneStateChanged?.Invoke(cutsceneId, false);
            OnCutsceneEnded?.Invoke(cutsceneId);
            onCutsceneEnded?.Invoke(cutsceneId);

            FadeToClear();
        }

        /// <summary>
        /// Fades the screen to black and waits for the configured duration.
        /// </summary>
        private IEnumerator FadeToBlackRoutine()
        {
            if (!useScreenFade || fader == null)
            {
                yield break;
            }

            fader.FadeToBlack();

            if (fadeWaitDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(fadeWaitDuration);
            }

            fader.SetBlackInstant();
        }

        /// <summary>
        /// Requests a fade from black back to clear.
        /// </summary>
        private void FadeToClear()
        {
            if (!useScreenFade || fader == null)
            {
                return;
            }

            fader.FadeToClear();
        }
    }
}