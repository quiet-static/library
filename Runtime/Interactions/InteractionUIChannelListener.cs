using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Forwards cross-scene interaction UI channel requests to the persistent UI manager.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Interaction UI Channel Listener")]
    public sealed class InteractionUIChannelListener : MonoBehaviour
    {
        [Tooltip("Channel carrying interaction UI requests from gameplay scenes.")]
        [SerializeField] private InteractionUIChannel channel;

        private CrossSceneChannelSubscription<InteractionUIChannel> subscription;

        private CrossSceneChannelSubscription<InteractionUIChannel> Subscription =>
            subscription ??=
                new CrossSceneChannelSubscription<InteractionUIChannel>(
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
        public void SetChannel(InteractionUIChannel value)
        {
            channel = value;
            if (isActiveAndEnabled)
            {
                Subscription.Bind(channel);
            }
        }

        private static void Subscribe(InteractionUIChannel value)
        {
            value.CommandRequested += HandleCommand;
        }

        private static void Unsubscribe(InteractionUIChannel value)
        {
            value.CommandRequested -= HandleCommand;
        }

        private static void HandleCommand(InteractionUICommand command)
        {
            switch (command.Type)
            {
                case InteractionUICommandType.ShowPrompt:
                    InteractionUIManager.Instance?.ShowPrompt(command.Text);
                    break;
                case InteractionUICommandType.HidePrompt:
                    InteractionUIManager.Instance?.HidePrompt();
                    break;
                case InteractionUICommandType.ShowMessage:
                    InteractionUIManager.Instance?.ShowMessage(command.Text);
                    break;
                case InteractionUICommandType.ShowTimedMessage:
                    InteractionUIManager.Instance?.ShowMessageForSeconds(
                        command.Text,
                        command.Seconds);
                    break;
                case InteractionUICommandType.ShowProgress:
                    InteractionUIManager.Instance?.ShowProgress(
                        command.Text,
                        command.Progress);
                    break;
                case InteractionUICommandType.HideProgress:
                    InteractionUIManager.Instance?.HideProgress();
                    break;
            }
        }
    }

}
