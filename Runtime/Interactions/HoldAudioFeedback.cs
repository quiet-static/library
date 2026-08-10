using QuietStatic.Toolkit.Audio;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Plays looping audio only while a HoldInteractable is actively receiving
    /// held input. Audio stops on release, cancellation, disable, or completion.
    /// </summary>
    [RequireComponent(typeof(HoldInteractable))]
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Hold Audio Feedback")]
    public sealed class HoldAudioFeedback : MonoBehaviour
    {
        [Tooltip("Hold interaction that controls playback. Defaults to this object.")]
        [SerializeField] private HoldInteractable holdInteractable;

        [Tooltip("Configured audio player started and stopped with held input.")]
        [SerializeField] private AudioEventPlayer audioPlayer;

        private void Reset()
        {
            holdInteractable = GetComponent<HoldInteractable>();
            audioPlayer = GetComponent<AudioEventPlayer>();
        }

        private void Awake()
        {
            if (holdInteractable == null)
            {
                holdInteractable = GetComponent<HoldInteractable>();
            }

            if (audioPlayer == null)
            {
                audioPlayer = GetComponent<AudioEventPlayer>();
            }
        }

        private void OnEnable()
        {
            holdInteractable.HoldBegan += HandleHoldBegan;
            holdInteractable.HoldEnded += HandleHoldEnded;
        }

        private void OnDisable()
        {
            if (holdInteractable != null)
            {
                holdInteractable.HoldBegan -= HandleHoldBegan;
                holdInteractable.HoldEnded -= HandleHoldEnded;
            }

            audioPlayer?.Stop();
        }

        private void HandleHoldBegan() => audioPlayer?.PlayContinuously();

        private void HandleHoldEnded() => audioPlayer?.Stop();
    }
}
