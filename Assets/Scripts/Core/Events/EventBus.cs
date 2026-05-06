using System;
using System.Collections.Generic;

namespace KingCardsSpire.Core.Events
{
    public sealed class EventBus
    {
        readonly Dictionary<Type, Delegate> _handlers = new();

        public void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            if (handler == null)
                return;
            var key = typeof(T);
            if (_handlers.TryGetValue(key, out var existing))
                _handlers[key] = Delegate.Combine(existing, handler);
            else
                _handlers[key] = handler;
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            if (handler == null)
                return;
            var key = typeof(T);
            if (!_handlers.TryGetValue(key, out var existing))
                return;
            var result = Delegate.Remove(existing, handler);
            if (result == null)
                _handlers.Remove(key);
            else
                _handlers[key] = result;
        }

        public void Publish<T>(T evt) where T : IEvent
        {
            if (evt == null)
                return;
            if (!_handlers.TryGetValue(typeof(T), out var del) || del == null)
                return;
            foreach (var d in del.GetInvocationList())
            {
                try
                {
                    ((Action<T>)d)?.Invoke(evt);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
