using System;
using System.Collections;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic
{
    /// <summary>
    /// Plays looping background music and optionally changes tracks based on global game state.
    /// </summary>
    /// <remarks>
    /// This component is intentionally generic. It does not know about title screens,
    /// gameplay, dialogue, cutscenes, pause menus, or game-over flow.
    ///
    /// Configure state-to-track mappings in the Inspector, or call the public playback
    /// methods directly from other systems.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public class MusicManager : ToolkitSingleton<MusicManager>
    {
        /// <summary>
        /// Associates a global state identifier with a music clip.
        /// </summary>
        [Serializable]
        public class StateMusicEntry
        {
            [Tooltip("Global game state that should use this music track.")]
            [SerializeField] private string stateId;

            [Tooltip("Music clip played when the configured state becomes active.")]
            [SerializeField] private AudioClip musicClip;

            [Tooltip("If true, entering this state leaves the currently playing music unchanged.")]
            [SerializeField] private bool keepCurrentMusic;

            /// <summary>
            /// Gets the state identifier associated with this music entry.
            /// </summary>
            public string StateId => stateId;

            /// <summary>
            /// Gets the music clip associated with this music entry.
            /// </summary>
            public AudioClip MusicClip => musicClip;

            /// <summary>
            /// Gets whether entering this state should preserve current music.
            /// </summary>
            public bool KeepCurrentMusic => keepCurrentMusic;
        }

        [Serializable]
        public class AudioClipUnityEvent : UnityEvent<AudioClip>
        {
        }

        /// <summary>
        /// Raised when a new music clip begins playing.
        /// </summary>
        public static event Action<AudioClip> OnMusicStarted;

        /// <summary>
        /// Raised when music playback stops.
        /// </summary>
        public static event Action OnMusicStopped;

        [Header("Audio Source")]
        [Tooltip("Audio source used for background music. If empty, one is found on this GameObject.")]
        [SerializeField] private AudioSource musicSource;

        [Header("State Music")]
        [Tooltip("Optional music rules applied when the global game state changes.")]
        [SerializeField] private StateMusicEntry[] stateMusicEntries;

        [Header("Settings")]
        [Tooltip("Default music volume from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float defaultVolume = 0.7f;

        [Tooltip("Default transition duration in unscaled seconds.")]
        [Min(0f)]
        [SerializeField] private float fadeDuration = 1.5f;

        [Tooltip("Optional clip played automatically if no state music mapping is found at startup.")]
        [SerializeField] private AudioClip startupMusic;

        [Tooltip("Whether music should begin automatically when this manager starts.")]
        [SerializeField] private bool playOnStart;

        [Tooltip("Whether mapped state changes should use faded transitions.")]
        [SerializeField] private bool useFadeOnStateChange = true;

        [Header("Unity Events")]
        [Tooltip("Invoked when a new music clip begins playing.")]
        [SerializeField] private AudioClipUnityEvent onMusicStarted;

        [Tooltip("Invoked when music playback stops.")]
        [SerializeField] private UnityEvent onMusicStopped;

        /// <summary>
        /// Current fade coroutine, if a transition is active.
        /// </summary>
        private Coroutine fadeRoutine;

        /// <summary>
        /// Gets the music source managed by this component.
        /// </summary>
        public AudioSource MusicSource => musicSource;

        /// <summary>
        /// Gets the currently assigned music clip.
        /// </summary>
        public AudioClip CurrentClip => musicSource != null ? musicSource.clip : null;

        /// <summary>
        /// Gets whether music is currently playing.
        /// </summary>
        public bool IsPlaying => musicSource != null && musicSource.isPlaying;

        /// <summary>
        /// Initializes the singleton and configures the audio source for 2D looping music.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
            }

            if (musicSource == null)
            {
                Debug.LogWarning(
                    $"{nameof(MusicManager)} could not find an {nameof(AudioSource)}.",
                    this
                );
                return;
            }

            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = defaultVolume;
        }

        private void OnEnable()
        {
            GameStateManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameStateManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        /// <summary>
        /// Waits one frame so other persistent managers can finish initializing.
        /// </summary>
        private IEnumerator Start()
        {
            yield return null;

            bool playedMappedStateMusic = false;

            if (GameStateManager.Instance != null)
            {
                playedMappedStateMusic = TryPlayMusicForState(
                    GameStateManager.Instance.CurrentState
                );
            }

            if (!playedMappedStateMusic && playOnStart && startupMusic != null)
            {
                PlayMusic(startupMusic);
            }
        }

        /// <summary>
        /// Plays a music clip immediately.
        /// </summary>
        /// <param name="clip">Music clip to play.</param>
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || musicSource == null)
            {
                return;
            }

            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                return;
            }

            StopFadeRoutine();

            musicSource.clip = clip;
            musicSource.volume = defaultVolume;
            musicSource.Play();

            OnMusicStarted?.Invoke(clip);
            onMusicStarted?.Invoke(clip);
        }

        /// <summary>
        /// Crossfades by fading current music out, swapping the clip, then fading in.
        /// </summary>
        /// <param name="clip">Music clip to play.</param>
        public void PlayMusicWithFade(AudioClip clip)
        {
            if (clip == null || musicSource == null)
            {
                return;
            }

            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                return;
            }

            StopFadeRoutine();
            fadeRoutine = StartCoroutine(FadeToNewMusic(clip));
        }

        /// <summary>
        /// Stops music immediately.
        /// </summary>
        public void StopMusic()
        {
            if (musicSource == null)
            {
                return;
            }

            StopFadeRoutine();

            musicSource.Stop();
            musicSource.clip = null;
            musicSource.volume = defaultVolume;

            OnMusicStopped?.Invoke();
            onMusicStopped?.Invoke();
        }

        /// <summary>
        /// Fades current music out and stops playback.
        /// </summary>
        public void StopMusicWithFade()
        {
            if (musicSource == null)
            {
                return;
            }

            StopFadeRoutine();

            if (!musicSource.isPlaying)
            {
                return;
            }

            fadeRoutine = StartCoroutine(FadeOutAndStop());
        }

        /// <summary>
        /// Updates the default music volume.
        /// </summary>
        /// <param name="volume">New volume from 0 to 1.</param>
        public void SetVolume(float volume)
        {
            defaultVolume = Mathf.Clamp01(volume);

            if (musicSource != null && fadeRoutine == null)
            {
                musicSource.volume = defaultVolume;
            }
        }

        /// <summary>
        /// Attempts to play music configured for a given state.
        /// </summary>
        /// <param name="stateId">Global state identifier to evaluate.</param>
        /// <returns>True if an entry was found and handled.</returns>
        public bool TryPlayMusicForState(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId) || stateMusicEntries == null)
            {
                return false;
            }

            foreach (StateMusicEntry entry in stateMusicEntries)
            {
                if (entry == null ||
                    !string.Equals(
                        entry.StateId?.Trim(),
                        stateId.Trim(),
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }

                if (entry.KeepCurrentMusic)
                {
                    return true;
                }

                if (entry.MusicClip == null)
                {
                    StopMusicWithFade();
                    return true;
                }

                if (useFadeOnStateChange)
                {
                    PlayMusicWithFade(entry.MusicClip);
                }
                else
                {
                    PlayMusic(entry.MusicClip);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles global state changes by checking configured music mappings.
        /// </summary>
        private void HandleGameStateChanged(
            string previousState,
            string newState
        )
        {
            TryPlayMusicForState(newState);
        }

        /// <summary>
        /// Fades out current music, swaps clips, then fades the new clip in.
        /// </summary>
        private IEnumerator FadeToNewMusic(AudioClip newClip)
        {
            if (musicSource.isPlaying)
            {
                yield return FadeVolumeTo(0f);
            }

            musicSource.Stop();
            musicSource.clip = newClip;
            musicSource.volume = 0f;
            musicSource.Play();

            OnMusicStarted?.Invoke(newClip);
            onMusicStarted?.Invoke(newClip);

            yield return FadeVolumeTo(defaultVolume);

            fadeRoutine = null;
        }

        /// <summary>
        /// Fades current music out and clears the active clip.
        /// </summary>
        private IEnumerator FadeOutAndStop()
        {
            yield return FadeVolumeTo(0f);

            musicSource.Stop();
            musicSource.clip = null;
            musicSource.volume = defaultVolume;

            fadeRoutine = null;

            OnMusicStopped?.Invoke();
            onMusicStopped?.Invoke();
        }

        /// <summary>
        /// Smoothly changes music source volume using unscaled time.
        /// </summary>
        private IEnumerator FadeVolumeTo(float targetVolume)
        {
            float startVolume = musicSource.volume;

            if (fadeDuration <= 0f)
            {
                musicSource.volume = targetVolume;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(elapsed / fadeDuration);

                musicSource.volume = Mathf.Lerp(
                    startVolume,
                    targetVolume,
                    progress
                );

                yield return null;
            }

            musicSource.volume = targetVolume;
        }

        /// <summary>
        /// Stops an in-progress fade transition.
        /// </summary>
        private void StopFadeRoutine()
        {
            if (fadeRoutine == null)
            {
                return;
            }

            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }
}