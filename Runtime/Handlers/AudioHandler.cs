using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    public class AudioHandler : MonoBehaviour
    {
        public void PlayMusic(AudioClip clip)
        {
            MusicManager.Instance.PlayMusic(clip);
        }

        public void StopMusic()
        {
            MusicManager.Instance.StopMusic();
        }

        public void PlayWithFade(AudioClip clip)
        {
            MusicManager.Instance.PlayMusicWithFade(clip);
        }

        public void StopWithFade()
        {
            MusicManager.Instance.StopMusicWithFade();
        }

        public void SetVolume(float volume)
        {
            MusicManager.Instance.SetVolume(volume);
        }
    }
}