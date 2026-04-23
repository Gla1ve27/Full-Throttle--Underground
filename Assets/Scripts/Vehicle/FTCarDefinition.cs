using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Car Definition", fileName = "FT_Car")]
    public sealed class FTCarDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string carId = "starter_01";
        public string displayName = "Starter Car";
        public string vehicleClass = "Starter";
        public string driveType = "RWD";
        public string engineCharacterTag = "rough_starter";
        public string audioProfileId = "starter_stock";
        public string garagePreviewRevStyle = "restrained";
        public string forcedInductionType = "";
        public string audioFamilyTag = "";
        public bool starterOwned = false;
        public bool haloCar = false;

        [Header("Presentation")]
        public GameObject worldPrefab;
        [Tooltip("Optional visual/model prefab placed inside the shared world prefab's ModelRoot.")]
        public GameObject visualPrefab;
        [Tooltip("Editor migration fallback for legacy roster visuals that are still path based.")]
        public string editorVisualPrefabPath = "";
        public Vector3 garageEuler = new(0f, 220f, 0f);
        public Vector3 garageCameraOffset = new(0f, 1.3f, -5.4f);

        [Header("Feel")]
        public FTCarFeel feel = new();
    }

    [System.Serializable]
    public sealed class FTCarFeel
    {
        [Range(0f, 10f)] public float acceleration = 4.0f;
        [Range(0f, 10f)] public float topSpeed = 4.0f;
        [Range(0f, 10f)] public float handling = 4.0f;
        [Range(0f, 1f)] public float driftBias = 0.35f;
        [Min(0.25f)] public float repairMultiplier = 1.0f;
    }
}
