using System;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>Marker contract for payloads sent as cross-scene commands.</summary>
    public interface ICrossSceneCommand { }

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
        where TCommand : ICrossSceneCommand
    {
        /// <summary>Raised synchronously whenever a caller dispatches a command.</summary>
        public event Action<TCommand> CommandRequested;

        /// <summary>Whether at least one enabled receiver is currently subscribed.</summary>
        public bool HasReceivers => CommandRequested != null;

        /// <summary>
        /// Delivers a command to every current receiver and reports whether any existed.
        /// </summary>
        protected bool Dispatch(TCommand command)
        {
            Action<TCommand> receivers = CommandRequested;
            if (receivers == null)
            {
                return false;
            }

            receivers.Invoke(command);
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
                channel = null;
                return;
            }

            TChannel previous = channel;
            channel = null;
            unsubscribe(previous);
        }
    }
}
