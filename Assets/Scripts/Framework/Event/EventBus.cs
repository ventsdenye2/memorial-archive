using System;
using System.Collections.Generic;

namespace MemorialArchive.Framework.Event
{
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> listeners = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<TEvent>(Action<TEvent> listener)
        {
            var eventType = typeof(TEvent);
            if (!listeners.TryGetValue(eventType, out var eventListeners))
            {
                eventListeners = new List<Delegate>();
                listeners[eventType] = eventListeners;
            }

            if (!eventListeners.Contains(listener))
            {
                eventListeners.Add(listener);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> listener)
        {
            var eventType = typeof(TEvent);
            if (!listeners.TryGetValue(eventType, out var eventListeners))
            {
                return;
            }

            eventListeners.Remove(listener);
            if (eventListeners.Count == 0)
            {
                listeners.Remove(eventType);
            }
        }

        public void Publish<TEvent>(TEvent gameEvent)
        {
            var eventType = typeof(TEvent);
            if (!listeners.TryGetValue(eventType, out var eventListeners))
            {
                return;
            }

            var snapshot = eventListeners.ToArray();
            foreach (var listener in snapshot)
            {
                ((Action<TEvent>)listener).Invoke(gameEvent);
            }
        }

        public void Clear()
        {
            listeners.Clear();
        }
    }
}
