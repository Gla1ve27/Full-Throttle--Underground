// ============================================================
// VehicleConstants.cs
// Part 1 — Architecture & Guardrails
// Place at: Assets/FullThrottle/Vehicles/VehicleConstants.cs
// ============================================================

namespace Underground.Vehicle
{
    public static class VehicleConstants
    {
        public const string VehiclesRoot      = "Assets/FullThrottle/Vehicles";
        public const string DataRoot          = "Assets/FullThrottle/Vehicles/Data";
        public const string RuntimeRoot       = "Assets/FullThrottle/Vehicles/Runtime";
        public const string VisualRoot        = "Assets/FullThrottle/Vehicles/Visual";
        public const string UpgradesRoot      = "Assets/FullThrottle/Vehicles/Upgrades";
        public const string EditorRoot        = "Assets/FullThrottle/Vehicles/Editor";

        // Per-Vehicle Subfolder Names
        public const string DataFolder = "Data";
        public const string PrefabsFolder = "Prefabs";
        public const string MaterialsFolder = "Materials";
        public const string MeshesFolder = "Meshes";
        public const string TexturesFolder = "Textures";

        // Naming Templates
        public static string DefinitionAssetName(string vehicleId) => $"{vehicleId}_Definition";
        public static string StatsAssetName(string vehicleId) => $"{vehicleId}_Stats";
        public static string VehicleFolder(string vehicleId) => $"{VehiclesRoot}/{vehicleId}";
        public static string VehicleDataFolder(string vehicleId) => $"{VehiclesRoot}/{vehicleId}/{DataFolder}";

        // Light rig child names — every vehicle prefab must follow this convention
        public const string HeadlightsRoot    = "HeadlightsRoot";
        public const string TailLightsRoot    = "TailLightsRoot";
        public const string BrakeLightsRoot   = "BrakeLightsRoot";
        public const string ReverseLightsRoot = "ReverseLightsRoot";

        // Legacy naming compatibility
        public const string HeadlightsRootName = HeadlightsRoot;
        public const string TailLightsRootName = TailLightsRoot;
        public const string BrakeLightsRootName = BrakeLightsRoot;
        public const string ReverseLightsRootName = ReverseLightsRoot;

        // ── Editor Menu Paths ────────────────────────────────────────────────
        public const string UndergroundMenuRoot = "Full Throttle/";
        public const string VehicleMenuRoot     = "Full Throttle/Vehicles/";
    }
}
