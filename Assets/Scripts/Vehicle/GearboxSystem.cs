using UnityEngine;

namespace Underground.Vehicle
{
    public class GearboxSystem : MonoBehaviour
    {
        public int CurrentGear { get; private set; } = 1;
        public float CurrentRPM { get; private set; }

        public void UpdateAutomatic(float wheelRpm, VehicleStatsData stats)
        {
            if (stats == null || stats.gearRatios == null || stats.gearRatios.Length <= 1)
            {
                return;
            }

            float currentRatio = GetCurrentDriveRatio(stats);
            CurrentRPM = CalculateEngineRpm(wheelRpm, currentRatio, stats);

            if (CurrentRPM > stats.shiftUpRPM && CurrentGear < stats.gearRatios.Length - 1)
            {
                CurrentGear++;
            }
            else if (CurrentRPM < stats.shiftDownRPM && CurrentGear > 1)
            {
                CurrentGear--;
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

        private static float CalculateEngineRpm(float wheelRpm, float driveRatio, VehicleStatsData stats)
        {
            return Mathf.Clamp(Mathf.Max(stats.idleRPM, Mathf.Abs(wheelRpm) * Mathf.Abs(driveRatio)), stats.idleRPM, stats.maxRPM);
        }
    }
}
