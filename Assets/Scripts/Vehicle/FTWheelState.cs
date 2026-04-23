using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [System.Serializable]
    public sealed class FTWheelState
    {
        public WheelCollider wheel;
        public Transform visual;
        public bool steer;
        public bool motor = true;
        public bool brake = true;
        public bool rear;

        [System.NonSerialized] public bool grounded;
        [System.NonSerialized] public float forwardSlip;
        [System.NonSerialized] public float sidewaysSlip;

        public void CaptureGround()
        {
            if (wheel != null && wheel.GetGroundHit(out WheelHit hit))
            {
                grounded = true;
                forwardSlip = hit.forwardSlip;
                sidewaysSlip = hit.sidewaysSlip;
            }
            else
            {
                grounded = false;
                forwardSlip = 0f;
                sidewaysSlip = 0f;
            }
        }

        public void UpdateVisual()
        {
            if (wheel == null || visual == null)
            {
                return;
            }

            wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visual.SetPositionAndRotation(position, rotation);
        }
    }
}
