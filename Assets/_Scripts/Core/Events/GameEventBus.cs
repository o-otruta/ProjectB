using System;
using System.Collections.Generic;

namespace ProjectB.Core.Events
{
    public class GameEventBus : IDisposable
    {
        private readonly Dictionary<Type, Delegate> _subscribers = new Dictionary<Type, Delegate>();

        public GameEventBus()
        {
        }

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var existing))
            {
                _subscribers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _subscribers[type] = handler;
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                {
                    _subscribers.Remove(type);
                }
                else
                {
                    _subscribers[type] = result;
                }
            }
        }

        public void Publish<T>(T evt) where T : struct
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var del) && del is Action<T> action)
            {
                action.Invoke(evt);
            }
        }

        public void Clear()
        {
            _subscribers.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}

