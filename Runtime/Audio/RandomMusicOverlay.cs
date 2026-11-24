using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>Plays a pool of random clips from a dedicated 2D or 3D scene AudioSource alongside a music cue.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Quiet Static Toolkit/Audio/Random Music Overlay")]
    public sealed class RandomMusicOverlay : MonoBehaviour
    {
        [Tooltip("Clips to choose from. Empty slots are ignored.")]
        [SerializeField] private AudioClip[] clips;

        [Tooltip("Shortest silence before the first clip and between clips, in seconds.")]
        [SerializeField, Min(0f)] private float minimumDelay = 5f;

        [Tooltip("Longest silence before the first clip and between clips, in seconds.")]
        [SerializeField, Min(0f)] private float maximumDelay = 15f;

        private AudioSource overlaySource;
        private AudioRequestChannel musicChannel;
        private AudioSource musicSource;
        private AudioClip musicClip;
        private float remainingDelay;
        private bool playingOverlay;
        private bool pausedOverlay;

        private void Awake() => overlaySource = GetComponent<AudioSource>();

        private void OnEnable() => PickDelay();

        private void OnDisable() => StopPlayback();

        /// <summary>Arms the emitter for a cue and starts its delay when that track becomes audible.</summary>
        public void Begin(AudioRequestChannel channel, AudioClip track)
        {
            StopPlayback();
            musicChannel = channel;
            musicSource = null;
            musicClip = track;
            PickDelay();
        }

        /// <summary>Cancels the cue and stops any overlay played by this emitter.</summary>
        public void End()
        {
            musicChannel = null;
            musicSource = null;
            musicClip = null;
            StopPlayback();
        }

        private void Update()
        {
            if (musicClip == null || musicChannel == null || overlaySource == null || !overlaySource.isActiveAndEnabled)
                return;

            if (musicSource == null)
                musicSource = musicChannel.GetMusicSource();

            if (musicSource == null || musicSource == overlaySource || musicSource.clip != musicClip)
            {
                StopPlayback();
                return;
            }

            if (!musicSource.isPlaying || (AudioListener.pause && !musicSource.ignoreListenerPause))
            {
                if (playingOverlay && !pausedOverlay)
                {
                    overlaySource.Pause();
                    pausedOverlay = true;
                }
                return;
            }

            if (AudioListener.pause && !overlaySource.ignoreListenerPause)
                return;

            if (pausedOverlay)
            {
                overlaySource.UnPause();
                pausedOverlay = false;
            }

            if (overlaySource.isPlaying)
                return;

            playingOverlay = false;
            remainingDelay -= Time.unscaledDeltaTime;
            if (remainingDelay > 0f)
                return;

            PickDelay();
            AudioClip clip = PickClip();
            if (clip != null)
            {
                overlaySource.PlayOneShot(clip);
                playingOverlay = true;
            }
        }

        private void StopPlayback()
        {
            if (playingOverlay && overlaySource != null)
                overlaySource.Stop();
            playingOverlay = false;
            pausedOverlay = false;
        }

        private void PickDelay()
        {
            float minimum = Mathf.Max(0f, minimumDelay);
            remainingDelay = Random.Range(minimum, Mathf.Max(minimum, maximumDelay));
        }

        private AudioClip PickClip()
        {
            AudioClip selected = null;
            int validCount = 0;
            if (clips == null)
                return null;

            foreach (AudioClip clip in clips)
            {
                if (clip == null)
                    continue;

                if (Random.Range(0, ++validCount) == 0)
                    selected = clip;
            }

            return selected;
        }
    }
}
