using UnityEngine;

namespace FullThrottle.SacredCore.Camera
{
    [System.Serializable]
    public sealed class FTDriftCameraMode
    {
        public float slipActivation = 0.38f;
        public float sideLookDegrees = 7f;
        public float rollDegrees = 2.5f;

        public float Weight(float slip01)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(slipActivation, 0.9f, slip01));
        }
    }
}
