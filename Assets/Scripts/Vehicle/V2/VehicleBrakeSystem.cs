using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Foot brake, handbrake, and reverse braking.
    /// </summary>
    public sealed class VehicleBrakeSystem : MonoBehaviour
    {
        [Header("Handbrake")]
        [SerializeField] private float maxHandbrakeTorque = 6500f;
        [SerializeField, Range(0f, 1f)] private float poweredHandbrakeTorqueScale = 0.28f;

        public void UpdateBraking(
            WheelSet[] wheels,
            VehicleState state,
            DriverCommand cmd,
            RuntimeVehicleStats stats)
        {
            if (wheels == null || stats == null)
            {
                return;
            }

            float footBrakeTorque = state.IsReversing && cmd.ReverseHeld
                ? 0f
                : cmd.Brake * stats.MaxBrakeTorque;

            float brakeGripMod = stats.BrakeGrip > 0f ? stats.BrakeGrip : 1f;
            footBrakeTorque *= brakeGripMod;

            float poweredRelease = Mathf.Lerp(1f, poweredHandbrakeTorqueScale, cmd.Throttle);
            float handbrakeTorque = cmd.Handbrake ? maxHandbrakeTorque * poweredRelease : 0f;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                wheel.collider.brakeTorque = footBrakeTorque + (wheel.handbrake ? handbrakeTorque : 0f);
            }
        }
    }
}
