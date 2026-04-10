using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Underground.Garage;
using Underground.Save;
using Underground.UI;
using Underground.World;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Underground.Vehicle
{
    public class PlayerCarAppearanceController : MonoBehaviour
    {
        private const string GameplayReflectionProbeName = "GameplayReflectionProbe";

        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private string currentCarId;
        [SerializeField] private string currentCarDisplayName;
        [SerializeField] private bool showroomPresentationMode;

        private bool currentAppliedAsShowroom;

        private Transform frontLeftWheelRoot;
        private Transform frontRightWheelRoot;
        private Transform rearLeftWheelRoot;
        private Transform rearRightWheelRoot;

        // ------------------------------------------------------------------
        // Compatibility guards for mixed third-party vehicle assets
        // ------------------------------------------------------------------

        private enum WheelRigCompatibility
        {
            StandardFourWheels,
            SharedRearAxle,
            EmbeddedSingleMesh
        }

        private static readonly string[] SharedRearAxleCarTokens =
        {
            "crownvic",
            "crown vic",
            "americansedan"
        };

        private static readonly string[] MaterialNormalizationWhitelistTokens =
        {
            "rmcar26",
            "arcadecar",
            "simpleretrocar",
            "americansedan",
            "invogames",
            "invo"
        };


        /// <summary>
        /// Fires after new visuals have been set up so other systems
        /// (e.g. VehicleNightLightingController) can rebuild their rigs.
        /// </summary>
        public event System.Action AppearanceChanged;

        public string CurrentCarId => currentCarId;
        public string CurrentCarDisplayName => string.IsNullOrEmpty(currentCarDisplayName)
            ? (vehicle != null && vehicle.BaseStats != null ? vehicle.BaseStats.displayName : string.Empty)
            : currentCarDisplayName;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ApplyCurrentSelection();
        }

        public bool ApplyCurrentSelection()
        {
            ResolveReferences();

            string targetCarId = progressManager != null && !string.IsNullOrEmpty(progressManager.CurrentOwnedCarId)
                ? progressManager.CurrentOwnedCarId
                : PlayerCarCatalog.StarterCarId;

            return ApplyAppearance(targetCarId);
        }

        public bool ApplyAppearance(string carId)
        {
            // Resolve legacy IDs transparently.
            carId = PlayerCarCatalog.MigrateCarId(carId);

            if (!PlayerCarCatalog.TryGetDefinition(carId, out PlayerCarDefinition definition))
            {
                definition = PlayerCarCatalog.GetStarterDefinition();
            }

            GameObject visualPrefab = definition.LoadVisualPrefab();

            // ── Part 5: Visual Kit Override ──
            // If a VisualKitManager is present and has an active kit,
            // use the kit's visual prefab instead of the base model.
            VisualKitManager kitManager = GetComponent<VisualKitManager>();
            if (kitManager != null && kitManager.HasActiveKit)
            {
                string kitPrefabPath = kitManager.ActiveVisualPrefabPath;
                if (!string.IsNullOrEmpty(kitPrefabPath))
                {
                    GameObject kitPrefab = null;
#if UNITY_EDITOR
                    kitPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(kitPrefabPath);
#endif
                    if (kitPrefab == null)
                    {
                        kitPrefab = Resources.Load<GameObject>(kitPrefabPath);
                    }

                    if (kitPrefab != null)
                    {
                        visualPrefab = kitPrefab;
                    }
                }
            }

            if (visualPrefab == null)
            {
                return false;
            }

            ResolveReferences();
            EnsureWheelRoots();
            ResetWheelRoots();
            if (modelRoot == null)
            {
                return false;
            }

            bool hasDetachedWheelVisuals =
                frontLeftWheelRoot != null && frontLeftWheelRoot.childCount > 0 &&
                frontRightWheelRoot != null && frontRightWheelRoot.childCount > 0 &&
                rearLeftWheelRoot != null && rearLeftWheelRoot.childCount > 0 &&
                rearRightWheelRoot != null && rearRightWheelRoot.childCount > 0;
            bool hasValidDetachedWheelVisuals =
                HasValidDetachedWheelVisual(frontLeftWheelRoot) &&
                HasValidDetachedWheelVisual(frontRightWheelRoot) &&
                HasValidDetachedWheelVisual(rearLeftWheelRoot) &&
                HasValidDetachedWheelVisual(rearRightWheelRoot);

            bool isGarageShowroom = IsGarageShowroomContext();

            if (currentCarId == definition.CarId &&
                currentAppliedAsShowroom == isGarageShowroom &&
                modelRoot.Find("ImportedVisual") != null &&
                (!hasDetachedWheelVisuals || hasValidDetachedWheelVisuals))
            {
                BindWheelVisuals(null, null, null, null, false);
                currentCarDisplayName = definition.DisplayName;
                ApplyPerCarStats(definition);
                EnsureGameplayReflectionProbe(isGarageShowroom);
                ApplyPlayerCarLODPolicy(modelRoot.Find("ImportedVisual"), isGarageShowroom);
                return true;
            }

            DestroyExistingVisuals();

            GameObject visualInstance = Object.Instantiate(visualPrefab, modelRoot, false);
            visualInstance.name = "ImportedVisual";
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;

            StripRuntimeComponents(visualInstance);
            DisableNonVisualHelperRenderers(visualInstance);
            ApplyPlayerCarLODPolicy(visualInstance.transform, isGarageShowroom);

            // ---- Wheel discovery: try authored path first, fall back to generic per-wheel ----
            Transform sourceFrontLeft = TryResolveWheel(visualInstance.transform, definition, definition.WheelMapping?.frontLeftPath, "frontleft", "fl", "FL");
            Transform sourceFrontRight = TryResolveWheel(visualInstance.transform, definition, definition.WheelMapping?.frontRightPath, "frontright", "fr", "FR");
            Transform sourceRearLeft = TryResolveWheel(visualInstance.transform, definition, definition.WheelMapping?.rearLeftPath, "rearleft", "rl", "RL");
            Transform sourceRearRight = TryResolveWheel(visualInstance.transform, definition, definition.WheelMapping?.rearRightPath, "rearright", "rr", "RR");
            bool sharesFrontAxleMesh = sourceFrontLeft != null && sourceFrontLeft == sourceFrontRight;
            bool sharesRearAxleMesh = sourceRearLeft != null && sourceRearLeft == sourceRearRight;
            WheelRigCompatibility wheelRigCompatibility = ResolveWheelRigCompatibility(definition, sourceFrontLeft, sourceFrontRight, sourceRearLeft, sourceRearRight);
            bool useSharedRearAxleCompatibility = wheelRigCompatibility == WheelRigCompatibility.SharedRearAxle;
            bool skipWheelBinding = wheelRigCompatibility == WheelRigCompatibility.EmbeddedSingleMesh;

            if (useSharedRearAxleCompatibility)
            {
                sharesRearAxleMesh = false;
            }

            float importedWheelRadius = 0.34f;
            TryGetAverageWheelRadius(sourceFrontLeft, sourceFrontRight, sourceRearLeft, sourceRearRight, out importedWheelRadius);

            float targetWheelRadius = ResolveWheelRadius();
            Vector3 frontLeftTarget = frontLeftWheelRoot != null ? frontLeftWheelRoot.localPosition : Vector3.zero;
            Vector3 frontRightTarget = frontRightWheelRoot != null ? frontRightWheelRoot.localPosition : Vector3.zero;
            Vector3 rearLeftTarget = rearLeftWheelRoot != null ? rearLeftWheelRoot.localPosition : Vector3.zero;
            Vector3 rearRightTarget = rearRightWheelRoot != null ? rearRightWheelRoot.localPosition : Vector3.zero;

            AlignImportedVehicleBody(
                visualInstance.transform,
                sourceFrontLeft,
                sourceFrontRight,
                sourceRearLeft,
                sourceRearRight,
                frontLeftTarget,
                frontRightTarget,
                rearLeftTarget,
                rearRightTarget,
                targetWheelRadius);

            if (ShouldMatchGameplayWheelLayoutToSource(definition, isGarageShowroom))
            {
                MatchGameplayWheelLayoutToSource(
                    sourceFrontLeft,
                    sourceFrontRight,
                    sourceRearLeft,
                    sourceRearRight,
                    importedWheelRadius);
                
                frontLeftTarget = frontLeftWheelRoot != null ? frontLeftWheelRoot.localPosition : frontLeftTarget;
                frontRightTarget = frontRightWheelRoot != null ? frontRightWheelRoot.localPosition : frontRightTarget;
                rearLeftTarget = rearLeftWheelRoot != null ? rearLeftWheelRoot.localPosition : rearLeftTarget;
                rearRightTarget = rearRightWheelRoot != null ? rearRightWheelRoot.localPosition : rearRightTarget;
                
                // Update target radius since we just moved the physics to match the imported tires
                targetWheelRadius = importedWheelRadius;
            }

            NormalizeImportedMaterials(visualInstance, definition.CarId, definition.DisplayName, isGarageShowroom);
            DisableNonVisualHelperRenderers(visualInstance);

            bool hasResolvedWheelSources =
                sourceFrontLeft != null &&
                sourceFrontRight != null &&
                sourceRearLeft != null &&
                sourceRearRight != null;

            // Determine whether to detach wheel visuals from the body and
            // place them at WheelCollider positions (for steering / rolling).
            // Cars with UseDetachedWheelVisuals == false keep their wheels
            // embedded in the body mesh.
            bool shouldDetachWheels = ShouldDetachWheelVisuals(definition, hasResolvedWheelSources, isGarageShowroom, wheelRigCompatibility);
            bool useMeshOnlyDetachedWheels = ShouldUseMeshOnlyDetachedWheels(definition, hasResolvedWheelSources, isGarageShowroom, wheelRigCompatibility);

            if (shouldDetachWheels)
            {
                Transform frontLeftHiddenSource = null;
                Transform frontRightHiddenSource = null;
                Transform rearLeftHiddenSource = null;
                Transform rearRightHiddenSource = null;

                Transform frontLeftVisual = !sharesFrontAxleMesh
                    ? CreateDetachedWheelVisual(frontLeftWheelRoot, sourceFrontLeft, targetWheelRadius, useMeshOnlyDetachedWheels, out frontLeftHiddenSource)
                    : null;
                Transform frontRightVisual = !sharesFrontAxleMesh
                    ? CreateDetachedWheelVisual(frontRightWheelRoot, sourceFrontRight, targetWheelRadius, useMeshOnlyDetachedWheels, out frontRightHiddenSource)
                    : null;
                Transform rearLeftVisual = null;
                Transform rearRightVisual = null;

                if (useSharedRearAxleCompatibility)
                {
                    rearLeftVisual = CreateDetachedWheelVisual(rearLeftWheelRoot, sourceRearLeft, targetWheelRadius, useMeshOnlyDetachedWheels, out rearLeftHiddenSource);
                    rearRightVisual = CreateDetachedWheelVisual(rearRightWheelRoot, sourceRearRight, targetWheelRadius, useMeshOnlyDetachedWheels, out rearRightHiddenSource);
                }
                else
                {
                    rearLeftVisual = !sharesRearAxleMesh
                        ? CreateDetachedWheelVisual(rearLeftWheelRoot, sourceRearLeft, targetWheelRadius, useMeshOnlyDetachedWheels, out rearLeftHiddenSource)
                        : null;
                    rearRightVisual = !sharesRearAxleMesh
                        ? CreateDetachedWheelVisual(rearRightWheelRoot, sourceRearRight, targetWheelRadius, useMeshOnlyDetachedWheels, out rearRightHiddenSource)
                        : null;
                }

                if (skipWheelBinding)
                {
                    BindWheelVisuals(null, null, null, null, false);
                }
                else
                {
                    BindWheelVisuals(frontLeftVisual, frontRightVisual, rearLeftVisual, rearRightVisual);
                }

                DisableWheelRenderers(frontLeftHiddenSource ?? sourceFrontLeft, frontLeftVisual != null);
                DisableWheelRenderers(frontRightHiddenSource ?? sourceFrontRight, frontRightVisual != null);
                DisableWheelRenderers(rearLeftHiddenSource ?? sourceRearLeft, rearLeftVisual != null);
                DisableWheelRenderers(rearRightHiddenSource ?? sourceRearRight, rearRightVisual != null);
            }
            else
            {
                // Showroom/menu preview should never bind imported wheel
                // transforms to wheel colliders. Doing so pulls wheel meshes
                // out of the body hierarchy after cycling between cars.
                if (isGarageShowroom)
                {
                    BindWheelVisuals(null, null, null, null, false);
                }
                // For gameplay cars that keep wheel meshes embedded in the
                // body prefab, bind those authored wheel transforms directly
                // to the wheel colliders.
                else if (hasResolvedWheelSources && !skipWheelBinding)
                {
                    BindWheelVisuals(
                        sharesFrontAxleMesh ? null : sourceFrontLeft,
                        sharesFrontAxleMesh ? null : sourceFrontRight,
                        sharesRearAxleMesh ? null : sourceRearLeft,
                        sharesRearAxleMesh ? null : sourceRearRight,
                        false);
                }
                else
                {
                    BindWheelVisuals(null, null, null, null, false);
                }
            }

            currentCarId = definition.CarId;
            currentCarDisplayName = definition.DisplayName;
            currentAppliedAsShowroom = isGarageShowroom;

            ApplyPerCarStats(definition);
            EnsureGameplayReflectionProbe(isGarageShowroom);

            // DYNAMIC WAKE-UP: Tell HDRP to immediately refresh reflections for the new car.
            // This prevents that "plastic" look on the first frame of gameplay.
            RefreshReflections();

            AppearanceChanged?.Invoke();
            return true;
        }

        private void RefreshReflections()
        {
            // Update Global Illumination
            DynamicGI.UpdateEnvironment();

            // Notify real visible renderers to refresh their probe anchors.
            // Do NOT resurrect helper collision/proxy renderers.
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                if (IsNonVisualHelperRenderer(r))
                {
                    r.enabled = false;
                    continue;
                }

                if (!r.enabled)
                {
                    continue;
                }

                r.enabled = false;
                r.enabled = true;
            }
        }

        public void SetShowroomPresentationMode(bool enabled)
        {
            showroomPresentationMode = enabled;
        }

        private bool ShouldDetachWheelVisuals(PlayerCarDefinition definition, bool hasResolvedWheelSources, bool isGarageShowroom, WheelRigCompatibility wheelRigCompatibility)
        {
            if (!hasResolvedWheelSources)
            {
                return false;
            }

            // In showroom/menu preview, keep the imported body intact and do
            // not create detached gameplay wheel visuals.
            if (isGarageShowroom)
            {
                return false;
            }

            if (wheelRigCompatibility == WheelRigCompatibility.EmbeddedSingleMesh)
            {
                return false;
            }

            if (wheelRigCompatibility == WheelRigCompatibility.SharedRearAxle)
            {
                return true;
            }

            return definition.UseDetachedWheelVisuals;
        }

        private bool ShouldUseMeshOnlyDetachedWheels(PlayerCarDefinition definition, bool hasResolvedWheelSources, bool isGarageShowroom, WheelRigCompatibility wheelRigCompatibility)
        {
            if (!hasResolvedWheelSources || isGarageShowroom)
            {
                return false;
            }

            return wheelRigCompatibility == WheelRigCompatibility.SharedRearAxle;
        }

        private static bool ShouldMatchGameplayWheelLayoutToSource(PlayerCarDefinition definition, bool isGarageShowroom)
        {
            if (isGarageShowroom || string.IsNullOrEmpty(definition.CarId))
            {
                return false;
            }

            // Cars with shared rear axle compatibility should not force both rear
            // WheelColliders onto the same imported mesh pivot. We keep their
            // gameplay wheel layout authored in the controller.
            if (MatchesAnyToken(definition.CarId, SharedRearAxleCarTokens) || MatchesAnyToken(definition.DisplayName, SharedRearAxleCarTokens))
            {
                return false;
            }

            // We now do this for ALL cars. Physics should adapt to the body, not the other way around.
            return true;
        }

        private static WheelRigCompatibility ResolveWheelRigCompatibility(
            PlayerCarDefinition definition,
            Transform sourceFrontLeft,
            Transform sourceFrontRight,
            Transform sourceRearLeft,
            Transform sourceRearRight)
        {
            if (MatchesAnyToken(definition.CarId, SharedRearAxleCarTokens) || MatchesAnyToken(definition.DisplayName, SharedRearAxleCarTokens))
            {
                return WheelRigCompatibility.SharedRearAxle;
            }

            if (sourceRearLeft != null && sourceRearRight != null && sourceRearLeft == sourceRearRight)
            {
                string sharedRearName = NormalizeName(sourceRearLeft.name);
                if (sharedRearName.Contains("wheelsb") || sharedRearName.Contains("rearaxle") || sharedRearName.Contains("rearwheelpair"))
                {
                    return WheelRigCompatibility.SharedRearAxle;
                }
            }

            return WheelRigCompatibility.StandardFourWheels;
        }

        private static bool ShouldNormalizeCarMaterials(string carId, string displayName)
        {
            return MatchesAnyToken(carId, MaterialNormalizationWhitelistTokens) ||
                   MatchesAnyToken(displayName, MaterialNormalizationWhitelistTokens);
        }

        private static bool MatchesAnyToken(string value, IEnumerable<string> tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            string normalizedValue = NormalizeName(value);
            foreach (string token in tokens)
            {
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                string normalizedToken = NormalizeName(token);
                if (!string.IsNullOrEmpty(normalizedToken) && normalizedValue.Contains(normalizedToken))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsGarageShowroomContext()
        {
            return showroomPresentationMode || GetComponentInParent<GarageShowroomController>() != null;
        }

        private void EnsureGameplayReflectionProbe(bool isGarageShowroom)
        {
            // Nuked. The player car will no longer spawn its own forced Realtime Reflection Probe.
            // It will rely perfectly on your authored scene's Global Illumination and Reflection Probes.
        }

        // ------------------------------------------------------------------
        // Per-car stats application
        // ------------------------------------------------------------------

        private void ApplyPerCarStats(PlayerCarDefinition definition)
        {
            if (vehicle == null)
            {
                return;
            }

            VehicleStatsData statsOverride = definition.LoadStatsAsset();
            if (statsOverride != null)
            {
                vehicle.Initialize(statsOverride);
            }
        }

        // ------------------------------------------------------------------
        // Wheel resolution: authored path → recursive fallback → generic tokens
        // ------------------------------------------------------------------

        private static Transform TryResolveWheel(Transform visualRoot, PlayerCarDefinition definition, string authoredPath, string genericToken1, string genericToken2, string label)
        {
            if (!string.IsNullOrEmpty(authoredPath))
            {
                Transform found = FindAuthoredWheel(visualRoot, authoredPath);
                if (found != null)
                {
                    return found;
                }

                Debug.LogWarning($"[PlayerCarAppearanceController] Authored wheel path '{authoredPath}' not found for {definition.CarId} ({label}). Falling back to generic search.");
            }

            Transform generic = FindWheelSource(visualRoot, genericToken1, genericToken2);
            if (generic != null)
            {
                return generic;
            }

            Debug.LogWarning($"[PlayerCarAppearanceController] No wheel found for {definition.CarId} ({label}). Prefab child hierarchy:\n{GetHierarchyDump(visualRoot, 0)}");
            return null;
        }

        private static Transform FindAuthoredWheel(Transform visualRoot, string path)
        {
            if (string.IsNullOrEmpty(path) || visualRoot == null)
            {
                return null;
            }

            Transform found = visualRoot.Find(path);
            if (found != null)
            {
                return found;
            }

            string lastSegment = path;
            int slashIndex = path.LastIndexOf('/');
            if (slashIndex >= 0 && slashIndex < path.Length - 1)
            {
                lastSegment = path.Substring(slashIndex + 1);
            }

            return FindChildRecursive(visualRoot, lastSegment);
        }

        private static string GetHierarchyDump(Transform root, int depth)
        {
            if (root == null || depth > 5)
            {
                return string.Empty;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            string indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}{root.name}");
            for (int i = 0; i < root.childCount; i++)
            {
                sb.Append(GetHierarchyDump(root.GetChild(i), depth + 1));
            }

            return sb.ToString();
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform deeper = FindChildRecursive(child, name);
                if (deeper != null)
                {
                    return deeper;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Reference resolution
        // ------------------------------------------------------------------

        private void ResolveReferences()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (vehicle == null)
            {
                vehicle = GetComponent<VehicleDynamicsController>();
            }

            if (modelRoot == null)
            {
                modelRoot = transform.Find("ModelRoot");
            }
        }

        private void EnsureWheelRoots()
        {
            frontLeftWheelRoot = FindOrCreateWheelRoot("FL_Visual", "FL_Collider");
            frontRightWheelRoot = FindOrCreateWheelRoot("FR_Visual", "FR_Collider");
            rearLeftWheelRoot = FindOrCreateWheelRoot("RL_Visual", "RL_Collider");
            rearRightWheelRoot = FindOrCreateWheelRoot("RR_Visual", "RR_Collider");
        }

        private void ResetWheelRoots()
        {
            ResetWheelRoot(frontLeftWheelRoot, "FL_Collider");
            ResetWheelRoot(frontRightWheelRoot, "FR_Collider");
            ResetWheelRoot(rearLeftWheelRoot, "RL_Collider");
            ResetWheelRoot(rearRightWheelRoot, "RR_Collider");
        }

        private void BindWheelVisuals()
        {
            BindWheelVisuals(null, null, null, null, true);
        }

        private void BindWheelVisuals(Transform frontLeftVisual, Transform frontRightVisual, Transform rearLeftVisual, Transform rearRightVisual)
        {
            BindWheelVisuals(frontLeftVisual, frontRightVisual, rearLeftVisual, rearRightVisual, true);
        }

        private void BindWheelVisuals(Transform frontLeftVisual, Transform frontRightVisual, Transform rearLeftVisual, Transform rearRightVisual, bool useWheelRootsAsFallback)
        {
            if (vehicle == null)
            {
                return;
            }

            vehicle.SetWheelVisualByColliderName("FL_Collider", frontLeftVisual != null ? frontLeftVisual : (useWheelRootsAsFallback ? frontLeftWheelRoot : null));
            vehicle.SetWheelVisualByColliderName("FR_Collider", frontRightVisual != null ? frontRightVisual : (useWheelRootsAsFallback ? frontRightWheelRoot : null));
            vehicle.SetWheelVisualByColliderName("RL_Collider", rearLeftVisual != null ? rearLeftVisual : (useWheelRootsAsFallback ? rearLeftWheelRoot : null));
            vehicle.SetWheelVisualByColliderName("RR_Collider", rearRightVisual != null ? rearRightVisual : (useWheelRootsAsFallback ? rearRightWheelRoot : null));
        }

        private void MatchGameplayWheelLayoutToSource(Transform frontLeftSource, Transform frontRightSource, Transform rearLeftSource, Transform rearRightSource, float targetRadius)
        {
            MatchWheelColliderAndRootToSource("FL_Collider", frontLeftWheelRoot, frontLeftSource, targetRadius);
            MatchWheelColliderAndRootToSource("FR_Collider", frontRightWheelRoot, frontRightSource, targetRadius);
            MatchWheelColliderAndRootToSource("RL_Collider", rearLeftWheelRoot, rearLeftSource, targetRadius);
            MatchWheelColliderAndRootToSource("RR_Collider", rearRightWheelRoot, rearRightSource, targetRadius);
        }

        private void MatchWheelColliderAndRootToSource(string colliderName, Transform wheelRoot, Transform sourceWheel, float targetRadius)
        {
            if (string.IsNullOrEmpty(colliderName) || sourceWheel == null)
            {
                return;
            }

            if (!TryGetWheelCenterLocal(transform, sourceWheel, out Vector3 sourceCenterLocal))
            {
                return;
            }

            Transform colliderTransform = transform.Find($"WheelColliders/{colliderName}");
            if (colliderTransform != null)
            {
                colliderTransform.localPosition = sourceCenterLocal;
                
                // DYNAMIC FIT: Set the physics collider radius to match the visual tire.
                WheelCollider wc = colliderTransform.GetComponent<WheelCollider>();
                if (wc != null && targetRadius > 0.1f)
                {
                    wc.radius = targetRadius;
                }
            }

            if (wheelRoot != null)
            {
                wheelRoot.localPosition = sourceCenterLocal;
                wheelRoot.localRotation = Quaternion.identity;
            }
        }

        private Transform FindOrCreateWheelRoot(string wheelRootName, string colliderName)
        {
            if (modelRoot == null)
            {
                return null;
            }

            Transform existingRoot = modelRoot.Find(wheelRootName);
            if (existingRoot != null)
            {
                return existingRoot;
            }

            Transform collider = transform.Find($"WheelColliders/{colliderName}");
            GameObject wheelRootObject = new GameObject(wheelRootName);
            wheelRootObject.transform.SetParent(modelRoot, false);
            wheelRootObject.transform.localPosition = collider != null ? collider.localPosition : Vector3.zero;
            wheelRootObject.transform.localRotation = Quaternion.identity;
            return wheelRootObject.transform;
        }

        // ------------------------------------------------------------------
        // Visual cleanup
        // ------------------------------------------------------------------

        private void DestroyExistingVisuals()
        {
            if (modelRoot == null)
            {
                return;
            }

#if UNITY_EDITOR
            RedirectEditorSelectionAwayFromTransientVisuals();
#endif

            for (int i = modelRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = modelRoot.GetChild(i);
                if (child == frontLeftWheelRoot || child == frontRightWheelRoot || child == rearLeftWheelRoot || child == rearRightWheelRoot)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }

            ClearChildren(frontLeftWheelRoot);
            ClearChildren(frontRightWheelRoot);
            ClearChildren(rearLeftWheelRoot);
            ClearChildren(rearRightWheelRoot);
        }

#if UNITY_EDITOR
        private void RedirectEditorSelectionAwayFromTransientVisuals()
        {
            Transform activeTransform = Selection.activeTransform;
            if (activeTransform == null || !WillDestroyTransform(activeTransform))
            {
                return;
            }

            Selection.activeGameObject = gameObject;
        }

        private bool WillDestroyTransform(Transform candidate)
        {
            if (candidate == null || modelRoot == null || !candidate.IsChildOf(modelRoot))
            {
                return false;
            }

            if (candidate == frontLeftWheelRoot || candidate == frontRightWheelRoot || candidate == rearLeftWheelRoot || candidate == rearRightWheelRoot)
            {
                return false;
            }

            if (frontLeftWheelRoot != null && candidate.IsChildOf(frontLeftWheelRoot))
            {
                return true;
            }

            if (frontRightWheelRoot != null && candidate.IsChildOf(frontRightWheelRoot))
            {
                return true;
            }

            if (rearLeftWheelRoot != null && candidate.IsChildOf(rearLeftWheelRoot))
            {
                return true;
            }

            if (rearRightWheelRoot != null && candidate.IsChildOf(rearRightWheelRoot))
            {
                return true;
            }

            Transform rootChild = candidate;
            while (rootChild.parent != null && rootChild.parent != modelRoot)
            {
                rootChild = rootChild.parent;
            }

            return rootChild != frontLeftWheelRoot &&
                   rootChild != frontRightWheelRoot &&
                   rootChild != rearLeftWheelRoot &&
                   rootChild != rearRightWheelRoot;
        }
#endif

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
        }

        private void ResetWheelRoot(Transform root, string colliderName)
        {
            if (root == null)
            {
                return;
            }

            Transform collider = transform.Find($"WheelColliders/{colliderName}");
            root.localPosition = collider != null ? collider.localPosition : Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------
        // Wheel radius + visuals
        // ------------------------------------------------------------------

        private float ResolveWheelRadius()
        {
            WheelCollider[] wheelColliders = GetComponentsInChildren<WheelCollider>(true);
            for (int i = 0; i < wheelColliders.Length; i++)
            {
                if (wheelColliders[i] != null && wheelColliders[i].radius > 0.001f)
                {
                    return wheelColliders[i].radius;
                }
            }

            return 0.34f;
        }

        private static Transform CreateDetachedWheelVisual(Transform wheelRoot, Transform sourceWheel, float targetWheelRadius)
        {
            return CreateDetachedWheelVisual(wheelRoot, sourceWheel, targetWheelRadius, false, out _);
        }

        private static Transform CreateDetachedWheelVisual(Transform wheelRoot, Transform sourceWheel, float targetWheelRadius, bool useBestMeshOnly, out Transform hiddenSource)
        {
            hiddenSource = null;

            if (wheelRoot == null || sourceWheel == null)
            {
                return null;
            }

            Transform cloneSource = sourceWheel;
            if (useBestMeshOnly)
            {
                cloneSource = FindBestWheelMeshTransform(sourceWheel) ?? sourceWheel;
            }

            // GHOST REMOVAL: Only hide renderers that are explicitly wheel-related.
            // This is the only safe way to ensure we don't hide car bodies or windows.
            Renderer[] sourceRenderers = sourceWheel.GetComponentsInChildren<Renderer>(true);
            foreach(var r in sourceRenderers)
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || 
                    n.Contains("rim") || n.Contains("hub") || n.Contains("spoke"))
                {
                    r.enabled = false;
                }
            }
            hiddenSource = sourceWheel;

            // SPAWN THE NEW DYNAMIC VISUAL
            GameObject wheelVisualClone = Object.Instantiate(cloneSource.gameObject, wheelRoot, false);
            wheelVisualClone.name = "WheelMesh_Dynamic";
            wheelVisualClone.SetActive(true);
            
            // Ensure the clone is visible
            Renderer[] cloneRenderers = wheelVisualClone.GetComponentsInChildren<Renderer>(true);
            foreach(var r in cloneRenderers) r.enabled = true;

            wheelVisualClone.transform.localPosition = Vector3.zero;
            wheelVisualClone.transform.localRotation = cloneSource.localRotation;
            NormalizeHierarchyScale(wheelVisualClone.transform);

            StripRuntimeComponents(wheelVisualClone);
            NormalizeDetachedWheelVisual(wheelRoot, wheelVisualClone.transform, targetWheelRadius);
            return wheelRoot;
        }

        private static void NormalizeHierarchyScale(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Vector3 scale = root.localScale;
            root.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            for (int i = 0; i < root.childCount; i++)
            {
                NormalizeHierarchyScale(root.GetChild(i));
            }
        }

        private static bool HasMirroredParity(Transform sourceWheel)
        {
            if (sourceWheel == null)
            {
                return false;
            }

            Vector3 lossy = sourceWheel.lossyScale;
            float parity = Mathf.Sign(lossy.x) * Mathf.Sign(lossy.y) * Mathf.Sign(lossy.z);
            return parity < 0f;
        }

        private static Transform FindBestWheelMeshTransform(Transform sourceWheel)
        {
            if (sourceWheel == null)
            {
                return null;
            }

            Renderer[] renderers = sourceWheel.GetComponentsInChildren<Renderer>(true);
            Transform best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                // Optimization: Skip things that are clearly not wheels (discs, rotors, calipers, hubs)
                string normalizedName = NormalizeName(renderer.gameObject.name);
                if (normalizedName.Contains("disc") || normalizedName.Contains("rotor") || 
                    normalizedName.Contains("caliper") || normalizedName.Contains("hub") || 
                    normalizedName.Contains("brake"))
                {
                    continue;
                }

                // FOR PLAYER CARS: Prioritize LOD0. If it's a higher LOD, skip it unless desperate.
                if (normalizedName.Contains("lod") && !normalizedName.Contains("lod0"))
                {
                    continue;
                }

                Transform candidate = renderer.transform;
                if (IsExcludedWheelDecoration(normalizedName))
                {
                    continue;
                }

                float radius = Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y, renderer.bounds.extents.z);
                
                // Thickness Check: A real wheel is thick. A brake disc/rotor is flat.
                // This is the absolute best way to fix the 'Solstice Blobs'.
                float thickness = Mathf.Min(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z);
                if (thickness < 0.12f && !normalizedName.Contains("rim") && !normalizedName.Contains("tire") && !normalizedName.Contains("wheel"))
                {
                    continue;
                }

                float score = radius;

                if (normalizedName.Contains("wheel")) score += 10f;
                if (normalizedName.Contains("tire") || normalizedName.Contains("tyre") || normalizedName.Contains("rim")) score += 5f;
                if (normalizedName.Contains("lod0")) score += 4f;
                else if (normalizedName.Contains("lod1")) score += 3f;
                else if (normalizedName.Contains("lod2")) score += 2f;
                else if (normalizedName.Contains("lod3")) score += 1f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static void NormalizeDetachedWheelVisual(Transform wheelRoot, Transform wheelClone, float targetWheelRadius)
        {
            if (wheelRoot == null || wheelClone == null)
            {
                return;
            }

            // Only force-center the pivot if there is a significant offset (> 1cm).
            if (TryGetCombinedRendererBoundsRelativeTo(wheelRoot, wheelClone, out Bounds bounds))
            {
                if (bounds.center.magnitude > 0.01f)
                {
                    wheelClone.localPosition -= bounds.center;
                }
            }

            // DYNAMIC FIT: We no longer force-scale the visuals. 
            // Instead, we moved the physics (WheelColliders) to match the visual size.
            // This keeps the tires looking exactly as modeled.
        }

        private static bool TryGetCombinedRendererBoundsRelativeTo(Transform relativeTo, Transform root, out Bounds bounds)
        {
            bounds = default;
            if (relativeTo == null || root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!TryGetRendererBoundsRelativeTo(relativeTo, renderer, out Bounds rendererBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds.min);
                    bounds.Encapsulate(rendererBounds.max);
                }
            }

            return hasBounds;
        }

        // ------------------------------------------------------------------
        // Vehicle body alignment
        // ------------------------------------------------------------------

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
            FitImportedVehicleScale(visualRoot, frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel,
                frontLeftTarget, frontRightTarget, rearLeftTarget, rearRightTarget, targetWheelRadius);

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

                if (!TryGetWheelCenterLocal(visualRoot, wheelAnchors[i], out Vector3 wheelCenter))
                {
                    continue;
                }

                importedCenter += wheelCenter;
                targetCenter += targetPositions[i];
                count++;
            }

            visualRoot.localPosition = count == 0
                ? new Vector3(0f, 0.08f, 0f)
                : (targetCenter / count) - (importedCenter / count);
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
            float verticalScale = 1f;
            if (TryGetAverageWheelRadius(frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel, out float importedWheelRadius) && importedWheelRadius > 0.001f)
            {
                // We shouldn't balloon the whole car just because the wheels are different.
                // We keep the vertical scale near 1.0 unless the model is extreme.
                wheelRadiusScale = Mathf.Clamp(targetWheelRadius / importedWheelRadius, 0.85f, 1.15f);
                verticalScale = wheelRadiusScale;
            }

            // Trust the model's natural wheelbase and track.
            // We only apply very subtle scaling to keep things within a sane range.
            visualRoot.localScale = new Vector3(
                Mathf.Clamp(trackScale, 0.85f, 1.15f),
                Mathf.Clamp(verticalScale, 0.85f, 1.15f),
                Mathf.Clamp(wheelbaseScale, 0.85f, 1.15f));
        }

        // ------------------------------------------------------------------
        // Utility: axle geometry
        // ------------------------------------------------------------------

        private static bool TryGetAxleCenterAndTrack(Transform root, Transform leftWheel, Transform rightWheel, out Vector3 axleCenter, out float trackWidth)
        {
            axleCenter = Vector3.zero;
            trackWidth = 0f;

            if (root == null || leftWheel == null || rightWheel == null)
            {
                return false;
            }

            if (!TryGetWheelCenterLocal(root, leftWheel, out Vector3 leftLocal) ||
                !TryGetWheelCenterLocal(root, rightWheel, out Vector3 rightLocal))
            {
                return false;
            }

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
                if (wheels[i] == null || !TryGetCombinedLocalRendererBounds(wheels[i], out Bounds bounds))
                {
                    continue;
                }

                radiusSum += Mathf.Max(bounds.extents.y, bounds.extents.z);
                count++;
            }

            averageRadius = count > 0 ? radiusSum / count : 0f;
            return count > 0;
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

        private static bool TryGetCombinedLocalRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!TryGetRendererBoundsRelativeTo(root, renderer, out Bounds rendererBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds.min);
                    bounds.Encapsulate(rendererBounds.max);
                }
            }

            return hasBounds;
        }

        private static bool TryGetWheelCenterLocal(Transform relativeTo, Transform wheel, out Vector3 localCenter)
        {
            localCenter = Vector3.zero;
            if (relativeTo == null || wheel == null)
            {
                return false;
            }

            if (TryGetCombinedLocalRendererBounds(wheel, out Bounds wheelBounds))
            {
                localCenter = relativeTo.InverseTransformPoint(wheel.TransformPoint(wheelBounds.center));
                return true;
            }

            localCenter = relativeTo.InverseTransformPoint(wheel.position);
            return true;
        }

        private static bool TryGetRendererBoundsRelativeTo(Transform relativeTo, Renderer renderer, out Bounds bounds)
        {
            bounds = default;
            if (relativeTo == null || renderer == null)
            {
                return false;
            }

            if (TryGetRendererLocalMeshBounds(renderer, out Bounds meshBounds))
            {
                Matrix4x4 toRelative = relativeTo.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                bool hasPoint = false;

                foreach (Vector3 corner in GetBoundsCorners(meshBounds))
                {
                    Vector3 point = toRelative.MultiplyPoint3x4(corner);
                    if (!hasPoint)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }

                return hasPoint;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 worldCenter = worldBounds.center;
            Vector3 worldExtents = worldBounds.extents;
            Vector3[] worldCorners =
            {
                worldCenter + new Vector3(-worldExtents.x, -worldExtents.y, -worldExtents.z),
                worldCenter + new Vector3(-worldExtents.x, -worldExtents.y,  worldExtents.z),
                worldCenter + new Vector3(-worldExtents.x,  worldExtents.y, -worldExtents.z),
                worldCenter + new Vector3(-worldExtents.x,  worldExtents.y,  worldExtents.z),
                worldCenter + new Vector3( worldExtents.x, -worldExtents.y, -worldExtents.z),
                worldCenter + new Vector3( worldExtents.x, -worldExtents.y,  worldExtents.z),
                worldCenter + new Vector3( worldExtents.x,  worldExtents.y, -worldExtents.z),
                worldCenter + new Vector3( worldExtents.x,  worldExtents.y,  worldExtents.z)
            };

            bool initialized = false;
            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector3 point = relativeTo.InverseTransformPoint(worldCorners[i]);
                if (!initialized)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }

            return initialized;
        }

        private static bool TryGetRendererLocalMeshBounds(Renderer renderer, out Bounds bounds)
        {
            bounds = default;
            if (renderer == null)
            {
                return false;
            }

            if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
            {
                bounds = skinnedRenderer.sharedMesh.bounds;
                return true;
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                bounds = meshFilter.sharedMesh.bounds;
                return true;
            }

            return false;
        }

        private static IEnumerable<Vector3> GetBoundsCorners(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            yield return center + new Vector3(-extents.x, -extents.y, -extents.z);
            yield return center + new Vector3(-extents.x, -extents.y,  extents.z);
            yield return center + new Vector3(-extents.x,  extents.y, -extents.z);
            yield return center + new Vector3(-extents.x,  extents.y,  extents.z);
            yield return center + new Vector3( extents.x, -extents.y, -extents.z);
            yield return center + new Vector3( extents.x, -extents.y,  extents.z);
            yield return center + new Vector3( extents.x,  extents.y, -extents.z);
            yield return center + new Vector3( extents.x,  extents.y,  extents.z);
        }

        // ------------------------------------------------------------------
        // Renderer management
        // ------------------------------------------------------------------

        private static void DisableWheelRenderers(Transform sourceWheel, bool shouldDisable)
        {
            if (sourceWheel == null || !shouldDisable)
            {
                return;
            }

            Renderer[] renderers = sourceWheel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        private static bool HasValidDetachedWheelVisual(Transform wheelRoot)
        {
            if (wheelRoot == null || wheelRoot.childCount == 0)
            {
                return false;
            }

            return !ContainsExcludedWheelDecoration(wheelRoot) && TryGetCombinedRendererBounds(wheelRoot, out _);
        }

        private static bool ContainsExcludedWheelDecoration(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            if (IsExcludedWheelDecoration(NormalizeName(root.name)))
            {
                return true;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                if (ContainsExcludedWheelDecoration(root.GetChild(i)))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        // Material normalization
        // ------------------------------------------------------------------

        private void ApplyPlayerCarLODPolicy(Transform root, bool isGarageShowroom)
        {
            if (root == null)
            {
                return;
            }

            // Player cars should never fall to ultra-low fallback LODs.
            // This fixes showroom/world cases where imported cars collapse into
            // a melted shell because Unity selects a distant LOD unexpectedly.
            LODGroup[] lodGroups = root.GetComponentsInChildren<LODGroup>(true);
            for (int i = 0; i < lodGroups.Length; i++)
            {
                LODGroup lodGroup = lodGroups[i];
                if (lodGroup == null)
                {
                    continue;
                }

                lodGroup.enabled = true;
                lodGroup.animateCrossFading = false;
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.ForceLOD(0);
                lodGroup.RecalculateBounds();
            }
        }

        private static void NormalizeImportedMaterials(GameObject root, string carId, string displayName, bool isGarageShowroom)
        {
            if (root == null)
            {
                return;
            }

            // Protect unsupported free assets from aggressive material remapping.
            // This keeps Solstice-style imports from collapsing into a flat blob,
            // while preserving normalization on vetted packs such as RMCar26 / InvoGames.
            if (!ShouldNormalizeCarMaterials(carId, displayName))
            {
                return;
            }

            Shader targetShader = Shader.Find("HDRP/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

            if (targetShader == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                // FIX: Inspect sharedMaterials first to avoid creating instances
                // on renderers that don't need normalization at all. The old code
                // called renderer.materials unconditionally which (a) created
                // blank instanced materials even when nothing needed to change,
                // and (b) lost texture references on those fresh instances.
                Material[] sharedMaterials = renderer.sharedMaterials;
                bool anyNeedsNormalization = false;
                for (int si = 0; si < sharedMaterials.Length; si++)
                {
                    if (ShouldNormalizeMaterial(sharedMaterials[si], targetShader, carId))
                    {
                        anyNeedsNormalization = true;
                        break;
                    }
                }

                if (!anyNeedsNormalization)
                {
                    continue;
                }

                // Now safe to allocate instanced materials — we confirmed at least
                // one slot on this renderer needs to be replaced.
                Material[] materials = renderer.materials;
                bool hasChanges = false;

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material sourceMaterial = materialIndex < sharedMaterials.Length && sharedMaterials[materialIndex] != null
                        ? sharedMaterials[materialIndex]
                        : materials[materialIndex];

                    if (sourceMaterial == null)
                    {
                        continue;
                    }

                    if (!ShouldNormalizeMaterial(sourceMaterial, targetShader, carId))
                    {
                        continue;
                    }

                    materials[materialIndex] = CreateNormalizedMaterial(sourceMaterial, targetShader, carId, isGarageShowroom);
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    renderer.materials = materials;
                }
            }
        }

        private static Material CreateNormalizedMaterial(Material sourceMaterial, Shader targetShader, string carId, bool isGarageShowroom)
        {
            if (sourceMaterial == null || targetShader == null)
            {
                return sourceMaterial;
            }

            Texture baseColorMap = GetMaterialTexture(sourceMaterial, "_BaseColorMap", "_BaseMap", "_MainTex");
            Texture normalMap = GetMaterialTexture(sourceMaterial, "_NormalMap", "_BumpMap");
            Texture emissiveMap = GetMaterialTexture(sourceMaterial, "_EmissiveColorMap", "_EmissionMap");
            Texture maskMap = GetMaterialTexture(sourceMaterial, "_MaskMap", "_MetallicGlossMap");
            Color baseColor = GetMaterialColor(sourceMaterial, "_BaseColor", "_Color");
            Color emissiveColor = GetMaterialColor(sourceMaterial, "_EmissiveColor", "_EmissionColor");
            float metallic = GetMaterialFloat(sourceMaterial, 0f, "_Metallic");
            float smoothness = Mathf.Clamp(GetMaterialFloat(sourceMaterial, 0.55f, "_Smoothness", "_Glossiness"), 0.15f, 0.95f);
            float normalScale = GetMaterialFloat(sourceMaterial, 1f, "_NormalScale", "_BumpScale");
            bool hasEmissiveMap = emissiveMap != null;
            bool isRmCar = IsRmCarFamily(carId);
            bool isPaintMaterial = IsPaintMaterial(sourceMaterial.name) || IsKnownRosterPaintMaterial(sourceMaterial.name, carId);
            bool isBodyMaterial = IsBodyMaterial(sourceMaterial.name) || IsKnownRosterBodyMaterial(sourceMaterial.name, carId);
            bool isTransparent = IsTransparentMaterial(sourceMaterial.name);

            baseColor.r = Mathf.Clamp01(baseColor.r);
            baseColor.g = Mathf.Clamp01(baseColor.g);
            baseColor.b = Mathf.Clamp01(baseColor.b);
            baseColor.a = Mathf.Clamp01(baseColor.a <= 0f ? 1f : baseColor.a);

            // REMOVED: All hardcoded overrides that were clamping metallic and smoothness.
            // We now honor the values coming from the source material assets directly.

            if (hasEmissiveMap)
            {
                emissiveColor *= 0.12f;
                emissiveColor.a = 1f;
            }
            // If no map, we respect the base emissive color as configured by the user.
            // If color is set manually but no map, we keep the color as is.

            Material normalizedMaterial = new Material(targetShader)
            {
                name = sourceMaterial.name
            };

            if (normalizedMaterial.HasProperty("_BaseColor")) normalizedMaterial.SetColor("_BaseColor", baseColor);
            if (normalizedMaterial.HasProperty("_Color")) normalizedMaterial.SetColor("_Color", baseColor);

            if (baseColorMap != null)
            {
                if (normalizedMaterial.HasProperty("_BaseColorMap")) normalizedMaterial.SetTexture("_BaseColorMap", baseColorMap);
                if (normalizedMaterial.HasProperty("_BaseMap")) normalizedMaterial.SetTexture("_BaseMap", baseColorMap);
                if (normalizedMaterial.HasProperty("_MainTex")) normalizedMaterial.SetTexture("_MainTex", baseColorMap);
            }

            if (normalMap != null)
            {
                if (normalizedMaterial.HasProperty("_NormalMap")) normalizedMaterial.SetTexture("_NormalMap", normalMap);
                if (normalizedMaterial.HasProperty("_BumpMap")) normalizedMaterial.SetTexture("_BumpMap", normalMap);
            }

            if (normalizedMaterial.HasProperty("_NormalScale")) normalizedMaterial.SetFloat("_NormalScale", normalScale);
            if (normalizedMaterial.HasProperty("_BumpScale")) normalizedMaterial.SetFloat("_BumpScale", normalScale);
            if (maskMap != null && normalizedMaterial.HasProperty("_MaskMap")) normalizedMaterial.SetTexture("_MaskMap", maskMap);

            if (emissiveMap != null)
            {
                if (normalizedMaterial.HasProperty("_EmissiveColorMap")) normalizedMaterial.SetTexture("_EmissiveColorMap", emissiveMap);
                if (normalizedMaterial.HasProperty("_EmissionMap")) normalizedMaterial.SetTexture("_EmissionMap", emissiveMap);
            }

            if (normalizedMaterial.HasProperty("_EmissiveColor")) normalizedMaterial.SetColor("_EmissiveColor", emissiveColor);
            if (normalizedMaterial.HasProperty("_EmissionColor")) normalizedMaterial.SetColor("_EmissionColor", emissiveColor);
            if (normalizedMaterial.HasProperty("_Metallic")) normalizedMaterial.SetFloat("_Metallic", metallic);
            if (normalizedMaterial.HasProperty("_Smoothness")) normalizedMaterial.SetFloat("_Smoothness", smoothness);
            if (normalizedMaterial.HasProperty("_SurfaceType")) normalizedMaterial.SetFloat("_SurfaceType", 0f);
            if (normalizedMaterial.HasProperty("_Surface")) normalizedMaterial.SetFloat("_Surface", 0f);
            if (normalizedMaterial.HasProperty("_ReceivesSSR")) normalizedMaterial.SetFloat("_ReceivesSSR", 1f);
            if (normalizedMaterial.HasProperty("_ReceivesSSRTransparent")) normalizedMaterial.SetFloat("_ReceivesSSRTransparent", 0f);
            if (normalizedMaterial.HasProperty("_EnvironmentReflections")) normalizedMaterial.SetFloat("_EnvironmentReflections", 1f);
            if (normalizedMaterial.HasProperty("_GlossyReflections")) normalizedMaterial.SetFloat("_GlossyReflections", 1f);
            if (normalizedMaterial.HasProperty("_SpecularHighlights")) normalizedMaterial.SetFloat("_SpecularHighlights", 1f);
            if (normalizedMaterial.HasProperty("_TransmissionEnable")) normalizedMaterial.SetFloat("_TransmissionEnable", 0f);
            if (normalizedMaterial.HasProperty("_EnableCoat")) normalizedMaterial.SetFloat("_EnableCoat", sourceMaterial.HasProperty("_EnableCoat") ? sourceMaterial.GetFloat("_EnableCoat") : (isPaintMaterial || isBodyMaterial ? 1f : 0f));
            if (normalizedMaterial.HasProperty("_CoatMask")) normalizedMaterial.SetFloat("_CoatMask", sourceMaterial.HasProperty("_CoatMask") ? sourceMaterial.GetFloat("_CoatMask") : (isPaintMaterial ? 0.9f : 0f));

            if (isTransparent)
            {
                if (normalizedMaterial.HasProperty("_SurfaceType")) normalizedMaterial.SetFloat("_SurfaceType", 1f); // Transparent
                if (normalizedMaterial.HasProperty("_BlendMode")) normalizedMaterial.SetFloat("_BlendMode", 0f);    // Alpha
                
                // GLASS IS NOT METAL: Force metallic to 0 to stop the "mirror" look.
                metallic = 0f;
                if (normalizedMaterial.HasProperty("_Metallic")) normalizedMaterial.SetFloat("_Metallic", metallic);
                
                // Lower alpha (0.18) makes glass look translucent.
                baseColor.a = 0.18f;
                if (normalizedMaterial.HasProperty("_BaseColor")) normalizedMaterial.SetColor("_BaseColor", baseColor);
                
                // Reduced smoothness prevents the 'mirror' look.
                if (normalizedMaterial.HasProperty("_Smoothness")) normalizedMaterial.SetFloat("_Smoothness", 0.92f);
                
                normalizedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                normalizedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            normalizedMaterial.DisableKeyword("_DISABLE_SSR");
            normalizedMaterial.DisableKeyword("_DISABLE_SSR_TRANSPARENT");

            return normalizedMaterial;
        }

        private static bool ShouldNormalizeMaterial(Material sourceMaterial, Shader targetShader, string carId)
        {
            if (sourceMaterial == null || targetShader == null)
            {
                return false;
            }

            if (IsLightMaterial(sourceMaterial.name))
            {
                return false;
            }

            // FIX: The old code had `sourceMaterial.shader != targetShader` as a catch-all
            // that normalized EVERY material whose shader didn't match — including city
            // geometry, road surfaces, and props from third-party packs that ship with
            // non-HDRP shaders. This coated everything in high-smoothness car paint.
            //
            // Now: only normalize materials whose NAME explicitly identifies them as
            // vehicle paint, body panels, or a known per-car roster material.
            // Shader mismatch alone is no longer sufficient reason to normalize.
            return IsPaintMaterial(sourceMaterial.name) ||
                   IsBodyMaterial(sourceMaterial.name) ||
                   IsKnownRosterPaintMaterial(sourceMaterial.name, carId) ||
                   IsKnownRosterBodyMaterial(sourceMaterial.name, carId);
        }

        private static bool IsRmCarFamily(string carId)
        {
            return !string.IsNullOrEmpty(carId) && carId.StartsWith("RMCar26", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPaintMaterial(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
            {
                return false;
            }

            // FIX: "color" and "atlas" removed — these matched city/prop materials.
            // "colour" (British spelling) is retained as it's car-specific.
            // "color" alone is far too generic (e.g. "color_building", "color_road").
            // "atlas" matched every texture atlas sheet used by level geometry.
            return materialName.IndexOf("paint", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("colour", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("police", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("taxi", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBodyMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("body", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car_color", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car colour", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car color", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("traffic-car", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsKnownRosterPaintMaterial(string materialName, string carId)
        {
            if (string.IsNullOrEmpty(materialName) || string.IsNullOrEmpty(carId))
            {
                return false;
            }

            string normalizedCarId = NormalizeName(carId);
            string normalizedMaterialName = NormalizeName(materialName);
            if (string.IsNullOrEmpty(normalizedMaterialName))
            {
                return false;
            }

            if (normalizedCarId == "simpleretrocar")
            {
                return normalizedMaterialName == "plane" ||
                       normalizedMaterialName == "lightblue" ||
                       normalizedMaterialName == "lightbrown";
            }

            if (normalizedCarId.StartsWith("arcadecar"))
            {
                return normalizedMaterialName.StartsWith("body");
            }

            if (normalizedCarId.StartsWith("americansedan"))
            {
                return normalizedMaterialName.Contains("carcolor") ||
                       normalizedMaterialName == "taximat" ||
                       normalizedMaterialName == "policemat";
            }

            return false;
        }

        private static bool IsKnownRosterBodyMaterial(string materialName, string carId)
        {
            if (string.IsNullOrEmpty(materialName) || string.IsNullOrEmpty(carId))
            {
                return false;
            }

            string normalizedCarId = NormalizeName(carId);
            string normalizedMaterialName = NormalizeName(materialName);
            if (string.IsNullOrEmpty(normalizedMaterialName))
            {
                return false;
            }

            if (normalizedCarId.StartsWith("arcadecar"))
            {
                return normalizedMaterialName.StartsWith("body");
            }

            if (normalizedCarId.StartsWith("americansedan"))
            {
                return normalizedMaterialName.Contains("carcolor");
            }

            return false;
        }

        private static bool IsTransparentMaterial(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
            {
                return false;
            }

            string lower = materialName.ToLowerInvariant();
            return lower.Contains("glass") || 
                   lower.Contains("window") || 
                   lower.Contains("screen") || 
                   lower.Contains("transparent") ||
                   lower.Contains("windshield");
        }

        private static bool IsLightMaterial(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
            {
                return false;
            }

            string normalizedName = NormalizeName(materialName);
            if (string.IsNullOrEmpty(normalizedName) ||
                normalizedName == "lightblue" ||
                normalizedName == "lightbrown" ||
                normalizedName == "lightgray" ||
                normalizedName == "lightgrey" ||
                normalizedName == "lightgreen" ||
                normalizedName == "lightred")
            {
                return false;
            }

            return normalizedName == "light" ||
                   normalizedName.Contains("headlight") ||
                   normalizedName.Contains("taillight") ||
                   normalizedName.Contains("brakelight") ||
                   normalizedName.Contains("reverselight") ||
                   normalizedName.Contains("foglight") ||
                   normalizedName.Contains("indicator") ||
                   normalizedName.Contains("turnsignal") ||
                   normalizedName.Contains("illumination") ||
                   normalizedName.Contains("emissive") ||
                   normalizedName.Contains("lamp");
        }

        // ------------------------------------------------------------------
        // Generic wheel discovery (fallback for cars without authored mapping)
        // ------------------------------------------------------------------

        private static Transform FindWheelSource(Transform root, params string[] tokens)
        {
            if (root == null)
            {
                return null;
            }

            string normalizedName = NormalizeName(root.name);
            if (IsExcludedWheelDecoration(normalizedName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindWheelSource(root.GetChild(i), tokens);
                if (result != null)
                {
                    return result;
                }
            }

            if (MatchesWheelName(normalizedName, tokens) && HasUsableWheelRenderer(root))
            {
                return root;
            }

            return null;
        }

        private static bool HasUsableWheelRenderer(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!IsExcludedWheelDecoration(NormalizeName(renderer.transform.name)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static bool MatchesWheelName(string normalizedName, string[] tokens)
        {
            if (string.IsNullOrEmpty(normalizedName) || tokens == null || tokens.Length == 0)
            {
                return false;
            }

            bool containsWheel = normalizedName.Contains("wheel");
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (normalizedName == token) return true;
                if (containsWheel && normalizedName.Contains(token)) return true;
                if (normalizedName.EndsWith(token)) return true;
            }

            return false;
        }

        private static bool IsExcludedWheelDecoration(string normalizedName)
        {
            if (string.IsNullOrEmpty(normalizedName))
            {
                return false;
            }

            return normalizedName.Contains("brake")
                || normalizedName.Contains("rotor")
                || normalizedName.Contains("caliper")
                || normalizedName.Contains("steeringwheel");
        }

        private static void DisableNonVisualHelperRenderers(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (IsNonVisualHelperRenderer(renderer))
                {
                    renderer.enabled = false;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }

        private static bool IsNonVisualHelperRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            string rendererName = NormalizeName(renderer.name);
            string objectName = NormalizeName(renderer.gameObject.name);

            string meshName = string.Empty;
            if (renderer.TryGetComponent<MeshFilter>(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            {
                meshName = NormalizeName(meshFilter.sharedMesh.name);
            }

            string materialName = string.Empty;
            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial != null)
            {
                materialName = NormalizeName(sharedMaterial.name);
            }

            bool nameLooksLikeHelper =
                ContainsAny(rendererName, "bodycol", "meshcol", "collision", "collider", "hitbox", "proxy", "bounds", "shellcol") ||
                ContainsAny(objectName, "bodycol", "meshcol", "collision", "collider", "hitbox", "proxy", "bounds", "shellcol") ||
                ContainsAny(meshName, "bodycol", "meshcol", "collision", "collider", "hitbox", "proxy", "bounds", "shellcol");

            bool exactBodyColCase =
                rendererName == "rmcar26bodycol" ||
                objectName == "rmcar26bodycol" ||
                meshName == "rmcar26bodycol";

            // If a helper mesh got remapped to a paint material, still hide it by name/mesh.
            return nameLooksLikeHelper || exactBodyColCase;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrEmpty(value) || needles == null)
            {
                return false;
            }

            for (int i = 0; i < needles.Length; i++)
            {
                string needle = needles[i];
                if (!string.IsNullOrEmpty(needle) && value.Contains(needle))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        // Component stripping
        // ------------------------------------------------------------------

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

                if (component is Collider collider) { collider.enabled = false; continue; }
                if (component is Rigidbody rigidbody) { rigidbody.isKinematic = true; rigidbody.useGravity = false; rigidbody.detectCollisions = false; continue; }
                if (component is Joint joint) { joint.enableCollision = false; continue; }
                if (component is AudioSource audioSource) { audioSource.Stop(); audioSource.enabled = false; continue; }
                if (component is Camera camera) { camera.enabled = false; continue; }
                if (component is Light light) { light.enabled = false; continue; }
                if (component is Animator animator) { animator.enabled = false; continue; }
                if (component is MonoBehaviour behaviour) { behaviour.enabled = false; continue; }
            }
        }

        // ------------------------------------------------------------------
        // Material/texture helpers
        // ------------------------------------------------------------------

        private static Texture GetMaterialTexture(Material material, params string[] propertyNames)
        {
            Texture runtimeTexture = GetFirstTexture(material, propertyNames);
            if (runtimeTexture != null)
            {
                return runtimeTexture;
            }

#if UNITY_EDITOR
            if (TryReadSerializedTexture(material, propertyNames, out Texture serializedTexture))
            {
                return serializedTexture;
            }
#endif

            return null;
        }

        private static Color GetMaterialColor(Material material, params string[] propertyNames)
        {
            if (material == null || propertyNames == null)
            {
                return Color.white;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (!string.IsNullOrEmpty(propertyName) && material.HasProperty(propertyName))
                {
                    return material.GetColor(propertyName);
                }
            }

#if UNITY_EDITOR
            if (TryReadSerializedColor(material, propertyNames, out Color serializedColor))
            {
                return serializedColor;
            }
#endif

            return Color.white;
        }

        private static float GetMaterialFloat(Material material, float fallbackValue, params string[] propertyNames)
        {
            if (material == null || propertyNames == null)
            {
                return fallbackValue;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (!string.IsNullOrEmpty(propertyName) && material.HasProperty(propertyName))
                {
                    return material.GetFloat(propertyName);
                }
            }

#if UNITY_EDITOR
            if (TryReadSerializedFloat(material, propertyNames, out float serializedFloat))
            {
                return serializedFloat;
            }
#endif

            return fallbackValue;
        }

        private static Texture GetFirstTexture(Material material, params string[] propertyNames)
        {
            if (material == null || propertyNames == null)
            {
                return null;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (!string.IsNullOrEmpty(propertyName) && material.HasProperty(propertyName))
                {
                    Texture value = material.GetTexture(propertyName);
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private static bool TryReadSerializedTexture(Material material, string[] propertyNames, out Texture texture)
        {
            texture = null;
            if (!TryLoadSerializedMaterialText(material, out string materialText))
            {
                return false;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (!TryExtractPropertyBlock(materialText, propertyNames[i], out string block))
                {
                    continue;
                }

                string guid = ExtractGuid(block);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                if (texture != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadSerializedColor(Material material, string[] propertyNames, out Color color)
        {
            color = Color.white;
            if (!TryLoadSerializedMaterialText(material, out string materialText))
            {
                return false;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (string.IsNullOrEmpty(propertyName))
                {
                    continue;
                }

                string marker = $"- {propertyName}: {{r:";
                int index = materialText.IndexOf(marker, System.StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                int endIndex = materialText.IndexOf('}', index);
                if (endIndex < 0)
                {
                    continue;
                }

                string line = materialText.Substring(index, endIndex - index + 1);
                if (TryParseColorLine(line, out color))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadSerializedFloat(Material material, string[] propertyNames, out float value)
        {
            value = 0f;
            if (!TryLoadSerializedMaterialText(material, out string materialText))
            {
                return false;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (string.IsNullOrEmpty(propertyName))
                {
                    continue;
                }

                string marker = $"- {propertyName}: ";
                int index = materialText.IndexOf(marker, System.StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                int valueStart = index + marker.Length;
                int valueEnd = materialText.IndexOf('\n', valueStart);
                if (valueEnd < 0)
                {
                    valueEnd = materialText.Length;
                }

                string rawValue = materialText.Substring(valueStart, valueEnd - valueStart).Trim();
                if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryLoadSerializedMaterialText(Material material, out string materialText)
        {
            materialText = null;
            if (material == null)
            {
                return false;
            }

            string materialPath = ResolveSerializedMaterialPath(material);
            if (string.IsNullOrEmpty(materialPath))
            {
                return false;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return false;
            }

            string normalizedAssetPath = materialPath.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(projectRoot, normalizedAssetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            materialText = File.ReadAllText(fullPath);
            return !string.IsNullOrEmpty(materialText);
        }

        private static string ResolveSerializedMaterialPath(Material material)
        {
            string materialPath = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(materialPath) && string.Equals(Path.GetExtension(materialPath), ".mat", System.StringComparison.OrdinalIgnoreCase))
            {
                return materialPath;
            }

            if (string.IsNullOrEmpty(material.name))
            {
                return null;
            }

            string[] searchFolders =
            {
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Materials",
                "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Ground"
            };

            string[] candidateNames = BuildMaterialSearchNames(material.name);
            for (int nameIndex = 0; nameIndex < candidateNames.Length; nameIndex++)
            {
                string candidateName = candidateNames[nameIndex];
                if (string.IsNullOrEmpty(candidateName))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets($"{candidateName} t:Material", searchFolders);
                for (int i = 0; i < guids.Length; i++)
                {
                    string candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(candidatePath))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileNameWithoutExtension(candidatePath);
                    if (string.Equals(fileName, candidateName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidatePath;
                    }
                }
            }

            return null;
        }

        private static string[] BuildMaterialSearchNames(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName))
            {
                return System.Array.Empty<string>();
            }

            List<string> names = new List<string>();
            AddMaterialSearchName(names, materialName);

            const string instanceSuffix = " (Instance)";
            if (materialName.EndsWith(instanceSuffix, System.StringComparison.OrdinalIgnoreCase))
            {
                AddMaterialSearchName(names, materialName.Substring(0, materialName.Length - instanceSuffix.Length));
            }

            int parenthesisIndex = materialName.IndexOf(" (", System.StringComparison.Ordinal);
            if (parenthesisIndex > 0)
            {
                AddMaterialSearchName(names, materialName.Substring(0, parenthesisIndex));
            }

            return names.ToArray();
        }

        private static void AddMaterialSearchName(List<string> names, string candidate)
        {
            string normalized = string.IsNullOrWhiteSpace(candidate) ? null : candidate.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], normalized, System.StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            names.Add(normalized);
        }

        private static bool TryExtractPropertyBlock(string materialText, string propertyName, out string block)
        {
            block = null;
            if (string.IsNullOrEmpty(materialText) || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            string marker = $"- {propertyName}:";
            int start = materialText.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            int nextProperty = materialText.IndexOf("\n    - ", start + marker.Length, System.StringComparison.Ordinal);
            if (nextProperty < 0)
            {
                nextProperty = materialText.Length;
            }

            block = materialText.Substring(start, nextProperty - start);
            return true;
        }

        private static string ExtractGuid(string block)
        {
            if (string.IsNullOrEmpty(block))
            {
                return null;
            }

            string marker = "guid:";
            int guidIndex = block.IndexOf(marker, System.StringComparison.Ordinal);
            if (guidIndex < 0)
            {
                return null;
            }

            int start = guidIndex + marker.Length;
            int end = block.IndexOf(',', start);
            if (end < 0) end = block.IndexOf('\n', start);
            if (end < 0) end = block.Length;

            string guid = block.Substring(start, end - start).Trim();
            return guid == "00000000000000000000000000000000" ? null : guid;
        }

        private static bool TryParseColorLine(string line, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            return TryExtractColorComponent(line, "r:", out float r)
                && TryExtractColorComponent(line, "g:", out float g)
                && TryExtractColorComponent(line, "b:", out float b)
                && TryExtractColorComponent(line, "a:", out float a)
                && SetColor(out color, r, g, b, a);
        }

        private static bool TryExtractColorComponent(string line, string marker, out float value)
        {
            value = 0f;
            int start = line.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            start += marker.Length;
            int end = line.IndexOf(',', start);
            if (end < 0) end = line.IndexOf('}', start);
            if (end < 0) end = line.Length;

            string raw = line.Substring(start, end - start).Trim();
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool SetColor(out Color color, float r, float g, float b, float a)
        {
            color = new Color(r, g, b, a);
            return true;
        }
#endif
    }
}
