using UnityEngine;

namespace Underground.Vehicle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Underground.Vehicle.V2.VehicleControllerV2))]
    public class WheelCustomizationController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private WheelCatalog wheelCatalog;
        [SerializeField] private CarWheelFitmentProfile fitmentProfile;
        [SerializeField] private CarCustomizationState customizationState = new CarCustomizationState();

        [Header("References")]
        [SerializeField] private Underground.Vehicle.V2.VehicleControllerV2 vehicle;
        [SerializeField] private PlayerCarAppearanceController appearanceController;
        [SerializeField] private Transform modelRoot;

        [Header("Runtime")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool rebuildAfterAppearanceChanges = true;
        [SerializeField] private string generatedWheelRootName = "FT_CustomWheel";

        public CarCustomizationState CustomizationState => customizationState;
        public WheelCatalog Catalog => wheelCatalog;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (appearanceController != null)
            {
                appearanceController.AppearanceChanged -= HandleAppearanceChanged;
                appearanceController.AppearanceChanged += HandleAppearanceChanged;
            }
        }

        private void OnDisable()
        {
            if (appearanceController != null)
            {
                appearanceController.AppearanceChanged -= HandleAppearanceChanged;
            }
        }

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyCurrentCustomization();
            }
        }

        public void SetCatalog(WheelCatalog catalog)
        {
            wheelCatalog = catalog;
            ApplyCurrentCustomization();
        }

        public void SetFitmentProfile(CarWheelFitmentProfile profile)
        {
            fitmentProfile = profile;
            ApplyCurrentCustomization();
        }

        public void SetSingleWheel(string wheelId)
        {
            customizationState.ApplySingleWheel(wheelId);
            ApplyCurrentCustomization();
        }

        public void SetFrontRearWheels(string frontWheelId, string rearWheelId)
        {
            customizationState.frontWheelId = frontWheelId;
            customizationState.rearWheelId = rearWheelId;
            ApplyCurrentCustomization();
        }

        public void SetCustomizationState(CarCustomizationState state)
        {
            if (state == null)
            {
                return;
            }

            customizationState = state;
            ApplyCurrentCustomization();
        }

        public void ApplyCurrentCustomization()
        {
            ResolveReferences();
            if (vehicle == null || modelRoot == null)
            {
                return;
            }

            WheelSet[] wheelSets = vehicle.WheelSets;
            if (wheelSets == null || wheelSets.Length == 0)
            {
                return;
            }

            if (!HasAnyResolvedWheelDefinition())
            {
                return;
            }

            HideImportedWheelRenderers();

            for (int i = 0; i < wheelSets.Length; i++)
            {
                ApplyWheelVisual(wheelSets[i]);
            }
        }

        private void HandleAppearanceChanged()
        {
            if (rebuildAfterAppearanceChanges)
            {
                ApplyCurrentCustomization();
            }
        }

        private bool HasAnyResolvedWheelDefinition()
        {
            return ResolveWheelDefinition(true) != null || ResolveWheelDefinition(false) != null;
        }

        private void ApplyWheelVisual(WheelSet wheel)
        {
            if (wheel == null || wheel.collider == null)
            {
                return;
            }

            bool frontAxle = IsFrontWheel(wheel);
            WheelDefinition definition = ResolveWheelDefinition(frontAxle);
            if (definition == null)
            {
                return;
            }

            Transform wheelRoot = ResolveWheelRoot(wheel, frontAxle);
            if (wheelRoot == null)
            {
                return;
            }

            ClearWheelRoot(wheelRoot);
            GameObject visual = InstantiateWheelVisual(definition, wheelRoot);
            visual.name = generatedWheelRootName;
            visual.transform.localPosition = ResolveLocalOffset(wheel, definition, frontAxle);
            visual.transform.localRotation = ResolveLocalRotation(wheel, frontAxle);
            visual.transform.localScale = ResolveLocalScale(wheel, definition, frontAxle);

            ApplyMaterials(visual, definition);
            StripRuntimeComponents(visual);
            vehicle.SetWheelVisualByColliderName(wheel.collider.name, wheelRoot);
        }

        private WheelDefinition ResolveWheelDefinition(bool frontAxle)
        {
            if (wheelCatalog == null)
            {
                return null;
            }

            string wheelId = frontAxle ? customizationState.frontWheelId : customizationState.rearWheelId;
            if (wheelCatalog.TryGetWheel(wheelId, out WheelDefinition definition) && definition.IsCompatible(frontAxle))
            {
                return definition;
            }

            string fallbackId = frontAxle ? customizationState.rearWheelId : customizationState.frontWheelId;
            if (wheelCatalog.TryGetWheel(fallbackId, out definition) && definition.IsCompatible(frontAxle))
            {
                return definition;
            }

            return wheelCatalog.GetFirstCompatible(frontAxle);
        }

        private Transform ResolveWheelRoot(WheelSet wheel, bool frontAxle)
        {
            Transform existing = wheel.mesh;
            if (existing != null)
            {
                if (IsWheelVisualRoot(existing))
                {
                    return existing;
                }

                if (existing.parent != null && IsWheelVisualRoot(existing.parent))
                {
                    return existing.parent;
                }
            }

            string rootName = ResolveWheelRootName(wheel);
            Transform root = modelRoot.Find(rootName);
            if (root != null)
            {
                return root;
            }

            Transform profileRoot = FindProfileHub(frontAxle, wheel.leftSide);
            if (profileRoot != null)
            {
                return profileRoot;
            }

            GameObject rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(modelRoot, false);
            rootObject.transform.localPosition = wheel.collider.transform.localPosition;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private Transform FindProfileHub(bool frontAxle, bool leftSide)
        {
            if (fitmentProfile == null)
            {
                return null;
            }

            string path = frontAxle
                ? (leftSide ? fitmentProfile.frontLeftHubPath : fitmentProfile.frontRightHubPath)
                : (leftSide ? fitmentProfile.rearLeftHubPath : fitmentProfile.rearRightHubPath);

            return string.IsNullOrEmpty(path) ? null : transform.Find(path);
        }

        private Vector3 ResolveLocalOffset(WheelSet wheel, WheelDefinition definition, bool frontAxle)
        {
            float axleCorrection = fitmentProfile != null
                ? (frontAxle ? fitmentProfile.frontOffsetCorrection : fitmentProfile.rearOffsetCorrection)
                : 0f;
            float userOffset = frontAxle ? customizationState.frontOffset : customizationState.rearOffset;
            float side = wheel.leftSide ? -1f : 1f;
            return Vector3.right * side * (definition.defaultOffset + axleCorrection + userOffset);
        }

        private Quaternion ResolveLocalRotation(WheelSet wheel, bool frontAxle)
        {
            float camber = frontAxle ? customizationState.frontCamberVisual : customizationState.rearCamberVisual;
            if (fitmentProfile != null)
            {
                camber += frontAxle ? fitmentProfile.frontCamberCorrection : fitmentProfile.rearCamberCorrection;
                bool mirror = wheel.leftSide ? fitmentProfile.mirrorLeftWheels : fitmentProfile.mirrorRightWheels;
                if (mirror)
                {
                    camber = -camber;
                }
            }

            return Quaternion.Euler(0f, 0f, camber);
        }

        private Vector3 ResolveLocalScale(WheelSet wheel, WheelDefinition definition, bool frontAxle)
        {
            float fallbackDiameter = wheel.collider != null ? wheel.collider.radius * 2f : definition.nominalDiameter;
            float diameter = frontAxle ? customizationState.frontDiameter : customizationState.rearDiameter;
            float width = frontAxle ? customizationState.frontWidth : customizationState.rearWidth;

            if (fitmentProfile != null)
            {
                diameter = fitmentProfile.ClampDiameter(diameter, fallbackDiameter);
                width = fitmentProfile.ClampWidth(width, definition.nominalWidth);
            }
            else
            {
                diameter = diameter > 0.01f ? diameter : fallbackDiameter;
                width = width > 0.01f ? width : definition.nominalWidth;
            }

            float axleScale = fitmentProfile != null
                ? (frontAxle ? fitmentProfile.frontScaleMultiplier : fitmentProfile.rearScaleMultiplier)
                : 1f;
            float diameterScale = diameter / Mathf.Max(0.01f, definition.nominalDiameter);
            float widthScale = width / Mathf.Max(0.01f, definition.nominalWidth);
            float uniformScale = diameterScale * axleScale;
            return new Vector3(widthScale * axleScale, uniformScale, uniformScale);
        }

        private GameObject InstantiateWheelVisual(WheelDefinition definition, Transform parent)
        {
            if (definition.wheelPrefab != null)
            {
                GameObject instance = Instantiate(definition.wheelPrefab, parent, false);
                instance.SetActive(true);
                return instance;
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fallback.transform.SetParent(parent, false);
            fallback.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Object.Destroy(fallback.GetComponent<Collider>());
            return fallback;
        }

        private static void ApplyMaterials(GameObject visual, WheelDefinition definition)
        {
            if (visual == null || definition == null || definition.materialOverrides == null || definition.materialOverrides.Length == 0)
            {
                return;
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] current = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < current.Length; materialIndex++)
                {
                    Material replacement = definition.materialOverrides[Mathf.Min(materialIndex, definition.materialOverrides.Length - 1)];
                    if (replacement != null)
                    {
                        current[materialIndex] = replacement;
                    }
                }

                renderer.sharedMaterials = current;
            }
        }

        private static void StripRuntimeComponents(GameObject visual)
        {
            Component[] components = visual.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform || component is MeshFilter || component is Renderer)
                {
                    continue;
                }

                if (component is Collider collider)
                {
                    Object.Destroy(collider);
                    continue;
                }

                if (component is Rigidbody rigidbody)
                {
                    Object.Destroy(rigidbody);
                    continue;
                }

                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void ClearWheelRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        private void ResolveReferences()
        {
            if (vehicle == null)
            {
                vehicle = GetComponent<Underground.Vehicle.V2.VehicleControllerV2>();
            }

            if (appearanceController == null)
            {
                appearanceController = GetComponent<PlayerCarAppearanceController>();
            }

            if (modelRoot == null)
            {
                modelRoot = transform.Find("ModelRoot");
            }
        }

        private void HideImportedWheelRenderers()
        {
            Transform importedVisual = modelRoot != null ? modelRoot.Find("ImportedVisual") : null;
            if (importedVisual == null)
            {
                return;
            }

            Renderer[] renderers = importedVisual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !LooksLikeWheelRenderer(renderer))
                {
                    continue;
                }

                renderer.enabled = false;
            }
        }

        private static bool IsFrontWheel(WheelSet wheel)
        {
            return wheel != null && (wheel.steer || string.Equals(wheel.axleId, "Front", System.StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsWheelVisualRoot(Transform transformToCheck)
        {
            if (transformToCheck == null)
            {
                return false;
            }

            string name = transformToCheck.name;
            return name == "FL_Visual" || name == "FR_Visual" || name == "RL_Visual" || name == "RR_Visual";
        }

        private static string ResolveWheelRootName(WheelSet wheel)
        {
            if (wheel == null || wheel.collider == null)
            {
                return "Wheel_Visual";
            }

            switch (wheel.collider.name)
            {
                case "FL_Collider": return "FL_Visual";
                case "FR_Collider": return "FR_Visual";
                case "RL_Collider": return "RL_Visual";
                case "RR_Collider": return "RR_Visual";
                default: return wheel.collider.name + "_Visual";
            }
        }

        private static bool LooksLikeWheelRenderer(Renderer renderer)
        {
            string objectName = renderer.gameObject.name.ToLowerInvariant();
            string rendererName = renderer.name.ToLowerInvariant();
            string meshName = string.Empty;
            if (renderer.TryGetComponent<MeshFilter>(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            {
                meshName = meshFilter.sharedMesh.name.ToLowerInvariant();
            }

            return LooksLikeWheelName(objectName) || LooksLikeWheelName(rendererName) || LooksLikeWheelName(meshName);
        }

        private static bool LooksLikeWheelName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            bool wheelLike = value.Contains("wheel")
                || value.Contains("tire")
                || value.Contains("tyre")
                || value.Contains("rim");
            bool brakeLike = value.Contains("brake")
                || value.Contains("rotor")
                || value.Contains("caliper")
                || value.Contains("disc");

            return wheelLike && !brakeLike;
        }
    }
}

