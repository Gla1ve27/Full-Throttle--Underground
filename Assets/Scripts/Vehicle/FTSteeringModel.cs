using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [System.Serializable]
    public sealed class FTSteeringModel
    {
        public float lowSpeedAngle = 38f;
        public float highSpeedAngle = 18f;
        public float highSpeedKph = 210f;
        public float response = 15f;

        public float Evaluate(float steerInput, float speedKph, float currentAngle, float dt)
        {
            float speedT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(35f, highSpeedKph, speedKph));
            float maxAngle = Mathf.Lerp(lowSpeedAngle, highSpeedAngle, speedT);
            float target = Mathf.Clamp(steerInput, -1f, 1f) * maxAngle;
            return Mathf.Lerp(currentAngle, target, 1f - Mathf.Exp(-response * dt));
        }
    }
}
