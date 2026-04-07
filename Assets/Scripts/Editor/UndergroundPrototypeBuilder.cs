using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using Underground.Progression;
using Underground.Race;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private const string PlayerCarPrefabPath = "Assets/Prefabs/Vehicles/PlayerCar.prefab";
        private const string StarterVehicleStatsPath = "Assets/ScriptableObjects/Vehicles/StarterCarStats.asset";
        private const string StarterEngineUpgradePath = "Assets/ScriptableObjects/Upgrades/StarterEngineUpgrade.asset";
        private const string StarterGripUpgradePath = "Assets/ScriptableObjects/Upgrades/StarterGripUpgrade.asset";
        private const string DayRacePath = "Assets/ScriptableObjects/Races/DaySprint.asset";
        private const string NightRacePath = "Assets/ScriptableObjects/Races/NightUnderground.asset";
        private const string WagerRacePath = "Assets/ScriptableObjects/Races/NightWager.asset";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap/Bootstrap.unity";
        private const string MainMenuScenePath = "Assets/Scenes/Menu/MainMenu.unity";
        private const string GarageScenePath = "Assets/Scenes/Garage/Garage.unity";
        private const string WorldScenePath = "Assets/Scenes/World/World.unity";
        private const string VehicleTestScenePath = "Assets/Scenes/Test/VehicleTest.unity";
        private const string FcgUrpPackagePath = "Assets/FCG/FCG-URP.unitypackage";
        private const string ImportedFcgFolderPath = "Assets/Fantastic City Generator";
        private const string FcgGeneratePrefabPath = "Assets/Fantastic City Generator/Generate.prefab";
        private const string FcgTrafficSystemPrefabPath = "Assets/Fantastic City Generator/Traffic System/Traffic System.prefab";
        private const string FcgUrpAssetPath = "Assets/Fantastic City Generator/URP Settings/UniversalRenderPipelineAsset.asset";
        private const string PreferredPlayerVisualPath = "Assets/Polyeler/Simple Retro Car/Prefabs/Simple Retro Car.prefab";
        private const string FallbackPlayerVisualPath = "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26.prefab";
        private const string TaxiPrefabPath = "Assets/High Matters/Free American Sedans/Prefabs/Taxi.prefab";
        private const string PolicePrefabPath = "Assets/High Matters/Free American Sedans/Prefabs/Police.prefab";
        private const string InterceptorPrefabPath = "Assets/Police Car & Helicopter/Prefabs/Interceptor.prefab";
        private const string DaySkyboxMaterialPath = "Assets/SkySeries Freebie/CasualDay.mat";
        private const string NightSkyboxMaterialPath = "Assets/SkySeries Freebie/CoriolisNight4k.mat";
        private const string SlimUiCanvasTemplatePath = "Assets/SlimUI/Modern Menu 1/Prefabs/Canvas Templates/Canvas_DefaultTemplate1.prefab";
        private const string SlimUiDemoScenePath = "Assets/SlimUI/Modern Menu 1/Scenes/Demos/Demo1.unity";
        private const string SlimUiMixerPath = "Assets/SlimUI/Modern Menu 1/Audio/Mixer.mixer";
        private const string RuntimeRootPrefabPath = "Assets/Prefabs/Managers/RuntimeRoot.prefab";
        private const string WorldSystemsPrefabPath = "Assets/Prefabs/Managers/WorldSystems.prefab";
        private const string FollowCameraPrefabPath = "Assets/Prefabs/Managers/FollowCamera.prefab";
        private const string HudPrefabPath = "Assets/Prefabs/UI/HUD.prefab";

        [MenuItem("Underground/Prototype/Build Full Prototype")]
        public static void BuildFullPrototype()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            ConfigureTagsAndLayers();

            if (!HasImportedFcg())
            {
                Debug.LogWarning("FCG URP package has not been imported yet. The prototype builder will use the custom fallback world layout until Assets/Fantastic City Generator exists.");
            }

            VehicleStatsData starterStats = CreateOrUpdateStarterVehicleStats();
            UpgradeDefinition engineUpgrade = CreateOrUpdateEngineUpgrade();
            CreateOrUpdateGripUpgrade();
            RaceDefinition dayRace = CreateOrUpdateDayRace();
            RaceDefinition nightRace = CreateOrUpdateNightRace();
            RaceDefinition wagerRace = CreateOrUpdateWagerRace();

            GameObject playerCarPrefab = CreateOrUpdatePlayerCarPrefab(starterStats);
            CreateSceneSupportPrefabs();

            CreateBootstrapScene();
            CreateMainMenuScene(playerCarPrefab);
            CreateGarageScene(playerCarPrefab, engineUpgrade);
            CreateWorldScene(playerCarPrefab, dayRace, nightRace, wagerRace);
            CreateVehicleTestScene(playerCarPrefab);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Prototype Ready", "Built the core prototype scenes, starter data, prefab, and project setup.", "OK");
        }

        public static void BuildVehicleTestSceneOnly()
        {
            EnsureProjectFolders();
            GameObject playerCarPrefab = CreateOrUpdatePlayerCarPrefab(CreateOrUpdateStarterVehicleStats());
            CreateSceneSupportPrefabs();
            CreateVehicleTestScene(playerCarPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Underground/Prototype/Import FCG URP Package")]
        public static void ImportFcgUrpPackage()
        {
            string absolutePackagePath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), FcgUrpPackagePath);
            if (!System.IO.File.Exists(absolutePackagePath))
            {
                EditorUtility.DisplayDialog("FCG Missing", "Assets/FCG/FCG-URP.unitypackage was not found.", "OK");
                return;
            }

            AssetDatabase.ImportPackage(FcgUrpPackagePath, false);
            EditorUtility.DisplayDialog("FCG Import Started", "Imported the FCG URP package. After Unity refreshes, rerun the prototype builder.", "OK");
        }

        [MenuItem("Underground/Project/Apply FCG URP Render Pipeline", priority = 10)]
        public static void ApplyFcgUrpRenderPipelineFromMenu()
        {
            EnsureProjectFolders();
            ConfigureRenderPipeline();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("URP Applied", "Assigned the FCG URP render pipeline asset to Graphics and all Quality levels.", "OK");
        }

        private static void EnsureProjectFolders()
        {
            string[] folders =
            {
                "Assets/Art/Cars", "Assets/Art/Environment", "Assets/Art/Props", "Assets/Art/UI", "Assets/Art/VFX", "Assets/Art/Audio",
                "Assets/Materials", "Assets/Materials/Generated", "Assets/Prefabs/Vehicles", "Assets/Prefabs/World", "Assets/Prefabs/Race", "Assets/Prefabs/UI", "Assets/Prefabs/Managers",
                "Assets/Scenes/Bootstrap", "Assets/Scenes/Menu", "Assets/Scenes/Garage", "Assets/Scenes/World", "Assets/Scenes/Test",
                "Assets/Scripts/Input", "Assets/Scripts/Camera", "Assets/Scripts/TimeSystem", "Assets/Scripts/Session", "Assets/Scripts/Save",
                "Assets/Scripts/Economy", "Assets/Scripts/Progression", "Assets/Scripts/Garage", "Assets/Scripts/Race", "Assets/Scripts/AI",
                "Assets/Scripts/UI", "Assets/Scripts/Audio", "Assets/Scripts/Utilities",
                "Assets/ScriptableObjects/Vehicles", "Assets/ScriptableObjects/Upgrades", "Assets/ScriptableObjects/Races", "Assets/Settings", "Assets/Resources"
            };

            for (int i = 0; i < folders.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(folders[i]))
                {
                    continue;
                }

                string parent = System.IO.Path.GetDirectoryName(folders[i])?.Replace("\\", "/");
                string name = System.IO.Path.GetFileName(folders[i]);
                if (!string.IsNullOrEmpty(parent))
                {
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        private static void ConfigureProjectSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            Time.fixedDeltaTime = 0.02f;
            Time.maximumDeltaTime = 0.05f;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 12;
            Physics.queriesHitTriggers = true;

            UnityEngine.Object playerSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            SerializedObject serializedObject = new SerializedObject(playerSettingsAsset);
            SerializedProperty inputHandler = serializedObject.FindProperty("activeInputHandler");
            if (inputHandler != null)
            {
                inputHandler.intValue = 2;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            ConfigureRenderPipeline();
        }

        private static void ConfigureRenderPipeline()
        {
            RenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(FcgUrpAssetPath);
            if (urpAsset == null)
            {
                return;
            }

            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;

            UnityEngine.Object qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            SerializedObject qualitySettings = new SerializedObject(qualityAsset);
            SerializedProperty levels = qualitySettings.FindProperty("m_QualitySettings");
            if (levels != null)
            {
                for (int i = 0; i < levels.arraySize; i++)
                {
                    SerializedProperty level = levels.GetArrayElementAtIndex(i);
                    SerializedProperty customRenderPipeline = level.FindPropertyRelative("customRenderPipeline");
                    if (customRenderPipeline != null)
                    {
                        customRenderPipeline.objectReferenceValue = urpAsset;
                    }
                }

                qualitySettings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureTagsAndLayers()
        {
            UnityEngine.Object tagAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject serializedObject = new SerializedObject(tagAsset);
            SerializedProperty tags = serializedObject.FindProperty("tags");
            SerializedProperty layers = serializedObject.FindProperty("layers");

            EnsureTag(tags, "Player");
            EnsureTag(tags, "Garage");
            EnsureTag(tags, "RaceStart");
            EnsureTag(tags, "Checkpoint");
            EnsureTag(tags, "AI");
            EnsureTag(tags, "Traffic");
            EnsureTag(tags, "RespawnPoint");

            EnsureLayer(layers, 8, "PlayerVehicle");
            EnsureLayer(layers, 9, "WorldStatic");
            EnsureLayer(layers, 10, "RaceTrigger");
            EnsureLayer(layers, 11, "Traffic");
            EnsureLayer(layers, 12, "GarageZone");
            EnsureLayer(layers, 13, "IgnoreVehicleCamera");
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureTag(SerializedProperty tags, string tag)
        {
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    return;
                }
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        }

        private static void EnsureLayer(SerializedProperty layers, int index, string layerName)
        {
            if (index >= 8 && index < layers.arraySize && string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
            {
                layers.GetArrayElementAtIndex(index).stringValue = layerName;
            }
        }

        private static VehicleStatsData CreateOrUpdateStarterVehicleStats()
        {
            return CreateOrUpdateAsset(StarterVehicleStatsPath, () => ScriptableObject.CreateInstance<VehicleStatsData>(), asset =>
            {
                asset.vehicleId = "starter_car";
                asset.displayName = "Starter Coupe";
            });
        }

        private static UpgradeDefinition CreateOrUpdateEngineUpgrade()
        {
            return CreateOrUpdateAsset(StarterEngineUpgradePath, () => ScriptableObject.CreateInstance<UpgradeDefinition>(), asset =>
            {
                asset.upgradeId = "engine_stage_1";
                asset.displayName = "Engine Stage 1";
                asset.category = UpgradeCategory.Engine;
                asset.cost = 1500;
                asset.motorTorqueAdd = 180f;
            });
        }

        private static UpgradeDefinition CreateOrUpdateGripUpgrade()
        {
            return CreateOrUpdateAsset(StarterGripUpgradePath, () => ScriptableObject.CreateInstance<UpgradeDefinition>(), asset =>
            {
                asset.upgradeId = "tires_stage_1";
                asset.displayName = "Street Tire Compound";
                asset.category = UpgradeCategory.Tires;
                asset.cost = 1200;
                asset.reputationRequired = 25;
                asset.forwardStiffnessAdd = 0.15f;
                asset.sidewaysStiffnessAdd = 0.2f;
            });
        }

        private static RaceDefinition CreateOrUpdateDayRace()
        {
            return CreateOrUpdateAsset(DayRacePath, () => ScriptableObject.CreateInstance<RaceDefinition>(), asset =>
            {
                asset.raceId = "day_sprint";
                asset.displayName = "Industrial Sprint";
                asset.raceType = RaceType.Sprint;
                asset.rewardMoney = 900;
                asset.rewardReputation = 12;
            });
        }

        private static RaceDefinition CreateOrUpdateNightRace()
        {
            return CreateOrUpdateAsset(NightRacePath, () => ScriptableObject.CreateInstance<RaceDefinition>(), asset =>
            {
                asset.raceId = "night_run";
                asset.displayName = "Midnight Run";
                asset.raceType = RaceType.Underground;
                asset.nightOnly = true;
                asset.rewardMoney = 1900;
                asset.rewardReputation = 30;
                asset.minReputation = 50;
            });
        }

        private static RaceDefinition CreateOrUpdateWagerRace()
        {
            return CreateOrUpdateAsset(WagerRacePath, () => ScriptableObject.CreateInstance<RaceDefinition>(), asset =>
            {
                asset.raceId = "wager_run";
                asset.displayName = "Neon Pinkslip";
                asset.raceType = RaceType.Wager;
                asset.nightOnly = true;
                asset.rewardMoney = 4000;
                asset.rewardReputation = 60;
                asset.minReputation = 200;
                asset.allowsCarWager = true;
            });
        }

        private static T CreateOrUpdateAsset<T>(string path, Func<T> factory, Action<T> configure) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = factory();
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject LoadPreferredPlayerVisualPrefab()
        {
            return LoadFirstExistingAsset<GameObject>(PreferredPlayerVisualPath, FallbackPlayerVisualPath);
        }

        private static GameObject LoadTaxiTrafficPrefab()
        {
            return LoadFirstExistingAsset<GameObject>(TaxiPrefabPath, PolicePrefabPath, InterceptorPrefabPath);
        }

        private static GameObject LoadFcgGeneratorPrefab()
        {
            return LoadFirstExistingAsset<GameObject>(FcgGeneratePrefabPath);
        }

        private static GameObject LoadFcgTrafficSystemPrefab()
        {
            return LoadFirstExistingAsset<GameObject>(FcgTrafficSystemPrefabPath);
        }

        private static GameObject LoadPoliceTrafficPrefab()
        {
            return LoadFirstExistingAsset<GameObject>(PolicePrefabPath, InterceptorPrefabPath, TaxiPrefabPath);
        }

        private static Material LoadDaySkyboxMaterial()
        {
            return LoadFirstExistingAsset<Material>(DaySkyboxMaterialPath);
        }

        private static Material LoadNightSkyboxMaterial()
        {
            return LoadFirstExistingAsset<Material>(NightSkyboxMaterialPath);
        }

        private static GameObject LoadSlimUiCanvasTemplate()
        {
            return LoadFirstExistingAsset<GameObject>(SlimUiCanvasTemplatePath);
        }

        private static AudioMixer LoadSlimUiMixer()
        {
            return LoadFirstExistingAsset<AudioMixer>(SlimUiMixerPath);
        }

        private static T LoadFirstExistingAsset<T>(params string[] paths) where T : UnityEngine.Object
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i]))
                {
                    continue;
                }

                T asset = AssetDatabase.LoadAssetAtPath<T>(paths[i]);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private static bool HasImportedFcg()
        {
            return AssetDatabase.IsValidFolder(ImportedFcgFolderPath);
        }
    }
}
