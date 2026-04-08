using System;
using System.Collections.Generic;

namespace Underground.Core.Architecture
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> handlersByEventType = new Dictionary<Type, Delegate>();

        public void Publish<TEvent>(TEvent eventData) where TEvent : struct
        {
            if (handlersByEventType.TryGetValue(typeof(TEvent), out Delegate callback) && callback is Action<TEvent> typedCallback)
            {
                typedCallback.Invoke(eventData);
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type eventType = typeof(TEvent);
            if (handlersByEventType.TryGetValue(eventType, out Delegate existing))
            {
                handlersByEventType[eventType] = Delegate.Combine(existing, handler);
            }
            else
            {
                handlersByEventType[eventType] = handler;
            }

            return new EventSubscription<TEvent>(this, handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                return;
            }

            Type eventType = typeof(TEvent);
            if (!handlersByEventType.TryGetValue(eventType, out Delegate existing))
            {
                return;
            }

            Delegate updated = Delegate.Remove(existing, handler);
            if (updated == null)
            {
                handlersByEventType.Remove(eventType);
                return;
            }

            handlersByEventType[eventType] = updated;
        }

        public void Clear()
        {
            handlersByEventType.Clear();
        }

        private sealed class EventSubscription<TEvent> : IDisposable where TEvent : struct
        {
            private readonly EventBus owner;
            private Action<TEvent> handler;

            public EventSubscription(EventBus owner, Action<TEvent> handler)
            {
                this.owner = owner;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (handler == null)
                {
                    return;
                }

                owner.Unsubscribe(handler);
                handler = null;
            }
        }
    }
}
