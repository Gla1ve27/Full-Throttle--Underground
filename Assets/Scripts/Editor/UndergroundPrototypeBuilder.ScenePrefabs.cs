using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Underground.Core;
using Underground.Progression;
using Underground.Save;
using Underground.UI;
using Underground.Vehicle;
using Underground.World;

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

        private static GameObject CreateOrUpdateRuntimeRootPrefab(bool preserveExistingAsset = false)
        {
            if (preserveExistingAsset && AssetExistsAtPath<GameObject>(RuntimeRootPrefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRootPrefabPath);
            }

            GameObject root = new GameObject("RuntimeRoot");
            root.AddComponent<PersistentRuntimeRoot>();
            root.AddComponent<SaveSystem>();
            root.AddComponent<PersistentProgressManager>();
            AddRuntimeSessionManager(root);
            root.AddComponent<VehicleOwnershipSystem>();

            GameSettingsManager settingsManager = root.AddComponent<GameSettingsManager>();
            SetObjectReference(settingsManager, "audioMixer", LoadSlimUiMixer());

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
            Transform sunPivot = CreateEmptyChild(worldSystems.transform, "SunPivot", Vector3.zero);
            Transform moonPivot = CreateEmptyChild(worldSystems.transform, "MoonPivot", Vector3.zero);

            GameObject sunObject = new GameObject("Directional Light");
            sunObject.transform.SetParent(sunPivot, false);
            Light sunLight = sunObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 0.34f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 0.88f;
            // Keep the sun shadow grounded on streets and curbs instead of letting it
            // "climb" vertical facades when HDRP uses the generated light in gameplay.
            sunLight.shadowBias = 0.04f;
            sunLight.shadowNormalBias = 0.2f;
            sunLight.color = new Color(1f, 0.95f, 0.9f);

            GameObject moonObject = new GameObject("Moon Light");
            moonObject.transform.SetParent(moonPivot, false);
            Light moonLight = moonObject.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.intensity = 0.05f;
            moonLight.color = new Color(0.5f, 0.6f, 1f);
            moonLight.shadows = LightShadows.None;

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

            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, HudPrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        }
    }
}
