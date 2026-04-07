using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Underground.Core;
using Underground.Progression;
using Underground.Save;
using Underground.Session;
using Underground.TimeSystem;
using Underground.UI;
using Underground.Vehicle;
using Underground.World;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        [MenuItem("Underground/Prefabs/Rebuild Generated Scene Prefabs", priority = 90)]
        public static void RebuildGeneratedScenePrefabs()
        {
            EnsureProjectFolders();
            ConfigureProjectSettings();
            ConfigureTagsAndLayers();
            CreateSceneSupportPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateSceneSupportPrefabs()
        {
            CreateOrUpdateRuntimeRootPrefab();
            CreateOrUpdateWorldSystemsPrefab();
            CreateOrUpdateFollowCameraPrefab();
            CreateOrUpdateHudPrefab();
        }

        private static GameObject CreateOrUpdateRuntimeRootPrefab()
        {
            GameObject root = new GameObject("RuntimeRoot");
            root.AddComponent<PersistentRuntimeRoot>();
            root.AddComponent<SaveSystem>();
            root.AddComponent<PersistentProgressManager>();
            root.AddComponent<RiskSystem>();
            root.AddComponent<SessionManager>();
            root.AddComponent<VehicleOwnershipSystem>();

            GameSettingsManager settingsManager = root.AddComponent<GameSettingsManager>();
            SetObjectReference(settingsManager, "audioMixer", LoadSlimUiMixer());

            PrefabUtility.SaveAsPrefabAsset(root, RuntimeRootPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRootPrefabPath);
        }

        private static GameObject CreateOrUpdateWorldSystemsPrefab()
        {
            GameObject worldSystems = new GameObject("WorldSystems");
            Transform sunPivot = CreateEmptyChild(worldSystems.transform, "SunPivot", Vector3.zero);

            GameObject sunObject = new GameObject("Directional Light");
            sunObject.transform.SetParent(sunPivot, false);
            sunObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light sunLight = sunObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.15f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 1f;
            sunLight.shadowBias = 0.06f;
            sunLight.shadowNormalBias = 0.65f;

            DayNightCycleController dayNight = worldSystems.AddComponent<DayNightCycleController>();
            SetObjectReference(dayNight, "sunPivot", sunPivot);
            SetObjectReference(dayNight, "sunLight", sunLight);
            SetObjectReference(dayNight, "daySkyboxMaterial", LoadDaySkyboxMaterial());
            SetObjectReference(dayNight, "nightSkyboxMaterial", LoadNightSkyboxMaterial());

            GameObject reflectionProbeObject = new GameObject("WorldReflectionProbe");
            reflectionProbeObject.transform.SetParent(worldSystems.transform, false);
            ReflectionProbe reflectionProbe = reflectionProbeObject.AddComponent<ReflectionProbe>();
            reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            reflectionProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            reflectionProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            reflectionProbe.size = new Vector3(600f, 180f, 600f);
            reflectionProbe.center = new Vector3(0f, 35f, 0f);
            reflectionProbe.boxProjection = true;
            reflectionProbe.importance = 1;
            reflectionProbe.intensity = 1.15f;

            AttachGlobalVolume(worldSystems.transform, "GlobalVolume");

            PrefabUtility.SaveAsPrefabAsset(worldSystems, WorldSystemsPrefabPath);
            Object.DestroyImmediate(worldSystems);
            return AssetDatabase.LoadAssetAtPath<GameObject>(WorldSystemsPrefabPath);
        }

        private static GameObject CreateOrUpdateFollowCameraPrefab()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3f, -7f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.renderingPath = RenderingPath.UsePlayerSettings;
            EnablePostProcessing(camera);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<VehicleCameraFollow>();
            cameraObject.AddComponent<VehicleSpeedEffectsController>();

            PrefabUtility.SaveAsPrefabAsset(cameraObject, FollowCameraPrefabPath);
            Object.DestroyImmediate(cameraObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(FollowCameraPrefabPath);
        }

        private static GameObject CreateOrUpdateHudPrefab()
        {
            Canvas canvas = CreateCanvas("HUD");
            StylizedHudComposer composer = canvas.gameObject.AddComponent<StylizedHudComposer>();
            composer.Compose();

            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, HudPrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        }
    }
}
