using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Underground.AI;
using Underground.Race;
using Underground.TimeSystem;
using Underground.UI;
using Underground.Vehicle;
using Underground.World;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private static void CreateWorldScene(GameObject playerCarPrefab, RaceDefinition dayRace, RaceDefinition nightRace, RaceDefinition wagerRace)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ComposeWorldScene(scene, playerCarPrefab, dayRace, nightRace, wagerRace);
            EditorSceneManager.SaveScene(scene, WorldScenePath);
        }

        private static void CreateVehicleTestScene(GameObject playerCarPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRuntimeRoot(false);
            GameObject systemsRoot = new GameObject("TestSceneSystems");
            EnsureWorldSystems(systemsRoot.transform);
            RenderSettings.skybox = LoadDaySkyboxMaterial();
            CreateGround("TestGround", new Vector3(50f, 1f, 50f));
            CreateTestLayout();
            GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(playerCarPrefab);
            car.transform.position = new Vector3(0f, 0.65f, -20f);
            CreateFollowCamera(car);
            CreateHudCanvas();
            EditorSceneManager.SaveScene(scene, VehicleTestScenePath);
        }

        private static void CreateGround(string name, Vector3 scale)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = name;
            ground.transform.localScale = scale;
            SetLayerRecursively(ground, LayerMask.NameToLayer("WorldStatic"));
        }

        private static void CreateFollowCamera(GameObject playerCar)
        {
            GameObject followCameraPrefab = CreateOrUpdateFollowCameraPrefab();
            GameObject cameraObject = followCameraPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(followCameraPrefab)
                : new GameObject("Main Camera");
            cameraObject.name = "Main Camera";
            cameraObject.transform.position = new Vector3(0f, 3f, -7f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            VehicleCameraFollow follow = cameraObject.GetComponent<VehicleCameraFollow>();
            if (follow == null)
            {
                follow = cameraObject.AddComponent<VehicleCameraFollow>();
            }
            SetObjectReference(follow, "targetVehicle", playerCar.GetComponent<VehicleDynamicsController>());
            SetObjectReference(follow, "target", playerCar.transform.Find("CameraTarget"));
            SetObjectReference(follow, "targetBody", playerCar.GetComponent<Rigidbody>());
        }

        private static void CreateTrackProps()
        {
            for (int i = 0; i < 6; i++)
            {
                CreateObstacle(new Vector3(-6f + (i * 2.5f), 0.75f, 18f + (i * 8f)), new Vector3(1f, 1.5f, 1f));
            }
        }

        private static void CreateTestLayout()
        {
            for (int i = 0; i < 10; i++)
            {
                CreateObstacle(new Vector3((i % 2 == 0 ? -6f : 6f), 0.75f, -4f + (i * 10f)), new Vector3(1f, 1.5f, 1f));
            }

            CreateObstacle(new Vector3(0f, 0.4f, 60f), new Vector3(14f, 0.8f, 5f));
            CreateObstacle(new Vector3(10f, 0.4f, 90f), new Vector3(8f, 0.8f, 8f));
            CreateObstacle(new Vector3(-10f, 0.4f, 120f), new Vector3(8f, 0.8f, 8f));
        }

        private static void CreateGarageEntrance()
        {
            GameObject entrance = new GameObject("GarageEntrance");
            entrance.tag = "Garage";
            BoxCollider collider = entrance.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(10f, 5f, 6f);
            entrance.transform.position = new Vector3(0f, 2f, -25f);
            entrance.AddComponent<GarageEntranceTrigger>();
        }

        private static void CreateRespawnCheckpoint(Vector3 position)
        {
            GameObject checkpoint = new GameObject("RespawnCheckpoint");
            BoxCollider collider = checkpoint.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(12f, 4f, 8f);
            checkpoint.transform.position = position;
            checkpoint.AddComponent<RespawnPointTrigger>();
        }

        private static void CreateRaceStart(Vector3 position, RaceDefinition definition, string name)
        {
            GameObject start = new GameObject(name);
            start.tag = "RaceStart";
            BoxCollider collider = start.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(10f, 3f, 4f);
            start.transform.position = position;
            RaceManager raceManager = start.AddComponent<RaceManager>();
            SetObjectReference(raceManager, "activeRace", definition);
            start.AddComponent<RaceStartTrigger>();
        }

        private static void CreateTrafficLoop()
        {
            Transform[] waypoints = new Transform[4];
            Vector3[] positions =
            {
                new Vector3(20f, 0.5f, -10f), new Vector3(30f, 0.5f, 20f), new Vector3(20f, 0.5f, 55f), new Vector3(8f, 0.5f, 18f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject waypoint = new GameObject($"TrafficWaypoint_{i + 1}");
                waypoint.transform.position = positions[i];
                waypoints[i] = waypoint.transform;
            }

            CreateTrafficVehicle("TrafficTaxi", LoadTaxiTrafficPrefab(), positions[0], waypoints);
            CreateTrafficVehicle("TrafficPolice", LoadPoliceTrafficPrefab(), positions[2], waypoints);
        }

        private static void CreateTrafficVehicle(string name, GameObject prefab, Vector3 position, Transform[] waypoints)
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

            trafficCar.tag = "Traffic";
            trafficCar.transform.position = position;
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
        }

        private static void CreatePoliceSetpiece()
        {
            GameObject policePrefab = LoadPoliceTrafficPrefab();
            if (policePrefab == null)
            {
                return;
            }

            GameObject policeCar = (GameObject)PrefabUtility.InstantiatePrefab(policePrefab);
            policeCar.name = "PoliceInterceptor_Parked";
            policeCar.transform.position = new Vector3(-22f, 0f, 42f);
            policeCar.transform.rotation = Quaternion.Euler(0f, 38f, 0f);
            StripRuntimeComponents(policeCar);
            SetLayerRecursively(policeCar, LayerMask.NameToLayer("Traffic"));
        }

        private static void CreateHudCanvas()
        {
            GameObject hudPrefab = CreateOrUpdateHudPrefab();
            GameObject hudObject = hudPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab)
                : CreateCanvas("HUD").gameObject;
            hudObject.name = "HUD";
            StylizedHudComposer composer = hudObject.GetComponent<StylizedHudComposer>();
            if (composer == null)
            {
                composer = hudObject.AddComponent<StylizedHudComposer>();
            }

            composer.Compose();
        }

        private static void CreateObstacle(Vector3 position, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            SetLayerRecursively(cube, LayerMask.NameToLayer("WorldStatic"));
        }
    }
}
