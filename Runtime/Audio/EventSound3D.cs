using UnityEngine;

namespace QuietStatic.Animation.Audio
{
    /// <summary>
    /// Plays a one-shot 3D sound from this GameObject and destroys the GameObject
    /// once the sound has finished playing.
    /// </summary>
    /// <remarks>
    /// This component is intended for temporary world-space sound effects such as
    /// jumpscares, object impacts, door sounds, environmental stingers, or other
    /// event-driven audio that should come from a specific location in the scene.
    ///
    /// Because this script destroys its GameObject after playback ends, it works well
    /// on spawned audio prefabs that exist only long enough to play a sound.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public class EventSound3D : MonoBehaviour
    {
        [Header("Audio Source Reference")]
        [Tooltip("The AudioSource used to play this 3D event sound. If left empty, this is automatically found on the same GameObject during Awake.")]
        [SerializeField] private AudioSource audioSrc;

        /// <summary>
        /// Automatically fills the AudioSource reference when the component is added
        /// or reset in the Unity Inspector.
        /// </summary>
        private void Reset()
        {
            audioSrc = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Ensures this component has a valid AudioSource reference before playback.
        /// </summary>
        private void Awake()
        {
            if (audioSrc == null)
            {
                audioSrc = GetComponent<AudioSource>();
            }
        }

        /// <summary>
        /// Plays the supplied audio clip as a fully 3D sound from this GameObject's position.
        /// </summary>
        /// <param name="clip">The audio clip to play. If null, playback is skipped and a warning is logged.</param>
        /// <param name="minDistance">The distance from the sound source where the audio begins to attenuate.</param>
        /// <param name="maxDistance">The maximum distance at which the sound can still be heard.</param>
        /// <param name="volume">The playback volume for this sound. A value of 1 is full volume.</param>
        public void Play(
            AudioClip clip,
            float minDistance = 5f,
            float maxDistance = 100f,
            float volume = 1f)
        {
            if (clip == null)
            {
                Debug.LogWarning($"{nameof(EventSound3D)} tried to play a null AudioClip.", this);
                return;
            }

            if (audioSrc == null)
            {
                Debug.LogWarning($"{nameof(EventSound3D)} has no AudioSource assigned.", this);
                return;
            }

            audioSrc.clip = clip;
            audioSrc.volume = volume;
            audioSrc.spatialBlend = 1f; // 1 = fully 3D.
            audioSrc.minDistance = minDistance;
            audioSrc.maxDistance = maxDistance;

            audioSrc.Play();
        }

        /// <summary>
        /// Destroys this temporary sound GameObject once the AudioSource has stopped playing.
        /// </summary>
        private void Update()
        {
            if (audioSrc == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!audioSrc.isPlaying)
            {
                Destroy(gameObject);
            }
        }
    }
}
