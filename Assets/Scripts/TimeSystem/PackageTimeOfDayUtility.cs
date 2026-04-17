using UnityEngine;

namespace Underground.TimeSystem
{
    public static class PackageTimeOfDayUtility
    {
        public static TimeOfDay FindPackageTimeOfDay()
        {
            return Object.FindFirstObjectByType<TimeOfDay>();
        }

        public static float GetHours(TimeOfDay timeOfDay)
        {
            return timeOfDay == null
                ? 12f
                : Mathf.Repeat(timeOfDay.seconds_passed / TimeOfDay.seconds_in_day * 24f, 24f);
        }

        public static bool IsNight(TimeOfDay timeOfDay)
        {
            float hours = GetHours(timeOfDay);
            return hours >= 20f || hours < 6f;
        }

        public static void SetHours(TimeOfDay timeOfDay, float hours)
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
