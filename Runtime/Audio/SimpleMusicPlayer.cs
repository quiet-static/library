using QuietStatic.Toolkit.Core;
using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>
    /// Provides a small, globally accessible music player for looping background music.
    /// </summary>
    /// <remarks>
    /// This class is intentionally simple: it owns one <see cref="AudioSource"/>, plays one looping
    /// music clip at a time, and exposes basic playback and volume controls for other systems.
    ///
    /// Because it inherits from <see cref="ToolkitSingleton{T}"/>, other scripts can access the active
    /// instance without needing a direct scene reference, assuming the singleton has been initialized.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public class SimpleMusicPlayer : ToolkitSingleton<SimpleMusicPlayer>
    {
        [Header("References")]
        [Tooltip("Audio source used to play music clips. If left empty, this is automatically found on the same GameObject.")]
        [SerializeField] private AudioSource source;

        [Header("Startup Behavior")]
        [Tooltip("If true, the assigned AudioSource clip will begin playing automatically when this object starts.")]
        [SerializeField] private bool playOnStart;

        /// <summary>
        /// Initializes the singleton and finds the local <see cref="AudioSource"/> if one was not assigned.
        /// </summary>
        /// <remarks>
        /// This runs before <see cref="Start"/>. Calling <c>base.Awake()</c> preserves the singleton setup
        /// handled by <see cref="ToolkitSingleton{T}"/>.
        /// </remarks>
        protected override void Awake()
        {
            base.Awake();

            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }
        }

        /// <summary>
        /// Automatically plays the currently assigned music clip when <see cref="playOnStart"/> is enabled.
        /// </summary>
        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        /// <summary>
        /// Resets inspector references when the component is first added or reset in the Unity Editor.
        /// </summary>
        /// <remarks>
        /// This does not run in the same way as gameplay initialization. It is only here to make the
        /// component easier to configure in the Inspector.
        /// </remarks>
        private void Reset()
        {
            source = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Plays the given clip as looping background music.
        /// </summary>
        /// <param name="clip">The music clip to play.</param>
        /// <remarks>
        /// If the same clip is already playing, this method does nothing. This prevents restarting the
        /// current track every time another script asks the music player to play the same clip.
        /// </remarks>
        public void PlayClip(AudioClip clip)
        {
            if (source == null || clip == null)
            {
                return;
            }

            if (source.clip == clip && source.isPlaying)
            {
                return;
            }

            source.clip = clip;
            source.loop = true;
            source.Play();
        }

        /// <summary>
        /// Plays the clip currently assigned to the music AudioSource.
        /// </summary>
        /// <remarks>
        /// This is useful when the AudioSource already has a clip assigned in the Inspector and another
        /// script only needs to begin playback.
        /// </remarks>
        public void Play()
        {
            if (source != null && source.clip != null)
            {
                source.Play();
            }
        }

        /// <summary>
        /// Stops the current music playback.
        /// </summary>
        /// <remarks>
        /// The assigned clip remains on the AudioSource, so calling <see cref="Play"/> afterward will play
        /// the same clip again from the beginning.
        /// </remarks>
        public void Stop()
        {
            if (source != null)
            {
                source.Stop();
            }
        }

        /// <summary>
        /// Sets the music volume.
        /// </summary>
        /// <param name="volume">
        /// Desired volume from 0 to 1. Values outside this range are clamped before being applied.
        /// </param>
        public void SetVolume(float volume)
        {
            if (source != null)
            {
                source.volume = Mathf.Clamp01(volume);
            }
        }
    }
}
