using QuietStatic.Toolkit.Audio;
using QuietStatic.Toolkit.Core;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Spawns reusable 3D one-shot sound prefabs in the world.
    /// </summary>
    /// <remarks>
    /// This manager does not know about footsteps, doors, flags, characters,
    /// jumpscares, or any game-specific events.
    ///
    /// Other systems call its public methods when they need a positional sound.
    /// </remarks>
    public class SfxManager : ToolkitSingleton<SfxManager>
    {
        [Header("Prefab")]
        [Tooltip("Prefab containing EventSound3D and its AudioSource setup.")]
        [SerializeField] private EventSound3D eventSound3DPrefab;

        [Header("Default 3D Audio Settings")]
        [Tooltip("Default minimum distance used when a caller does not provide one.")]
        [Min(0f)]
        [SerializeField] private float defaultMinDistance = 5f;

        [Tooltip("Default maximum distance used when a caller does not provide one.")]
        [Min(0f)]
        [SerializeField] private float defaultMaxDistance = 100f;

        [Tooltip("Default volume used when a caller does not provide one.")]
        [Range(0f, 1f)]
        [SerializeField] private float defaultVolume = 1f;

        /// <summary>
        /// Spawns and plays a 3D sound at a world position using default audio settings.
        /// </summary>
        /// <param name="clip">Audio clip to play.</param>
        /// <param name="worldPosition">World position where the sound should originate.</param>
        /// <returns>The spawned EventSound3D instance, or null if playback failed.</returns>
        public EventSound3D PlayAtPosition(
            AudioClip clip,
            Vector3 worldPosition
        )
        {
            return PlayAtPosition(
                clip,
                worldPosition,
                defaultMinDistance,
                defaultMaxDistance,
                defaultVolume
            );
        }

        /// <summary>
        /// Spawns and plays a configurable 3D sound at a world position.
        /// </summary>
        /// <param name="clip">Audio clip to play.</param>
        /// <param name="worldPosition">World position where the sound should originate.</param>
        /// <param name="minDistance">Distance where the sound is heard at full volume.</param>
        /// <param name="maxDistance">Distance where the sound becomes inaudible.</param>
        /// <param name="volume">Playback volume from 0 to 1.</param>
        /// <returns>The spawned EventSound3D instance, or null if playback failed.</returns>
        public EventSound3D PlayAtPosition(
            AudioClip clip,
            Vector3 worldPosition,
            float minDistance,
            float maxDistance,
            float volume
        )
        {
            if (eventSound3DPrefab == null)
            {
                GameLogger.Warning(
                    "PlayAtPosition",
                    this,
                    $"{nameof(SfxManager)} has no EventSound3D prefab assigned."
                );
                return null;
            }

            if (clip == null)
            {
                return null;
            }

            EventSound3D sound = Instantiate(
                eventSound3DPrefab,
                worldPosition,
                Quaternion.identity
            );

            sound.Play(
                clip,
                Mathf.Max(0f, minDistance),
                Mathf.Max(minDistance, maxDistance),
                Mathf.Clamp01(volume)
            );

            return sound;
        }

        /// <summary>
        /// Spawns a sound at a transform and parents it so it follows that object.
        /// </summary>
        /// <param name="clip">Audio clip to play.</param>
        /// <param name="followTarget">Transform the sound should follow.</param>
        /// <param name="volume">Playback volume from 0 to 1.</param>
        /// <returns>The spawned EventSound3D instance, or null if playback failed.</returns>
        public EventSound3D PlayAttached(
            AudioClip clip,
            Transform followTarget,
            float volume = 1f
        )
        {
            if (followTarget == null)
            {
                return null;
            }

            EventSound3D sound = PlayAtPosition(
                clip,
                followTarget.position,
                defaultMinDistance,
                defaultMaxDistance,
                volume
            );

            if (sound != null)
            {
                sound.transform.SetParent(followTarget, true);
            }

            return sound;
        }
    }
}
