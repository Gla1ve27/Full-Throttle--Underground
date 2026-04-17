using System;
using UnityEngine;

namespace Underground.Vehicle
{
    public class GearboxSystem : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private bool useManualTransmission;

        [Header("Arcade Automatic")]
        [SerializeField] private float minTimeBetweenShifts = 0.42f;
        [SerializeField] private float upshiftDuration = 0.16f;
        [SerializeField] private float downshiftLockoutAfterUpshift = 0.72f;
        [SerializeField] private float minimumUpshiftSpeedKph = 18f;
        [SerializeField] private float minimumSpeedGainPerGearKph = 22f;
        [SerializeField, Range(0f, 1f)] private float fullThrottleUpshiftRpm01 = 0.92f;
        [SerializeField, Range(0f, 1f)] private float cruiseUpshiftRpm01 = 0.78f;
        [SerializeField, Range(0f, 1f)] private float coastUpshiftRpm01 = 0.7f;
        [SerializeField, Range(0f, 1f)] private float normalDownshiftRpm01 = 0.34f;
        [SerializeField, Range(0f, 1f)] private float kickdownThrottle = 0.82f;
        [SerializeField, Range(0f, 1f)] private float kickdownRpm01 = 0.56f;
        [SerializeField, Range(0f, 1f)] private float postUpshiftRpmFloor01 = 0.42f;
        [SerializeField, Range(0f, 1f)] private float lowerGearSafetyRpm01 = 0.93f;
        [SerializeField, Range(0f, 1f)] private float throttleLiftHoldRpm01 = 0.26f;

        public event Action<int, int> GearChanged;

        public int CurrentGear { get; private set; } = 1;
        public float CurrentRPM { get; private set; }
        public float LastShiftTime { get; private set; } = -999f;
        public bool UseManualTransmission => useManualTransmission;
        public bool IsShifting => Time.time < shiftCompleteTime;
        public float ShiftBlend => IsShifting && upshiftDuration > 0f
            ? 1f - Mathf.Clamp01((shiftCompleteTime - Time.time) / upshiftDuration)
            : 1f;

        private float lastAutomaticThrottle;
        private float shiftCompleteTime = -999f;

        public void UpdateAutomatic(float wheelRpm, VehicleStatsData stats, float throttle01 = 0f, float speedKph = 0f)
        {
            if (stats == null || stats.gearRatios == null || stats.gearRatios.Length <= 1)
            {
                return;
            }

            if (useManualTransmission)
            {
                UpdateRpmOnly(wheelRpm, stats, GetCurrentDriveRatio(stats));
                return;
            }

            float throttle = Mathf.Clamp01(throttle01);
            float throttleDelta = throttle - lastAutomaticThrottle;
            CurrentRPM = CalculateEngineRpm(wheelRpm, GetCurrentDriveRatio(stats), stats);
            int previousGear = CurrentGear;
            bool canShift = Time.time - LastShiftTime >= minTimeBetweenShifts;
            int maxGear = stats.gearRatios.Length - 1;

            if (canShift && ShouldUpshift(wheelRpm, stats, throttle, Mathf.Abs(speedKph), maxGear))
            {
                CurrentGear++;
            }
            else if (canShift && ShouldDownshift(wheelRpm, stats, throttle, throttleDelta, Mathf.Abs(speedKph)))
            {
                CurrentGear--;
            }

            if (CurrentGear != previousGear)
            {
                LastShiftTime = Time.time;
                shiftCompleteTime = Time.time + upshiftDuration;
                CurrentRPM = CalculateEngineRpm(wheelRpm, GetCurrentDriveRatio(stats), stats);
                GearChanged?.Invoke(previousGear, CurrentGear);
            }

            lastAutomaticThrottle = throttle;
        }

        public void UpdateRpmOnly(float wheelRpm, VehicleStatsData stats, float driveRatio)
        {
            if (stats == null)
            {
                return;
            }

            CurrentRPM = CalculateEngineRpm(wheelRpm, driveRatio, stats);
        }

        public float GetCurrentDriveRatio(VehicleStatsData stats)
        {
            if (stats == null || stats.gearRatios == null || stats.gearRatios.Length <= 1)
            {
                return 1f;
            }

            int gearIndex = Mathf.Clamp(CurrentGear, 1, stats.gearRatios.Length - 1);
            return stats.gearRatios[gearIndex] * stats.finalDriveRatio;
        }

        public void ResetToFirstGear()
        {
            CurrentGear = 1;
            CurrentRPM = 0f;
            lastAutomaticThrottle = 0f;
            LastShiftTime = -999f;
            shiftCompleteTime = -999f;
        }

        public void SetUseManualTransmission(bool enabled)
        {
            useManualTransmission = enabled;
        }

        public bool TryShiftUp(VehicleStatsData stats, float wheelRpm)
        {
            return TryShift(stats, wheelRpm, 1);
        }

        public bool TryShiftDown(VehicleStatsData stats, float wheelRpm)
        {
            return TryShift(stats, wheelRpm, -1);
        }

        private bool TryShift(VehicleStatsData stats, float wheelRpm, int direction)
        {
            if (stats == null || stats.gearRatios == null || stats.gearRatios.Length <= 1)
            {
                return false;
            }

            if (Time.time - LastShiftTime < minTimeBetweenShifts)
            {
                return false;
            }

            int previousGear = CurrentGear;
            int maxGear = stats.gearRatios.Length - 1;
            CurrentGear = Mathf.Clamp(CurrentGear + direction, 1, maxGear);
            if (CurrentGear == previousGear)
            {
                return false;
            }

            LastShiftTime = Time.time;
            shiftCompleteTime = Time.time + upshiftDuration;
            CurrentRPM = CalculateEngineRpm(wheelRpm, GetCurrentDriveRatio(stats), stats);
            GearChanged?.Invoke(previousGear, CurrentGear);
            return true;
        }

        private static float CalculateEngineRpm(float wheelRpm, float driveRatio, VehicleStatsData stats)
        {
            return Mathf.Clamp(Mathf.Max(stats.idleRPM, Mathf.Abs(wheelRpm) * Mathf.Abs(driveRatio)), stats.idleRPM, stats.maxRPM);
        }

        private bool ShouldUpshift(float wheelRpm, VehicleStatsData stats, float throttle01, float speedKph, int maxGear)
        {
            if (CurrentGear >= maxGear)
            {
                return false;
            }

            float gearSpeedFloor = minimumUpshiftSpeedKph + (CurrentGear - 1) * minimumSpeedGainPerGearKph;
            if (speedKph < gearSpeedFloor)
            {
                return false;
            }

            float rpm01 = Mathf.InverseLerp(stats.idleRPM, stats.maxRPM, CurrentRPM);
            float throttleShiftPoint = Mathf.Lerp(cruiseUpshiftRpm01, fullThrottleUpshiftRpm01, throttle01);
            float coastShiftPoint = Mathf.Lerp(coastUpshiftRpm01, throttleShiftPoint, throttle01);
            if (rpm01 < coastShiftPoint)
            {
                return false;
            }

            // Prevent arcade launch wheelspin from walking the gearbox upward before
            // the car has enough road speed to live in the next gear.
            float nextRpm = CalculateEngineRpm(wheelRpm, stats.gearRatios[CurrentGear + 1] * stats.finalDriveRatio, stats);
            float nextRpm01 = Mathf.InverseLerp(stats.idleRPM, stats.maxRPM, nextRpm);
            return nextRpm01 >= postUpshiftRpmFloor01;
        }

        private bool ShouldDownshift(float wheelRpm, VehicleStatsData stats, float throttle01, float throttleDelta, float speedKph)
        {
            if (CurrentGear <= 1 || Time.time - LastShiftTime < downshiftLockoutAfterUpshift)
            {
                return false;
            }

            float rpm01 = Mathf.InverseLerp(stats.idleRPM, stats.maxRPM, CurrentRPM);
            float lowerRpm = CalculateEngineRpm(wheelRpm, stats.gearRatios[CurrentGear - 1] * stats.finalDriveRatio, stats);
            float lowerRpm01 = Mathf.InverseLerp(stats.idleRPM, stats.maxRPM, lowerRpm);
            bool lowerGearSafe = lowerRpm01 < lowerGearSafetyRpm01;
            bool throttleLiftHold = throttle01 < 0.08f && rpm01 > throttleLiftHoldRpm01;
            bool normalLugging = rpm01 < normalDownshiftRpm01 && throttle01 < kickdownThrottle && !throttleLiftHold;
            bool kickdown = throttle01 >= kickdownThrottle && throttleDelta > 0.25f && rpm01 < kickdownRpm01;

            return lowerGearSafe && speedKph > 1f && (normalLugging || kickdown);
        }
    }
}
