using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Underground.Audio;
using Underground.Vehicle;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private static GameObject CreateOrUpdatePlayerCarPrefab(VehicleStatsData starterStats)
        {
            const float wheelRadius = 0.34f;
            Vector3 frontLeftWheelPosition = new Vector3(-0.85f, 0.2f, 1.38f);
            Vector3 frontRightWheelPosition = new Vector3(0.85f, 0.2f, 1.38f);
            Vector3 rearLeftWheelPosition = new Vector3(-0.85f, 0.2f, -1.35f);
            Vector3 rearRightWheelPosition = new Vector3(0.85f, 0.2f, -1.35f);

            GameObject carRoot = new GameObject("PlayerCar");
            carRoot.tag = "Player";
            SetLayerRecursively(carRoot, LayerMask.NameToLayer("PlayerVehicle"));
            carRoot.transform.position = new Vector3(0f, 0.65f, 0f);

            BoxCollider collider = carRoot.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(1.9f, 0.9f, 4.2f);
            carRoot.AddComponent<Rigidbody>();

            InputReader input = carRoot.AddComponent<VehicleInput>();
            EngineModel engine = carRoot.AddComponent<EngineModel>();
            GearboxSystem gearbox = carRoot.AddComponent<GearboxSystem>();
            VehicleDynamicsController controller = carRoot.AddComponent<VehicleDynamicsController>();
            carRoot.AddComponent<VehicleDamageSystem>();
            carRoot.AddComponent<VehicleNightLightingController>();
            CarRespawn respawn = carRoot.AddComponent<CarRespawn>();

            Transform modelRoot = CreateEmptyChild(carRoot.transform, "ModelRoot", Vector3.zero);
            Transform wheelColliderRoot = CreateEmptyChild(carRoot.transform, "WheelColliders", Vector3.zero);
            Transform centerOfMass = CreateEmptyChild(carRoot.transform, "CenterOfMass", new Vector3(0f, -0.3f, 0.1f));
            CreateEmptyChild(carRoot.transform, "CameraTarget", new Vector3(0f, 1.08f, -0.02f));
            Transform spawnPoint = CreateEmptyChild(carRoot.transform, "SpawnPoint", Vector3.zero);

            ImportedVehicleVisual importedVisual = AttachImportedPlayerVisual(
                modelRoot,
                frontLeftWheelPosition,
                frontRightWheelPosition,
                rearLeftWheelPosition,
                rearRightWheelPosition,
                wheelRadius);

            WheelBuild fl = CreateWheel(modelRoot, wheelColliderRoot, "FL", "Front", true, true, true, false, frontLeftWheelPosition, wheelRadius, importedVisual.frontLeftWheel);
            WheelBuild fr = CreateWheel(modelRoot, wheelColliderRoot, "FR", "Front", false, true, true, false, frontRightWheelPosition, wheelRadius, importedVisual.frontRightWheel);
            WheelBuild rl = CreateWheel(modelRoot, wheelColliderRoot, "RL", "Rear", true, false, false, true, rearLeftWheelPosition, wheelRadius, importedVisual.rearLeftWheel);
            WheelBuild rr = CreateWheel(modelRoot, wheelColliderRoot, "RR", "Rear", false, false, false, true, rearRightWheelPosition, wheelRadius, importedVisual.rearRightWheel);

            GameObject audioRoot = new GameObject("AudioRoot");
            audioRoot.transform.SetParent(carRoot.transform, false);
            AudioSource audioSource = audioRoot.AddComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            GameObject whineRoot = new GameObject("Whine");
            whineRoot.transform.SetParent(audioRoot.transform, false);
            AudioSource whineSource = whineRoot.AddComponent<AudioSource>();
            whineSource.loop = true;
            whineSource.playOnAwake = false;
            VehicleAudioController audioController = audioRoot.AddComponent<VehicleAudioController>();

            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("baseStats").objectReferenceValue = starterStats;
            controllerSo.FindProperty("input").objectReferenceValue = input;
            controllerSo.FindProperty("engineModel").objectReferenceValue = engine;
            controllerSo.FindProperty("gearbox").objectReferenceValue = gearbox;
            controllerSo.FindProperty("centerOfMassReference").objectReferenceValue = centerOfMass;
            SerializedProperty wheelsProperty = controllerSo.FindProperty("wheels");
            wheelsProperty.arraySize = 4;
            ApplyWheel(wheelsProperty.GetArrayElementAtIndex(0), fl);
            ApplyWheel(wheelsProperty.GetArrayElementAtIndex(1), fr);
            ApplyWheel(wheelsProperty.GetArrayElementAtIndex(2), rl);
            ApplyWheel(wheelsProperty.GetArrayElementAtIndex(3), rr);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject respawnSo = new SerializedObject(respawn);
            respawnSo.FindProperty("vehicle").objectReferenceValue = controller;
            respawnSo.FindProperty("input").objectReferenceValue = input;
            respawnSo.FindProperty("defaultRespawnPoint").objectReferenceValue = spawnPoint;
            respawnSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject audioSo = new SerializedObject(audioController);
            audioSo.FindProperty("gearbox").objectReferenceValue = gearbox;
            audioSo.FindProperty("vehicle").objectReferenceValue = controller;
            audioSo.FindProperty("engineSource").objectReferenceValue = audioSource;
            audioSo.FindProperty("whineSource").objectReferenceValue = whineSource;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(carRoot, PlayerCarPrefabPath);
            Object.DestroyImmediate(carRoot);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCarPrefabPath);
        }

        private static ImportedVehicleVisual AttachImportedPlayerVisual(
            Transform modelRoot,
            Vector3 frontLeftWheelPosition,
            Vector3 frontRightWheelPosition,
            Vector3 rearLeftWheelPosition,
            Vector3 rearRightWheelPosition,
            float targetWheelRadius)
        {
            GameObject visualPrefab = LoadPreferredPlayerVisualPrefab();
            if (visualPrefab == null)
            {
                CreateBodyVisuals(modelRoot);
                return default;
            }

            GameObject visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
            visualInstance.name = "ImportedVisual";
            visualInstance.transform.SetParent(modelRoot, false);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;
            StripRuntimeComponents(visualInstance);
            ConvertRendererMaterialsToUrp(visualInstance);

            Transform sourceFrontLeft = FindDeepChild(visualInstance.transform, "FL");
            Transform sourceFrontRight = FindDeepChild(visualInstance.transform, "FR");
            Transform sourceRearLeft = FindDeepChild(visualInstance.transform, "RL");
            Transform sourceRearRight = FindDeepChild(visualInstance.transform, "RR");

            AlignImportedVehicleBody(
                visualInstance.transform,
                sourceFrontLeft,
                sourceFrontRight,
                sourceRearLeft,
                sourceRearRight,
                frontLeftWheelPosition,
                frontRightWheelPosition,
                rearLeftWheelPosition,
                rearRightWheelPosition,
                targetWheelRadius);

            ImportedVehicleVisual build = new ImportedVehicleVisual
            {
                root = visualInstance,
                frontLeftWheel = CreateDetachedWheelVisual(modelRoot, "FL", frontLeftWheelPosition, targetWheelRadius, sourceFrontLeft),
                frontRightWheel = CreateDetachedWheelVisual(modelRoot, "FR", frontRightWheelPosition, targetWheelRadius, sourceFrontRight),
                rearLeftWheel = CreateDetachedWheelVisual(modelRoot, "RL", rearLeftWheelPosition, targetWheelRadius, sourceRearLeft),
                rearRightWheel = CreateDetachedWheelVisual(modelRoot, "RR", rearRightWheelPosition, targetWheelRadius, sourceRearRight)
            };

            DisableWheelRenderers(sourceFrontLeft);
            DisableWheelRenderers(sourceFrontRight);
            DisableWheelRenderers(sourceRearLeft);
            DisableWheelRenderers(sourceRearRight);

            return build;
        }

        private static void CreateBodyVisuals(Transform parent)
        {
            CreateVisual(parent, "Body", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.55f, 4f), Quaternion.identity);
            CreateVisual(parent, "Cabin", PrimitiveType.Cube, new Vector3(0f, 0.85f, -0.15f), new Vector3(1.35f, 0.5f, 1.8f), Quaternion.identity);
        }

        private static WheelBuild CreateWheel(Transform modelRoot, Transform colliderRoot, string suffix, string axleId, bool leftSide, bool steer, bool drive, bool handbrake, Vector3 localPosition, float wheelRadius, Transform importedWheelVisual)
        {
            GameObject colliderObject = new GameObject($"{suffix}_Collider");
            colliderObject.transform.SetParent(colliderRoot, false);
            colliderObject.transform.localPosition = localPosition;
            WheelCollider wheelCollider = colliderObject.AddComponent<WheelCollider>();
            wheelCollider.radius = wheelRadius;
            wheelCollider.mass = 25f;
            wheelCollider.suspensionDistance = 0.2f;

            Transform visualTransform = importedWheelVisual;
            if (visualTransform == null)
            {
                GameObject wheelVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelVisual.name = $"{suffix}_Mesh";
                wheelVisual.transform.SetParent(modelRoot, false);
                wheelVisual.transform.localPosition = localPosition;
                wheelVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelVisual.transform.localScale = new Vector3(0.68f, 0.12f, 0.68f);
                Object.DestroyImmediate(wheelVisual.GetComponent<Collider>());
                visualTransform = wheelVisual.transform;
            }

            return new WheelBuild
            {
                axleId = axleId,
                leftSide = leftSide,
                collider = wheelCollider,
                mesh = visualTransform,
                steer = steer,
                drive = drive,
                handbrake = handbrake
            };
        }

        private static void AlignImportedVehicleBody(
            Transform visualRoot,
            Transform frontLeftWheel,
            Transform frontRightWheel,
            Transform rearLeftWheel,
            Transform rearRightWheel,
            Vector3 frontLeftTarget,
            Vector3 frontRightTarget,
            Vector3 rearLeftTarget,
            Vector3 rearRightTarget,
            float targetWheelRadius)
        {
            FitImportedVehicleScale(
                visualRoot,
                frontLeftWheel,
                frontRightWheel,
                rearLeftWheel,
                rearRightWheel,
                frontLeftTarget,
                frontRightTarget,
                rearLeftTarget,
                rearRightTarget,
                targetWheelRadius);

            Transform[] wheelAnchors = { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
            Vector3[] targetPositions = { frontLeftTarget, frontRightTarget, rearLeftTarget, rearRightTarget };

            Vector3 importedCenter = Vector3.zero;
            Vector3 targetCenter = Vector3.zero;
            int count = 0;

            for (int i = 0; i < wheelAnchors.Length; i++)
            {
                if (wheelAnchors[i] == null)
                {
                    continue;
                }

                importedCenter += visualRoot.InverseTransformPoint(wheelAnchors[i].position);
                targetCenter += targetPositions[i];
                count++;
            }

            if (count == 0)
            {
                visualRoot.localPosition = new Vector3(0f, 0.08f, 0f);
                return;
            }

            visualRoot.localPosition = (targetCenter / count) - (importedCenter / count);
        }

        private static void FitImportedVehicleScale(
            Transform visualRoot,
            Transform frontLeftWheel,
            Transform frontRightWheel,
            Transform rearLeftWheel,
            Transform rearRightWheel,
            Vector3 frontLeftTarget,
            Vector3 frontRightTarget,
            Vector3 rearLeftTarget,
            Vector3 rearRightTarget,
            float targetWheelRadius)
        {
            if (visualRoot == null)
            {
                return;
            }

            bool hasFrontAxle = TryGetAxleCenterAndTrack(visualRoot, frontLeftWheel, frontRightWheel, out Vector3 importedFrontCenter, out float importedFrontTrack);
            bool hasRearAxle = TryGetAxleCenterAndTrack(visualRoot, rearLeftWheel, rearRightWheel, out Vector3 importedRearCenter, out float importedRearTrack);

            Vector3 targetFrontCenter = (frontLeftTarget + frontRightTarget) * 0.5f;
            Vector3 targetRearCenter = (rearLeftTarget + rearRightTarget) * 0.5f;
            float targetFrontTrack = Mathf.Abs(frontRightTarget.x - frontLeftTarget.x);
            float targetRearTrack = Mathf.Abs(rearRightTarget.x - rearLeftTarget.x);

            float trackScale = 1f;
            float wheelbaseScale = 1f;

            if (hasFrontAxle || hasRearAxle)
            {
                float importedTrack = 0f;
                float targetTrack = 0f;
                int trackCount = 0;

                if (hasFrontAxle && importedFrontTrack > 0.001f)
                {
                    importedTrack += importedFrontTrack;
                    targetTrack += targetFrontTrack;
                    trackCount++;
                }

                if (hasRearAxle && importedRearTrack > 0.001f)
                {
                    importedTrack += importedRearTrack;
                    targetTrack += targetRearTrack;
                    trackCount++;
                }

                if (trackCount > 0)
                {
                    trackScale = targetTrack / Mathf.Max(0.001f, importedTrack);
                }
            }

            if (hasFrontAxle && hasRearAxle)
            {
                float importedWheelbase = Mathf.Abs(importedFrontCenter.z - importedRearCenter.z);
                float targetWheelbase = Mathf.Abs(targetFrontCenter.z - targetRearCenter.z);
                if (importedWheelbase > 0.001f)
                {
                    wheelbaseScale = targetWheelbase / importedWheelbase;
                }
            }

            float wheelRadiusScale = 1f;
            if (TryGetAverageWheelRadius(frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel, out float importedWheelRadius) && importedWheelRadius > 0.001f)
            {
                wheelRadiusScale = Mathf.Clamp(targetWheelRadius / importedWheelRadius, 0.6f, 1.6f);
            }

            float verticalScale = Mathf.Clamp((trackScale + wheelbaseScale + wheelRadiusScale) / 3f, 0.7f, 1.4f);
            visualRoot.localScale = new Vector3(
                Mathf.Clamp(trackScale, 0.75f, 1.35f),
                verticalScale,
                Mathf.Clamp(wheelbaseScale, 0.75f, 1.35f));
        }

        private static bool TryGetAxleCenterAndTrack(Transform root, Transform leftWheel, Transform rightWheel, out Vector3 axleCenter, out float trackWidth)
        {
            axleCenter = Vector3.zero;
            trackWidth = 0f;

            if (root == null || leftWheel == null || rightWheel == null)
            {
                return false;
            }

            Vector3 leftLocal = root.InverseTransformPoint(leftWheel.position);
            Vector3 rightLocal = root.InverseTransformPoint(rightWheel.position);
            axleCenter = (leftLocal + rightLocal) * 0.5f;
            trackWidth = Mathf.Abs(rightLocal.x - leftLocal.x);
            return true;
        }

        private static bool TryGetAverageWheelRadius(Transform frontLeftWheel, Transform frontRightWheel, Transform rearLeftWheel, Transform rearRightWheel, out float averageRadius)
        {
            Transform[] wheels = { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
            float radiusSum = 0f;
            int count = 0;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || !TryGetCombinedRendererBounds(wheels[i], out Bounds bounds))
                {
                    continue;
                }

                radiusSum += Mathf.Max(bounds.extents.y, bounds.extents.z);
                count++;
            }

            averageRadius = count > 0 ? radiusSum / count : 0f;
            return count > 0;
        }

        private static Transform CreateDetachedWheelVisual(Transform modelRoot, string suffix, Vector3 localPosition, float targetWheelRadius, Transform sourceWheel)
        {
            GameObject wheelVisualRoot = new GameObject($"{suffix}_Visual");
            wheelVisualRoot.transform.SetParent(modelRoot, false);
            wheelVisualRoot.transform.localPosition = localPosition;
            wheelVisualRoot.transform.localRotation = Quaternion.identity;

            if (sourceWheel == null)
            {
                GameObject wheelVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelVisual.name = "Mesh";
                wheelVisual.transform.SetParent(wheelVisualRoot.transform, false);
                wheelVisual.transform.localPosition = Vector3.zero;
                wheelVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelVisual.transform.localScale = new Vector3(0.68f, 0.12f, 0.68f);
                Object.DestroyImmediate(wheelVisual.GetComponent<Collider>());
                return wheelVisualRoot.transform;
            }

            GameObject wheelVisualClone = Object.Instantiate(sourceWheel.gameObject, wheelVisualRoot.transform, false);
            wheelVisualClone.name = "Mesh";
            wheelVisualClone.transform.localPosition = Vector3.zero;
            StripRuntimeComponents(wheelVisualClone);
            NormalizeDetachedWheelVisual(wheelVisualRoot.transform, wheelVisualClone.transform, targetWheelRadius);
            return wheelVisualRoot.transform;
        }

        private static void NormalizeDetachedWheelVisual(Transform wheelRoot, Transform wheelClone, float targetWheelRadius)
        {
            if (wheelRoot == null || wheelClone == null)
            {
                return;
            }

            if (!TryGetCombinedRendererBounds(wheelClone, out Bounds bounds))
            {
                return;
            }

            Vector3 localCenter = wheelRoot.InverseTransformPoint(bounds.center);
            wheelClone.localPosition -= localCenter;

            float measuredRadius = Mathf.Max(bounds.extents.y, bounds.extents.z);
            if (measuredRadius > 0.001f)
            {
                float uniformScale = targetWheelRadius / measuredRadius;
                wheelClone.localScale *= uniformScale;
            }

            if (TryGetCombinedRendererBounds(wheelClone, out Bounds adjustedBounds))
            {
                Vector3 adjustedCenter = wheelRoot.InverseTransformPoint(adjustedBounds.center);
                wheelClone.localPosition -= adjustedCenter;
            }
        }

        private static bool TryGetCombinedRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return hasBounds;
        }

        private static void DisableWheelRenderers(Transform sourceWheel)
        {
            if (sourceWheel == null)
            {
                return;
            }

            Renderer[] renderers = sourceWheel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        private static void ConvertRendererMaterialsToUrp(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] sharedMaterials = renderers[i].sharedMaterials;
                bool changed = false;

                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material sourceMaterial = sharedMaterials[materialIndex];
                    Material convertedMaterial = GetOrCreateUrpMaterial(sourceMaterial);
                    if (convertedMaterial != null && convertedMaterial != sourceMaterial)
                    {
                        sharedMaterials[materialIndex] = convertedMaterial;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderers[i].sharedMaterials = sharedMaterials;
                }
            }
        }

        private static Material GetOrCreateUrpMaterial(Material sourceMaterial)
        {
            if (sourceMaterial == null)
            {
                return null;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                return sourceMaterial;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            if (string.IsNullOrEmpty(sourcePath))
            {
                return CreateTransientUrpMaterial(sourceMaterial, urpLit);
            }

            string fileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string targetPath = $"Assets/Materials/Generated/{fileName}_URP.mat";
            Material convertedMaterial = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (convertedMaterial == null)
            {
                convertedMaterial = new Material(urpLit);
                AssetDatabase.CreateAsset(convertedMaterial, targetPath);
            }

            CopyMaterialProperties(sourceMaterial, convertedMaterial);
            EditorUtility.SetDirty(convertedMaterial);
            return convertedMaterial;
        }

        private static Material CreateTransientUrpMaterial(Material sourceMaterial, Shader urpLit)
        {
            Material convertedMaterial = new Material(urpLit);
            CopyMaterialProperties(sourceMaterial, convertedMaterial);
            return convertedMaterial;
        }

        private static void CopyMaterialProperties(Material sourceMaterial, Material targetMaterial)
        {
            targetMaterial.shader = Shader.Find("Universal Render Pipeline/Lit");

            if (TryGetTexture(sourceMaterial, out Texture baseTexture, "_BaseMap", "_MainTex"))
            {
                targetMaterial.SetTexture("_BaseMap", baseTexture);
            }

            if (TryGetTexture(sourceMaterial, out Texture normalMap, "_BumpMap"))
            {
                targetMaterial.SetTexture("_BumpMap", normalMap);
                targetMaterial.EnableKeyword("_NORMALMAP");
            }

            if (TryGetTexture(sourceMaterial, out Texture emissionMap, "_EmissionMap"))
            {
                targetMaterial.SetTexture("_EmissionMap", emissionMap);
                targetMaterial.EnableKeyword("_EMISSION");
            }

            if (TryGetColor(sourceMaterial, out Color baseColor, "_BaseColor", "_Color"))
            {
                targetMaterial.SetColor("_BaseColor", baseColor);
            }

            if (TryGetColor(sourceMaterial, out Color emissionColor, "_EmissionColor"))
            {
                targetMaterial.SetColor("_EmissionColor", emissionColor);
            }

            if (TryGetFloat(sourceMaterial, out float metallic, "_Metallic"))
            {
                targetMaterial.SetFloat("_Metallic", metallic);
            }

            if (TryGetFloat(sourceMaterial, out float smoothness, "_Smoothness", "_Glossiness"))
            {
                targetMaterial.SetFloat("_Smoothness", smoothness);
            }

            bool isTransparent = sourceMaterial.renderQueue >= (int)RenderQueue.Transparent;
            if (!isTransparent && TryGetFloat(sourceMaterial, out float mode, "_Mode"))
            {
                isTransparent = mode >= 2f;
            }

            if (isTransparent)
            {
                SetupTransparentUrpMaterial(targetMaterial);
            }
            else
            {
                SetupOpaqueUrpMaterial(targetMaterial);
            }
        }

        private static void SetupOpaqueUrpMaterial(Material material)
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        private static void SetupTransparentUrpMaterial(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static bool TryGetTexture(Material material, out Texture texture, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (material.HasProperty(propertyNames[i]))
                {
                    texture = material.GetTexture(propertyNames[i]);
                    if (texture != null)
                    {
                        return true;
                    }
                }
            }

            texture = null;
            return false;
        }

        private static bool TryGetColor(Material material, out Color color, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (material.HasProperty(propertyNames[i]))
                {
                    color = material.GetColor(propertyNames[i]);
                    return true;
                }
            }

            color = Color.white;
            return false;
        }

        private static bool TryGetFloat(Material material, out float value, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (material.HasProperty(propertyNames[i]))
                {
                    value = material.GetFloat(propertyNames[i]);
                    return true;
                }
            }

            value = 0f;
            return false;
        }

        private static void ApplyWheel(SerializedProperty property, WheelBuild wheel)
        {
            property.FindPropertyRelative("axleId").stringValue = wheel.axleId;
            property.FindPropertyRelative("leftSide").boolValue = wheel.leftSide;
            property.FindPropertyRelative("collider").objectReferenceValue = wheel.collider;
            property.FindPropertyRelative("mesh").objectReferenceValue = wheel.mesh;
            property.FindPropertyRelative("steer").boolValue = wheel.steer;
            property.FindPropertyRelative("drive").boolValue = wheel.drive;
            property.FindPropertyRelative("handbrake").boolValue = wheel.handbrake;
        }

        private static Transform CreateEmptyChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static void CreateVisual(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDeepChild(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void StripRuntimeComponents(GameObject root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform || component is MeshFilter || component is Renderer)
                {
                    continue;
                }

                if (component is Collider || component is Rigidbody || component is Joint || component is AudioSource || component is Camera || component is Light || component is Animator || component is MonoBehaviour)
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        private struct WheelBuild
        {
            public string axleId;
            public bool leftSide;
            public WheelCollider collider;
            public Transform mesh;
            public bool steer;
            public bool drive;
            public bool handbrake;
        }

        private struct ImportedVehicleVisual
        {
            public GameObject root;
            public Transform frontLeftWheel;
            public Transform frontRightWheel;
            public Transform rearLeftWheel;
            public Transform rearRightWheel;
        }
    }
}
