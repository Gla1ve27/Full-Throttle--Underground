using System;

namespace FullThrottle.SacredCore.Runtime
{
    /// <summary>
    /// Public event-bus facade for the new FT core. It wraps the existing signal bus so
    /// old FT scripts and new FT scripts do not split into two event systems.
    /// </summary>
    public sealed class FTEventBus
    {
        private readonly FTSignalBus signalBus;

        public FTEventBus(FTSignalBus signalBus)
        {
            this.signalBus = signalBus ?? throw new ArgumentNullException(nameof(signalBus));
        }

        public void Subscribe<T>(Action<T> listener) where T : struct, IFTSignal
        {
            signalBus.Subscribe(listener);
        }

        public void Unsubscribe<T>(Action<T> listener) where T : struct, IFTSignal
        {
            signalBus.Unsubscribe(listener);
        }

        public void Raise<T>(T signal) where T : struct, IFTSignal
        {
            signalBus.Raise(signal);
        }
    }
}
