using System;
using QuietStatic.Toolkit.Characters.Player;
using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>
    /// Plays positional footstep sounds when the attached player controller emits a step.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Possible footstep clips. One is selected at random per step.")]
        [SerializeField] private AudioClip[] footstepClips;

        [Header("Audio")]
        [Tooltip("Volume passed to SfxManager when playing each footstep.")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Tooltip("Optional transform used as the footstep sound origin. Defaults to this object.")]
        [SerializeField] private Transform soundOrigin;

        private PlayerController playerController;

        /// <summary>
        /// Resolves required references when they were not assigned in the Inspector.
        /// </summary>
        private void Awake()
        {
            if (soundOrigin == null)
            {
                soundOrigin = transform;
            }

            playerController = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            PlayerController.OnFootstep += HandleFootstep;
        }

        private void OnDisable()
        {
            PlayerController.OnFootstep -= HandleFootstep;
        }

        /// <summary>
        /// Plays a step only when it came from the controller on this GameObject.
        /// </summary>
        private void HandleFootstep(PlayerController source)
        {
            if (source != playerController)
            {
                return;
            }

            PlayFootstep();
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
