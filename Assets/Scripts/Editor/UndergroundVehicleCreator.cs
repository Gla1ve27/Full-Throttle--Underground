#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using Underground.Vehicle;

namespace Underground.Editor
{
    /// <summary>
    /// Part 6 — Underground Modular Car Creator
    /// 
    /// Editor window for one-click creation of a new vehicle's folder structure,
    /// VehicleDefinition, and VehicleStatsData assets.
    /// 
    /// Menu: Underground → Vehicles → Create New Vehicle
    /// </summary>
    public class UndergroundVehicleCreator : EditorWindow
    {
        private string _vehicleId = "new_vehicle";
        private string _displayName = "New Vehicle";
        private string _manufacturerName = "Underground";
        private VehicleArchetype _archetype = VehicleArchetype.Sports;
        private DrivetrainType _drivetrain = DrivetrainType.RWD;
        private bool _generatePrefabTemplate = true;
        private bool _generateLightRig = true;

        [MenuItem(VehicleConstants.VehicleMenuRoot + "Create New Vehicle", priority = 0)]
        public static void ShowWindow()
        {
            GetWindow<UndergroundVehicleCreator>("Vehicle Creator");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Base Identity", EditorStyles.boldLabel);
            _vehicleId = EditorGUILayout.TextField("Vehicle ID", _vehicleId);
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            _manufacturerName = EditorGUILayout.TextField("Manufacturer", _manufacturerName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Classification", EditorStyles.boldLabel);
            _archetype = (VehicleArchetype)EditorGUILayout.EnumPopup("Archetype", _archetype);
            _drivetrain = (DrivetrainType)EditorGUILayout.EnumPopup("Drivetrain", _drivetrain);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Advanced Options", EditorStyles.boldLabel);
            _generatePrefabTemplate = EditorGUILayout.Toggle("Generate Prefab Template", _generatePrefabTemplate);
            _generateLightRig = EditorGUILayout.Toggle("Generate Light Rig", _generateLightRig);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Vehicle Content", GUILayout.Height(30)))
            {
                CreateVehicle();
            }
        }

        private void CreateVehicle()
        {
            if (string.IsNullOrEmpty(_vehicleId))
            {
                EditorUtility.DisplayDialog("Error", "Vehicle ID cannot be empty.", "OK");
                return;
            }

            string vehicleFolder = VehicleConstants.VehicleFolder(_vehicleId);
            string dataFolder = VehicleConstants.VehicleDataFolder(_vehicleId);

            if (AssetDatabase.IsValidFolder(vehicleFolder))
            {
                if (!EditorUtility.DisplayDialog("Warning", $"Folder already exists for '{_vehicleId}'. Overwrite existing assets?", "Yes", "Cancel"))
                {
                    return;
                }
            }

            // 1. Create Folder Structure
            EnsureFolder(vehicleFolder);
            EnsureFolder(dataFolder);
            EnsureFolder($"{vehicleFolder}/{VehicleConstants.PrefabsFolder}");
            EnsureFolder($"{vehicleFolder}/{VehicleConstants.MaterialsFolder}");
            EnsureFolder($"{vehicleFolder}/{VehicleConstants.MeshesFolder}");
            EnsureFolder($"{vehicleFolder}/{VehicleConstants.TexturesFolder}");

            // 2. Create VehicleStatsData
            VehicleStatsData stats = ScriptableObject.CreateInstance<VehicleStatsData>();
            stats.vehicleId = _vehicleId;
            stats.displayName = _displayName;
            stats.archetype = _archetype;
            stats.drivetrain = _drivetrain;

            // Apply reasonable defaults based on archetype (partially reusing logic from RosterAssetGenerator)
            ApplyBaselineStats(stats);

            string statsPath = $"{dataFolder}/{VehicleConstants.StatsAssetName(_vehicleId)}.asset";
            AssetDatabase.CreateAsset(stats, statsPath);

            // 3. Create VehicleDefinition
            VehicleDefinition def = ScriptableObject.CreateInstance<VehicleDefinition>();
            def.vehicleId = _vehicleId;
            def.displayName = _displayName;
            def.manufacturerLoreName = _manufacturerName;
            def.archetype = _archetype;
            def.drivetrain = _drivetrain;
            def.stats = stats;
            def.statsAssetPath = statsPath;

            string defPath = $"{dataFolder}/{VehicleConstants.DefinitionAssetName(_vehicleId)}.asset";
            AssetDatabase.CreateAsset(def, defPath);

            // 4. Optionally create prefab template
            if (_generatePrefabTemplate)
            {
                CreatePrefabTemplate(def, vehicleFolder);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = def;
            EditorUtility.DisplayDialog("Success", $"Created vehicle content for '{_displayName}' at {vehicleFolder}", "OK");
        }

        private void ApplyBaselineStats(VehicleStatsData stats)
        {
            // Set some universal defaults
            stats.maxSpeedKph = 200f;
            stats.defaultMass = 1200f;
            stats.horsepower = 200f;
            stats.maxMotorTorque = 400f;
            stats.maxBrakeTorque = 4000f;
            stats.forwardStiffness = 1.3f;
            stats.sidewaysStiffness = 1.4f;
            
            // Torque curve placeholder
            stats.torqueCurve = new AnimationCurve(
                new Keyframe(0f, 0.4f), 
                new Keyframe(0.2f, 0.6f), 
                new Keyframe(0.6f, 0.9f), 
                new Keyframe(1f, 0.6f)
            );
        }

        private void CreatePrefabTemplate(VehicleDefinition def, string vehicleFolder)
        {
            string prefabPath = $"{vehicleFolder}/{VehicleConstants.PrefabsFolder}/{_vehicleId}_VisualPrefab.prefab";
            
            GameObject root = new GameObject($"{_vehicleId}_VisualPrefab");
            
            // Add a placeholder body cube
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "BodyPlaceholder";
            body.transform.SetParent(root.transform);
            body.transform.localScale = new Vector3(1.6f, 1.2f, 4f);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            // Create light rigs
            if (_generateLightRig)
            {
                CreateLightRoot(root.transform, VehicleConstants.HeadlightsRootName);
                CreateLightRoot(root.transform, VehicleConstants.TailLightsRootName);
                CreateLightRoot(root.transform, VehicleConstants.BrakeLightsRootName);
                CreateLightRoot(root.transform, VehicleConstants.ReverseLightsRootName);
            }

            // Save as prefab
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);

            // Link to definition
            def.visualPrefabPath = prefabPath;
        }

        private void CreateLightRoot(Transform parent, string name)
        {
            GameObject lightRoot = new GameObject(name);
            lightRoot.transform.SetParent(parent);
            lightRoot.transform.localPosition = Vector3.zero;
        }

        private void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string currentPath = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = nextPath;
            }
        }
    }
}
#endif
