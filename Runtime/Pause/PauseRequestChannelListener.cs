using UnityEngine;

namespace QuietStatic.Toolkit.Pause
{
    /// <summary>Executes pause commands through the assigned persistent authority.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Pause/Pause Request Channel Listener")]
    public sealed class PauseRequestChannelListener : MonoBehaviour
    {
        [Tooltip("Channel carrying pause requests from gameplay and menu scenes.")]
        [RequiredCommandChannel(isReceiver: true)]
        [SerializeField] private PauseRequestChannel channel;

        [Tooltip("Persistent pause authority that executes accepted commands.")]
        [SerializeField] private PauseManager pauseManager;

        private CrossSceneChannelSubscription<PauseRequestChannel> subscription;

        private CrossSceneChannelSubscription<PauseRequestChannel> Subscription =>
            subscription ??= new CrossSceneChannelSubscription<PauseRequestChannel>(
                Subscribe,
                Unsubscribe);

        private void OnEnable() => Subscription.Bind(channel);

        private void OnDisable() => Subscription.Unbind();

        /// <summary>Assigns the channel and refreshes an active subscription.</summary>
        public void SetChannel(PauseRequestChannel value)
        {
            channel = value;
            if (isActiveAndEnabled)
            {
                Subscription.Bind(channel);
            }
        }

        /// <summary>Assigns the same-scene pause authority.</summary>
        public void SetPauseManager(PauseManager value) => pauseManager = value;

        private void Subscribe(PauseRequestChannel value) =>
            value.CommandRequested += HandleCommand;

        private void Unsubscribe(PauseRequestChannel value) =>
            value.CommandRequested -= HandleCommand;

        private void HandleCommand(PauseCommand command)
        {
            if (pauseManager == null)
            {
                GameLogger.Warning(
                    $"[{nameof(PauseRequestChannelListener)}] Cannot execute " +
                    $"{command.Type}; no {nameof(PauseManager)} is assigned.",
                    this);
                return;
            }

            switch (command.Type)
            {
                case PauseCommandType.Toggle:
                    pauseManager.TogglePause();
                    break;
                case PauseCommandType.Pause:
                    pauseManager.PauseGame();
                    break;
                case PauseCommandType.Resume:
                    pauseManager.ResumeGame();
                    break;
                case PauseCommandType.ForceResume:
                    pauseManager.ForceResumeTime();
                    break;
            }
        }
    }
}
