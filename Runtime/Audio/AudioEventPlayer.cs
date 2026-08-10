using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>
    /// Used for emitting sounds from game objects upon interaction, or otherwise
    /// </summary>
    public class AudioEventPlayer : MonoBehaviour
    {
        /// <summary>
        /// Wrapper for picking in Unity's Inspector how the clips should be played
        /// </summary>
        enum HowToPlay
        {
            None,
            InOrder,
            Random
        }

        [Header("Clips")]
        [Tooltip("Main audio to play")]
        [SerializeField] private AudioClip clip;

        [Tooltip("Optional audio clips to choose from randomly to play")]
        [SerializeField] private AudioClip[] clips;

        [Header("Playing Clips")]
        [Tooltip("Tagging if there are multiple clips")]
        [SerializeField] private bool hasMultipleClips;

        [Tooltip("How multiple clips should be played")]
        [SerializeField] private HowToPlay howToPlay;

        [Header("3D Audio")]
        [Tooltip("Where the sound/s should originate")]
        [SerializeField] private Transform objectTransform;

        [Tooltip("Distance at which the sound plays at full configured volume.")]
        [Min(0f)]
        [SerializeField] private float minDistance = 1f;

        [Tooltip("Distance beyond which the sound is no longer audible.")]
        [Min(0f)]
        [SerializeField] private float maxDistance = 15f;

        [Tooltip("How loud the sound should be when it plays")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        /// <summary>
        /// When playing clips in order, this keeps track of which clip should be played next.
        /// </summary>
        private int currIndex;

        /// <summary>
        /// The temporary sound instance created by <see cref="PlayContinuously"/>.
        /// </summary>
        private EventSound3D continuousSound;

        private void Awake()
        {
            currIndex = 0;
        }

        private void OnDestroy()
        {
            Stop();
        }

        /// <summary>
        /// Plays one configured clip using the selected ordering mode.
        /// </summary>
        public void Play()
        {
            AudioClip selectedClip = SelectClip();
            PlayClip(selectedClip, false);
        }

        /// <summary>
        /// Plays the configured audio continuously until <see cref="Stop"/> is called.
        /// Calling this again replaces the currently playing continuous sound.
        /// </summary>
        public void PlayContinuously()
        {
            Stop();

            AudioClip selectedClip = SelectClip();
            continuousSound = PlayClip(selectedClip, true);
        }

        /// <summary>
        /// Stops the continuous sound and destroys its temporary audio prefab.
        /// This method is parameterless so it can be assigned directly to a UnityEvent.
        /// </summary>
        public void Stop()
        {
            if (continuousSound == null)
            {
                return;
            }

            continuousSound.Stop();
            continuousSound = null;
        }

        private AudioClip SelectClip()
        {
            switch (howToPlay)
            {
                case HowToPlay.InOrder:
                    return GetInOrderClip();
                case HowToPlay.Random:
                    return GetRandomClip();
                default:
                    return clip;
            }
        }

        private EventSound3D PlayClip(AudioClip selectedClip, bool loop)
        {
            if (selectedClip == null || SfxManager.Instance == null)
            {
                return null;
            }

            Transform origin = objectTransform != null ? objectTransform : transform;

            return SfxManager.Instance.PlayAtPosition(
                selectedClip,
                origin.position,
                minDistance,
                maxDistance,
                volume,
                loop
            );
        }

        private AudioClip GetInOrderClip()
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            AudioClip selectedClip = clips[currIndex];
            currIndex = (currIndex + 1) % clips.Length;

            return selectedClip;
        }

        /// <summary>
        /// Returns a random non-null audio clip.
        /// </summary>
        private AudioClip GetRandomClip()
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];

                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
