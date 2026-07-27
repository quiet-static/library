using System;
using System.Collections.Generic;

namespace QuietStatic
{
    /// <summary>
    /// Marker interface for payloads that can be published through <see cref="EventBus{T}"/>.
    /// </summary>
    public interface IEvent { }

    /// <summary>
    /// Provides a process-wide, strongly typed publish/subscribe channel for one event payload type.
    /// </summary>
    /// <typeparam name="T">Event payload type delivered to every registered listener.</typeparam>
    /// <remarks>
    /// Each closed generic type owns an independent listener list. Listeners should normally
    /// subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c>. Publishing is synchronous:
    /// callbacks run immediately on the publishing thread, in reverse subscription order.
    /// </remarks>
    public static class EventBus<T> where T : IEvent
    {
        private static readonly List<Action<T>> listeners = new List<Action<T>>();

        /// <summary>Registers a listener if it is not already subscribed.</summary>
        /// <param name="listener">Callback that receives published event data.</param>
        public static void Subscribe(Action<T> listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        /// <summary>Removes a previously registered listener.</summary>
        /// <param name="listener">Callback to remove. Unknown callbacks are ignored.</param>
        public static void Unsubscribe(Action<T> listener)
        {
            if (listeners.Contains(listener))
            {
                listeners.Remove(listener);
            }
        }

        /// <summary>Synchronously delivers an event payload to all current listeners.</summary>
        /// <param name="eventData">Payload delivered unchanged to each callback.</param>
        /// <remarks>
        /// Iteration runs backward so a callback may safely unsubscribe itself. Exceptions are
        /// not swallowed; a listener exception stops the remaining publication and reaches the caller.
        /// </remarks>
        public static void Publish(T eventData)
        {
            // Loop backwards to allow unsubscriptions during execution safely
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i]?.Invoke(eventData);
            }
        }
    }
}
