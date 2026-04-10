using System;
using UnityEngine;

namespace Underground.Vehicle
{
    public class GearboxSystem : MonoBehaviour
    {
        [SerializeField] private float minTimeBetweenShifts = 0.4f;
        [SerializeField] private bool useManualTransmission;

        public event Action<int, int> GearChanged;

        public int CurrentGear { get; private set; } = 1;
        public float CurrentRPM { get; private set; }
        public float LastShiftTime { get; private set; } = -999f;
        public bool UseManualTransmission => useManualTransmission;

        public void UpdateAutomatic(float wheelRpm, VehicleStatsData stats)
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

            float currentRatio = GetCurrentDriveRatio(stats);
            CurrentRPM = CalculateEngineRpm(wheelRpm, currentRatio, stats);
            int previousGear = CurrentGear;
            bool canShift = Time.time - LastShiftTime >= minTimeBetweenShifts;

            if (canShift && CurrentRPM > stats.shiftUpRPM && CurrentGear < stats.gearRatios.Length - 1)
            {
                CurrentGear++;
            }
            else if (canShift && CurrentRPM < stats.shiftDownRPM && CurrentGear > 1)
            {
                CurrentGear--;
            }

            if (CurrentGear != previousGear)
            {
                LastShiftTime = Time.time;
                CurrentRPM = CalculateEngineRpm(wheelRpm, GetCurrentDriveRatio(stats), stats);
                GearChanged?.Invoke(previousGear, CurrentGear);
            }
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
            CurrentRPM = CalculateEngineRpm(wheelRpm, GetCurrentDriveRatio(stats), stats);
            GearChanged?.Invoke(previousGear, CurrentGear);
            return true;
        }

        private static float CalculateEngineRpm(float wheelRpm, float driveRatio, VehicleStatsData stats)
        {
            return Mathf.Clamp(Mathf.Max(stats.idleRPM, Mathf.Abs(wheelRpm) * Mathf.Abs(driveRatio)), stats.idleRPM, stats.maxRPM);
        }
    }
}
