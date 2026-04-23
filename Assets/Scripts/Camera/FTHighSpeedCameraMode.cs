using UnityEngine;

namespace FullThrottle.SacredCore.Camera
{
    [System.Serializable]
    public sealed class FTHighSpeedCameraMode
    {
        public float activationSpeedKph = 145f;
        public float extraDistance = 2.2f;
        public float extraFov = 9f;
        public float shakeAmount = 0.035f;

        public float Weight(float speedKph)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(activationSpeedKph, activationSpeedKph + 75f, speedKph));
        }
    }
}
