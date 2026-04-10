using System;
using System.Collections.Generic;

namespace Underground.Race
{
    public readonly struct QuickRaceTrackEntry
    {
        public QuickRaceTrackEntry(string raceId, string displayName, RaceType raceType)
        {
            RaceId = raceId;
            DisplayName = displayName;
            RaceType = raceType;
        }

        public string RaceId { get; }
        public string DisplayName { get; }
        public RaceType RaceType { get; }
    }

    public static class RaceCatalog
    {
        private static readonly QuickRaceTrackEntry[] KnownTracks =
        {
            new QuickRaceTrackEntry("day_sprint", "Industrial Sprint", RaceType.Sprint),
            new QuickRaceTrackEntry("night_run", "Midnight Run", RaceType.Underground),
            new QuickRaceTrackEntry("wager_run", "Neon Pinkslip", RaceType.Wager)
        };

        public static IReadOnlyList<QuickRaceTrackEntry> Tracks => KnownTracks;

        public static string NormalizeRaceId(string raceId)
        {
            return string.IsNullOrWhiteSpace(raceId)
                ? string.Empty
                : raceId.Trim();
        }

        public static bool TryGetTrack(string raceId, out QuickRaceTrackEntry entry)
        {
            string resolvedRaceId = NormalizeRaceId(raceId);
            for (int i = 0; i < KnownTracks.Length; i++)
            {
                if (string.Equals(KnownTracks[i].RaceId, resolvedRaceId, StringComparison.OrdinalIgnoreCase))
                {
                    entry = KnownTracks[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
