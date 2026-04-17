using UnityEngine;

namespace Underground.Vehicle
{
    [CreateAssetMenu(
        fileName = "NewWheelDefinition",
        menuName = "Underground/Vehicle/Customization/Wheel Definition")]
    public class WheelDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string wheelId;
        public string displayName;
        public string brandName;

        [Header("Visual")]
        public GameObject wheelPrefab;
        public Material[] materialOverrides;
        public Mesh lod0Mesh;
        public Mesh lod1Mesh;

        [Header("Fitment")]
        public float nominalDiameter = 0.68f;
        public float nominalWidth = 0.24f;
        public float defaultTireSidewall = 0.10f;
        public float defaultOffset = 0f;
        public bool allowFront = true;
        public bool allowRear = true;
        public float brakeClearance = 0.18f;

        [Header("Progression")]
        public int unlockStage;
        public int price;

        public bool IsCompatible(bool frontAxle)
        {
            return frontAxle ? allowFront : allowRear;
        }
    }
}
