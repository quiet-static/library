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

        [Tooltip("Minimum distance the sound should travel")]
        [SerializeField] private float minDistance = 1f;

        [Tooltip("Maximum distance the sound should travel")]
        [SerializeField] private float maxDistance = 15f;

        [Tooltip("How loud the sound should be when it plays")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        /// <summary>
        /// When playing clips in order, this keeps track of which clip should be played next.
        /// </summary>
        private int currIndex;

        private void Awake()
        {
            currIndex = 0;
        }

        public void Play()
        {
            switch (howToPlay)
            {
                case HowToPlay.InOrder:
                    PlayInOrderClip();
                    break;
                case HowToPlay.Random:
                    PlayRandomClip();
                    break;
                default:
                    PlayMainClip();
                    break;
            }
        }


        private void PlayMainClip()
        {
            if (clip == null || SfxManager.Instance == null)
            {
                return;
            }

            SfxManager.Instance.PlayAtPosition(
                clip,
                objectTransform.position,
                minDistance,
                maxDistance,
                volume
            );
        }

        private void PlayInOrderClip()
        {
            AudioClip clip = clips[currIndex];
            currIndex++;

            if (currIndex > clips.Length - 1)
            {
                currIndex = 0;
            }

            if (clip == null || SfxManager.Instance == null)
            {
                return;
            }

            SfxManager.Instance.PlayAtPosition(
                clip,
                objectTransform.position,
                1f,
                15f,
                volume
            );
        }

        private void PlayRandomClip()
        {
            AudioClip clip = GetRandomClip();

            if (clip == null || SfxManager.Instance == null)
            {
                return;
            }

            SfxManager.Instance.PlayAtPosition(
                clip,
                objectTransform.position,
                1f,
                15f,
                volume
            );
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