// ============================================================
// UndergroundVehicleCreator.cs
// Part 6 — Underground Modular Car Creator
// Place at: Assets/FullThrottle/Vehicles/Editor/UndergroundVehicleCreator.cs
//
// Access via: Underground → Vehicles → Create New Vehicle
// ============================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Underground.Vehicle.Editor
{
    public class UndergroundVehicleCreator : EditorWindow
    {
        // ── Input fields ─────────────────────────────────────────────────────
        private string _vehicleId      = "new_vehicle";
        private string _displayName    = "New Vehicle";
        private VehicleArchetype _archetype   = VehicleArchetype.Sports;
        private DrivetrainType   _drivetrain  = DrivetrainType.RWD;
        private bool _genPrefabTemplate  = true;
        private bool _genLightRig        = true;
        private bool _addToCatalog       = true;

        private Vector2 _scroll;
        private string  _lastResult = "";

        // ── Menu entry ───────────────────────────────────────────────────────
        [MenuItem("Underground/Vehicles/Create New Vehicle")]
        public static void ShowWindow()
        {
            var window = GetWindow<UndergroundVehicleCreator>("New Vehicle");
            window.minSize = new Vector2(420f, 480f);
            window.Show();
        }

        // ── GUI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(8);
            GUILayout.Label("Underground Vehicle Creator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates the standard folder structure and starter assets for a new vehicle.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // ── Identity ──────────────────────────────────────────────────
            GUILayout.Label("Identity", EditorStyles.boldLabel);
            _vehicleId   = EditorGUILayout.TextField("Vehicle ID (snake_case)", _vehicleId);
            _displayName = EditorGUILayout.TextField("Display Name",            _displayName);
            _archetype   = (VehicleArchetype)EditorGUILayout.EnumPopup("Archetype",  _archetype);
            _drivetrain  = (DrivetrainType)  EditorGUILayout.EnumPopup("Drivetrain", _drivetrain);

            EditorGUILayout.Space(8);
            GUILayout.Label("Options", EditorStyles.boldLabel);
            _genPrefabTemplate = EditorGUILayout.Toggle("Generate Prefab Template", _genPrefabTemplate);
            _genLightRig       = EditorGUILayout.Toggle("Generate Light Rig",       _genLightRig);
            _addToCatalog      = EditorGUILayout.Toggle("Register in VehicleRoster comment", _addToCatalog);

            EditorGUILayout.Space(4);
            string previewPath = $"{VehicleConstants.VehiclesRoot}/{_vehicleId}/";
            EditorGUILayout.HelpBox($"Output: {previewPath}", MessageType.None);
            EditorGUILayout.Space(8);

            GUI.enabled = IsValidId(_vehicleId);
            if (GUILayout.Button("Create Vehicle", GUILayout.Height(36)))
            {
                CreateVehicle();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_lastResult))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_lastResult, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Creation logic ───────────────────────────────────────────────────
        private void CreateVehicle()
        {
            string root = $"{VehicleConstants.VehiclesRoot}/{_vehicleId}";

            // Folder structure
            CreateFolder(root);
            CreateFolder($"{root}/Data");
            CreateFolder($"{root}/Prefabs");
            CreateFolder($"{root}/Materials");
            CreateFolder($"{root}/Meshes");
            CreateFolder($"{root}/Textures");

            // VehicleStatsData asset
            string statsPath = $"{root}/Data/{_vehicleId}_Stats.asset";
            if (!File.Exists(Path.Combine(Application.dataPath,
                statsPath.Replace("Assets/", ""))))
            {
                VehicleStatsData stats = CreateInstance<VehicleStatsData>();
                AssetDatabase.CreateAsset(stats, statsPath);
            }

            // VehicleDefinition asset
            string defPath = $"{root}/Data/{_vehicleId}_Definition.asset";
            if (!File.Exists(Path.Combine(Application.dataPath,
                defPath.Replace("Assets/", ""))))
            {
                VehicleDefinition def = CreateInstance<VehicleDefinition>();
                def.vehicleId    = _vehicleId;
                def.displayName  = _displayName;
                def.archetype    = _archetype;
                def.drivetrain   = _drivetrain;
                def.statsAssetPath = statsPath;
                def.stats = AssetDatabase.LoadAssetAtPath<VehicleStatsData>(statsPath);
                AssetDatabase.CreateAsset(def, defPath);
            }

            // Optional prefab template (empty GameObject with correct structure)
            if (_genPrefabTemplate)
            {
                string prefabPath = $"{root}/Prefabs/{_vehicleId}_Visual.prefab";
                if (!File.Exists(Path.Combine(Application.dataPath,
                    prefabPath.Replace("Assets/", ""))))
                {
                    GameObject root3D = new GameObject(_vehicleId);

                    // Wheel mount stubs
                    string[] wheelNames = {
                        "RearLeft_WheelMount", "RearRight_WheelMount",
                        "FrontLeft_WheelMount", "FrontRight_WheelMount"
                    };
                    foreach (string wn in wheelNames)
                        new GameObject(wn).transform.SetParent(root3D.transform, false);

                    if (_genLightRig)
                    {
                        new GameObject(VehicleConstants.HeadlightsRoot).transform
                            .SetParent(root3D.transform, false);
                        new GameObject(VehicleConstants.TailLightsRoot).transform
                            .SetParent(root3D.transform, false);
                        new GameObject(VehicleConstants.BrakeLightsRoot).transform
                            .SetParent(root3D.transform, false);
                        new GameObject(VehicleConstants.ReverseLightsRoot).transform
                            .SetParent(root3D.transform, false);
                    }

                    bool success;
                    PrefabUtility.SaveAsPrefabAsset(root3D, prefabPath, out success);
                    DestroyImmediate(root3D);

                    if (success)
                    {
                        // Update definition with prefab path
                        VehicleDefinition def =
                            AssetDatabase.LoadAssetAtPath<VehicleDefinition>(defPath);
                        if (def != null)
                        {
                            def.visualPrefabPath = prefabPath;
                            EditorUtility.SetDirty(def);
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _lastResult = $"✓ Created vehicle '{_vehicleId}' at {root}/\n" +
                          $"  • {_vehicleId}_Definition.asset\n" +
                          $"  • {_vehicleId}_Stats.asset\n" +
                          (_genPrefabTemplate ? $"  • {_vehicleId}_Visual.prefab\n" : "") +
                          "\nDon't forget to add it to VehicleRoster.cs!";

            // Ping the definition in Project window
            AssetDatabase.LoadAssetAtPath<VehicleDefinition>(defPath);
            EditorGUIUtility.PingObject(
                AssetDatabase.LoadAssetAtPath<VehicleDefinition>(defPath));
        }

        private static void CreateFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent  = Path.GetDirectoryName(path).Replace('\\', '/');
                string folder  = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (char c in id)
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            return true;
        }
    }
}
#endif
