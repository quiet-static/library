using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace QuietStatic.Toolkit.Animation.Environment
{
    /// <summary>Coordinates binary TV power, video playback, and optional Animator presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class TVPowerController : MonoBehaviour
    {
        [Header("Power State")]
        [Tooltip("Whether the TV begins powered on when this scene loads.")]
        [SerializeField] private bool startsPoweredOn;

        [Tooltip("Root object containing the visible screen and video player.")]
        [SerializeField] private GameObject screen;

        [Tooltip("Video player started and stopped with TV power.")]
        [SerializeField] private VideoPlayer videoPlayer;

        [Header("Binary Animation")]
        [Tooltip("Animator using a BinarySwitch-compatible controller or Animator Override Controller.")]
        [SerializeField] private Animator animator;

        [Tooltip("Animator trigger representing the powered-on state.")]
        [SerializeField] private string powerOnTrigger = "Open";

        [Tooltip("Animator trigger representing the powered-off state.")]
        [SerializeField] private string powerOffTrigger = "Close";

        [Header("Events")]
        [Tooltip("Invoked after the TV powers on.")]
        [SerializeField] private UnityEvent onPoweredOn;

        [Tooltip("Invoked after the TV powers off.")]
        [SerializeField] private UnityEvent onPoweredOff;

        /// <summary>Gets whether the TV is currently powered on.</summary>
        public bool IsPoweredOn { get; private set; }

        private void Start()
        {
            ApplyPower(startsPoweredOn, false);
        }

        /// <summary>Toggles the TV between its powered-on and powered-off states.</summary>
        public void TogglePower()
        {
            ApplyPower(!IsPoweredOn, true);
        }

        /// <summary>Powers the TV on.</summary>
        public void TurnOn()
        {
            ApplyPower(true, true);
        }

        /// <summary>Powers the TV off.</summary>
        public void TurnOff()
        {
            ApplyPower(false, true);
        }

        private void ApplyPower(bool poweredOn, bool notify)
        {
            IsPoweredOn = poweredOn;

            if (animator != null)
            {
                string trigger = poweredOn ? powerOnTrigger : powerOffTrigger;
                if (!string.IsNullOrWhiteSpace(trigger))
                {
                    animator.SetTrigger(trigger.Trim());
                }
            }

            if (screen != null)
            {
                screen.SetActive(poweredOn);
            }

            if (videoPlayer != null)
            {
                if (poweredOn)
                {
                    videoPlayer.Play();
                }
                else
                {
                    videoPlayer.Stop();
                }
            }

            if (!notify)
            {
                return;
            }

            if (poweredOn)
            {
                onPoweredOn?.Invoke();
            }
            else
            {
                onPoweredOff?.Invoke();
            }
        }
    }
}
