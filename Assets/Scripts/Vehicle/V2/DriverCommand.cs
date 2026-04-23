namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Immutable per-frame snapshot of driver intent.
    /// Produced by <see cref="VehicleInputAdapter"/>, consumed by every physics system.
    /// Separates input reading from physics consumption.
    /// </summary>
    public readonly struct DriverCommand
    {
        public readonly float Throttle;
        public readonly float Brake;
        public readonly float Steering;
        public readonly bool Handbrake;
        public readonly bool ReverseHeld;
        public readonly bool UpshiftRequested;
        public readonly bool DownshiftRequested;

        public DriverCommand(
            float throttle,
            float brake,
            float steering,
            bool handbrake,
            bool reverseHeld,
            bool upshiftRequested = false,
            bool downshiftRequested = false)
        {
            Throttle = throttle;
            Brake = brake;
            Steering = steering;
            Handbrake = handbrake;
            ReverseHeld = reverseHeld;
            UpshiftRequested = upshiftRequested;
            DownshiftRequested = downshiftRequested;
        }

        public static readonly DriverCommand None = new DriverCommand(0f, 0f, 0f, false, false);
    }
}
