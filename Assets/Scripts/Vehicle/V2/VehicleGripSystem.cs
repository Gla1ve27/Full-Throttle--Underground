using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Layered dynamic grip: base grip, weight transfer, slip penalty,
    /// speed falloff, throttle-under-steer loss, and per-axle stats.
    /// </summary>
    public sealed class VehicleGripSystem : MonoBehaviour
    {
        [Header("Grip Model")]
        [SerializeField] private float baseLoadGripBias = 0.92f;
        [SerializeField] private float maxLoadGripBias = 1.08f;
        [SerializeField] private float slipGripPenalty = 0.18f;

        [Header("Slide Friction")]
        [SerializeField] private float slideSlipAngleThreshold = 15f;
        [SerializeField] private float fullSlideSlipAngle = 28f;
        [SerializeField] private float slideSideFrictionMultiplier = 0.7f;
        [SerializeField] private float slideForwardFrictionMultiplier = 0.96f;

        public void UpdateGrip(
            WheelSet[] wheels,
            VehicleState state,
            DriverCommand cmd,
            RuntimeVehicleStats stats,
            float driftAssistBlend)
        {
            if (wheels == null || stats == null || state == null)
            {
                return;
            }

            float slideT = GetSlideBlend(state);
            float slideSideMultiplier = Mathf.Lerp(1f, slideSideFrictionMultiplier, slideT);
            float slideForwardMultiplier = Mathf.Lerp(1f, slideForwardFrictionMultiplier, slideT);
            float speedGripFalloff = Mathf.Lerp(1f, 0.92f, Mathf.InverseLerp(180f, 350f, state.SpeedKph));
            float steerMagnitude = Mathf.Abs(cmd.Steering);
            float throttleSteerGripLoss = steerMagnitude * cmd.Throttle * 0.06f;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet wheel = wheels[i];
                if (wheel == null || wheel.collider == null)
                {
                    continue;
                }

                bool isFront = wheel.axleId == "Front" || wheel.steer;
                bool isRear = !isFront;
                float axleGrip = isFront ? stats.FrontGrip : stats.RearGrip;
                float tractionMod = stats.Traction;

                // Weight transfer
                float weightMul = GetWeightTransferGripMultiplier(state, isFront, wheel.leftSide);

                // Load bias and slip penalty
                float loadBias = 1f;
                float slipPenaltyMul = 1f;

                if (wheel.collider.GetGroundHit(out WheelHit hit))
                {
                    float suspensionDist = Mathf.Max(0.001f, wheel.collider.suspensionDistance);
                    float travel = (-wheel.collider.transform.InverseTransformPoint(hit.point).y - wheel.collider.radius) / suspensionDist;
                    travel = Mathf.Clamp01(travel);
                    loadBias = Mathf.Lerp(maxLoadGripBias, baseLoadGripBias, travel);
                    float combinedSlip = Mathf.Abs(hit.sidewaysSlip);
                    slipPenaltyMul = Mathf.Clamp01(1f - (combinedSlip * slipGripPenalty));
                }

                float combinedGrip = axleGrip * tractionMod * weightMul * loadBias * slipPenaltyMul
                    * speedGripFalloff * (1f - throttleSteerGripLoss);

                // Drift grip reduction for rear wheels
                if (isRear && driftAssistBlend > 0f)
                {
                    combinedGrip *= Mathf.Lerp(1f, 0.38f, driftAssistBlend);
                }

                // Handbrake and power slide rear grip
                if (isRear)
                {
                    if (cmd.Handbrake)
                    {
                        combinedGrip *= Mathf.Lerp(1f, 0.24f, Mathf.Clamp01(cmd.Throttle + 0.35f));
                    }

                    float powerSlide = cmd.Throttle > 0.45f && Mathf.Abs(cmd.Steering) > 0.18f && state.SpeedKph > 8f
                        ? Mathf.InverseLerp(8f, 45f, state.SpeedKph)
                        : 0f;
                    combinedGrip *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(powerSlide));

                    float lowSpeedDonut = cmd.Handbrake && cmd.Throttle > 0.35f
                        ? Mathf.InverseLerp(0.05f, 0.7f, Mathf.Abs(cmd.Steering) + cmd.Throttle * 0.35f)
                        : 0f;
                    combinedGrip *= Mathf.Lerp(1f, 0.18f, Mathf.Clamp01(lowSpeedDonut));
                }

                // Apply to friction curves
                WheelFrictionCurve forward = wheel.collider.forwardFriction;
                float forwardDriftMul = isRear ? Mathf.Lerp(1f, 0.985f, driftAssistBlend) : 1f;
                forward.stiffness = stats.ForwardStiffness * combinedGrip * slideForwardMultiplier * forwardDriftMul;
                wheel.collider.forwardFriction = forward;

                WheelFrictionCurve sideways = wheel.collider.sidewaysFriction;
                sideways.stiffness = stats.SidewaysStiffness * combinedGrip * slideSideMultiplier;
                wheel.collider.sidewaysFriction = sideways;
            }
        }

        private float GetSlideBlend(VehicleState state)
        {
            if (state.SlipAngleDegrees <= slideSlipAngleThreshold)
            {
                return 0f;
            }

            return Mathf.InverseLerp(slideSlipAngleThreshold, Mathf.Max(slideSlipAngleThreshold + 0.01f, fullSlideSlipAngle), state.SlipAngleDegrees);
        }

        private float GetWeightTransferGripMultiplier(VehicleState state, bool isFrontAxle, bool isLeftSide)
        {
            float longMul;
            if (isFrontAxle)
            {
                longMul = 1f + Mathf.Clamp01(-state.LongitudinalLoadShift) * 0.15f
                             - Mathf.Clamp01(state.LongitudinalLoadShift) * 0.10f;
            }
            else
            {
                longMul = 1f + Mathf.Clamp01(state.LongitudinalLoadShift) * 0.15f
                             - Mathf.Clamp01(-state.LongitudinalLoadShift) * 0.10f;
            }

            float latSign = isLeftSide ? -1f : 1f;
            float outsideLoad = Mathf.Clamp01(state.LateralLoadShift * latSign);
            float insideUnload = Mathf.Clamp01(-state.LateralLoadShift * latSign);
            float latMul = 1f + outsideLoad * 0.08f - insideUnload * 0.06f;

            return Mathf.Max(0.5f, longMul * latMul);
        }
    }
}
