using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [System.Serializable]
    public sealed class FTDriftModel
    {
        [Range(0f, 1f)] public float driftBias = 0.35f;
        public float handbrakeRearGrip = 0.28f;
        public float handbrakeRearForwardGrip = 0.52f;
        public float throttleRearGrip = 0.68f;
        public float minimumDriftSpeedKph = 26f;

        public void Apply(FTWheelState wheelState, bool handbrake, float throttle, float speedKph)
        {
            if (wheelState == null || wheelState.wheel == null || !wheelState.rear)
            {
                return;
            }

            WheelFrictionCurve sideways = wheelState.wheel.sidewaysFriction;
            float target = sideways.stiffness;
            if (speedKph >= minimumDriftSpeedKph)
            {
                if (handbrake)
                {
                    target *= Mathf.Lerp(1f, handbrakeRearGrip, driftBias);
                }
                else if (throttle > 0.65f)
                {
                    target *= Mathf.Lerp(1f, throttleRearGrip, driftBias);
                }
            }

            sideways.stiffness = Mathf.Max(0.18f, target);
            wheelState.wheel.sidewaysFriction = sideways;

            if (handbrake && speedKph >= minimumDriftSpeedKph)
            {
                WheelFrictionCurve forward = wheelState.wheel.forwardFriction;
                forward.stiffness = Mathf.Max(0.14f, forward.stiffness * Mathf.Lerp(1f, handbrakeRearForwardGrip, driftBias));
                wheelState.wheel.forwardFriction = forward;
            }
        }
    }
}
