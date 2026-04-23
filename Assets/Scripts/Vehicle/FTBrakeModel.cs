using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [System.Serializable]
    public sealed class FTBrakeModel
    {
        public float brakeTorque = 4200f;
        public float handbrakeTorque = 8200f;
        public float reverseMotorTorque = 850f;
        [Header("ABS")]
        public bool absEnabled = true;
        [Range(0.05f, 1f)] public float absSlipThreshold = 0.34f;
        [Range(0.1f, 1f)] public float absMinBrakeScale = 0.42f;
        public float absMinimumSpeedKph = 18f;

        public float EvaluateBrake(float brakeInput)
        {
            return Mathf.Clamp01(brakeInput) * brakeTorque;
        }

        public float ApplyAbs(float brakeTorqueValue, float brakeInput, float speedKph, float forwardSlip, bool grounded, out bool active)
        {
            active = false;
            if (!absEnabled || !grounded || brakeInput <= 0.05f || speedKph < absMinimumSpeedKph)
            {
                return brakeTorqueValue;
            }

            float slip = Mathf.Abs(forwardSlip);
            if (slip <= absSlipThreshold)
            {
                return brakeTorqueValue;
            }

            active = true;
            float intervention = Mathf.InverseLerp(absSlipThreshold, absSlipThreshold + 0.55f, slip);
            float scale = Mathf.Lerp(1f, absMinBrakeScale, Mathf.Clamp01(intervention));
            return brakeTorqueValue * scale;
        }
    }
}
