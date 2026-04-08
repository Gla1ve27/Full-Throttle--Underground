using System;
using System.Reflection;
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
        private const string FcgHdrpPackagePath = "Assets/FCG/FCG-HDRP.unitypackage";
        private const string ImportedFcgFolderPath = "Assets/Fantastic City Generator";
        private const string FcgGeneratePrefabPath = "Assets/Fantastic City Generator/Generate.prefab";
        private const string FcgTrafficSystemPrefabPath = "Assets/Fantastic City Generator/Traffic System/Traffic System.prefab";
        private const string SimpleRetroHdrpPackagePath = "Assets/Polyeler/Simple Retro Car/HDRP_ExtractMe.unitypackage";
        private const string FcgUrpAssetPath = "Assets/Fantastic City Generator/URP Settings/UniversalRenderPipelineAsset.asset";
        private const string FcgHdrpAssetSearchHint = "Assets/Fantastic City Generator/HDRP Settings";
        private const string ProjectSsrUrpAssetPath = "Assets/Settings/ProjectURP/PC_RPAsset.asset";
        private const string ProjectUltraUrpAssetPath = "Assets/Settings/ProjectURP/Ultra_PipelineAsset.asset";
        private const string ProjectHdrpFolderPath = "Assets/Settings/ProjectHDRP";
        private const string ProjectHdrpAssetPath = "Assets/Settings/ProjectHDRP/HDRenderPipelineAsset.asset";
        private const string ProjectWorldVolumeProfilePath = "Assets/Settings/ProjectHDRP/WorldVolumeProfile.asset";
        private const string ProjectGarageVolumeProfilePath = "Assets/Settings/ProjectHDRP/GarageVolumeProfile.asset";
        private const string PreferredPlayerVisualPath = "Assets/Polyeler/Simple Retro Car/Prefabs/Simple Retro Car.prefab";
        private const string FallbackPlayerVisualPath = "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26.prefab";
        private const string TaxiPrefabPath = "Assets/High Matters/Free American Sedans/Prefabs/Taxi.prefab";
        private const string PolicePrefabPath = "Assets/High Matters/Free American Sedans/Prefabs/Police.prefab";
        private const string InterceptorPrefabPath = "Assets/Police Car & Helicopter/Prefabs/Interceptor.prefab";
        private const string DaySkyboxMaterialPath = "Assets/SkySeries Freebie/FluffballDay.mat";
        private const string NightSkyboxMaterialPath = "Assets/SkySeries Freebie/CoriolisNight4k.mat";
        private const string DaySkyboxCubemapPath = "Assets/SkySeries Freebie/FreebieHdri/FluffballDay4k.hdr";
        private const string NightSkyboxCubemapPath = "Assets/SkySeries Freebie/FreebieHdri/CoriolisNight4k.hdr";
        private const string FallbackDaySkyboxMaterialPath = "Assets/Materials/Generated/DaySkyboxFallback.mat";
        private const string FallbackNightSkyboxMaterialPath = "Assets/Materials/Generated/NightSkyboxFallback.mat";
        private const string SlimUiCanvasTemplatePath = "Assets/SlimUI/Modern Menu 1/Prefabs/Canvas Templates/Canvas_DefaultTemplate1.prefab";
        private const string SlimUiDemoScenePath = "Assets/SlimUI/Modern Menu 1/Scenes/Demos/Demo1.unity";
        private const string SlimUiMixerPath = "Assets/SlimUI/Modern Menu 1/Audio/Mixer.mixer";
        private const string RuntimeRootPrefabPath = "Assets/Prefabs/Managers/RuntimeRoot.prefab";
        private const string WorldSystemsPrefabPath = "Assets/Prefabs/Managers/WorldSystems.prefab";
        private const string FollowCameraPrefabPath = "Assets/Prefabs/Managers/FollowCamera.prefab";
        private const string HudPrefabPath = "Assets/Prefabs/UI/HUD.prefab";
        private const string ImportedSpeedometerPrefabPath = "Assets/Prefabs/UI/Speedometer.prefab";
        private static readonly Vector3 PreferredWorldStartPosition = new Vector3(182.57f, 1.2f, 301.96f);
        private static readonly Vector3 PreferredWorldStartEuler = new Vector3(0f, 180.169f, 0.002f);

        [MenuItem("Underground/Legacy/Prototype/Build Full Prototype")]
        public static void BuildFullPrototype()
        {
            BuildFullPrototype(showDialog: true);
        }

        internal static void BuildFullPrototype(bool showDialog)
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            ConfigureTagsAndLayers();

            if (!HasImportedFcg())
            {
                Debug.LogWarning("FCG HDRP content has not been imported yet. The prototype builder will use the custom fallback world layout until Assets/Fantastic City Generator exists.");
            }

            VehicleStatsData starterStats = CreateOrUpdateStarterVehicleStats();
            UpgradeDefinition engineUpgrade = CreateOrUpdateEngineUpgrade();
            CreateOrUpdateGripUpgrade();
            RaceDefinition dayRace = CreateOrUpdateDayRace();
            RaceDefinition nightRace = CreateOrUpdateNightRace();
            RaceDefinition wagerRace = CreateOrUpdateWagerRace();

            GameObject playerCarPrefab = CreateOrUpdatePlayerCarPrefab(starterStats, preserveExistingAsset: true);
            CreateSceneSupportPrefabs(preserveExistingAssets: true);

            CreateBootstrapScene(preserveExistingScene: true);
            CreateMainMenuScene(playerCarPrefab, preserveExistingScene: true);
            CreateGarageScene(playerCarPrefab, engineUpgrade, preserveExistingScene: true);
            CreateWorldScene(playerCarPrefab, dayRace, nightRace, wagerRace, preserveExistingScene: true);
            CreateVehicleTestScene(playerCarPrefab, preserveExistingScene: true);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Full Game Rebuilt", "Rebuilt the core scenes, generated prefabs, starter data, and project rendering setup.", "OK");
            }
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

        [MenuItem("Underground/Project/Import/FCG URP Package")]
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

        [MenuItem("Underground/Project/Import/FCG HDRP Package")]
        public static void ImportFcgHdrpPackage()
        {
            ImportLocalUnityPackage(FcgHdrpPackagePath, "FCG HDRP");
        }

        [MenuItem("Underground/Project/Import/Retro Car HDRP Package")]
        public static void ImportSimpleRetroHdrpPackage()
        {
            ImportLocalUnityPackage(SimpleRetroHdrpPackagePath, "Retro Car HDRP");
        }

        [MenuItem("Underground/Project/Apply HDRP Pipeline", priority = 10)]
        public static void ApplyHdrpRenderPipelineFromMenu()
        {
            ApplyHdrpRenderPipeline(true);
        }

        internal static void ApplyHdrpRenderPipeline(bool showDialog)
        {
            EnsureProjectFolders();
            ConfigureRenderPipeline();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (showDialog)
            {
                EditorUtility.DisplayDialog("HDRP Applied", "Assigned the HDRP render pipeline asset to Graphics and all Quality levels.", "OK");
            }
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
                "Assets/Scripts/UI", "Assets/Scripts/Audio", "Assets/Scripts/Utilities", "Assets/Scripts/World",
                "Assets/ScriptableObjects/Vehicles", "Assets/ScriptableObjects/Upgrades", "Assets/ScriptableObjects/Races", "Assets/Settings", "Assets/Settings/ProjectHDRP", "Assets/Resources"
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
            RenderPipelineAsset pipelineAsset = LoadPreferredRenderPipelineAsset();
            if (pipelineAsset == null)
            {
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;

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
                        customRenderPipeline.objectReferenceValue = pipelineAsset;
                    }
                }

                qualitySettings.ApplyModifiedPropertiesWithoutUndo();
            }

            EnsureSsrRendererFeatures();
            ConfigureDefaultVolumeProfile();
        }

        private static bool HasHdrpPackageInstalled()
        {
            return FindType(
                "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime") != null;
        }

        private static RenderPipelineAsset LoadPreferredRenderPipelineAsset()
        {
            if (HasHdrpPackageInstalled())
            {
                RenderPipelineAsset hdrpAsset = LoadHdrpRenderPipelineAsset();
                if (hdrpAsset != null)
                {
                    return hdrpAsset;
                }
            }

            return LoadFirstExistingAsset<RenderPipelineAsset>(ProjectSsrUrpAssetPath, ProjectUltraUrpAssetPath, FcgUrpAssetPath);
        }

        private static RenderPipelineAsset LoadHdrpRenderPipelineAsset()
        {
            RenderPipelineAsset knownAsset = LoadFirstExistingAsset<RenderPipelineAsset>(
                ProjectHdrpAssetPath,
                "Assets/Settings/ProjectHDRP/HDRP_Default.asset");
            if (knownAsset != null && IsHdrpAsset(knownAsset))
            {
                return knownAsset;
            }

            string[] guids = AssetDatabase.FindAssets("t:RenderPipelineAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                RenderPipelineAsset candidate = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (candidate == null || !IsHdrpAsset(candidate))
                {
                    continue;
                }

                if (path.Contains("ProjectHDRP") || path.Contains("HDRP Settings") || path.Contains("HDRenderPipeline"))
                {
                    return candidate;
                }
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                RenderPipelineAsset candidate = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (candidate != null && IsHdrpAsset(candidate))
                {
                    return candidate;
                }
            }

            RenderPipelineAsset createdAsset = CreateHdrpRenderPipelineAsset();
            if (createdAsset != null)
            {
                return createdAsset;
            }

            return null;
        }

        private static bool IsHdrpAsset(RenderPipelineAsset asset)
        {
            return asset != null && asset.GetType().FullName != null && asset.GetType().FullName.Contains("HighDefinition");
        }

        private static RenderPipelineAsset CreateHdrpRenderPipelineAsset()
        {
            EnsureProjectFolders();

            Type hdrpAssetType = FindType(
                "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime");
            if (hdrpAssetType == null)
            {
                return null;
            }

            RenderPipelineAsset existingAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(ProjectHdrpAssetPath);
            if (existingAsset != null)
            {
                return existingAsset;
            }

            ScriptableObject newAsset = ScriptableObject.CreateInstance(hdrpAssetType);
            if (newAsset == null)
            {
                return null;
            }

            newAsset.name = "HDRenderPipelineAsset";
            InvokeMethodIfPresent(newAsset, "Reset");
            AssetDatabase.CreateAsset(newAsset, ProjectHdrpAssetPath);
            EditorUtility.SetDirty(newAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ProjectHdrpAssetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(ProjectHdrpAssetPath);
        }

        private static Type FindType(params string[] typeNames)
        {
            for (int i = 0; i < typeNames.Length; i++)
            {
                Type type = Type.GetType(typeNames[i]);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void InvokeMethodIfPresent(object instance, string methodName)
        {
            if (instance == null || string.IsNullOrEmpty(methodName))
            {
                return;
            }

            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(instance, null);
        }

        private static void ImportLocalUnityPackage(string packagePath, string displayName)
        {
            string absolutePackagePath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), packagePath);
            if (!System.IO.File.Exists(absolutePackagePath))
            {
                EditorUtility.DisplayDialog($"{displayName} Missing", $"{packagePath} was not found.", "OK");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            EditorUtility.DisplayDialog($"{displayName} Import Started", $"Imported {displayName}. After Unity refreshes, rerun the prototype builder.", "OK");
        }

        private static void ConfigureTagsAndLayers()
        {
            UnityEngine.Object tagAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject serializedObject = new SerializedObject(tagAsset);
            SerializedProperty tags = serializedObject.FindProperty("tags");
            SerializedProperty layers = serializedObject.FindProperty("layers");

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
                asset.vehicleId = PlayerCarCatalog.StarterCarId;
                asset.displayName = "RMCar26";
                asset.maxMotorTorque = 540f;
                asset.maxBrakeTorque = 4800f;
                asset.maxSpeedKph = 136f;
                asset.maxSteerAngle = 20.5f;
                asset.highSpeedSteerReduction = 0.18f;
                asset.downforce = 34f;
                asset.lateralGripAssist = 1.15f;
                asset.antiRollForce = 3200f;
                asset.handbrakeGripMultiplier = 0.5f;
                asset.centerOfMassOffset = new Vector3(0f, -0.48f, 0.03f);
                asset.spring = 30000f;
                asset.damper = 3900f;
                asset.suspensionDistance = 0.18f;
                asset.forwardStiffness = 1.28f;
                asset.sidewaysStiffness = 1.45f;
                asset.maxRPM = 5800f;
                asset.shiftUpRPM = 5000f;
                asset.shiftDownRPM = 1800f;
                asset.finalDriveRatio = 3.85f;
                asset.gearRatios = new[] { 0f, 2.35f, 1.82f, 1.42f, 1.08f, 0.92f, 0.74f };
                asset.defaultMass = 1480f;
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
            Material material = LoadFirstExistingAsset<Material>(DaySkyboxMaterialPath, FallbackDaySkyboxMaterialPath);
            return material != null
                ? material
                : CreateOrUpdateFallbackSkyboxMaterial(
                    FallbackDaySkyboxMaterialPath,
                    "DaySkyboxFallback",
                    new Color(0.48f, 0.58f, 0.72f),
                    new Color(0.9f, 0.9f, 0.88f),
                    1.45f,
                    0.7f);
        }

        private static Material LoadNightSkyboxMaterial()
        {
            Material material = LoadFirstExistingAsset<Material>(NightSkyboxMaterialPath, FallbackNightSkyboxMaterialPath);
            return material != null
                ? material
                : CreateOrUpdateFallbackSkyboxMaterial(
                    FallbackNightSkyboxMaterialPath,
                    "NightSkyboxFallback",
                    new Color(0.02f, 0.03f, 0.08f),
                    new Color(0.08f, 0.12f, 0.2f),
                    0.25f,
                    0.2f);
        }

        private static Cubemap LoadDaySkyCubemap()
        {
            Cubemap cubemap = LoadFirstExistingAsset<Cubemap>(DaySkyboxCubemapPath);
            if (cubemap != null)
            {
                return cubemap;
            }

            Material material = LoadDaySkyboxMaterial();
            return ExtractCubemapFromSkyMaterial(material);
        }

        private static Cubemap LoadNightSkyCubemap()
        {
            Cubemap cubemap = LoadFirstExistingAsset<Cubemap>(NightSkyboxCubemapPath);
            if (cubemap != null)
            {
                return cubemap;
            }

            Material material = LoadNightSkyboxMaterial();
            return ExtractCubemapFromSkyMaterial(material);
        }

        private static Cubemap ExtractCubemapFromSkyMaterial(Material material)
        {
            if (material == null)
            {
                return null;
            }

            Texture texture = null;
            if (material.HasProperty("_Tex"))
            {
                texture = material.GetTexture("_Tex");
            }
            else if (material.HasProperty("_MainTex"))
            {
                texture = material.GetTexture("_MainTex");
            }

            return texture as Cubemap;
        }

        private static Material CreateOrUpdateFallbackSkyboxMaterial(string assetPath, string materialName, Color skyTint, Color groundColor, float exposure, float atmosphereThickness)
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null)
            {
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(skyShader)
                {
                    name = materialName
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.shader = skyShader;
            material.name = materialName;

            if (material.HasProperty("_SkyTint"))
            {
                material.SetColor("_SkyTint", skyTint);
            }

            if (material.HasProperty("_GroundColor"))
            {
                material.SetColor("_GroundColor", groundColor);
            }

            if (material.HasProperty("_Exposure"))
            {
                material.SetFloat("_Exposure", exposure);
            }

            if (material.HasProperty("_AtmosphereThickness"))
            {
                material.SetFloat("_AtmosphereThickness", atmosphereThickness);
            }

            if (material.HasProperty("_SunSize"))
            {
                material.SetFloat("_SunSize", 0.03f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject LoadSlimUiCanvasTemplate()
        {
            return LoadFirstExistingAsset<GameObject>(SlimUiCanvasTemplatePath);
        }

        private static AudioMixer LoadSlimUiMixer()
        {
            return LoadFirstExistingAsset<AudioMixer>(SlimUiMixerPath);
        }

        private static GameObject LoadHudSpeedometerPrefab()
        {
            return LoadFirstExistingAsset<GameObject>(ImportedSpeedometerPrefabPath);
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

        private static bool AssetExistsAtPath<T>(string path) where T : UnityEngine.Object
        {
            return !string.IsNullOrWhiteSpace(path) && AssetDatabase.LoadAssetAtPath<T>(path) != null;
        }

        private static bool HasImportedFcg()
        {
            return AssetDatabase.IsValidFolder(ImportedFcgFolderPath);
        }
    }
}
