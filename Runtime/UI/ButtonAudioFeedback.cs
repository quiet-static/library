using UnityEngine;
using UnityEngine.Audio;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>Plays button feedback from the persistent audio scene.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/UI/Button Audio Feedback")]
    public sealed class ButtonAudioFeedback : MonoBehaviour
    {
        [Tooltip("The click played by audible buttons when activated.")]
        [SerializeField] private AudioClip click;

        [Tooltip("Mixer group controlling the volume of button feedback.")]
        [SerializeField] private AudioMixerGroup output;

        private AudioSource source;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.outputAudioMixerGroup = output;
        }

        private void OnEnable() => AudibleButton.Activated += PlayClick;

        private void OnDisable() => AudibleButton.Activated -= PlayClick;

        private void PlayClick()
        {
            if (click != null)
                source.PlayOneShot(click);
        }
    }
}
