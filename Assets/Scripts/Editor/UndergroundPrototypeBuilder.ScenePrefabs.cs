using FullThrottle.SacredCore.Audio;
using FullThrottle.SacredCore.Campaign;
using FullThrottle.SacredCore.Career;
using FullThrottle.SacredCore.Economy;
using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Progression;
using FullThrottle.SacredCore.Race;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Vehicle;
using FullThrottle.SacredCore.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        [MenuItem("Full Throttle/Prefabs/Rebuild Generated Scene Prefabs", priority = 90)]
        public static void RebuildGeneratedScenePrefabs()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            ConfigureTagsAndLayers();
            CreateSceneSupportPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateSceneSupportPrefabs(bool preserveExistingAssets = false)
        {
            CreateOrUpdateRuntimeRootPrefab(preserveExistingAssets);
            CreateOrUpdateWorldSystemsPrefab(preserveExistingAssets);
            CreateOrUpdateFollowCameraPrefab(preserveExistingAssets);
            CreateOrUpdateHudPrefab(preserveExistingAssets);
        }

        /// <summary>
        /// Phase 5 migration: RuntimeRoot prefab now uses the FT sacred-core service stack
        /// instead of legacy PersistentProgressManager / VehicleOwnershipSystem / SaveSystem.
        /// </summary>
        private static GameObject CreateOrUpdateRuntimeRootPrefab(bool preserveExistingAsset = false)
        {
            if (preserveExistingAsset && AssetExistsAtPath<GameObject>(RuntimeRootPrefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRootPrefabPath);
            }

            GameObject root = new GameObject("FT_RuntimeRoot");

            // ── Sacred-core bootstrap and persistence ──
            FTBootstrap bootstrap = root.AddComponent<FTBootstrap>();
            FTRuntimeRoot runtimeRoot = root.AddComponent<FTRuntimeRoot>();
            FTServiceRegistry serviceRegistry = root.AddComponent<FTServiceRegistry>();
            FTSaveGateway saveGateway = root.AddComponent<FTSaveGateway>();

            // ── Registries ──
            FTCarRegistry carRegistry = root.AddComponent<FTCarRegistry>();
            FTAudioProfileRegistry audioRegistry = root.AddComponent<FTAudioProfileRegistry>();

            // ── Selected car truth ──
            FTSelectedCarRuntime selectedCar = root.AddComponent<FTSelectedCarRuntime>();

            // ── World travel ──
            FTWorldTravelDirector worldTravel = root.AddComponent<FTWorldTravelDirector>();

            // ── Career / economy / race pressure ──
            FTCareerDirector career = root.AddComponent<FTCareerDirector>();
            FTRiskEconomyDirector risk = root.AddComponent<FTRiskEconomyDirector>();
            FTHeatDirector heat = root.AddComponent<FTHeatDirector>();
            FTWagerDirector wager = root.AddComponent<FTWagerDirector>();
            FTRivalDirector rival = root.AddComponent<FTRivalDirector>();
            FTCampaignDirector campaign = root.AddComponent<FTCampaignDirector>();
            FTNarrativeDirector narrative = root.AddComponent<FTNarrativeDirector>();
            FTProgressionDirector progression = root.AddComponent<FTProgressionDirector>();

            // ── Audio identity ──
            FTAudioIdentityDirector audioIdentity = root.AddComponent<FTAudioIdentityDirector>();
            FTAudioRosterValidator audioValidator = root.AddComponent<FTAudioRosterValidator>();

            // ── Validation ──
            FTSacredCoreHealthCheck healthCheck = root.AddComponent<FTSacredCoreHealthCheck>();
            FTGameContext context = root.AddComponent<FTGameContext>();

            // ── Session (optional legacy compat) ──
            AddRuntimeSessionManager(root);

            // ── Settings (audio mixer) ──
            GameSettingsManager settingsManager = root.AddComponent<GameSettingsManager>();
            SetObjectReference(settingsManager, "audioMixer", LoadSlimUiMixer());
            SetObjectReference(campaign, "campaign", LoadFullThrottleCampaign());
            EnsureRuntimeRadioOnObject(root);

            // ── Wire bootstrap ──
            SetObjectReference(bootstrap, "saveGateway", saveGateway);
            AssignBehaviourArray(bootstrap, "serviceBehaviours",
                runtimeRoot,
                serviceRegistry,
                saveGateway,
                carRegistry,
                audioRegistry,
                selectedCar,
                worldTravel,
                career,
                risk,
                heat,
                wager,
                rival,
                campaign,
                narrative,
                progression,
                audioIdentity,
                audioValidator,
                healthCheck,
                context);

            // ── Wire FTCarRegistry with all car definitions from disk ──
            string carsFolder = "Assets/ScriptableObjects/FullThrottle/Cars";
            if (AssetDatabase.IsValidFolder(carsFolder))
            {
                string[] guids = AssetDatabase.FindAssets("t:FTCarDefinition", new[] { carsFolder });
                SerializedObject carRegSo = new SerializedObject(carRegistry);
                SerializedProperty carsProp = carRegSo.FindProperty("cars");
                carsProp.ClearArray();
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    FTCarDefinition def = AssetDatabase.LoadAssetAtPath<FTCarDefinition>(assetPath);
                    if (def != null)
                    {
                        carsProp.InsertArrayElementAtIndex(carsProp.arraySize);
                        carsProp.GetArrayElementAtIndex(carsProp.arraySize - 1).objectReferenceValue = def;
                    }
                }
                carRegSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── Wire FTAudioProfileRegistry with all audio profiles from disk ──
            string audioFolder = "Assets/ScriptableObjects/FullThrottle/AudioProfiles";
            if (AssetDatabase.IsValidFolder(audioFolder))
            {
                string[] guids = AssetDatabase.FindAssets("t:FTVehicleAudioProfile", new[] { audioFolder });
                SerializedObject audioRegSo = new SerializedObject(audioRegistry);
                SerializedProperty profilesProp = audioRegSo.FindProperty("profiles");
                profilesProp.ClearArray();
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    FTVehicleAudioProfile profile = AssetDatabase.LoadAssetAtPath<FTVehicleAudioProfile>(assetPath);
                    if (profile != null)
                    {
                        profilesProp.InsertArrayElementAtIndex(profilesProp.arraySize);
                        profilesProp.GetArrayElementAtIndex(profilesProp.arraySize - 1).objectReferenceValue = profile;
                    }
                }
                audioRegSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, RuntimeRootPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRootPrefabPath);
        }

        private static GameObject CreateOrUpdateWorldSystemsPrefab(bool preserveExistingAsset = false)
        {
            if (preserveExistingAsset && AssetExistsAtPath<GameObject>(WorldSystemsPrefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(WorldSystemsPrefabPath);
            }

            GameObject worldSystems = new GameObject("WorldSystems");

            GameObject dayNightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fantastic City Generator/DayNight/DayNight.prefab");
            if (dayNightPrefab != null)
            {
                GameObject dayNightInst = (GameObject)PrefabUtility.InstantiatePrefab(dayNightPrefab, worldSystems.transform);
                dayNightInst.name = "DayNight";
                DayNight dayNight = dayNightInst.GetComponent<DayNight>();
                if (dayNight != null)
                {
                    dayNight.duskNightOnly = true;
                    dayNight.isNight = true;
                    dayNight.night = true;
                    dayNight.minimumDuskNightBlend = Mathf.Max(dayNight.minimumDuskNightBlend, 0.72f);
                    dayNight.syncWithTimeOfDay = true;
                }
            }
            else
            {
                Debug.LogWarning("[UndergroundPrototypeBuilder] Could not find DayNight prefab at Assets/Fantastic City Generator/DayNight/DayNight.prefab");
            }

            AttachGlobalVolume(worldSystems.transform, "GlobalVolume", ProjectWorldVolumeProfilePath);

            PrefabUtility.SaveAsPrefabAsset(worldSystems, WorldSystemsPrefabPath);
            Object.DestroyImmediate(worldSystems);
            return AssetDatabase.LoadAssetAtPath<GameObject>(WorldSystemsPrefabPath);
        }

        private static GameObject CreateOrUpdateFollowCameraPrefab(bool preserveExistingAsset = false)
        {
            if (preserveExistingAsset && AssetExistsAtPath<GameObject>(FollowCameraPrefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(FollowCameraPrefabPath);
            }

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3f, -7f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.renderingPath = RenderingPath.UsePlayerSettings;
            camera.clearFlags = CameraClearFlags.Skybox;
            EnablePostProcessing(camera);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<VehicleCameraFollow>();
            // VehicleSpeedEffectsController removed — its functionality is now built into VehicleCameraFollow.

            PrefabUtility.SaveAsPrefabAsset(cameraObject, FollowCameraPrefabPath);
            Object.DestroyImmediate(cameraObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(FollowCameraPrefabPath);
        }

        private static GameObject CreateOrUpdateHudPrefab(bool preserveExistingAsset = false)
        {
            if (preserveExistingAsset && AssetExistsAtPath<GameObject>(HudPrefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            }

            Canvas canvas = CreateCanvas("HUD");
            canvas.transform.localScale = Vector3.one;
            StylizedHudComposer composer = canvas.gameObject.AddComponent<StylizedHudComposer>();
            SetObjectReference(composer, "speedometerPrefab", LoadHudSpeedometerPrefab());
            composer.Compose();
            EnsureRadioPopupOnCanvas(canvas);

            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, HudPrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        }

        /// <summary>
        /// Utility used by the RuntimeRoot builder to wire FTBootstrap.serviceBehaviours.
        /// </summary>
        private static void AssignBehaviourArray(Object target, string propertyName, params MonoBehaviour[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
}
