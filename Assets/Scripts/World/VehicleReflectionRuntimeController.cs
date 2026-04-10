using UnityEngine;
using Underground.Garage;
using Underground.UI;
using Underground.Vehicle;

namespace Underground.World
{
    [DisallowMultipleComponent]
    public sealed class VehicleReflectionRuntimeController : MonoBehaviour
    {
        private const string ReflectionProbeName = "RuntimeVehicleReflectionProbe";

        [SerializeField] private bool allowRealtimeProbe;
        [SerializeField] private Vector3 probeOffset = new Vector3(0f, 4.5f, 0f);
        [SerializeField] private Vector3 probeSize = new Vector3(96f, 28f, 96f);
        [SerializeField] private float probeIntensity = 1.1f;

        // Guard: material overrides must only run once per component lifetime.
        // Calling renderer.materials (plural) creates new instances every call —
        // doing it repeatedly leaks memory and stacks material mutations.
        private bool _materialsApplied;

        // Guard: probe setup must only run once. Re-running it every second via
        // AdvancedGraphicsSettingsRuntimeController was redundant and triggered
        // the MissingComponentException on the frame AddComponent<ReflectionProbe>
        // hadn't fully registered yet.
        private bool _probeSetUp;

        public void Configure(bool enableRealtimeProbe)
        {
            allowRealtimeProbe = enableRealtimeProbe;
            ApplyReflectionSupport();
        }

        public static bool IsVehicleLikeObject(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            if (target.GetComponent<VehicleDynamicsController>() != null || target.GetComponent<PlayerCarAppearanceController>() != null)
            {
                return true;
            }

            string targetName = target.name;
            if (IsExcludedVehicleName(targetName))
            {
                return false;
            }

            if (target.CompareTag("Player") || target.CompareTag("Traffic") || HasVehicleKeyword(targetName))
            {
                return HasUsableRenderer(target);
            }

            return false;
        }

        private void Awake()
        {
            ApplyReflectionSupport();
        }

        private void OnEnable()
        {
            // Do NOT call ApplyReflectionSupport() here.
            // OnEnable fires every time this object is re-enabled (traffic pooling,
            // scene transitions). Running it here would call renderer.materials on
            // every re-enable, leaking material instances and re-stacking mutations.
        }

        public void ApplyReflectionSupport()
        {
            // Fully transitioned to an "Authored-First" model. 
            // The renderer now respects the materials assigned to the prefab and the 
            // reflection probes/volumes placed in the scene, rather than using code-forced overrides.
        }

        /// <summary>
        /// Re-applies probe settings only (size, resolution, intensity) without
        /// touching materials or creating new probe objects.
        /// Called by AdvancedGraphicsSettingsRuntimeController when settings change.
        /// </summary>
        public void RefreshProbeSettings(Vector3 size, int resolution, float intensity, float blendDistance)
        {
            Transform probeTransform = transform.Find(ReflectionProbeName);
            if (probeTransform == null)
            {
                return;
            }

            ReflectionProbe probe = probeTransform.GetComponent<ReflectionProbe>();
            if (probe == null)
            {
                return;
            }

            probe.size = size;
            probe.resolution = resolution;
            probe.intensity = intensity;
            probe.blendDistance = blendDistance;
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static bool IsPaintMaterial(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
            {
                return false;
            }

            // NOTE: "color" and "atlas" are intentionally excluded.
            // "color" matches generic city/prop materials (e.g. "color_building").
            // "atlas" matches every texture atlas sheet used by level geometry.
            // "colour" (British spelling) is kept — it's specific enough to be
            // safely treated as a car paint indicator.
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

        private static bool IsTransparentMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("window", System.StringComparison.OrdinalIgnoreCase) >= 0);
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

        private static string NormalizeName(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static bool HasUsableRenderer(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                if (renderers[rendererIndex] != null && renderers[rendererIndex] is not ParticleSystemRenderer)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasVehicleKeyword(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf("car", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("vehicle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("traffic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("taxi", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("police", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("interceptor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("bus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("truck", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("furgao", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("tempra", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("gontijo", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("granfury", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("soul", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("vesta", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("fiat", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("rmcar", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsExcludedVehicleName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf("wheel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("trafficlight", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("tlight", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("helicopter", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("plane", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
