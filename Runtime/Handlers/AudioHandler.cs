using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>
    /// Exposes common <see cref="MusicManager"/> operations to Inspector UnityEvents.
    /// </summary>
    /// <remarks>
    /// Place this on a persistent handler object. Scene buttons, triggers, and timelines can
    /// reference these methods without holding a direct reference to the music manager.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Handlers/Audio Handler")]
    public class AudioHandler : MonoBehaviour
    {
        /// <summary>Starts a music clip immediately.</summary>
        public void PlayMusic(AudioClip clip)
        {
            MusicManager.Instance.PlayMusic(clip);
        }

        /// <summary>Stops the current music immediately.</summary>
        public void StopMusic()
        {
            MusicManager.Instance.StopMusic();
        }

        /// <summary>Transitions to a music clip using the manager's configured fade.</summary>
        public void PlayWithFade(AudioClip clip)
        {
            MusicManager.Instance.PlayMusicWithFade(clip);
        }

        /// <summary>Stops the current music using the manager's configured fade.</summary>
        public void StopWithFade()
        {
            MusicManager.Instance.StopMusicWithFade();
        }

        /// <summary>Sets normalized music volume.</summary>
        /// <param name="volume">Linear volume, normally from zero to one.</param>
        public void SetVolume(float volume)
        {
            MusicManager.Instance.SetVolume(volume);
        }
    }
}
