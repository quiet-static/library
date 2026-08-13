using System.Collections.Generic;
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
        public void InteractionUIChannel_ForwardsTypedMessageAndPromptRequests()
        {
            InteractionUIChannel channel = ScriptableObject.CreateInstance<InteractionUIChannel>();
            var commands = new List<InteractionUICommand>();
            channel.CommandRequested += commands.Add;

            channel.ShowMessage("Dinner is ready");
            channel.ShowPrompt("[E] Interact");
            channel.HidePrompt();
            channel.ShowProgress("Eating", 0.4f);
            channel.HideProgress();

            Assert.That(commands, Has.Count.EqualTo(5));
            Assert.That(commands[0].Type, Is.EqualTo(InteractionUICommandType.ShowMessage));
            Assert.That(commands[0].Text, Is.EqualTo("Dinner is ready"));
            Assert.That(commands[1].Type, Is.EqualTo(InteractionUICommandType.ShowPrompt));
            Assert.That(commands[1].Text, Is.EqualTo("[E] Interact"));
            Assert.That(commands[2].Type, Is.EqualTo(InteractionUICommandType.HidePrompt));
            Assert.That(commands[3].Type, Is.EqualTo(InteractionUICommandType.ShowProgress));
            Assert.That(commands[3].Text, Is.EqualTo("Eating"));
            Assert.That(commands[3].Progress, Is.EqualTo(0.4f));
            Assert.That(commands[4].Type, Is.EqualTo(InteractionUICommandType.HideProgress));
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void PlayerActivityChannel_ForwardsLimitedLookContext()
        {
            PlayerActivityChannel channel =
                ScriptableObject.CreateInstance<PlayerActivityChannel>();
            GameObject anchor = new("Player Anchor");
            GameObject focus = new("Camera Focus");
            PlayerActivityContext received = default;
            channel.ContextBegan += value => received = value;

            channel.Begin(anchor.transform, focus.transform, 20f, 12f, true);

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
        public void AudioRequestChannel_ForwardsTypedGlobalSfxRequests()
        {
            AudioRequestChannel channel = ScriptableObject.CreateInstance<AudioRequestChannel>();
            var commands = new List<AudioCommandType>();
            channel.CommandRequested += command => commands.Add(command.Type);

            channel.DespawnSpawnedSfx();
            channel.DisableSfxSpawning();
            channel.EnableSfxSpawning();

            CollectionAssert.AreEqual(
                new[]
                {
                    AudioCommandType.DespawnSpawnedSfx,
                    AudioCommandType.DisableSfxSpawning,
                    AudioCommandType.EnableSfxSpawning
                },
                commands);
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

            objectState.ClearState();

            Assert.That(
                stateCommand.Type,
                Is.EqualTo(ObjectStateCommandType.Clear));
            Assert.That(stateCommand.State, Is.Null);

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
