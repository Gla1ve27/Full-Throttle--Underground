using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Vehicle;
using FullThrottle.SacredCore.Vehicle;
using FullThrottle.SacredCore.Audio;
using FullThrottle.SacredCore.World;
using FullThrottle.SacredCore.EditorTools;

namespace Underground.EditorTools
{
    public static class PlayerCarUpgradeRepairTool
    {
        private static readonly Vector3 DefaultFrontLeft = new Vector3(-0.85f, 0.2f, 1.38f);
        private static readonly Vector3 DefaultFrontRight = new Vector3(0.85f, 0.2f, 1.38f);
        private static readonly Vector3 DefaultRearLeft = new Vector3(-0.85f, 0.2f, -1.35f);
        private static readonly Vector3 DefaultRearRight = new Vector3(0.85f, 0.2f, -1.35f);

        [MenuItem("Full Throttle/Repair/Repair PlayerCar In Open Scene", priority = 5)]
        public static void RepairPlayerCarInOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Repair PlayerCar", "No active scene is open.", "OK");
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            int repaired = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                repaired += RepairPlayerCarsRecursive(roots[i].transform);
            }

            if (repaired > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
            }

            EditorUtility.DisplayDialog("Repair PlayerCar", $"Repaired {repaired} PlayerCar object(s) in the open scene.", "OK");
        }

        [MenuItem("Full Throttle/Repair/Rebuild Managed Scenes With Fresh PlayerCar", priority = 6)]
        public static void RebuildManagedScenesWithFreshPlayerCar()
        {
            UndergroundPrototypeBuilder.RebuildMainMenuSceneFromTopMenu();
            UndergroundPrototypeBuilder.RebuildGarageSceneFromTopMenu();
            UndergroundPrototypeBuilder.RebuildWorldScene();
            EditorUtility.DisplayDialog("Repair PlayerCar", "Rebuilt Main Menu, Garage, and World using a freshly generated PlayerCar prefab.", "OK");
        }

        private static int RepairPlayerCarsRecursive(Transform root)
        {
            int count = 0;
            if (root == null)
            {
                return 0;
            }

            if (LooksLikePlayerCar(root.gameObject))
            {
                RepairSinglePlayerCar(root.gameObject);
                count++;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                count += RepairPlayerCarsRecursive(root.GetChild(i));
            }

            return count;
        }

        private static bool LooksLikePlayerCar(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            if (go.name == "PlayerCar" || go.CompareTag("Player"))
            {
                return true;
            }

            return go.GetComponent<FTVehicleController>() != null
                || go.GetComponent<FTPlayerVehicleBinder>() != null;
        }

        private static void RepairSinglePlayerCar(GameObject car)
        {
            Undo.RegisterFullObjectHierarchyUndo(car, "Repair PlayerCar");
            RemoveMissingScriptsRecursive(car);

            Rigidbody rb = EnsureComponent<Rigidbody>(car);
            BoxCollider bodyCollider = EnsureComponent<BoxCollider>(car);
            bodyCollider.center = new Vector3(0f, 0.45f, 0f);
            bodyCollider.size = new Vector3(1.9f, 0.9f, 4.2f);

            rb.mass = Mathf.Max(1480f, rb.mass);
            rb.linearDamping = 0.12f;
            rb.angularDamping = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Missing scripts are cleaned up by RemoveMissingScriptsRecursive

            Transform audioRoot = car.transform.Find("AudioRoot");
            if (audioRoot != null)
            {
                audioRoot.name = "FTVehicleAudio";
            }
            else
            {
                audioRoot = EnsureChild(car.transform, "FTVehicleAudio", Vector3.zero);
            }

            // FT Components
            EnsureComponent<FTPlayerVehicleBinder>(car);
            EnsureComponent<FTDriverInput>(car);
            EnsureComponent<FTVehicleTelemetry>(car);
            FTVehicleController controller = EnsureComponent<FTVehicleController>(car);
            EnsureComponent<VehicleNightLightingController>(car);
            EnsureComponent<FTRespawnDirector>(car);
            
            EnsureComponent<FTVehicleAudioDirector>(audioRoot.gameObject);
            EnsureComponent<FTEngineAudioFeed>(audioRoot.gameObject);
            EnsureComponent<FTEngineLoopMixer>(audioRoot.gameObject);
            EnsureComponent<FTShiftAudioDirector>(audioRoot.gameObject);
            EnsureComponent<FTTurboAudioDirector>(audioRoot.gameObject);
            EnsureComponent<FTSweetenerAudioDirector>(audioRoot.gameObject);
            EnsureComponent<FTSurfaceAudioDirector>(audioRoot.gameObject);
            EnsureComponent<FTAudioMixerRouter>(audioRoot.gameObject);

            Transform modelRoot = EnsureChild(car.transform, "ModelRoot", Vector3.zero);
            Transform wheelCollidersRoot = EnsureChild(car.transform, "WheelColliders", Vector3.zero);
            EnsureChild(car.transform, "CameraTarget", new Vector3(0f, 1.08f, -0.02f));
            EnsureChild(car.transform, "SpawnPoint", Vector3.zero);

            WheelCollider fl = EnsureWheelCollider(wheelCollidersRoot, "FL_Collider", DefaultFrontLeft);
            WheelCollider fr = EnsureWheelCollider(wheelCollidersRoot, "FR_Collider", DefaultFrontRight);
            WheelCollider rl = EnsureWheelCollider(wheelCollidersRoot, "RL_Collider", DefaultRearLeft);
            WheelCollider rr = EnsureWheelCollider(wheelCollidersRoot, "RR_Collider", DefaultRearRight);

            DisableImportedPhysics(modelRoot, wheelCollidersRoot);

            SerializedObject controllerSo = new SerializedObject(controller);
            SerializedProperty wheels = controllerSo.FindProperty("wheels");
            wheels.arraySize = 4;
            
            Transform flVis = FindVisualRoot(modelRoot, "FL_Visual", fl.transform.localPosition);
            Transform frVis = FindVisualRoot(modelRoot, "FR_Visual", fr.transform.localPosition);
            Transform rlVis = FindVisualRoot(modelRoot, "RL_Visual", rl.transform.localPosition);
            Transform rrVis = FindVisualRoot(modelRoot, "RR_Visual", rr.transform.localPosition);

            ConfigureWheelSetFT(wheels.GetArrayElementAtIndex(0), fl, flVis, steer: true, motor: true, brake: true, rear: false);
            ConfigureWheelSetFT(wheels.GetArrayElementAtIndex(1), fr, frVis, steer: true, motor: true, brake: true, rear: false);
            ConfigureWheelSetFT(wheels.GetArrayElementAtIndex(2), rl, rlVis, steer: false, motor: true, brake: true, rear: true);
            ConfigureWheelSetFT(wheels.GetArrayElementAtIndex(3), rr, rrVis, steer: false, motor: true, brake: true, rear: true);
            
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            SceneView.RepaintAll();
            EditorUtility.SetDirty(car);
        }

        private static void ConfigureWheelSetFT(SerializedProperty element, WheelCollider collider, Transform visual, bool steer, bool motor, bool brake, bool rear)
        {
            element.FindPropertyRelative("wheel").objectReferenceValue = collider;
            element.FindPropertyRelative("visual").objectReferenceValue = visual;
            element.FindPropertyRelative("steer").boolValue = steer;
            element.FindPropertyRelative("motor").boolValue = motor;
            element.FindPropertyRelative("brake").boolValue = brake;
            element.FindPropertyRelative("rear").boolValue = rear;
        }

        private static void RemoveComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp != null) Object.DestroyImmediate(comp, true);
        }

        private static Transform FindVisualRoot(Transform modelRoot, string name, Vector3 fallbackLocalPosition)
        {
            Transform existing = modelRoot.Find(name);
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(modelRoot, false);
            go.transform.localPosition = fallbackLocalPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static WheelCollider EnsureWheelCollider(Transform parent, string name, Vector3 localPosition)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;

            WheelCollider wc = EnsureComponent<WheelCollider>(go);
            wc.radius = Mathf.Clamp(wc.radius > 0.05f ? wc.radius : 0.34f, 0.2f, 0.5f);
            wc.suspensionDistance = Mathf.Max(0.15f, wc.suspensionDistance);
            JointSpring spring = wc.suspensionSpring;
            spring.spring = Mathf.Max(30000f, spring.spring);
            spring.damper = Mathf.Max(3900f, spring.damper);
            spring.targetPosition = 0.45f;
            wc.suspensionSpring = spring;
            return wc;
        }

        private static void DisableImportedPhysics(Transform modelRoot, Transform wheelCollidersRoot)
        {
            if (modelRoot == null)
            {
                return;
            }

            Component[] components = modelRoot.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.transform.IsChildOf(wheelCollidersRoot))
                {
                    continue;
                }

                if (component is Collider col)
                {
                    col.enabled = false;
                }
                else if (component is Rigidbody body)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                    body.detectCollisions = false;
                }
            }
        }

        private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            return Undo.AddComponent<T>(go);
        }

        private static void RemoveMissingScriptsRecursive(GameObject go)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                RemoveMissingScriptsRecursive(go.transform.GetChild(i).gameObject);
            }
        }
    }
}
