using System;
using System.Collections.Generic;
using System.Linq;
using FCG;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Underground.AI;
using Underground.Core;
using Underground.Garage;
using Underground.Progression;
using Underground.Race;
using Underground.Save;
using Underground.TimeSystem;
using Underground.UI;
using Underground.Vehicle;
using Underground.World;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        [MenuItem("Underground/Rebuild Full Game", priority = 1)]
        public static void RebuildFullGameFromTopMenu()
        {
            ReloadAllGeneratedContent();
        }

        [MenuItem("Underground/Build Full Game", priority = 2)]
        public static void BuildFullGameFromTopMenu()
        {
            RebuildFullGameFromTopMenu();
        }

        [MenuItem("Underground/Reload All", priority = 3)]
        public static void ReloadAllGeneratedContent()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string sceneToReload = ResolveManagedSceneToReload(SceneManager.GetActiveScene().path);
            BuildFullPrototype(showDialog: false);
            ForceReloadGeneratedAssets();
            OpenReloadedScene(sceneToReload);
            EditorUtility.DisplayDialog("Full Game Reloaded", "Rebuilt and reloaded the generated scenes, prefabs, starter data, and rendering setup.", "OK");
        }

        [MenuItem("Underground/World/Rebuild World Scene", priority = 20)]
        public static void RebuildWorldScene()
        {
            PreparePrototypeAssets(out GameObject playerCarPrefab, out _, out RaceDefinition dayRace, out RaceDefinition nightRace, out RaceDefinition wagerRace);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ComposeWorldScene(scene, playerCarPrefab, dayRace, nightRace, wagerRace, null);
            EditorSceneManager.SaveScene(scene, WorldScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("World Ready", "Built the world scene with FCG huge-city automation when available.", "OK");
        }

        [MenuItem("Underground/World/Rebuild Open Scene World", priority = 21)]
        public static void RebuildOpenSceneWorld()
        {
            PreparePrototypeAssets(out GameObject playerCarPrefab, out _, out RaceDefinition dayRace, out RaceDefinition nightRace, out RaceDefinition wagerRace);
            Scene scene = SceneManager.GetActiveScene();
            ComposeWorldScene(scene, playerCarPrefab, dayRace, nightRace, wagerRace, DetachPreservedWorldUiRoot());
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Underground/UI/Rebuild Main Menu Scene", priority = 40)]
        public static void RebuildMainMenuSceneFromTopMenu()
        {
            PreparePrototypeAssets(out GameObject playerCarPrefab, out _, out _, out _, out _);
            CreateManagedMainMenuScene(playerCarPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Underground/UI/Rebuild Garage Scene", priority = 41)]
        public static void RebuildGarageSceneFromTopMenu()
        {
            PreparePrototypeAssets(out GameObject playerCarPrefab, out UpgradeDefinition engineUpgrade, out _, out _, out _);
            CreateGarageScene(playerCarPrefab, engineUpgrade, preserveExistingScene: true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Underground/UI/Rebuild HUD In Open Scene", priority = 42)]
        public static void RebuildHudInOpenScene()
        {
            GameObject uiRoot = GetOrCreateSceneObject("UndergroundUI");
            ClearChildren(uiRoot.transform);
            CreateHudCanvasUnder(uiRoot.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Underground/Races/Rebuild Race Layout In Open Scene", priority = 60)]
        public static void RebuildRaceLayoutInOpenScene()
        {
            PreparePrototypeAssets(out _, out _, out RaceDefinition dayRace, out RaceDefinition nightRace, out RaceDefinition wagerRace);
            GameObject gameplayRoot = GetOrCreateSceneObject("UndergroundGameplay");
            Transform raceRoot = gameplayRoot.transform.Find("RaceLayout");
            if (raceRoot != null)
            {
                Object.DestroyImmediate(raceRoot.gameObject);
            }

            raceRoot = CreateEmptyChild(gameplayRoot.transform, "RaceLayout", Vector3.zero);
            WorldAnchorSet anchors = ResolveWorldAnchors();
            CreateRaceStartUnder(raceRoot, "Industrial Sprint", dayRace, anchors.dayRacePose);
            CreateRaceStartUnder(raceRoot, "Midnight Run", nightRace, anchors.nightRacePose);
            CreateRaceStartUnder(raceRoot, "Neon Pinkslip", wagerRace, anchors.wagerRacePose);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Underground/Systems/Place Runtime Systems In Open Scene", priority = 80)]
        public static void PlaceRuntimeSystemsInOpenScene()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            ConfigureTagsAndLayers();
            EnsureRuntimeRoot(false);
            EnsureWorldSystems(GetOrCreateSceneObject("UndergroundGameplay").transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static string ResolveManagedSceneToReload(string activeScenePath)
        {
            string[] managedScenePaths =
            {
                BootstrapScenePath,
                MainMenuScenePath,
                MainMenuNewScenePath,
                GarageScenePath,
                WorldScenePath,
                VehicleTestScenePath
            };

            for (int i = 0; i < managedScenePaths.Length; i++)
            {
                if (string.Equals(managedScenePaths[i], activeScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return managedScenePaths[i];
                }
            }

            return WorldScenePath;
        }

        private static void ForceReloadGeneratedAssets()
        {
            string[] generatedAssetPaths =
            {
                PlayerCarPrefabPath,
                StarterVehicleStatsPath,
                StarterEngineUpgradePath,
                StarterGripUpgradePath,
                DayRacePath,
                NightRacePath,
                WagerRacePath,
                RuntimeRootPrefabPath,
                WorldSystemsPrefabPath,
                FollowCameraPrefabPath,
                HudPrefabPath,
                ProjectHdrpAssetPath,
                ProjectWorldVolumeProfilePath,
                ProjectGarageVolumeProfilePath,
                BootstrapScenePath,
                MainMenuScenePath,
                MainMenuNewScenePath,
                GarageScenePath,
                WorldScenePath,
                VehicleTestScenePath
            };

            for (int i = 0; i < generatedAssetPaths.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(generatedAssetPaths[i]) && AssetDatabase.LoadMainAssetAtPath(generatedAssetPaths[i]) != null)
                {
                    AssetDatabase.ImportAsset(generatedAssetPaths[i], ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void OpenReloadedScene(string scenePath)
        {
            string targetScenePath = string.IsNullOrWhiteSpace(scenePath) ? WorldScenePath : scenePath;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetScenePath) != null)
            {
                EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Single);
            }
        }

        private static void PreparePrototypeAssets(out GameObject playerCarPrefab, out UpgradeDefinition engineUpgrade, out RaceDefinition dayRace, out RaceDefinition nightRace, out RaceDefinition wagerRace)
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            ConfigureTagsAndLayers();

            if (HasImportedFcg())
            {
                PrepareFcgAssets();
            }

            VehicleStatsData starterStats = CreateOrUpdateStarterVehicleStats();
            engineUpgrade = CreateOrUpdateEngineUpgrade();
            CreateOrUpdateGripUpgrade();
            dayRace = CreateOrUpdateDayRace();
            nightRace = CreateOrUpdateNightRace();
            wagerRace = CreateOrUpdateWagerRace();
            playerCarPrefab = CreateOrUpdatePlayerCarPrefab(starterStats);
            CreateSceneSupportPrefabs();
        }

        private static void PrepareFcgAssets()
        {
            FCityGenerator editorWindow = ScriptableObject.CreateInstance<FCityGenerator>();
            try
            {
                editorWindow.LoadAssets(true);
            }
            finally
            {
                Object.DestroyImmediate(editorWindow);
            }
        }

        private static void ComposeWorldScene(Scene scene, GameObject playerCarPrefab, RaceDefinition dayRace, RaceDefinition nightRace, RaceDefinition wagerRace, Transform preservedUiRoot)
        {
            EnsureRuntimeRoot(false);

            GameObject generatedRoot = RecreateSceneObject("UndergroundGenerated");
            Transform systemsRoot = CreateEmptyChild(generatedRoot.transform, "UndergroundSystems", Vector3.zero);
            Transform environmentRoot = CreateEmptyChild(generatedRoot.transform, "UndergroundEnvironment", Vector3.zero);
            Transform gameplayRoot = CreateEmptyChild(generatedRoot.transform, "UndergroundGameplay", Vector3.zero);
            Transform uiRoot;
            if (preservedUiRoot != null)
            {
                preservedUiRoot.name = "UndergroundUI";
                preservedUiRoot.SetParent(generatedRoot.transform, false);
                uiRoot = preservedUiRoot;
            }
            else
            {
                uiRoot = CreateEmptyChild(generatedRoot.transform, "UndergroundUI", Vector3.zero);
            }

            EnsureWorldSystems(systemsRoot);

            bool builtHugeCity = TryGenerateHugeFcgCity(environmentRoot);
            if (!builtHugeCity)
            {
                CreateFallbackWorldEnvironment(environmentRoot);
            }

            RemoveImportedSkyOverrides();
            InitializeWorldLightingPreview(systemsRoot);

            WorldAnchorSet anchors = ResolveWorldAnchors();
            GameObject playerCar = CreatePlayerCarInstance(gameplayRoot, playerCarPrefab, anchors.playerSpawnPose);
            CreateDynamicWorldReflectionProbe(gameplayRoot, playerCar.transform);
            CreateFollowCameraUnder(generatedRoot.transform, playerCar);
            Canvas preservedHudCanvas = uiRoot.GetComponentInChildren<Canvas>(true);
            if (preservedHudCanvas != null)
            {
                RefreshPreservedHudCanvas(preservedHudCanvas);
            }
            else
            {
                CreateHudCanvasUnder(uiRoot);
            }
            CreateGarageEntranceUnder(gameplayRoot, anchors.garagePose);
            CreateRespawnCheckpointUnder(gameplayRoot, anchors.respawnPose);

            Transform raceRoot = CreateEmptyChild(gameplayRoot, "RaceLayout", Vector3.zero);
            CreateRaceStartUnder(raceRoot, "Industrial Sprint", dayRace, anchors.dayRacePose);
            CreateRaceStartUnder(raceRoot, "Midnight Run", nightRace, anchors.nightRacePose);
            CreateRaceStartUnder(raceRoot, "Neon Pinkslip", wagerRace, anchors.wagerRacePose);

            EnsureTrafficSystem(gameplayRoot, playerCar.transform, builtHugeCity);
            RenderSettings.skybox = LoadDaySkyboxMaterial();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1.0f;
            RenderSettings.fog = true; 
            
            Lightmapping.realtimeGI = true;
            Lightmapping.bakedGI = true;
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void InitializeWorldLightingPreview(Transform systemsRoot)
        {
            if (systemsRoot == null)
            {
                return;
            }

            TimeOfDay packageTimeOfDay = systemsRoot.GetComponentInChildren<TimeOfDay>(true);
            if (packageTimeOfDay != null)
            {
                PackageTimeOfDayUtility.SetHours(packageTimeOfDay, 14f);
            }

            Light sunLight = systemsRoot.GetComponentInChildren<Light>(true);
            if (sunLight != null && sunLight.type == LightType.Directional)
            {
                RenderSettings.sun = sunLight;
            }
        }

        private static void EnsureRuntimeRoot(bool includeBootstrapLoader)
        {
            GameObject runtimeRoot = GameObject.Find("RuntimeRoot");
            if (runtimeRoot == null)
            {
                GameObject runtimeRootPrefab = CreateOrUpdateRuntimeRootPrefab();
                runtimeRoot = runtimeRootPrefab != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(runtimeRootPrefab)
                    : new GameObject("RuntimeRoot");
                runtimeRoot.name = "RuntimeRoot";
            }

            GetOrAddComponent<PersistentRuntimeRoot>(runtimeRoot);
            GetOrAddComponent<SaveSystem>(runtimeRoot);
            GetOrAddComponent<PersistentProgressManager>(runtimeRoot);
            AddRuntimeSessionManager(runtimeRoot);
            GetOrAddComponent<VehicleOwnershipSystem>(runtimeRoot);
            GameSettingsManager settingsManager = GetOrAddComponent<GameSettingsManager>(runtimeRoot);
            SetObjectReference(settingsManager, "audioMixer", LoadSlimUiMixer());

            BootstrapSceneLoader loader = runtimeRoot.GetComponent<BootstrapSceneLoader>();
            if (includeBootstrapLoader)
            {
                ConfigureBootstrapSceneLoader(GetOrAddComponent<BootstrapSceneLoader>(runtimeRoot));
            }
            else if (loader != null)
            {
                Object.DestroyImmediate(loader);
            }
        }

        private static void EnsureWorldSystems(Transform parent)
        {
            GameObject existing = GameObject.Find("WorldSystems");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject worldSystemsPrefab = CreateOrUpdateWorldSystemsPrefab();
            GameObject worldSystems = worldSystemsPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(worldSystemsPrefab)
                : new GameObject("WorldSystems");
            worldSystems.name = "WorldSystems";
            worldSystems.transform.SetParent(parent, false);
        }

        private static ReflectionProbe CreateOrRefreshSceneReflectionProbe(Transform parent, Vector3 center, Vector3 size)
        {
            Transform existing = parent.Find("SceneReflectionProbe");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject probeObject = new GameObject("SceneReflectionProbe");
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.localPosition = center;

            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            
            // Safe property set for Shape
            SetPropertyIfPresent(probe, "shape", 1); // 1 = Sphere in most versions
            
            // Radius 120 to match the previous influence size
            const float defaultRadius = 120f;
            var radiusField = typeof(ReflectionProbe).GetProperty("sphereRadius") ?? typeof(ReflectionProbe).GetProperty("radius");
            if (radiusField != null) radiusField.SetValue(probe, defaultRadius);
            
            probe.size = size; // Fallback
            probe.center = Vector3.zero;
            probe.boxProjection = false; 
            probe.intensity = 0.6f;
            probe.importance = 10;

            // Inject HDRP specific data to prevent blowout
            Type hdProbeDataType = FindType("UnityEngine.Rendering.HighDefinition.HDAdditionalReflectionData, Unity.RenderPipelines.HighDefinition.Runtime");
            if (hdProbeDataType != null)
            {
                Component hdProbeData = probe.gameObject.GetComponent(hdProbeDataType) ?? probe.gameObject.AddComponent(hdProbeDataType);
                SetPropertyIfPresent(hdProbeData, "multiplier", 0.6f);
                SetPropertyIfPresent(hdProbeData, "weight", 1.0f);
            }

            int playerVehicleLayer = LayerMask.NameToLayer("PlayerVehicle");
            probe.cullingMask = playerVehicleLayer >= 0 ? ~(1 << playerVehicleLayer) : ~0;
            return probe;
        }

        private static void CreateDynamicWorldReflectionProbe(Transform parent, Transform target)
        {
            if (parent == null || target == null)
            {
                return;
            }

            ReflectionProbe probe = CreateOrRefreshSceneReflectionProbe(parent, target.localPosition + new Vector3(0f, 5f, 0f), new Vector3(120f, 36f, 120f));
            if (probe == null)
            {
                return;
            }

            PlayerReflectionProbeController controller = GetOrAddComponent<PlayerReflectionProbeController>(probe.gameObject);
            SetObjectReference(controller, "target", target);
            SetVector3Value(controller, "probeOffset", new Vector3(0f, 5f, 0f));
            SetVector3Value(controller, "probeSize", new Vector3(120f, 36f, 120f));
            SetFloatValue(controller, "snapDistance", 10f);
            SetFloatValue(controller, "refreshInterval", 0.2f);
            SetFloatValue(controller, "minMoveDistance", 6f);
        }

        private static bool TryGenerateHugeFcgCity(Transform parent)
        {
            if (!HasImportedFcg())
            {
                return false;
            }

            GameObject generatorPrefab = LoadFcgGeneratorPrefab();
            if (generatorPrefab == null)
            {
                return false;
            }

            GameObject generatorObject = (GameObject)PrefabUtility.InstantiatePrefab(generatorPrefab);
            generatorObject.name = "FCG_Generator";
            generatorObject.transform.SetParent(parent, false);

            CityGenerator generator = generatorObject.GetComponent<CityGenerator>();
            if (generator == null)
            {
                Object.DestroyImmediate(generatorObject);
                return false;
            }

            generator.GenerateCity(4, true, false);
            generator.GenerateAllBuildings(true, 170f);

            GameObject cityMaker = GameObject.Find("City-Maker");
            if (cityMaker != null)
            {
                cityMaker.transform.SetParent(parent, true);
                SetLayerRecursively(cityMaker, LayerMask.NameToLayer("WorldStatic"));
            }

            return cityMaker != null;
        }

        private static void RemoveImportedSkyOverrides()
        {
            DestroySceneObjectsWithComponent("DayNight");
            DestroySceneObjectsWithComponent("ShiftAtRuntime");
        }

        private static void DestroySceneObjectsWithComponent(string componentTypeName)
        {
            if (string.IsNullOrWhiteSpace(componentTypeName))
            {
                return;
            }

            Type componentType = Type.GetType(componentTypeName) ?? AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.Name == componentTypeName);
            if (componentType == null)
            {
                return;
            }

            Component[] components = Object.FindObjectsByType(componentType, FindObjectsSortMode.None)
                .OfType<Component>()
                .ToArray();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    Object.DestroyImmediate(components[i].gameObject);
                }
            }
        }

        private static void CreateFallbackWorldEnvironment(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "WorldGround";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(70f, 1f, 70f);
            SetLayerRecursively(ground, LayerMask.NameToLayer("WorldStatic"));

            for (int i = 0; i < 6; i++)
            {
                CreateObstacleUnder(parent, new Vector3(-6f + (i * 2.5f), 0.75f, 18f + (i * 8f)), new Vector3(1f, 1.5f, 1f));
            }
        }

        private static void EnsureTrafficSystem(Transform parent, Transform playerTransform, bool usingFcg)
        {
            if (usingFcg)
            {
                GameObject trafficPrefab = LoadFcgTrafficSystemPrefab();
                if (trafficPrefab == null)
                {
                    return;
                }

                GameObject trafficSystemObject = (GameObject)PrefabUtility.InstantiatePrefab(trafficPrefab);
                trafficSystemObject.name = "Traffic System";
                trafficSystemObject.transform.SetParent(parent, false);

                TrafficSystem trafficSystem = trafficSystemObject.GetComponent<TrafficSystem>();
                if (trafficSystem != null)
                {
                    trafficSystem.player = playerTransform;
                    trafficSystem.maxVehiclesWithPlayer = 120;
                    trafficSystem.around = 420f;
                    trafficSystem.trafficLightHand = 0;
                    EditorUtility.SetDirty(trafficSystem);
                }

                trafficSystemObject.AddComponent<TrafficNightLightingInstaller>();

                return;
            }

            Transform trafficRoot = CreateEmptyChild(parent, "FallbackTraffic", Vector3.zero);
            Transform[] waypoints = new Transform[4];
            Vector3[] positions =
            {
                new Vector3(20f, 0.5f, -10f), new Vector3(30f, 0.5f, 20f), new Vector3(20f, 0.5f, 55f), new Vector3(8f, 0.5f, 18f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                waypoints[i] = CreateEmptyChild(trafficRoot, $"TrafficWaypoint_{i + 1}", positions[i]);
            }

            CreateTrafficVehicleUnder(trafficRoot, "TrafficTaxi", LoadTaxiTrafficPrefab(), positions[0], waypoints);
            CreateTrafficVehicleUnder(trafficRoot, "TrafficPolice", LoadPoliceTrafficPrefab(), positions[2], waypoints);
        }

        private static GameObject CreatePlayerCarInstance(Transform parent, GameObject playerCarPrefab, Pose pose)
        {
            GameObject playerCar = (GameObject)PrefabUtility.InstantiatePrefab(playerCarPrefab);
            playerCar.name = "PlayerCar";
            playerCar.transform.SetParent(parent, false);
            playerCar.transform.SetPositionAndRotation(pose.position, pose.rotation);
            return playerCar;
        }

        private static void CreateFollowCameraUnder(Transform parent, GameObject playerCar)
        {
            GameObject followCameraPrefab = CreateOrUpdateFollowCameraPrefab();
            GameObject cameraObject = followCameraPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(followCameraPrefab)
                : new GameObject("Main Camera");

            cameraObject.name = "Main Camera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = playerCar.transform.position + new Vector3(0f, 3f, -7f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            VehicleCameraFollow follow = GetOrAddComponent<VehicleCameraFollow>(cameraObject);
            SetObjectReference(follow, "targetVehicle", playerCar.GetComponent<VehicleDynamicsController>());
            SetObjectReference(follow, "target", playerCar.transform.Find("CameraTarget"));
            SetObjectReference(follow, "targetBody", playerCar.GetComponent<Rigidbody>());
        }

        private static void CreateHudCanvasUnder(Transform parent)
        {
            GameObject hudPrefab = CreateOrUpdateHudPrefab();
            GameObject hudObject = hudPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab)
                : CreateCanvas("HUD").gameObject;

            hudObject.name = "HUD";
            hudObject.transform.SetParent(parent, false);
            hudObject.transform.localScale = Vector3.one;

            StylizedHudComposer composer = GetOrAddComponent<StylizedHudComposer>(hudObject);
            composer.Compose();
        }

        private static void CreateGarageEntranceUnder(Transform parent, Pose pose)
        {
            GameObject entrance = new GameObject("GarageEntrance");
            entrance.transform.SetParent(parent, false);
            entrance.tag = "Garage";
            entrance.transform.SetPositionAndRotation(pose.position, pose.rotation);
            BoxCollider collider = entrance.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(12f, 5f, 10f);
            collider.center = new Vector3(0f, 0f, -10f);
            entrance.AddComponent<GarageEntranceTrigger>();
        }

        private static void CreateRespawnCheckpointUnder(Transform parent, Pose pose)
        {
            GameObject checkpoint = new GameObject("RespawnCheckpoint");
            checkpoint.transform.SetParent(parent, false);
            checkpoint.transform.SetPositionAndRotation(pose.position, pose.rotation);
            BoxCollider collider = checkpoint.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(12f, 4f, 8f);
            checkpoint.AddComponent<RespawnPointTrigger>();
        }

        private static void CreateRaceStartUnder(Transform parent, string name, RaceDefinition definition, Pose pose)
        {
            GameObject start = new GameObject(name);
            start.transform.SetParent(parent, false);
            start.transform.SetPositionAndRotation(pose.position, pose.rotation);
            start.tag = "RaceStart";
            BoxCollider collider = start.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(10f, 3f, 4f);

            RaceManager raceManager = start.AddComponent<RaceManager>();
            SetObjectReference(raceManager, "activeRace", definition);
            start.AddComponent<RaceStartTrigger>();

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            marker.transform.SetParent(start.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            marker.transform.localScale = new Vector3(6f, 0.05f, 2.2f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        private static void CreateObstacleUnder(Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            SetLayerRecursively(cube, LayerMask.NameToLayer("WorldStatic"));
        }

        private static void CreateTrafficVehicleUnder(Transform parent, string name, GameObject prefab, Vector3 position, Transform[] waypoints)
        {
            GameObject trafficCar;
            if (prefab != null)
            {
                trafficCar = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                trafficCar.name = name;
                StripRuntimeComponents(trafficCar);
            }
            else
            {
                trafficCar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trafficCar.name = name;
                trafficCar.transform.localScale = new Vector3(1.8f, 1f, 4f);
            }

            trafficCar.transform.SetParent(parent, false);
            trafficCar.tag = "Traffic";
            trafficCar.transform.localPosition = position;
            SetLayerRecursively(trafficCar, LayerMask.NameToLayer("Traffic"));

            AIWaypointFollower ai = trafficCar.AddComponent<AIWaypointFollower>();
            SerializedObject aiSo = new SerializedObject(ai);
            SerializedProperty pointsProperty = aiSo.FindProperty("waypoints");
            pointsProperty.arraySize = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++)
            {
                pointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            }
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            VehicleNightLightingController lightingController = trafficCar.GetComponent<VehicleNightLightingController>() ?? trafficCar.AddComponent<VehicleNightLightingController>();
            lightingController.ConfigureForTraffic(false);
        }

        private static WorldAnchorSet ResolveWorldAnchors()
        {
            Pose preferredStartPose = new Pose(PreferredWorldStartPosition, Quaternion.Euler(PreferredWorldStartEuler));

            FCGWaypointsContainer[] roads = Object.FindObjectsByType<FCGWaypointsContainer>(FindObjectsSortMode.None)
                .Where(container => container != null && container.transform.childCount > 2)
                .ToArray();

            if (roads.Length < 4)
            {
                return WorldAnchorSet.FromPreferredStart(preferredStartPose);
            }

            for (int i = 0; i < roads.Length; i++)
            {
                roads[i].GetWaypoints();
            }

            FCGWaypointsContainer spawnRoad = roads.OrderBy(container => container.transform.position.sqrMagnitude).First();
            List<FCGWaypointsContainer> selection = SelectSpreadRoads(roads, spawnRoad, 4);
            if (selection.Count < 4)
            {
                return WorldAnchorSet.FromPreferredStart(preferredStartPose);
            }

            Pose playerSpawn = CreateRoadPose(selection[0], 1.2f, 0);
            Pose garagePose = OffsetPose(playerSpawn, new Vector3(0f, 0f, 28f));
            Pose respawnPose = CreateRoadPose(selection[0], 1.2f, Mathf.Min(2, Mathf.Max(0, selection[0].transform.childCount - 2)));
            Pose dayRacePose = CreateRoadPose(selection[1], 0.25f, 0);
            Pose nightRacePose = CreateRoadPose(selection[2], 0.25f, 0);
            Pose wagerRacePose = CreateRoadPose(selection[3], 0.25f, 0);

            return new WorldAnchorSet(playerSpawn, garagePose, respawnPose, dayRacePose, nightRacePose, wagerRacePose);
        }

        private static List<FCGWaypointsContainer> SelectSpreadRoads(FCGWaypointsContainer[] roads, FCGWaypointsContainer seed, int count)
        {
            List<FCGWaypointsContainer> selected = new List<FCGWaypointsContainer> { seed };
            List<FCGWaypointsContainer> remaining = roads.Where(road => road != seed).ToList();

            while (selected.Count < count && remaining.Count > 0)
            {
                FCGWaypointsContainer next = remaining
                    .OrderByDescending(candidate => selected.Min(chosen => Vector3.Distance(candidate.transform.position, chosen.transform.position)))
                    .First();

                selected.Add(next);
                remaining.Remove(next);
            }

            return selected;
        }

        private static Pose CreateRoadPose(FCGWaypointsContainer road, float heightOffset, int nodeIndex)
        {
            int clampedIndex = Mathf.Clamp(nodeIndex, 0, Mathf.Max(0, road.transform.childCount - 2));
            Vector3 position = road.Node(1, clampedIndex) + Vector3.up * heightOffset;
            Quaternion rotation = road.NodeRotation(1, clampedIndex);
            return new Pose(position, rotation);
        }

        private static Pose OffsetPose(Pose pose, Vector3 localOffset)
        {
            return new Pose(pose.position + (pose.rotation * localOffset), pose.rotation);
        }

        private static GameObject RecreateSceneObject(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            return new GameObject(name);
        }

        private static GameObject GetOrCreateSceneObject(string name)
        {
            GameObject existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.GetChild(i).gameObject);
            }
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private readonly struct WorldAnchorSet
        {
            public static WorldAnchorSet Fallback => FromPreferredStart(
                new Pose(new Vector3(0f, 1.2f, -20f), Quaternion.identity));

            public static WorldAnchorSet FromPreferredStart(Pose startPose)
            {
                return new WorldAnchorSet(
                startPose,
                startPose,
                startPose,
                new Pose(new Vector3(8f, 0.2f, 48f), Quaternion.identity),
                new Pose(new Vector3(22f, 0.2f, 94f), Quaternion.identity),
                new Pose(new Vector3(-18f, 0.2f, 94f), Quaternion.identity));
            }

            public WorldAnchorSet(Pose playerSpawnPose, Pose garagePose, Pose respawnPose, Pose dayRacePose, Pose nightRacePose, Pose wagerRacePose)
            {
                this.playerSpawnPose = playerSpawnPose;
                this.garagePose = garagePose;
                this.respawnPose = respawnPose;
                this.dayRacePose = dayRacePose;
                this.nightRacePose = nightRacePose;
                this.wagerRacePose = wagerRacePose;
            }

            public readonly Pose playerSpawnPose;
            public readonly Pose garagePose;
            public readonly Pose respawnPose;
            public readonly Pose dayRacePose;
            public readonly Pose nightRacePose;
            public readonly Pose wagerRacePose;
        }
    }
}
