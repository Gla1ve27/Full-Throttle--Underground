using System;
using System.Collections.Generic;
using FullThrottle.SacredCore.Race;

namespace FullThrottle.SacredCore.Runtime
{
    public interface IFTSignal { }

    public sealed class FTSignalBus
    {
        private readonly Dictionary<Type, Delegate> handlers = new();

        public void Subscribe<T>(Action<T> listener) where T : struct, IFTSignal
        {
            if (listener == null) return;
            Type key = typeof(T);
            handlers.TryGetValue(key, out Delegate current);
            handlers[key] = Delegate.Combine(current, listener);
        }

        public void Unsubscribe<T>(Action<T> listener) where T : struct, IFTSignal
        {
            if (listener == null) return;
            Type key = typeof(T);
            if (!handlers.TryGetValue(key, out Delegate current)) return;
            Delegate updated = Delegate.Remove(current, listener);
            if (updated == null) handlers.Remove(key);
            else handlers[key] = updated;
        }

        public void Raise<T>(T signal) where T : struct, IFTSignal
        {
            if (handlers.TryGetValue(typeof(T), out Delegate current) && current is Action<T> callback)
            {
                callback.Invoke(signal);
            }
        }
    }

    public readonly struct FTCarSelectionChangedSignal : IFTSignal
    {
        public readonly string CarId;
        public FTCarSelectionChangedSignal(string carId) => CarId = carId;
    }

    public readonly struct FTWorldTravelQueuedSignal : IFTSignal
    {
        public readonly string CarId;
        public readonly string SpawnPointId;
        public FTWorldTravelQueuedSignal(string carId, string spawnPointId)
        {
            CarId = carId;
            SpawnPointId = spawnPointId;
        }
    }

    public readonly struct FTMoneyChangedSignal : IFTSignal
    {
        public readonly int Money;
        public FTMoneyChangedSignal(int money) => Money = money;
    }

    public readonly struct FTRepChangedSignal : IFTSignal
    {
        public readonly int Reputation;
        public FTRepChangedSignal(int reputation) => Reputation = reputation;
    }

    public readonly struct FTHeatChangedSignal : IFTSignal
    {
        public readonly int Heat;
        public FTHeatChangedSignal(int heat) => Heat = heat;
    }

    public readonly struct FTWagerChangedSignal : IFTSignal
    {
        public readonly int Exposure;
        public FTWagerChangedSignal(int exposure) => Exposure = exposure;
    }

    public readonly struct FTRaceResolvedSignal : IFTSignal
    {
        public readonly FTRaceDefinition Race;
        public readonly bool Won;
        public FTRaceResolvedSignal(FTRaceDefinition race, bool won)
        {
            Race = race;
            Won = won;
        }
    }

    public readonly struct FTSacredCoreHealthSignal : IFTSignal
    {
        public readonly bool Passed;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly string Summary;

        public FTSacredCoreHealthSignal(bool passed, int errorCount, int warningCount, string summary)
        {
            Passed = passed;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Summary = summary;
        }
    }
}
