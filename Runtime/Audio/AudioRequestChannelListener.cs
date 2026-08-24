using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>
    /// Forwards cross-scene audio channel requests to the persistent audio managers.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Audio/Audio Request Channel Listener")]
    public sealed class AudioRequestChannelListener : MonoBehaviour
    {
        [Tooltip("Channel carrying global audio requests from gameplay scenes.")]
        [RequiredCommandChannel(isReceiver: true)]
        [SerializeField] private AudioRequestChannel channel;

        [Tooltip("Persistent music service that executes music commands.")]
        [SerializeField] private MusicManager musicManager;

        [Tooltip("Persistent sound-effects service that executes SFX commands.")]
        [SerializeField] private SfxManager sfxManager;

        private CrossSceneChannelSubscription<AudioRequestChannel> subscription;

        private CrossSceneChannelSubscription<AudioRequestChannel> Subscription =>
            subscription ??= new CrossSceneChannelSubscription<AudioRequestChannel>(
                Subscribe,
                Unsubscribe);

        private void OnEnable()
        {
            Subscription.Bind(channel);
        }

        private void OnDisable()
        {
            Subscription.Unbind();
        }

        /// <summary>Changes the channel and updates the live subscription.</summary>
        public void SetChannel(AudioRequestChannel value)
        {
            channel = value;
            if (isActiveAndEnabled)
            {
                Subscription.Bind(channel);
            }
        }

        private void Subscribe(AudioRequestChannel value)
        {
            value.CommandRequested += HandleCommand;
        }

        private void Unsubscribe(AudioRequestChannel value)
        {
            value.CommandRequested -= HandleCommand;
        }

        private void HandleCommand(AudioCommand command)
        {
            switch (command.Type)
            {
                case AudioCommandType.PlayMusic:
                    musicManager?.PlayMusic(command.Clip);
                    break;
                case AudioCommandType.StopMusic:
                    musicManager?.StopMusic();
                    break;
                case AudioCommandType.PlayMusicWithFade:
                    musicManager?.PlayMusicWithFade(command.Clip);
                    break;
                case AudioCommandType.StopMusicWithFade:
                    musicManager?.StopMusicWithFade();
                    break;
                case AudioCommandType.SetMusicVolume:
                    musicManager?.SetVolume(command.Value);
                    break;
                case AudioCommandType.PauseSpawnedSfx:
                    sfxManager?.PauseSpawnedSounds();
                    break;
                case AudioCommandType.ResumeSpawnedSfx:
                    sfxManager?.ResumeSpawnedSounds();
                    break;
                case AudioCommandType.DespawnSpawnedSfx:
                    sfxManager?.DespawnSpawnedSounds();
                    break;
                case AudioCommandType.EnableSfxSpawning:
                    sfxManager?.EnableSpawning();
                    break;
                case AudioCommandType.DisableSfxSpawning:
                    sfxManager?.DisableSpawning();
                    break;
                case AudioCommandType.PauseMusic:
                    musicManager?.PauseMusic();
                    break;
                case AudioCommandType.ResumeMusic:
                    musicManager?.ResumeMusic();
                    break;
                case AudioCommandType.PlaySfxAtPosition:
                    EventSound3D sound = sfxManager?.PlayAtPosition(
                        command.Clip,
                        command.Position,
                        command.MinDistance,
                        command.MaxDistance,
                        command.Value,
                        command.Loop);
                    command.SoundCreated?.Invoke(sound);
                    break;
            }
        }
    }
}
