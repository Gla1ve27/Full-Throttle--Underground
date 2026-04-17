namespace Underground.Race
{
    public static class RaceCatalog
    {
        public static string NormalizeRaceId(string raceId)
        {
            return string.IsNullOrWhiteSpace(raceId)
                ? string.Empty
                : raceId.Trim();
        }
    }
}
