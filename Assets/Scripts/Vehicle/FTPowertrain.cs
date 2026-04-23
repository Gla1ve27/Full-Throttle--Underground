using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [System.Serializable]
    public sealed class FTPowertrain
    {
        public float idleRPM = 900f;
        public float redlineRPM = 7200f;
        public float maxMotorTorque = 1550f;
        public float maxSpeedKph = 235f;
        public AnimationCurve torqueCurve = new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.35f, 0.95f),
            new Keyframe(0.72f, 1f),
            new Keyframe(1f, 0.72f));

        public float EvaluateTorque(float throttle, float rpm01, float speedKph)
        {
            float speedLimiter = Mathf.InverseLerp(maxSpeedKph, maxSpeedKph * 0.92f, speedKph);
            float torque = maxMotorTorque * Mathf.Clamp01(throttle) * Mathf.Max(0.1f, torqueCurve.Evaluate(Mathf.Clamp01(rpm01)));
            return torque * Mathf.Clamp01(speedLimiter);
        }

        public float Rpm01(float rpm)
        {
            return Mathf.InverseLerp(idleRPM, redlineRPM, rpm);
        }
    }
}
