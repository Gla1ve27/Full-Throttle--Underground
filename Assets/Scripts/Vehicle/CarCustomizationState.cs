using UnityEngine;

namespace Underground.Vehicle
{
    [System.Serializable]
    public class CarCustomizationState
    {
        [Header("Wheel Selection")]
        public string frontWheelId;
        public string rearWheelId;

        [Header("Sizing")]
        public float frontDiameter;
        public float rearDiameter;
        public float frontWidth;
        public float rearWidth;

        [Header("Fitment")]
        public float frontOffset;
        public float rearOffset;
        public float frontCamberVisual;
        public float rearCamberVisual;

        public void ApplySingleWheel(string wheelId)
        {
            frontWheelId = wheelId;
            rearWheelId = wheelId;
        }
    }
}
