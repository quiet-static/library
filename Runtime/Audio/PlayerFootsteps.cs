using System;
using QuietStatic.Toolkit.Characters;
using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>
    /// Plays positional footstep sounds while the attached character is moving.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Controls the character's movement speed state.")]
        [SerializeField] private CharacterMotor motor;

        [Header("Footstep Clips")]
        [Tooltip("Possible footstep clips. One is selected at random per step.")]
        [SerializeField] private AudioClip[] footstepClips;

        [Header("Timing")]
        [Tooltip("Seconds between footsteps while walking.")]
        [Min(0.01f)]
        [SerializeField] private float walkStepInterval = 0.55f;

        [Tooltip("Seconds between footsteps while sprinting.")]
        [Min(0.01f)]
        [SerializeField] private float sprintStepInterval = 0.35f;

        [Tooltip("Minimum normalized movement speed required to emit footsteps.")]
        [Min(0f)]
        [SerializeField] private float minSpeedForFootsteps = 0.1f;

        [Header("Audio")]
        [Tooltip("Volume passed to SfxManager when playing each footstep.")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Tooltip("Optional transform used as the footstep sound origin. Defaults to this object.")]
        [SerializeField] private Transform soundOrigin;

        private float footstepTimer;

        /// <summary>
        /// Resolves required references when they were not assigned in the Inspector.
        /// </summary>
        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<CharacterMotor>();
            }

            if (soundOrigin == null)
            {
                soundOrigin = transform;
            }
        }

        /// <summary>
        /// Tracks movement and plays a footstep when the current step interval elapses.
        /// </summary>
        private void Update()
        {
            if (motor == null || footstepClips == null || footstepClips.Length == 0)
            {
                return;
            }

            bool isMoving = motor.NormalizedSpeed > minSpeedForFootsteps;

            if (!isMoving)
            {
                footstepTimer = 0f;
                return;
            }

            float stepInterval = GameInputManager.Instance.Sprint
                ? sprintStepInterval
                : walkStepInterval;

            footstepTimer += Time.deltaTime;

            if (footstepTimer < stepInterval)
            {
                return;
            }

            PlayFootstep();
            footstepTimer = 0f;
        }

        /// <summary>
        /// Selects a random assigned clip and plays it at the configured sound origin.
        /// </summary>
        private void PlayFootstep()
        {
            AudioClip clip = GetRandomClip();

            if (clip == null || SfxManager.Instance == null)
            {
                return;
            }

            SfxManager.Instance.PlayAtPosition(
                clip,
                soundOrigin.position,
                1f,
                15f,
                volume
            );

        }

        /// <summary>
        /// Returns a random non-null footstep clip.
        /// </summary>
        private AudioClip GetRandomClip()
        {
            if (footstepClips == null || footstepClips.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < footstepClips.Length; i++)
            {
                AudioClip clip = footstepClips[UnityEngine.Random.Range(0, footstepClips.Length)];

                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
