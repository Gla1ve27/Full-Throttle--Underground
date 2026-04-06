using System.Collections.Generic;
using Underground.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underground.EditorTools
{
    public static class Phase1SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Phase1_VehicleTest.unity";
        private const string PrefabPath = "Assets/Prefabs/PlayerCar.prefab";

        private struct WheelBuildResult
        {
            public WheelCollider collider;
            public Transform visual;
        }

        [MenuItem("Underground/Phase 1/Create Vehicle Test Scene")]
        public static void CreateVehicleTestScene()
        {
            EnsureProjectFolders();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateDirectionalLight();
            CreateGround();

            GameObject playerCar = CreatePlayerCar();
            CreateChaseCamera(playerCar);

            PrefabUtility.SaveAsPrefabAssetAndConnect(playerCar, PrefabPath, InteractionMode.AutomatedAction);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeGameObject = playerCar;
            EditorGUIUtility.PingObject(playerCar);

            EditorUtility.DisplayDialog(
                "Phase 1 Ready",
                "Created Assets/Scenes/Phase1_VehicleTest.unity and wired the player car prefab for Phase 1.",
                "OK");
        }

        private static void EnsureProjectFolders()
        {
            string[] folders =
            {
                "Assets/Art",
                "Assets/Prefabs",
                "Assets/Scenes",
                "Assets/Scripts",
                "Assets/Scripts/Core",
                "Assets/Scripts/Vehicle",
                "Assets/Scripts/AI",
                "Assets/Scripts/World",
                "Assets/Scripts/Race",
                "Assets/Scripts/Progression",
                "Assets/Scripts/UI",
                "Assets/Scripts/Save",
                "Assets/Scripts/Systems",
                "Assets/ScriptableObjects",
                "Assets/Settings"
            };

            for (int i = 0; i < folders.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(folders[i]))
                {
                    string parent = System.IO.Path.GetDirectoryName(folders[i]).Replace("\\", "/");
                    string folderName = System.IO.Path.GetFileName(folders[i]);
                    AssetDatabase.CreateFolder(parent, folderName);
                }
            }
        }

        private static void CreateDirectionalLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(25f, 1f, 25f);
        }

        private static GameObject CreatePlayerCar()
        {
            GameObject carRoot = new GameObject("PlayerCar");
            carRoot.transform.position = new Vector3(0f, 0.65f, 0f);

            Rigidbody rigidbody = carRoot.AddComponent<Rigidbody>();
            rigidbody.mass = 1350f;
            rigidbody.drag = 0.02f;
            rigidbody.angularDrag = 0.5f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BoxCollider bodyCollider = carRoot.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0f, 0.45f, 0f);
            bodyCollider.size = new Vector3(1.9f, 0.9f, 4.2f);

            VehicleInput vehicleInput = carRoot.AddComponent<VehicleInput>();
            VehicleDynamicsController vehicleController = carRoot.AddComponent<VehicleDynamicsController>();

            Transform centerOfMass = CreateEmptyChild(carRoot.transform, "CenterOfMass", new Vector3(0f, -0.3f, 0.15f));
            CreateEmptyChild(carRoot.transform, "CameraTarget", new Vector3(0f, 1.25f, -0.15f));

            CreateBodyVisuals(carRoot.transform);

            WheelBuildResult frontLeft = CreateWheel(carRoot.transform, "Wheel_FL", new Vector3(-0.85f, 0.2f, 1.38f));
            WheelBuildResult frontRight = CreateWheel(carRoot.transform, "Wheel_FR", new Vector3(0.85f, 0.2f, 1.38f));
            WheelBuildResult rearLeft = CreateWheel(carRoot.transform, "Wheel_RL", new Vector3(-0.85f, 0.2f, -1.35f));
            WheelBuildResult rearRight = CreateWheel(carRoot.transform, "Wheel_RR", new Vector3(0.85f, 0.2f, -1.35f));

            vehicleController.inputSource = vehicleInput;
            vehicleController.centerOfMassOverride = centerOfMass;
            vehicleController.maxMotorTorque = 2400f;
            vehicleController.maxBrakeTorque = 3600f;
            vehicleController.maxHandbrakeTorque = 6000f;
            vehicleController.topSpeedKph = 180f;
            vehicleController.maxSteerAngle = 32f;
            vehicleController.steeringSpeedReferenceKph = 160f;
            vehicleController.downforce = 90f;
            vehicleController.lateralGripAssist = 2.5f;
            vehicleController.antiRollForce = 4500f;
            vehicleController.resetLift = 1.2f;
            vehicleController.axles = new List<VehicleDynamicsController.WheelAxle>
            {
                new VehicleDynamicsController.WheelAxle
                {
                    label = "Front Axle",
                    leftWheel = frontLeft.collider,
                    rightWheel = frontRight.collider,
                    leftVisual = frontLeft.visual,
                    rightVisual = frontRight.visual,
                    steering = true,
                    powered = true,
                    handbrake = false
                },
                new VehicleDynamicsController.WheelAxle
                {
                    label = "Rear Axle",
                    leftWheel = rearLeft.collider,
                    rightWheel = rearRight.collider,
                    leftVisual = rearLeft.visual,
                    rightVisual = rearRight.visual,
                    steering = false,
                    powered = true,
                    handbrake = true
                }
            };

            return carRoot;
        }

        private static void CreateBodyVisuals(Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(parent);
            body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(1.8f, 0.55f, 4f);
            RemoveCollider(body);

            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cabin";
            cabin.transform.SetParent(parent);
            cabin.transform.localPosition = new Vector3(0f, 0.85f, -0.15f);
            cabin.transform.localRotation = Quaternion.identity;
            cabin.transform.localScale = new Vector3(1.35f, 0.5f, 1.8f);
            RemoveCollider(cabin);
        }

        private static WheelBuildResult CreateWheel(Transform parent, string name, Vector3 localPosition)
        {
            GameObject wheelRoot = new GameObject(name);
            wheelRoot.transform.SetParent(parent);
            wheelRoot.transform.localPosition = localPosition;
            wheelRoot.transform.localRotation = Quaternion.identity;

            WheelCollider wheelCollider = wheelRoot.AddComponent<WheelCollider>();
            wheelCollider.radius = 0.34f;
            wheelCollider.mass = 25f;
            wheelCollider.wheelDampingRate = 1f;
            wheelCollider.forceAppPointDistance = 0.1f;
            wheelCollider.suspensionDistance = 0.2f;

            JointSpring suspensionSpring = wheelCollider.suspensionSpring;
            suspensionSpring.spring = 35000f;
            suspensionSpring.damper = 4500f;
            suspensionSpring.targetPosition = 0.45f;
            wheelCollider.suspensionSpring = suspensionSpring;

            WheelFrictionCurve forwardFriction = wheelCollider.forwardFriction;
            forwardFriction.extremumSlip = 0.4f;
            forwardFriction.extremumValue = 1.3f;
            forwardFriction.asymptoteSlip = 0.8f;
            forwardFriction.asymptoteValue = 0.95f;
            forwardFriction.stiffness = 1.55f;
            wheelCollider.forwardFriction = forwardFriction;

            WheelFrictionCurve sidewaysFriction = wheelCollider.sidewaysFriction;
            sidewaysFriction.extremumSlip = 0.22f;
            sidewaysFriction.extremumValue = 1.2f;
            sidewaysFriction.asymptoteSlip = 0.5f;
            sidewaysFriction.asymptoteValue = 0.9f;
            sidewaysFriction.stiffness = 1.9f;
            wheelCollider.sidewaysFriction = sidewaysFriction;

            GameObject wheelVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheelVisual.name = name + "_Visual";
            wheelVisual.transform.SetParent(parent);
            wheelVisual.transform.localPosition = localPosition;
            wheelVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheelVisual.transform.localScale = new Vector3(0.68f, 0.12f, 0.68f);
            RemoveCollider(wheelVisual);

            return new WheelBuildResult
            {
                collider = wheelCollider,
                visual = wheelVisual.transform
            };
        }

        private static void CreateChaseCamera(GameObject playerCar)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3f, -7f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            Camera cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.fieldOfView = 60f;
            cameraObject.AddComponent<AudioListener>();

            VehicleCameraFollow cameraFollow = cameraObject.AddComponent<VehicleCameraFollow>();
            VehicleDynamicsController vehicleController = playerCar.GetComponent<VehicleDynamicsController>();

            cameraFollow.targetVehicle = vehicleController;
            cameraFollow.target = playerCar.transform.Find("CameraTarget");
            cameraFollow.targetBody = playerCar.GetComponent<Rigidbody>();
            cameraFollow.followDistance = 6.5f;
            cameraFollow.followHeight = 2.2f;
            cameraFollow.followSmoothTime = 0.12f;
            cameraFollow.rotationSharpness = 10f;
            cameraFollow.lookAheadDistance = 4f;
            cameraFollow.minFieldOfView = 60f;
            cameraFollow.maxFieldOfView = 78f;
            cameraFollow.speedForMaxFieldOfView = 180f;
        }

        private static Transform CreateEmptyChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            return child.transform;
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
