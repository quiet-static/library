using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Operation carried by an <see cref="InteractionUICommand"/>.</summary>
    public enum InteractionUICommandType
    {
        ShowPrompt,
        HidePrompt,
        ShowMessage,
        ShowTimedMessage,
        ShowProgress,
        HideProgress,
        ShowReadable,
        HideReadable
    }

    /// <summary>Typed cross-scene interaction UI command.</summary>
    public readonly struct InteractionUICommand : ICrossSceneCommand
    {
        /// <summary>Creates an interaction UI command.</summary>
        public InteractionUICommand(
            InteractionUICommandType type,
            string text = "",
            float seconds = 0f,
            float progress = 0f,
            ReadableContentDefinition readable = null,
            UnityEngine.Object readableSource = null)
        {
            Type = type;
            Text = text ?? string.Empty;
            Seconds = seconds;
            Progress = Mathf.Clamp01(progress);
            Readable = readable;
            ReadableSource = readableSource;
        }

        /// <summary>Requested interaction UI operation.</summary>
        public InteractionUICommandType Type { get; }

        /// <summary>Prompt or message text carried by the command.</summary>
        public string Text { get; }

        /// <summary>Optional display duration for timed messages.</summary>
        public float Seconds { get; }

        /// <summary>Optional normalized progress carried by progress commands.</summary>
        public float Progress { get; }

        /// <summary>Optional long-form readable content carried by the command.</summary>
        public ReadableContentDefinition Readable { get; }

        /// <summary>Optional scene object that requested the readable.</summary>
        public UnityEngine.Object ReadableSource { get; }
    }

    /// <summary>
    /// Relays interaction UI requests between scenes without referencing scene objects.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InteractionUIChannel",
        menuName = "Quiet Static Toolkit/Interactions/Interaction UI Channel"
    )]
    public sealed class InteractionUIChannel :
        CrossSceneCommandChannel<InteractionUICommand>
    {
        /// <summary>Raised when listeners should display an interaction prompt.</summary>
        public event Action<string> PromptShowRequested;

        /// <summary>Raised when listeners should hide the current prompt.</summary>
        public event Action PromptHideRequested;

        /// <summary>Raised when listeners should display a message.</summary>
        public event Action<string> MessageShowRequested;

        /// <summary>Raised when listeners should display a message for a custom duration.</summary>
        public event Action<string, float> TimedMessageShowRequested;

        /// <summary>Raised when listeners should display a named progress meter.</summary>
        public event Action<string, float> ProgressShowRequested;

        /// <summary>Raised when listeners should hide the progress meter.</summary>
        public event Action ProgressHideRequested;

        /// <summary>Raised after the overlay displays readable content.</summary>
        public event Action<ReadableContentDefinition, UnityEngine.Object> ReadableOpened;

        /// <summary>Raised after the displayed readable content closes.</summary>
        public event Action<ReadableContentDefinition, UnityEngine.Object> ReadableClosed;

        /// <summary>Requests that listeners display an interaction prompt.</summary>
        public void ShowPrompt(string prompt)
        {
            Dispatch(new InteractionUICommand(
                InteractionUICommandType.ShowPrompt,
                prompt));
            PromptShowRequested?.Invoke(prompt);
        }

        /// <summary>Requests that listeners hide the current interaction prompt.</summary>
        public void HidePrompt()
        {
            Dispatch(new InteractionUICommand(
                InteractionUICommandType.HidePrompt));
            PromptHideRequested?.Invoke();
        }

        /// <summary>Requests that listeners display a message using their default duration.</summary>
        public void ShowMessage(string text)
        {
            Dispatch(new InteractionUICommand(
                InteractionUICommandType.ShowMessage,
                text));
            MessageShowRequested?.Invoke(text);
        }

        /// <summary>Requests that listeners display a message for a custom duration.</summary>
        public void ShowMessageForSeconds(string text, float seconds)
        {
            Dispatch(new InteractionUICommand(
                InteractionUICommandType.ShowTimedMessage,
                text,
                seconds));
            TimedMessageShowRequested?.Invoke(text, seconds);
        }

        /// <summary>Requests that listeners display a named, normalized progress meter.</summary>
        public void ShowProgress(string label, float normalizedProgress)
        {
            float progress = Mathf.Clamp01(normalizedProgress);
            Dispatch(new InteractionUICommand(
                InteractionUICommandType.ShowProgress,
                label,
                progress: progress));
            ProgressShowRequested?.Invoke(label, progress);
        }

        /// <summary>Requests that listeners hide the current progress meter.</summary>
        public void HideProgress()
        {
            Dispatch(new InteractionUICommand(
                InteractionUICommandType.HideProgress));
            ProgressHideRequested?.Invoke();
        }

        /// <summary>Shows a modal long-form readable such as a letter or note.</summary>
        public void ShowReadable(
            ReadableContentDefinition definition,
            UnityEngine.Object source = null)
        {
            if (definition == null) return;
            Dispatch(new InteractionUICommand(
                InteractionUICommandType.ShowReadable,
                readable: definition,
                readableSource: source));
        }

        /// <summary>Hides the current long-form readable.</summary>
        public void HideReadable()
        {
            Dispatch(new InteractionUICommand(
                InteractionUICommandType.HideReadable));
        }

        internal void NotifyReadableOpened(
            ReadableContentDefinition definition,
            UnityEngine.Object source) => ReadableOpened?.Invoke(definition, source);

        internal void NotifyReadableClosed(
            ReadableContentDefinition definition,
            UnityEngine.Object source) => ReadableClosed?.Invoke(definition, source);
    }

}
