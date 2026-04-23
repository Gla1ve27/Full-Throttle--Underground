using System;
using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// V2 gearbox with explicit shift state machine, torque cut during shifts,
    /// better kickdown logic, and post-shift recouple behavior.
    /// Replaces <see cref="Underground.Vehicle.V2.GearboxSystemV2"/> from the legacy stack.
    /// </summary>
    public sealed class GearboxSystemV2 : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private bool useManualTransmission;

        [Header("Shift Timing")]
        [SerializeField] private float upshiftDuration = 0.16f;
        [SerializeField] private float downshiftDuration = 0.10f;
        [SerializeField] private float minTimeBetweenShifts = 0.42f;
        [SerializeField] private float downshiftLockoutAfterUpshift = 0.72f;
        [SerializeField, Range(0f, 1f)] private float shiftTorqueCut = 0.38f;

        [Header("Automatic Shift Points")]
        [SerializeField, Range(0f, 1f)] private float fullThrottleUpshiftRpm01 = 0.92f;
        [SerializeField, Range(0f, 1f)] private float cruiseUpshiftRpm01 = 0.78f;
        [SerializeField, Range(0f, 1f)] private float coastUpshiftRpm01 = 0.70f;
        [SerializeField, Range(0f, 1f)] private float normalDownshiftRpm01 = 0.34f;
        [SerializeField, Range(0f, 1f)] private float kickdownThrottle = 0.82f;
        [SerializeField, Range(0f, 1f)] private float kickdownRpm01 = 0.56f;
        [SerializeField, Range(0f, 1f)] private float postUpshiftRpmFloor01 = 0.42f;
        [SerializeField, Range(0f, 1f)] private float lowerGearSafetyRpm01 = 0.93f;
        [SerializeField, Range(0f, 1f)] private float throttleLiftHoldRpm01 = 0.26f;
        [SerializeField] private float minimumUpshiftSpeedKph = 18f;
        [SerializeField] private float minimumSpeedGainPerGearKph = 22f;

        public event Action<int, int, ShiftDirection> GearChanged;

        public int CurrentGear { get; private set; } = 1;
        public bool UseManualTransmission => useManualTransmission;
        public float ShiftTorqueCut => shiftTorqueCut;

        private float lastShiftTime = -999f;
        private float shiftCompleteTime = -999f;
        private float lastAutomaticThrottle;

        public void SetUseManualTransmission(bool enabled)
        {
            useManualTransmission = enabled;
        }

        /// <summary>
        /// Main update. Reads the VehicleState and DriverCommand, decides gear,
        /// writes shift state back into VehicleState.
        /// </summary>
        public void UpdateGearbox(
            VehicleState state,
            DriverCommand cmd,
            VehicleStatsData stats)
        {
            if (state == null || stats == null || stats.gearRatios == null || stats.gearRatios.Length <= 1)
            {
                return;
            }

            int maxGear = stats.gearRatios.Length - 1;
            bool canShift = Time.time - lastShiftTime >= minTimeBetweenShifts;

            // ── Shift State Tracking ──
            bool isShifting = Time.time < shiftCompleteTime;
            float shiftProgress = 1f;
            if (isShifting)
            {
                float shiftDuration = state.LastShiftDirection == ShiftDirection.Down ? downshiftDuration : upshiftDuration;
                shiftProgress = shiftDuration > 0f ? 1f - Mathf.Clamp01((shiftCompleteTime - Time.time) / shiftDuration) : 1f;
            }

            state.IsShifting = isShifting;
            state.ShiftProgress = shiftProgress;

            // ── Gear Selection ──
            if (state.IsReversing)
            {
                // In reverse, lock to gear 1 for RPM computation
                CurrentGear = 1;
            }
            else if (useManualTransmission)
            {
                if (canShift && cmd.UpshiftRequested && CurrentGear < maxGear)
                {
                    ExecuteShift(state, stats, CurrentGear + 1, ShiftDirection.Up);
                }
                else if (canShift && cmd.DownshiftRequested && CurrentGear > 1)
                {
                    ExecuteShift(state, stats, CurrentGear - 1, ShiftDirection.Down);
                }
            }
            else
            {
                // Automatic transmission logic
                int previousGear = CurrentGear;
                float throttleDelta = cmd.Throttle - lastAutomaticThrottle;

                if (canShift && ShouldUpshift(state, stats, cmd.Throttle, Mathf.Abs(state.ForwardSpeedKph), maxGear))
                {
                    ExecuteShift(state, stats, CurrentGear + 1, ShiftDirection.Up);
                }
                else if (canShift && ShouldDownshift(state, stats, cmd.Throttle, throttleDelta, Mathf.Abs(state.ForwardSpeedKph)))
                {
                    ExecuteShift(state, stats, CurrentGear - 1, ShiftDirection.Down);
                }

                lastAutomaticThrottle = cmd.Throttle;
            }

            // ── Write state ──
            state.Gear = CurrentGear;

            // ── Clutch engagement during shift ──
            if (isShifting)
            {
                // Disengage clutch at start of shift, re-engage toward end
                float clutchCurve = shiftProgress < 0.5f
                    ? Mathf.Lerp(1f, 0.15f, shiftProgress * 2f)
                    : Mathf.Lerp(0.15f, 1f, (shiftProgress - 0.5f) * 2f);
                state.ClutchEngagement = clutchCurve;
            }
            else
            {
                state.ClutchEngagement = 1f;
            }
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

        public float GetReverseDriveRatio(VehicleStatsData stats)
        {
            if (stats == null || stats.gearRatios == null || stats.gearRatios.Length <= 1)
            {
                return 1f;
            }

            return Mathf.Abs(stats.gearRatios[1] * stats.finalDriveRatio);
        }

        public void ResetToFirstGear()
        {
            CurrentGear = 1;
            lastShiftTime = -999f;
            shiftCompleteTime = -999f;
            lastAutomaticThrottle = 0f;
        }

        private void ExecuteShift(VehicleState state, VehicleStatsData stats, int newGear, ShiftDirection direction)
        {
            int prevGear = CurrentGear;
            CurrentGear = Mathf.Clamp(newGear, 1, stats.gearRatios.Length - 1);

            if (CurrentGear == prevGear)
            {
                return;
            }

            lastShiftTime = Time.time;
            float duration = direction == ShiftDirection.Down ? downshiftDuration : upshiftDuration;
            shiftCompleteTime = Time.time + duration;

            state.PreviousGear = prevGear;
            state.LastShiftDirection = direction;

            GearChanged?.Invoke(prevGear, CurrentGear, direction);
        }

        private bool ShouldUpshift(VehicleState state, VehicleStatsData stats, float throttle, float speedKph, int maxGear)
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

            float rpm01 = state.NormalizedRPM;
            float throttleShiftPoint = Mathf.Lerp(cruiseUpshiftRpm01, fullThrottleUpshiftRpm01, throttle);
            float coastShiftPoint = Mathf.Lerp(coastUpshiftRpm01, throttleShiftPoint, throttle);
            if (rpm01 < coastShiftPoint)
            {
                return false;
            }

            // Safety: don't upshift if the next gear would drop RPM below the floor
            float nextRatio = stats.gearRatios[CurrentGear + 1] * stats.finalDriveRatio;
            float currentRatio = GetCurrentDriveRatio(stats);
            float rpmInNextGear = currentRatio > 0.01f ? state.EngineRPM * (nextRatio / currentRatio) : stats.idleRPM;
            float nextRpm01 = Mathf.InverseLerp(stats.idleRPM, stats.maxRPM, rpmInNextGear);
            return nextRpm01 >= postUpshiftRpmFloor01;
        }

        private bool ShouldDownshift(VehicleState state, VehicleStatsData stats, float throttle, float throttleDelta, float speedKph)
        {
            if (CurrentGear <= 1 || Time.time - lastShiftTime < downshiftLockoutAfterUpshift)
            {
                return false;
            }

            float rpm01 = state.NormalizedRPM;
            float lowerRatio = stats.gearRatios[CurrentGear - 1] * stats.finalDriveRatio;
            float currentRatio = GetCurrentDriveRatio(stats);
            float lowerRpm = currentRatio > 0.01f ? state.EngineRPM * (lowerRatio / currentRatio) : stats.maxRPM;
            float lowerRpm01 = Mathf.InverseLerp(stats.idleRPM, stats.maxRPM, lowerRpm);
            bool lowerGearSafe = lowerRpm01 < lowerGearSafetyRpm01;
            bool throttleLiftHold = throttle < 0.08f && rpm01 > throttleLiftHoldRpm01;
            bool normalLugging = rpm01 < normalDownshiftRpm01 && throttle < kickdownThrottle && !throttleLiftHold;
            bool kickdown = throttle >= kickdownThrottle && throttleDelta > 0.25f && rpm01 < kickdownRpm01;

            return lowerGearSafe && speedKph > 1f && (normalLugging || kickdown);
        }
    }
}


