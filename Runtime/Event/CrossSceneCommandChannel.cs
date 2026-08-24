using System;
using QuietStatic.Toolkit.DebugTools;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Non-generic base for ScriptableObject channels that bridge scene content and
    /// persistent systems.
    /// </summary>
    public abstract class CrossSceneCommandChannel : ScriptableObject { }

    /// <summary>
    /// Base for fire-and-forget channels that dispatch one strongly typed command stream.
    /// </summary>
    /// <typeparam name="TCommand">Payload describing the requested operation.</typeparam>
    public abstract class CrossSceneCommandChannel<TCommand> :
        CrossSceneCommandChannel
    {
        /// <summary>Raised synchronously whenever a caller dispatches a command.</summary>
        public event Action<TCommand> CommandRequested;

        /// <summary>Whether at least one enabled receiver is currently subscribed.</summary>
        public bool HasReceivers => CommandRequested != null;

        /// <summary>Correlation ID assigned to the most recently traced dispatch.</summary>
        public string LastCorrelationId { get; private set; } = string.Empty;

        /// <summary>
        /// Delivers a command to every current receiver and reports whether any existed.
        /// </summary>
        protected bool Dispatch(TCommand command)
        {
            return Dispatch(command, null);
        }

        /// <summary>
        /// Delivers a command and exposes its trace correlation before receivers run.
        /// </summary>
        /// <remarks>
        /// The callback is invoked synchronously, including when tracing is disabled
        /// (in which case the correlation ID is empty). This lets specialized
        /// request/result channels associate a terminal result before a receiver can
        /// publish one during dispatch.
        /// </remarks>
        protected bool Dispatch(
            TCommand command,
            Action<string> correlationAssigned)
        {
            Action<TCommand> receivers = CommandRequested;
            string correlationId = string.Empty;
            string payload = string.Empty;
            if (DebugTrace.Enabled)
            {
                correlationId = DebugTrace.BeginCorrelation();
                LastCorrelationId = correlationId;
                payload = command is null ? "null" : command.ToString();
                DebugTrace.RecordCommand(
                    correlationId,
                    name,
                    typeof(TCommand).Name,
                    payload,
                    string.Empty,
                    "Submitted",
                    this);
            }

            correlationAssigned?.Invoke(correlationId);

            if (receivers == null)
            {
                if (DebugTrace.Enabled)
                {
                    DebugTrace.RecordCommand(
                        correlationId,
                        name,
                        typeof(TCommand).Name,
                        payload,
                        string.Empty,
                        "Rejected: no receiver",
                        this);
                }
                return false;
            }

            receivers.Invoke(command);
            if (DebugTrace.Enabled)
            {
                DebugTrace.RecordCommand(
                    correlationId,
                    name,
                    typeof(TCommand).Name,
                    payload,
                    receivers.Method.DeclaringType?.Name ?? "Receiver",
                    "Accepted",
                    this);
            }
            return true;
        }
    }

    /// <summary>
    /// Tracks the exact channel subscribed by a component so runtime channel changes
    /// cannot leave callbacks attached to an old asset.
    /// </summary>
    /// <typeparam name="TChannel">Concrete channel asset type.</typeparam>
    public sealed class CrossSceneChannelSubscription<TChannel>
        where TChannel : CrossSceneCommandChannel
    {
        private readonly Action<TChannel> subscribe;
        private readonly Action<TChannel> unsubscribe;
        private TChannel channel;

        /// <summary>Creates a tracked subscription using the supplied callbacks.</summary>
        public CrossSceneChannelSubscription(
            Action<TChannel> subscribe,
            Action<TChannel> unsubscribe)
        {
            this.subscribe = subscribe ??
                throw new ArgumentNullException(nameof(subscribe));
            this.unsubscribe = unsubscribe ??
                throw new ArgumentNullException(nameof(unsubscribe));
        }

        /// <summary>The channel currently subscribed, or null.</summary>
        public TChannel Channel => channel;

        /// <summary>Moves the subscription to a new channel.</summary>
        public void Bind(TChannel value)
        {
            if (channel == value)
            {
                return;
            }

            Unbind();
            if (value == null)
            {
                return;
            }

            subscribe(value);
            channel = value;
        }

        /// <summary>Removes callbacks from the channel that was actually subscribed.</summary>
        public void Unbind()
        {
            if (channel == null)
            {
                return;
            }

            TChannel previous = channel;
            channel = null;
            unsubscribe(previous);
        }
    }
}
