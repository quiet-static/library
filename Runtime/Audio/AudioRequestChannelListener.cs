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
        [SerializeField] private AudioRequestChannel channel;

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

        private static void Subscribe(AudioRequestChannel value)
        {
            value.CommandRequested += HandleCommand;
        }

        private static void Unsubscribe(AudioRequestChannel value)
        {
            value.CommandRequested -= HandleCommand;
        }

        private static void HandleCommand(AudioCommand command)
        {
            switch (command.Type)
            {
                case AudioCommandType.PlayMusic:
                    MusicManager.Instance?.PlayMusic(command.Clip);
                    break;
                case AudioCommandType.StopMusic:
                    MusicManager.Instance?.StopMusic();
                    break;
                case AudioCommandType.PlayMusicWithFade:
                    MusicManager.Instance?.PlayMusicWithFade(command.Clip);
                    break;
                case AudioCommandType.StopMusicWithFade:
                    MusicManager.Instance?.StopMusicWithFade();
                    break;
                case AudioCommandType.SetMusicVolume:
                    MusicManager.Instance?.SetVolume(command.Value);
                    break;
                case AudioCommandType.PauseSpawnedSfx:
                    SfxManager.Instance?.PauseSpawnedSounds();
                    break;
                case AudioCommandType.ResumeSpawnedSfx:
                    SfxManager.Instance?.ResumeSpawnedSounds();
                    break;
                case AudioCommandType.DespawnSpawnedSfx:
                    SfxManager.Instance?.DespawnSpawnedSounds();
                    break;
                case AudioCommandType.EnableSfxSpawning:
                    SfxManager.Instance?.EnableSpawning();
                    break;
                case AudioCommandType.DisableSfxSpawning:
                    SfxManager.Instance?.DisableSpawning();
                    break;
                case AudioCommandType.PauseMusic:
                    MusicManager.Instance?.PauseMusic();
                    break;
                case AudioCommandType.ResumeMusic:
                    MusicManager.Instance?.ResumeMusic();
                    break;
            }
        }
    }
}
