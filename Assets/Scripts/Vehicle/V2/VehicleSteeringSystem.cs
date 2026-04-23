using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Speed-adaptive steering with drift steer bonus.
    /// Low-speed: wide lock for tight turns. High-speed: narrow lock for stability.
    /// </summary>
    public sealed class VehicleSteeringSystem : MonoBehaviour
    {
        [Header("Steering Lock")]
        [SerializeField] private float lowSpeedSteerLock = 34f;
        [SerializeField] private float highSpeedSteerLock = 7.5f;
        [SerializeField] private float steeringAuthorityHighSpeedFloor = 0.38f;

        [Header("Drift Steering")]
        [SerializeField] private float driftSteerBonus = 2.5f;
        [SerializeField] private float slideSlipAngleThreshold = 15f;
        [SerializeField] private float fullSlideSlipAngle = 28f;

        public void UpdateSteering(
            WheelSet[] wheels,
            VehicleState state,
            DriverCommand cmd,
            RuntimeVehicleStats stats)
        {
            if (wheels == null || stats == null || state == null)
            {
                return;
            }

            float absForwardSpeed = Mathf.Abs(state.ForwardSpeedKph);
            float speedFactor = Mathf.InverseLerp(0f, Mathf.Max(1f, stats.MaxSpeedKph), absForwardSpeed);
            float steeringLockT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 205f, absForwardSpeed));
            float dynamicSteerLock = Mathf.Lerp(lowSpeedSteerLock, highSpeedSteerLock, steeringLockT);

            float highSpeedFloor = Mathf.Max(stats.HighSpeedSteerReduction, steeringAuthorityHighSpeedFloor);
            float steerReduction = Mathf.Lerp(1f, highSpeedFloor, speedFactor);

            // Drift steer bonus
            float slideBonus = state.IsSliding
                ? Mathf.InverseLerp(slideSlipAngleThreshold, fullSlideSlipAngle, state.SlipAngleDegrees) * driftSteerBonus
                : 0f;

            float targetSteerAngle = cmd.Steering * Mathf.Min(
                dynamicSteerLock + slideBonus,
                stats.MaxSteerAngle * steerReduction);

            float steerResponse = stats.SteeringResponse > 0f ? stats.SteeringResponse : 72f;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null || !wheel.steer)
                {
                    continue;
                }

                wheel.collider.steerAngle = Mathf.MoveTowards(
                    wheel.collider.steerAngle,
                    targetSteerAngle,
                    steerResponse * Time.fixedDeltaTime);
            }
        }
    }
}
