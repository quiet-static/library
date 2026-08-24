using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>Operation carried by an <see cref="AudioCommand"/>.</summary>
    public enum AudioCommandType
    {
        PlayMusic,
        StopMusic,
        PlayMusicWithFade,
        StopMusicWithFade,
        SetMusicVolume,
        PauseSpawnedSfx,
        ResumeSpawnedSfx,
        DespawnSpawnedSfx,
        EnableSfxSpawning,
        DisableSfxSpawning,
        PauseMusic,
        ResumeMusic,
        PlaySfxAtPosition
    }

    /// <summary>Typed cross-scene audio command.</summary>
    public readonly struct AudioCommand
    {
        /// <summary>Creates an audio command with optional clip and numeric data.</summary>
        public AudioCommand(
            AudioCommandType type,
            AudioClip clip = null,
            float value = 0f,
            Vector3 position = default,
            float minDistance = 1f,
            float maxDistance = 15f,
            bool loop = false,
            Action<EventSound3D> soundCreated = null)
        {
            Type = type;
            Clip = clip;
            Value = value;
            Position = position;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            Loop = loop;
            SoundCreated = soundCreated;
        }

        /// <summary>Requested audio operation.</summary>
        public AudioCommandType Type { get; }

        /// <summary>Optional music clip used by playback commands.</summary>
        public AudioClip Clip { get; }

        /// <summary>Optional numeric argument used by volume commands.</summary>
        public float Value { get; }

        /// <summary>World position used by positional sound commands.</summary>
        public Vector3 Position { get; }

        /// <summary>Full-volume distance used by positional sound commands.</summary>
        public float MinDistance { get; }

        /// <summary>Maximum audible distance used by positional sound commands.</summary>
        public float MaxDistance { get; }

        /// <summary>Whether a positional sound repeats until stopped.</summary>
        public bool Loop { get; }

        /// <summary>Optional synchronous completion callback receiving the spawned sound.</summary>
        public Action<EventSound3D> SoundCreated { get; }
    }

    /// <summary>
    /// Relays global music and sound-effect requests without referencing persistent managers.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AudioRequestChannel",
        menuName = "Quiet Static Toolkit/Audio/Audio Request Channel"
    )]
    public sealed class AudioRequestChannel :
        CrossSceneCommandChannel<AudioCommand>
    {
        /// <summary>Requests immediate music playback.</summary>
        public void PlayMusic(AudioClip clip)
        {
            Dispatch(new AudioCommand(AudioCommandType.PlayMusic, clip));
        }
        /// <summary>Requests that music stop immediately.</summary>
        public void StopMusic()
        {
            Dispatch(new AudioCommand(AudioCommandType.StopMusic));
        }
        /// <summary>Requests a faded transition to a music clip.</summary>
        public void PlayWithFade(AudioClip clip)
        {
            Dispatch(new AudioCommand(
                AudioCommandType.PlayMusicWithFade,
                clip));
        }
        /// <summary>Requests that music fade out and stop.</summary>
        public void StopWithFade()
        {
            Dispatch(new AudioCommand(AudioCommandType.StopMusicWithFade));
        }
        /// <summary>Requests a normalized music volume.</summary>
        public void SetVolume(float volume)
        {
            Dispatch(new AudioCommand(
                AudioCommandType.SetMusicVolume,
                value: volume));
        }
        /// <summary>Requests that managed sound effects pause.</summary>
        public void PauseSpawnedSfx()
        {
            Dispatch(new AudioCommand(AudioCommandType.PauseSpawnedSfx));
        }
        /// <summary>Requests that channel-paused sound effects resume.</summary>
        public void ResumeSpawnedSfx()
        {
            Dispatch(new AudioCommand(AudioCommandType.ResumeSpawnedSfx));
        }
        /// <summary>Requests that managed sound effects stop and despawn.</summary>
        public void DespawnSpawnedSfx()
        {
            Dispatch(new AudioCommand(AudioCommandType.DespawnSpawnedSfx));
        }
        /// <summary>Requests that new sound-effect spawning be enabled.</summary>
        public void EnableSfxSpawning()
        {
            Dispatch(new AudioCommand(AudioCommandType.EnableSfxSpawning));
        }
        /// <summary>Requests that new sound-effect spawning be disabled.</summary>
        public void DisableSfxSpawning()
        {
            Dispatch(new AudioCommand(AudioCommandType.DisableSfxSpawning));
        }
        /// <summary>Requests that music pause while retaining playback position.</summary>
        public void PauseMusic()
        {
            Dispatch(new AudioCommand(AudioCommandType.PauseMusic));
        }
        /// <summary>Requests that paused music resume.</summary>
        public void ResumeMusic()
        {
            Dispatch(new AudioCommand(AudioCommandType.ResumeMusic));
        }

        /// <summary>Requests a positional sound and returns the receiver-created instance.</summary>
        public EventSound3D PlayAtPosition(
            AudioClip clip,
            Vector3 position,
            float minDistance = 1f,
            float maxDistance = 15f,
            float volume = 1f,
            bool loop = false)
        {
            EventSound3D created = null;
            Dispatch(new AudioCommand(
                AudioCommandType.PlaySfxAtPosition,
                clip,
                volume,
                position,
                minDistance,
                maxDistance,
                loop,
                sound => created = sound));
            return created;
        }
    }
}
