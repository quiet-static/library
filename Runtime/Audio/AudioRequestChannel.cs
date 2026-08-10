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
        ResumeMusic
    }

    /// <summary>Typed cross-scene audio command.</summary>
    public readonly struct AudioCommand : ICrossSceneCommand
    {
        /// <summary>Creates an audio command with optional clip and numeric data.</summary>
        public AudioCommand(
            AudioCommandType type,
            AudioClip clip = null,
            float value = 0f)
        {
            Type = type;
            Clip = clip;
            Value = value;
        }

        /// <summary>Requested audio operation.</summary>
        public AudioCommandType Type { get; }

        /// <summary>Optional music clip used by playback commands.</summary>
        public AudioClip Clip { get; }

        /// <summary>Optional numeric argument used by volume commands.</summary>
        public float Value { get; }
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
        /// <summary>Raised when listeners should immediately play a music clip.</summary>
        public event Action<AudioClip> MusicPlayRequested;
        /// <summary>Raised when listeners should immediately stop music.</summary>
        public event Action MusicStopRequested;
        /// <summary>Raised when listeners should transition to a music clip.</summary>
        public event Action<AudioClip> MusicFadePlayRequested;
        /// <summary>Raised when listeners should fade out and stop music.</summary>
        public event Action MusicFadeStopRequested;
        /// <summary>Raised when listeners should change normalized music volume.</summary>
        public event Action<float> MusicVolumeRequested;
        /// <summary>Raised when listeners should pause spawned sound effects.</summary>
        public event Action SpawnedSfxPauseRequested;
        /// <summary>Raised when listeners should resume spawned sound effects.</summary>
        public event Action SpawnedSfxResumeRequested;
        /// <summary>Raised when listeners should despawn all managed sound effects.</summary>
        public event Action SpawnedSfxDespawnRequested;
        /// <summary>Raised when listeners should allow new sound-effect spawns.</summary>
        public event Action SfxSpawningEnableRequested;
        /// <summary>Raised when listeners should reject new sound-effect spawns.</summary>
        public event Action SfxSpawningDisableRequested;
        /// <summary>Raised when listeners should pause music.</summary>
        public event Action MusicPauseRequested;
        /// <summary>Raised when listeners should resume music.</summary>
        public event Action MusicResumeRequested;

        /// <summary>Requests immediate music playback.</summary>
        public void PlayMusic(AudioClip clip)
        {
            Dispatch(new AudioCommand(AudioCommandType.PlayMusic, clip));
            MusicPlayRequested?.Invoke(clip);
        }
        /// <summary>Requests that music stop immediately.</summary>
        public void StopMusic()
        {
            Dispatch(new AudioCommand(AudioCommandType.StopMusic));
            MusicStopRequested?.Invoke();
        }
        /// <summary>Requests a faded transition to a music clip.</summary>
        public void PlayWithFade(AudioClip clip)
        {
            Dispatch(new AudioCommand(
                AudioCommandType.PlayMusicWithFade,
                clip));
            MusicFadePlayRequested?.Invoke(clip);
        }
        /// <summary>Requests that music fade out and stop.</summary>
        public void StopWithFade()
        {
            Dispatch(new AudioCommand(AudioCommandType.StopMusicWithFade));
            MusicFadeStopRequested?.Invoke();
        }
        /// <summary>Requests a normalized music volume.</summary>
        public void SetVolume(float volume)
        {
            Dispatch(new AudioCommand(
                AudioCommandType.SetMusicVolume,
                value: volume));
            MusicVolumeRequested?.Invoke(volume);
        }
        /// <summary>Requests that managed sound effects pause.</summary>
        public void PauseSpawnedSfx()
        {
            Dispatch(new AudioCommand(AudioCommandType.PauseSpawnedSfx));
            SpawnedSfxPauseRequested?.Invoke();
        }
        /// <summary>Requests that channel-paused sound effects resume.</summary>
        public void ResumeSpawnedSfx()
        {
            Dispatch(new AudioCommand(AudioCommandType.ResumeSpawnedSfx));
            SpawnedSfxResumeRequested?.Invoke();
        }
        /// <summary>Requests that managed sound effects stop and despawn.</summary>
        public void DespawnSpawnedSfx()
        {
            Dispatch(new AudioCommand(AudioCommandType.DespawnSpawnedSfx));
            SpawnedSfxDespawnRequested?.Invoke();
        }
        /// <summary>Requests that new sound-effect spawning be enabled.</summary>
        public void EnableSfxSpawning()
        {
            Dispatch(new AudioCommand(AudioCommandType.EnableSfxSpawning));
            SfxSpawningEnableRequested?.Invoke();
        }
        /// <summary>Requests that new sound-effect spawning be disabled.</summary>
        public void DisableSfxSpawning()
        {
            Dispatch(new AudioCommand(AudioCommandType.DisableSfxSpawning));
            SfxSpawningDisableRequested?.Invoke();
        }
        /// <summary>Requests that music pause while retaining playback position.</summary>
        public void PauseMusic()
        {
            Dispatch(new AudioCommand(AudioCommandType.PauseMusic));
            MusicPauseRequested?.Invoke();
        }
        /// <summary>Requests that paused music resume.</summary>
        public void ResumeMusic()
        {
            Dispatch(new AudioCommand(AudioCommandType.ResumeMusic));
            MusicResumeRequested?.Invoke();
        }
    }
}
