// ============================================================
// WorldTimeChangedEvent.cs
// Place at: Assets/FullThrottle/Core/Events/WorldTimeChangedEvent.cs
//
// Published by DayNightCycleController every 0.05h of game time.
// Consumed by VehicleLightController and PlayerReflectionProbeController.
// ============================================================

namespace Underground.TimeSystem
{
    public readonly struct WorldTimeChangedEvent
    {
        /// <summary>Current time of day in 0–24 range.</summary>
        public readonly float TimeOfDay;

        /// <summary>True when time is outside 6h–20h (night driving).</summary>
        public readonly bool IsNight;

        public WorldTimeChangedEvent(float timeOfDay, bool isNight)
        {
            TimeOfDay = timeOfDay;
            IsNight   = isNight;
        }
    }
}
