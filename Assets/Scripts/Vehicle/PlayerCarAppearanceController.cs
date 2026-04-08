using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Underground.Garage;
using Underground.Save;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Underground.Vehicle
{
    public class PlayerCarAppearanceController : MonoBehaviour
    {
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
                return true;
            }

            DestroyExistingVisuals();

            GameObject visualInstance = Object.Instantiate(visualPrefab, modelRoot, false);
            visualInstance.name = "ImportedVisual";
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;

            StripRuntimeComponents(visualInstance);

            // ---- Wheel discovery: try authored path first, fall back to generic per-wheel ----
            Transform sourceFrontLeft = TryResolveWheel(visualInstance.transform, definition, definition.WheelMapping?.frontLeftPath, "frontleft", "fl", "FL");
            Transform sourceFrontRight = TryResolveWheel(visualInstance.transform, definition, definition.WheelMapping?.frontRightPath, "frontright", "fr", "FR");
            Transform sourceRearLeft = TryResolveWheel(visualInstance.transform, definition, definition.WheelMapping?.rearLeftPath, "rearleft", "rl", "RL");
            Transform sourceRearRight = TryResolveWheel(visualInstance.transform, definition, definition.WheelMapping?.rearRightPath, "rearright", "rr", "RR");
            bool sharesFrontAxleMesh = sourceFrontLeft != null && sourceFrontLeft == sourceFrontRight;
            bool sharesRearAxleMesh = sourceRearLeft != null && sourceRearLeft == sourceRearRight;

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
                    sourceRearRight);
                frontLeftTarget = frontLeftWheelRoot != null ? frontLeftWheelRoot.localPosition : frontLeftTarget;
                frontRightTarget = frontRightWheelRoot != null ? frontRightWheelRoot.localPosition : frontRightTarget;
                rearLeftTarget = rearLeftWheelRoot != null ? rearLeftWheelRoot.localPosition : rearLeftTarget;
                rearRightTarget = rearRightWheelRoot != null ? rearRightWheelRoot.localPosition : rearRightTarget;
            }

            NormalizeImportedMaterials(visualInstance);

            bool hasResolvedWheelSources =
                sourceFrontLeft != null &&
                sourceFrontRight != null &&
                sourceRearLeft != null &&
                sourceRearRight != null;

            // Determine whether to detach wheel visuals from the body and
            // place them at WheelCollider positions (for steering / rolling).
            // Cars with UseDetachedWheelVisuals == false keep their wheels
            // embedded in the body mesh.
            bool shouldDetachWheels = ShouldDetachWheelVisuals(definition, hasResolvedWheelSources, isGarageShowroom);
            bool useMeshOnlyDetachedWheels = ShouldUseMeshOnlyDetachedWheels(definition, hasResolvedWheelSources, isGarageShowroom);

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
                Transform rearLeftVisual = !sharesRearAxleMesh
                    ? CreateDetachedWheelVisual(rearLeftWheelRoot, sourceRearLeft, targetWheelRadius, useMeshOnlyDetachedWheels, out rearLeftHiddenSource)
                    : null;
                Transform rearRightVisual = !sharesRearAxleMesh
                    ? CreateDetachedWheelVisual(rearRightWheelRoot, sourceRearRight, targetWheelRadius, useMeshOnlyDetachedWheels, out rearRightHiddenSource)
                    : null;

                BindWheelVisuals(frontLeftVisual, frontRightVisual, rearLeftVisual, rearRightVisual);

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
                else if (hasResolvedWheelSources)
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
            AppearanceChanged?.Invoke();
            return true;
        }

        public void SetShowroomPresentationMode(bool enabled)
        {
            showroomPresentationMode = enabled;
        }

        private bool ShouldDetachWheelVisuals(PlayerCarDefinition definition, bool hasResolvedWheelSources, bool isGarageShowroom)
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

            return definition.UseDetachedWheelVisuals;
        }

        private bool ShouldUseMeshOnlyDetachedWheels(PlayerCarDefinition definition, bool hasResolvedWheelSources, bool isGarageShowroom)
        {
            if (!hasResolvedWheelSources || isGarageShowroom)
            {
                return false;
            }

            return false;
        }

        private static bool ShouldMatchGameplayWheelLayoutToSource(PlayerCarDefinition definition, bool isGarageShowroom)
        {
            if (isGarageShowroom || string.IsNullOrEmpty(definition.CarId))
            {
                return false;
            }

            return definition.CarId.StartsWith("arcade_car_");
        }

        private bool IsGarageShowroomContext()
        {
            return showroomPresentationMode || GetComponentInParent<GarageShowroomController>() != null;
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

        /// <summary>
        /// Tries to find a wheel source transform using multiple strategies in order:
        /// 1. Authored path from CarWheelMapping (direct, then recursive last-segment)
        /// 2. Generic token-based search (the original fallback)
        /// Logs diagnostic info when the authored path fails.
        /// </summary>
        private static Transform TryResolveWheel(Transform visualRoot, PlayerCarDefinition definition, string authoredPath, string genericToken1, string genericToken2, string label)
        {
            // Strategy 1: Authored path
            if (!string.IsNullOrEmpty(authoredPath))
            {
                Transform found = FindAuthoredWheel(visualRoot, authoredPath);
                if (found != null)
                {
                    return found;
                }

                Debug.LogWarning($"[PlayerCarAppearanceController] Authored wheel path '{authoredPath}' not found for {definition.CarId} ({label}). Falling back to generic search.");
            }

            // Strategy 2: Generic token search
            Transform generic = FindWheelSource(visualRoot, genericToken1, genericToken2);
            if (generic != null)
            {
                return generic;
            }

            // Total failure — dump hierarchy for diagnostics
            Debug.LogWarning($"[PlayerCarAppearanceController] No wheel found for {definition.CarId} ({label}). Prefab child hierarchy:\n{GetHierarchyDump(visualRoot, 0)}");
            return null;
        }

        /// <summary>
        /// Finds a wheel transform using the authored path.
        /// The path is relative to the visual prefab root and may be nested (e.g. "RMCar26_WheelFrontLeft/Wheel_A").
        /// If the exact path fails, tries a recursive name-match on the last segment.
        /// </summary>
        private static Transform FindAuthoredWheel(Transform visualRoot, string path)
        {
            if (string.IsNullOrEmpty(path) || visualRoot == null)
            {
                return null;
            }

            // Try direct hierarchical path first.
            Transform found = visualRoot.Find(path);
            if (found != null)
            {
                return found;
            }

            // Fall back to searching for the last segment recursively.
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

        private void MatchGameplayWheelLayoutToSource(Transform frontLeftSource, Transform frontRightSource, Transform rearLeftSource, Transform rearRightSource)
        {
            MatchWheelColliderAndRootToSource("FL_Collider", frontLeftWheelRoot, frontLeftSource);
            MatchWheelColliderAndRootToSource("FR_Collider", frontRightWheelRoot, frontRightSource);
            MatchWheelColliderAndRootToSource("RL_Collider", rearLeftWheelRoot, rearLeftSource);
            MatchWheelColliderAndRootToSource("RR_Collider", rearRightWheelRoot, rearRightSource);
        }

        private void MatchWheelColliderAndRootToSource(string colliderName, Transform wheelRoot, Transform sourceWheel)
        {
            if (string.IsNullOrEmpty(colliderName) || sourceWheel == null)
            {
                return;
            }

            if (!TryGetWheelCenterLocal(transform, sourceWheel, out Vector3 sourceCenterLocal))
            {
                return;
            }

            Transform collider = transform.Find($"WheelColliders/{colliderName}");
            if (collider != null)
            {
                collider.localPosition = sourceCenterLocal;
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
            hiddenSource = sourceWheel;

            if (wheelRoot == null || sourceWheel == null)
            {
                return null;
            }

            Transform cloneSource = sourceWheel;
            if (useBestMeshOnly)
            {
                cloneSource = FindBestWheelMeshTransform(sourceWheel) ?? sourceWheel;
                hiddenSource = cloneSource;
            }

            // Default path clones the authored wheel hierarchy to preserve
            // multi-part assemblies. Mesh-only mode is used for packs whose
            // wheel parent transforms pull non-wheel geometry with them.
            GameObject wheelVisualClone = Object.Instantiate(cloneSource.gameObject, wheelRoot, false);
            wheelVisualClone.name = "Mesh";
            wheelVisualClone.transform.localPosition = Vector3.zero;
            wheelVisualClone.transform.localRotation = cloneSource.localRotation;
            bool mirroredHierarchy = HasMirroredParity(cloneSource);
            NormalizeHierarchyScale(wheelVisualClone.transform);

            if (mirroredHierarchy)
            {
                wheelVisualClone.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

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

                Transform candidate = renderer.transform;
                string normalizedName = NormalizeName(candidate.name);
                if (IsExcludedWheelDecoration(normalizedName))
                {
                    continue;
                }

                float radius = Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y, renderer.bounds.extents.z);
                float score = radius;

                if (normalizedName.Contains("wheel"))
                {
                    score += 10f;
                }

                if (normalizedName.Contains("tire") || normalizedName.Contains("tyre") || normalizedName.Contains("rim"))
                {
                    score += 5f;
                }

                if (normalizedName.Contains("lod0"))
                {
                    score += 4f;
                }
                else if (normalizedName.Contains("lod1"))
                {
                    score += 3f;
                }
                else if (normalizedName.Contains("lod2"))
                {
                    score += 2f;
                }
                else if (normalizedName.Contains("lod3"))
                {
                    score += 1f;
                }

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

            if (!TryGetCombinedRendererBoundsRelativeTo(wheelRoot, wheelClone, out Bounds bounds))
            {
                return;
            }

            wheelClone.localPosition -= bounds.center;

            if (!TryGetCombinedRendererBoundsRelativeTo(wheelRoot, wheelClone, out Bounds adjustedBounds))
            {
                return;
            }

            float measuredRadius = Mathf.Max(adjustedBounds.extents.y, adjustedBounds.extents.z);
            if (measuredRadius > 0.001f)
            {
                float uniformScale = targetWheelRadius / measuredRadius;
                wheelClone.localScale *= uniformScale;
            }
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
                worldCenter + new Vector3(-worldExtents.x, -worldExtents.y, worldExtents.z),
                worldCenter + new Vector3(-worldExtents.x, worldExtents.y, -worldExtents.z),
                worldCenter + new Vector3(-worldExtents.x, worldExtents.y, worldExtents.z),
                worldCenter + new Vector3(worldExtents.x, -worldExtents.y, -worldExtents.z),
                worldCenter + new Vector3(worldExtents.x, -worldExtents.y, worldExtents.z),
                worldCenter + new Vector3(worldExtents.x, worldExtents.y, -worldExtents.z),
                worldCenter + new Vector3(worldExtents.x, worldExtents.y, worldExtents.z)
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
            yield return center + new Vector3(-extents.x, -extents.y, extents.z);
            yield return center + new Vector3(-extents.x, extents.y, -extents.z);
            yield return center + new Vector3(-extents.x, extents.y, extents.z);
            yield return center + new Vector3(extents.x, -extents.y, -extents.z);
            yield return center + new Vector3(extents.x, -extents.y, extents.z);
            yield return center + new Vector3(extents.x, extents.y, -extents.z);
            yield return center + new Vector3(extents.x, extents.y, extents.z);
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

        private static void NormalizeImportedMaterials(GameObject root)
        {
            if (root == null)
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

                Material[] materials = renderer.materials;
                Material[] sharedMaterials = renderer.sharedMaterials;
                bool hasChanges = false;

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    if (material.shader == targetShader)
                    {
                        continue;
                    }

                    Material sourceMaterial = materialIndex < sharedMaterials.Length && sharedMaterials[materialIndex] != null
                        ? sharedMaterials[materialIndex]
                        : material;
                    materials[materialIndex] = CreateNormalizedMaterial(sourceMaterial, targetShader);
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    renderer.materials = materials;
                }
            }
        }

        private static Material CreateNormalizedMaterial(Material sourceMaterial, Shader targetShader)
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
            float smoothness = Mathf.Clamp(GetMaterialFloat(sourceMaterial, 0.55f, "_Smoothness", "_Glossiness"), 0.15f, 0.72f);
            float normalScale = GetMaterialFloat(sourceMaterial, 1f, "_NormalScale", "_BumpScale");
            bool hasEmissiveMap = emissiveMap != null;

            baseColor.r = Mathf.Clamp01(baseColor.r);
            baseColor.g = Mathf.Clamp01(baseColor.g);
            baseColor.b = Mathf.Clamp01(baseColor.b);
            baseColor.a = Mathf.Clamp01(baseColor.a <= 0f ? 1f : baseColor.a);

            if (!hasEmissiveMap)
            {
                emissiveColor = Color.black;
            }
            else
            {
                emissiveColor *= 0.12f;
                emissiveColor.a = 1f;
            }

            Material normalizedMaterial = new Material(targetShader)
            {
                name = sourceMaterial.name
            };

            if (normalizedMaterial.HasProperty("_BaseColor"))
            {
                normalizedMaterial.SetColor("_BaseColor", baseColor);
            }

            if (normalizedMaterial.HasProperty("_Color"))
            {
                normalizedMaterial.SetColor("_Color", baseColor);
            }

            if (baseColorMap != null)
            {
                if (normalizedMaterial.HasProperty("_BaseColorMap"))
                {
                    normalizedMaterial.SetTexture("_BaseColorMap", baseColorMap);
                }

                if (normalizedMaterial.HasProperty("_BaseMap"))
                {
                    normalizedMaterial.SetTexture("_BaseMap", baseColorMap);
                }

                if (normalizedMaterial.HasProperty("_MainTex"))
                {
                    normalizedMaterial.SetTexture("_MainTex", baseColorMap);
                }
            }

            if (normalMap != null)
            {
                if (normalizedMaterial.HasProperty("_NormalMap"))
                {
                    normalizedMaterial.SetTexture("_NormalMap", normalMap);
                }

                if (normalizedMaterial.HasProperty("_BumpMap"))
                {
                    normalizedMaterial.SetTexture("_BumpMap", normalMap);
                }
            }

            if (normalizedMaterial.HasProperty("_NormalScale"))
            {
                normalizedMaterial.SetFloat("_NormalScale", normalScale);
            }

            if (normalizedMaterial.HasProperty("_BumpScale"))
            {
                normalizedMaterial.SetFloat("_BumpScale", normalScale);
            }

            if (maskMap != null && normalizedMaterial.HasProperty("_MaskMap"))
            {
                normalizedMaterial.SetTexture("_MaskMap", maskMap);
            }

            if (emissiveMap != null)
            {
                if (normalizedMaterial.HasProperty("_EmissiveColorMap"))
                {
                    normalizedMaterial.SetTexture("_EmissiveColorMap", emissiveMap);
                }

                if (normalizedMaterial.HasProperty("_EmissionMap"))
                {
                    normalizedMaterial.SetTexture("_EmissionMap", emissiveMap);
                }
            }

            if (normalizedMaterial.HasProperty("_EmissiveColor"))
            {
                normalizedMaterial.SetColor("_EmissiveColor", emissiveColor);
            }

            if (normalizedMaterial.HasProperty("_EmissionColor"))
            {
                normalizedMaterial.SetColor("_EmissionColor", emissiveColor);
            }

            if (normalizedMaterial.HasProperty("_Metallic"))
            {
                normalizedMaterial.SetFloat("_Metallic", metallic);
            }

            if (normalizedMaterial.HasProperty("_Smoothness"))
            {
                normalizedMaterial.SetFloat("_Smoothness", smoothness);
            }

            if (normalizedMaterial.HasProperty("_SurfaceType"))
            {
                normalizedMaterial.SetFloat("_SurfaceType", 0f);
            }

            if (normalizedMaterial.HasProperty("_Surface"))
            {
                normalizedMaterial.SetFloat("_Surface", 0f);
            }

            return normalizedMaterial;
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

                if (normalizedName == token)
                {
                    return true;
                }

                if (containsWheel && normalizedName.Contains(token))
                {
                    return true;
                }

                if (normalizedName.EndsWith(token))
                {
                    return true;
                }
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

                if (component is Collider collider)
                {
                    collider.enabled = false;
                    continue;
                }

                if (component is Rigidbody rigidbody)
                {
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                    rigidbody.detectCollisions = false;
                    continue;
                }

                if (component is Joint joint)
                {
                    joint.enableCollision = false;
                    continue;
                }

                if (component is AudioSource audioSource)
                {
                    audioSource.Stop();
                    audioSource.enabled = false;
                    continue;
                }

                if (component is Camera camera)
                {
                    camera.enabled = false;
                    continue;
                }

                if (component is Light light)
                {
                    light.enabled = false;
                    continue;
                }

                if (component is Animator animator)
                {
                    animator.enabled = false;
                    continue;
                }

                if (component is MonoBehaviour behaviour)
                {
                    behaviour.enabled = false;
                    continue;
                }
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
            if (end < 0)
            {
                end = block.IndexOf('\n', start);
            }
            if (end < 0)
            {
                end = block.Length;
            }

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
            if (end < 0)
            {
                end = line.IndexOf('}', start);
            }
            if (end < 0)
            {
                end = line.Length;
            }

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
