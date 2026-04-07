using UnityEngine;

namespace Underground.Progression
{
    public enum UpgradeCategory
    {
        Engine,
        Tires,
        Brakes,
        Suspension,
        WeightReduction,
        Transmission
    }

    [CreateAssetMenu(menuName = "Racing/Upgrade Definition", fileName = "UpgradeDefinition")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string upgradeId;
        public string displayName;
        public UpgradeCategory category;
        public int cost;
        public int reputationRequired;

        [Header("Physics Effects")]
        public float motorTorqueAdd;
        public float brakeTorqueAdd;
        public float forwardStiffnessAdd;
        public float sidewaysStiffnessAdd;
        public float springAdd;
        public float damperAdd;
        public float massDelta;
        public float shiftSpeedBonus;
    }
}
