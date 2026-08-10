using NUnit.Framework;
using QuietStatic.Toolkit.Audio;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Saving;
using QuietStatic.Toolkit.SceneFlow;
using QuietStatic.Toolkit.Utilities;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public class RequestChannelTests
    {
        [Test]
        public void InteractionUIChannel_ForwardsMessageAndPromptRequests()
        {
            InteractionUIChannel channel = ScriptableObject.CreateInstance<InteractionUIChannel>();
            string message = null;
            string prompt = null;
            bool promptHidden = false;
            string progressLabel = null;
            float progress = -1f;
            bool progressHidden = false;
            channel.MessageShowRequested += value => message = value;
            channel.PromptShowRequested += value => prompt = value;
            channel.PromptHideRequested += () => promptHidden = true;
            channel.ProgressShowRequested += (label, value) =>
            {
                progressLabel = label;
                progress = value;
            };
            channel.ProgressHideRequested += () => progressHidden = true;

            channel.ShowMessage("Dinner is ready");
            channel.ShowPrompt("[E] Interact");
            channel.HidePrompt();
            channel.ShowProgress("Eating", 0.4f);
            channel.HideProgress();

            Assert.That(message, Is.EqualTo("Dinner is ready"));
            Assert.That(prompt, Is.EqualTo("[E] Interact"));
            Assert.That(promptHidden, Is.True);
            Assert.That(progressLabel, Is.EqualTo("Eating"));
            Assert.That(progress, Is.EqualTo(0.4f));
            Assert.That(progressHidden, Is.True);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void PlayerActivityChannel_ForwardsAnchorAndLimitedLookContext()
        {
            PlayerActivityChannel channel =
                ScriptableObject.CreateInstance<PlayerActivityChannel>();
            GameObject anchor = new("Player Anchor");
            GameObject focus = new("Camera Focus");
            Transform legacyAnchor = null;
            PlayerActivityContext received = default;
            channel.Began += value => legacyAnchor = value;
            channel.ContextBegan += value => received = value;

            channel.Begin(anchor.transform, focus.transform, 20f, 12f, true);

            Assert.That(legacyAnchor, Is.SameAs(anchor.transform));
            Assert.That(received.PlayerAnchor, Is.SameAs(anchor.transform));
            Assert.That(received.CameraFocusTarget, Is.SameAs(focus.transform));
            Assert.That(received.HorizontalLookRange, Is.EqualTo(20f));
            Assert.That(received.VerticalLookRange, Is.EqualTo(12f));
            Assert.That(received.SnapCameraToFocus, Is.True);

            Object.DestroyImmediate(focus);
            Object.DestroyImmediate(anchor);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void AudioRequestChannel_ForwardsGlobalSfxRequests()
        {
            AudioRequestChannel channel = ScriptableObject.CreateInstance<AudioRequestChannel>();
            int despawnCount = 0;
            bool spawningEnabled = true;
            channel.SpawnedSfxDespawnRequested += () => despawnCount++;
            channel.SfxSpawningDisableRequested += () => spawningEnabled = false;
            channel.SfxSpawningEnableRequested += () => spawningEnabled = true;

            channel.DespawnSpawnedSfx();
            channel.DisableSfxSpawning();

            Assert.That(despawnCount, Is.EqualTo(1));
            Assert.That(spawningEnabled, Is.False);

            channel.EnableSfxSpawning();

            Assert.That(spawningEnabled, Is.True);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void TypedCommands_PreserveArgumentsAcrossChannelKinds()
        {
            InteractionUIChannel interaction =
                ScriptableObject.CreateInstance<InteractionUIChannel>();
            SaveRequestChannel saving =
                ScriptableObject.CreateInstance<SaveRequestChannel>();
            ObjectStateChannel objectState =
                ScriptableObject.CreateInstance<ObjectStateChannel>();
            ObjectStateDefinition state =
                ScriptableObject.CreateInstance<ObjectStateDefinition>();
            InteractionUICommand interactionCommand = default;
            SaveCommand saveCommand = default;
            ObjectStateCommand stateCommand = default;

            interaction.CommandRequested += value =>
                interactionCommand = value;
            saving.CommandRequested += value => saveCommand = value;
            objectState.CommandRequested += value => stateCommand = value;

            interaction.ShowMessageForSeconds("Locked", 2.5f);
            saving.RequestSave(3, "Entry");
            objectState.ActivateState(state);

            Assert.That(
                interactionCommand.Type,
                Is.EqualTo(InteractionUICommandType.ShowTimedMessage));
            Assert.That(interactionCommand.Text, Is.EqualTo("Locked"));
            Assert.That(interactionCommand.Seconds, Is.EqualTo(2.5f));
            Assert.That(saveCommand.Type, Is.EqualTo(SaveCommandType.Save));
            Assert.That(saveCommand.Slot, Is.EqualTo(3));
            Assert.That(saveCommand.ArrivalSpawnId, Is.EqualTo("Entry"));
            Assert.That(
                stateCommand.Type,
                Is.EqualTo(ObjectStateCommandType.Activate));
            Assert.That(stateCommand.State, Is.SameAs(state));

            Object.DestroyImmediate(state);
            Object.DestroyImmediate(objectState);
            Object.DestroyImmediate(saving);
            Object.DestroyImmediate(interaction);
        }

        [Test]
        public void Subscription_RebindsWithoutLeavingOldChannelAttached()
        {
            InteractionUIChannel first =
                ScriptableObject.CreateInstance<InteractionUIChannel>();
            InteractionUIChannel second =
                ScriptableObject.CreateInstance<InteractionUIChannel>();
            int received = 0;
            System.Action<InteractionUICommand> receiver =
                command => received++;
            var subscription =
                new CrossSceneChannelSubscription<InteractionUIChannel>(
                    channel => channel.CommandRequested += receiver,
                    channel => channel.CommandRequested -= receiver);

            subscription.Bind(first);
            first.HidePrompt();
            subscription.Bind(second);
            first.HidePrompt();
            second.HidePrompt();
            subscription.Unbind();
            second.HidePrompt();

            Assert.That(received, Is.EqualTo(2));
            Assert.That(subscription.Channel, Is.Null);

            Object.DestroyImmediate(second);
            Object.DestroyImmediate(first);
        }

        [Test]
        public void SceneFlowChannel_ReportsReceiverAvailability()
        {
            SceneFlowRequestChannel channel =
                ScriptableObject.CreateInstance<SceneFlowRequestChannel>();
            SceneFlowCommand received = default;

            Assert.That(channel.TryTransitionToScene("House"), Is.False);

            channel.CommandRequested += command => received = command;

            Assert.That(channel.TryTransitionToScene(" House "), Is.True);
            Assert.That(
                received.Type,
                Is.EqualTo(SceneFlowCommandType.Transition));
            Assert.That(received.SceneName, Is.EqualTo("House"));
            Assert.That(
                received.Transition.TargetSceneName,
                Is.EqualTo("House"));

            Object.DestroyImmediate(channel);
        }
    }
}
