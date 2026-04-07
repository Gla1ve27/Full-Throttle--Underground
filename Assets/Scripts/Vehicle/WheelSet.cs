using UnityEngine;

namespace Underground.Vehicle
{
    [System.Serializable]
    public class WheelSet
    {
        public string axleId = "Front";
        public bool leftSide = true;
        public WheelCollider collider;
        public Transform mesh;
        public bool steer;
        public bool drive;
        public bool handbrake;
    }
}
