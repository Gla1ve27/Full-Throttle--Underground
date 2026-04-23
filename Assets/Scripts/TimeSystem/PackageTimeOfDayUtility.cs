using UnityEngine;

namespace Underground.TimeSystem
{
    public static class PackageTimeOfDayUtility
    {
        public const float DuskStartHour = 17.25f;
        public const float DawnEndHour = 5.25f;
        public const float DefaultDuskNightHour = 21f;

        public static TimeOfDay FindPackageTimeOfDay()
        {
            return Object.FindFirstObjectByType<TimeOfDay>();
        }

        public static float GetHours(TimeOfDay timeOfDay)
        {
            return ConstrainToDuskNightHours(GetRawHours(timeOfDay));
        }

        public static float GetRawHours(TimeOfDay timeOfDay)
        {
            return timeOfDay == null
                ? DefaultDuskNightHour
                : Mathf.Repeat(timeOfDay.seconds_passed / TimeOfDay.seconds_in_day * 24f, 24f);
        }

        public static bool IsNight(TimeOfDay timeOfDay)
        {
            return IsDuskNightHour(GetHours(timeOfDay));
        }

        public static bool IsDuskNightHour(float hours)
        {
            hours = Mathf.Repeat(hours, 24f);
            return hours >= DuskStartHour || hours < DawnEndHour;
        }

        public static float ConstrainToDuskNightHours(float hours)
        {
            hours = Mathf.Repeat(hours, 24f);
            return IsDuskNightHour(hours) ? hours : DefaultDuskNightHour;
        }

        public static void SetHours(TimeOfDay timeOfDay, float hours)
        {
            SetUnrestrictedHours(timeOfDay, ConstrainToDuskNightHours(hours));
        }

        public static void SetUnrestrictedHours(TimeOfDay timeOfDay, float hours)
        {
            if (timeOfDay == null)
            {
                return;
            }

            float clampedHours = Mathf.Repeat(hours, 24f);
            int wholeHours = Mathf.FloorToInt(clampedHours);
            float minuteFraction = (clampedHours - wholeHours) * 60f;
            int wholeMinutes = Mathf.FloorToInt(minuteFraction);
            int wholeSeconds = Mathf.FloorToInt((minuteFraction - wholeMinutes) * 60f);
            timeOfDay.SetTimeOfDay(wholeHours, wholeMinutes, wholeSeconds);
        }
    }
}
