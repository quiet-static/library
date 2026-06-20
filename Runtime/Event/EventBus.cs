using System;
using System.Collections.Generic;

namespace QuietStatic
{
    // Empty interface to define what can be treated as an event
    public interface IEvent { }

    public static class EventBus<T> where T : IEvent
    {
        private static readonly List<Action<T>> listeners = new List<Action<T>>();

        public static void Subscribe(Action<T> listener)
        {
            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        public static void Unsubscribe(Action<T> listener)
        {
            if (listeners.Contains(listener))
            {
                listeners.Remove(listener);
            }
        }

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
