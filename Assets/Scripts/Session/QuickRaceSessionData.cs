using Underground.Race;

namespace Underground.Session
{
    public enum QuickRaceTransmissionMode
    {
        Automatic,
        Manual
    }

    public static class QuickRaceSessionData
    {
        public static bool HasPendingLaunch { get; private set; }
        public static bool IsActive { get; private set; }
        public static string SelectedRaceId { get; private set; }
        public static string SelectedCarId { get; private set; }
        public static RaceType SelectedRaceType { get; private set; }
        public static QuickRaceTransmissionMode TransmissionMode { get; private set; } = QuickRaceTransmissionMode.Automatic;

        public static void Configure(string raceId, string carId, RaceType raceType, QuickRaceTransmissionMode transmissionMode)
        {
            SelectedRaceId = RaceCatalog.NormalizeRaceId(raceId);
            SelectedCarId = carId;
            SelectedRaceType = raceType;
            TransmissionMode = transmissionMode;
            HasPendingLaunch = !string.IsNullOrEmpty(SelectedRaceId);
            IsActive = HasPendingLaunch;
        }

        public static void MarkLaunchConsumed()
        {
            HasPendingLaunch = false;
        }

        public static bool IsSelectedRace(string raceId)
        {
            string resolvedRaceId = RaceCatalog.NormalizeRaceId(raceId);
            return !string.IsNullOrEmpty(SelectedRaceId)
                && string.Equals(SelectedRaceId, resolvedRaceId, System.StringComparison.OrdinalIgnoreCase);
        }

        public static void Clear()
        {
            HasPendingLaunch = false;
            IsActive = false;
            SelectedRaceId = string.Empty;
            SelectedCarId = string.Empty;
            SelectedRaceType = RaceType.Sprint;
            TransmissionMode = QuickRaceTransmissionMode.Automatic;
        }
    }
}
