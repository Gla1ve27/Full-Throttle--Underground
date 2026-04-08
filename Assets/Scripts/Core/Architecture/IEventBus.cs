using System;

namespace Underground.Core.Architecture
{
    public interface IEventBus
    {
        void Publish<TEvent>(TEvent eventData) where TEvent : struct;
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
        void Clear();
    }
}
