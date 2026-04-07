using TMPro;
using SlimUI.ModernMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Underground.Core;
using Underground.Garage;
using Underground.Progression;
using Underground.Save;
using Underground.Session;
using Underground.UI;
using Underground.Vehicle;
using Underground.World;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private static void CreateBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRuntimeRoot(true);
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void CreateMainMenuScene(GameObject playerCarPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SlimUiDemoScenePath) != null)
            {
                CreateMainMenuSceneFromSlimUiDemo(playerCarPrefab);
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRuntimeRoot(false);
            EnsureEventSystem();
            ComposeGarageBackdropForMenu(playerCarPrefab);

            MainMenuController menu = new GameObject("MainMenuController").AddComponent<MainMenuController>();
            GameObject slimUiPrefab = LoadSlimUiCanvasTemplate();
            if (slimUiPrefab != null)
            {
                GameObject menuRoot = (GameObject)PrefabUtility.InstantiatePrefab(slimUiPrefab);
                menuRoot.name = "Main_Menu";
                menuRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                menuRoot.transform.localScale = Vector3.one;
                SlimUiMainMenuBinder binder = menuRoot.AddComponent<SlimUiMainMenuBinder>();
                SetObjectReference(binder, "menuController", menu);
            }
            else
            {
                Canvas canvas = CreateCanvas("MainMenuCanvas");
                EnsureEventSystem();
                CreateTitle(canvas.transform, "FULL THROTTLE UNDERGROUND", new Vector2(0f, 160f), 54f);
                CreateButton(canvas.transform, "Continue", new Vector2(0f, 20f), menu.ContinueGame);
                CreateButton(canvas.transform, "New Game", new Vector2(0f, -60f), menu.StartNewGame);
                CreateButton(canvas.transform, "Quit", new Vector2(0f, -140f), menu.QuitGame);
            }

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateMainMenuSceneFromSlimUiDemo(GameObject playerCarPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null)
            {
                AssetDatabase.DeleteAsset(MainMenuScenePath);
            }

            AssetDatabase.CopyAsset(SlimUiDemoScenePath, MainMenuScenePath);
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            RemoveMissingScriptsFromScene(scene);

            CreateRuntimeRoot(false);
            EnsureEventSystem();
            ComposeGarageBackdropForMenu(playerCarPrefab);

            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            if (menu == null)
            {
                menu = new GameObject("MainMenuController").AddComponent<MainMenuController>();
            }

            SlimUiMainMenuBinder binder = Object.FindFirstObjectByType<SlimUiMainMenuBinder>(FindObjectsInactive.Include);
            if (binder == null)
            {
                GameObject slimUiRoot = FindSlimUiMenuRoot();
                if (slimUiRoot != null)
                {
                    binder = slimUiRoot.GetComponent<SlimUiMainMenuBinder>();
                    if (binder == null)
                    {
                        binder = slimUiRoot.AddComponent<SlimUiMainMenuBinder>();
                    }
                }
            }

            if (binder != null)
            {
                SetObjectReference(binder, "menuController", menu);
            }

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateGarageScene(GameObject playerCarPrefab, UpgradeDefinition engineUpgrade)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRuntimeRoot(false);
            EnsureEventSystem();
            ComposeGarageShowroomScene(playerCarPrefab, engineUpgrade);
            EditorSceneManager.SaveScene(scene, GarageScenePath);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GarageScenePath, true),
                new EditorBuildSettingsScene(WorldScenePath, true),
                new EditorBuildSettingsScene(VehicleTestScenePath, true)
            };
        }

        private static void CreateRuntimeRoot(bool includeBootstrapLoader)
        {
            GameObject runtimeRootPrefab = CreateOrUpdateRuntimeRootPrefab();
            GameObject runtimeRoot = runtimeRootPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(runtimeRootPrefab)
                : new GameObject("RuntimeRoot");

            runtimeRoot.name = "RuntimeRoot";
            if (includeBootstrapLoader)
            {
                runtimeRoot.AddComponent<BootstrapSceneLoader>();
            }
        }

        private static void CreateDirectionalLight(Transform parent = null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            if (parent != null)
            {
                lightObject.transform.SetParent(parent, false);
            }

            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static void CreateMenuCamera()
        {
            CreateStaticCamera(new Vector3(0f, 3f, -9f), Quaternion.Euler(14f, 0f, 0f));
        }

        private static GameObject FindSlimUiMenuRoot()
        {
            UIMenuManager slimMenu = Object.FindFirstObjectByType<UIMenuManager>(FindObjectsInactive.Include);
            if (slimMenu != null)
            {
                return slimMenu.gameObject;
            }

            GameObject namedRoot = GameObject.Find("Main_Menu");
            return namedRoot;
        }

        private static void RemoveMissingScriptsFromScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                RemoveMissingScriptsRecursive(roots[i]);
            }
        }

        private static void RemoveMissingScriptsRecursive(GameObject gameObject)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                RemoveMissingScriptsRecursive(transform.GetChild(i).gameObject);
            }
        }

        private static void CreateStaticCamera(Vector3 position, Quaternion rotation)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(position, rotation);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateGarageShell()
        {
            CreateGround("GarageFloor", new Vector3(12f, 1f, 12f));
            CreateObstacle(new Vector3(0f, 2.5f, 12f), new Vector3(24f, 5f, 0.5f));
            CreateObstacle(new Vector3(0f, 2.5f, -12f), new Vector3(24f, 5f, 0.5f));
            CreateObstacle(new Vector3(12f, 2.5f, 0f), new Vector3(0.5f, 5f, 24f));
            CreateObstacle(new Vector3(-12f, 2.5f, 0f), new Vector3(0.5f, 5f, 24f));
        }
    }
}
