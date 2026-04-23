using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [System.Serializable]
    public sealed class FTGripModel
    {
        public float forwardStiffness = 1.18f;
        public float sidewaysStiffness = 1.08f;
        public float highSpeedGripLoss = 0.12f;
        [Header("Traction Control")]
        public bool tractionControlEnabled = true;
        [Range(0.05f, 1f)] public float tractionSlipThreshold = 0.38f;
        [Range(0.1f, 1f)] public float tractionMinTorqueScale = 0.55f;
        public float tractionMinimumSpeedKph = 8f;

        public void Apply(WheelCollider wheel, float speedKph)
        {
            if (wheel == null)
            {
                return;
            }

            float speedLoss = Mathf.Lerp(0f, highSpeedGripLoss, Mathf.InverseLerp(110f, 230f, speedKph));
            WheelFrictionCurve forward = wheel.forwardFriction;
            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            forward.stiffness = Mathf.Max(0.2f, forwardStiffness - speedLoss);
            sideways.stiffness = Mathf.Max(0.2f, sidewaysStiffness - speedLoss);
            wheel.forwardFriction = forward;
            wheel.sidewaysFriction = sideways;
        }

        public float ApplyTractionControl(float motorTorque, float throttle, float speedKph, float forwardSlip, bool grounded, out bool active)
        {
            active = false;
            if (!tractionControlEnabled || !grounded || throttle <= 0.08f || speedKph < tractionMinimumSpeedKph)
            {
                return motorTorque;
            }

            float slip = Mathf.Abs(forwardSlip);
            if (slip <= tractionSlipThreshold)
            {
                return motorTorque;
            }

            active = true;
            float intervention = Mathf.InverseLerp(tractionSlipThreshold, tractionSlipThreshold + 0.65f, slip);
            float scale = Mathf.Lerp(1f, tractionMinTorqueScale, Mathf.Clamp01(intervention));
            return motorTorque * scale;
        }
    }
}
