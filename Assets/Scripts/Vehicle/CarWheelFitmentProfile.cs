using UnityEngine;

namespace Underground.Vehicle
{
    [CreateAssetMenu(
        fileName = "NewCarWheelFitmentProfile",
        menuName = "Underground/Vehicle/Customization/Car Wheel Fitment Profile")]
    public class CarWheelFitmentProfile : ScriptableObject
    {
        [Header("Vehicle")]
        public string vehicleId;

        [Header("Optional Hub Paths")]
        public string frontLeftHubPath = "ModelRoot/FL_Visual";
        public string frontRightHubPath = "ModelRoot/FR_Visual";
        public string rearLeftHubPath = "ModelRoot/RL_Visual";
        public string rearRightHubPath = "ModelRoot/RR_Visual";

        [Header("Scale")]
        public float frontScaleMultiplier = 1f;
        public float rearScaleMultiplier = 1f;
        public float minDiameter = 0.56f;
        public float maxDiameter = 0.86f;
        public float minWidth = 0.16f;
        public float maxWidth = 0.38f;

        [Header("Offset")]
        public float frontOffsetCorrection;
        public float rearOffsetCorrection;

        [Header("Visual Alignment")]
        public bool mirrorLeftWheels = true;
        public bool mirrorRightWheels;
        public float frontCamberCorrection;
        public float rearCamberCorrection;

        public float ClampDiameter(float diameter, float fallback)
        {
            float value = diameter > 0.01f ? diameter : fallback;
            return Mathf.Clamp(value, minDiameter, maxDiameter);
        }

        public float ClampWidth(float width, float fallback)
        {
            float value = width > 0.01f ? width : fallback;
            return Mathf.Clamp(value, minWidth, maxWidth);
        }
    }
}
