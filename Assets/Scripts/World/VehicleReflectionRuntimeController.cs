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

        public void Configure(bool enableRealtimeProbe)
        {
            allowRealtimeProbe = enableRealtimeProbe;
            ApplyReflectionSupport();
        }

        private void Awake()
        {
            ApplyReflectionSupport();
        }

        private void OnEnable()
        {
            ApplyReflectionSupport();
        }

        public void ApplyReflectionSupport()
        {
            ApplyMaterialOverrides();
            EnsureReflectionProbe();
        }

        private void ApplyMaterialOverrides()
        {
            GameSettingsManager settingsManager = GameSettingsManager.Instance ?? FindFirstObjectByType<GameSettingsManager>();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                bool hasChanges = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (!ShouldTuneMaterial(material))
                    {
                        continue;
                    }

                    TuneMaterial(material, settingsManager);
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    renderer.materials = materials;
                }
            }
        }

        private void EnsureReflectionProbe()
        {
            GameSettingsManager settingsManager = GameSettingsManager.Instance ?? FindFirstObjectByType<GameSettingsManager>();
            bool shouldUseRealtimeProbe =
                allowRealtimeProbe ||
                CompareTag("Player") ||
                GetComponentInParent<GarageShowroomController>() != null;
            if (!shouldUseRealtimeProbe || GetComponent<PlayerCarAppearanceController>() != null)
            {
                return;
            }

            Transform probeTransform = transform.Find(ReflectionProbeName);
            GameObject probeObject = probeTransform != null ? probeTransform.gameObject : new GameObject(ReflectionProbeName);
            if (probeTransform == null)
            {
                probeObject.transform.SetParent(transform, false);
            }

            probeObject.transform.localPosition = probeOffset;
            probeObject.transform.localRotation = Quaternion.identity;

            ReflectionProbe probe = probeObject.GetComponent<ReflectionProbe>() ?? probeObject.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.boxProjection = true;
            probe.importance = 1000;
            probe.intensity = settingsManager != null
                ? settingsManager.CarReflectionDetail switch
                {
                    0 => 0.95f,
                    1 => 1.05f,
                    _ => 1.15f
                }
                : probeIntensity;
            probe.size = settingsManager != null
                ? settingsManager.CarReflectionDetail switch
                {
                    0 => new Vector3(72f, 22f, 72f),
                    1 => new Vector3(96f, 28f, 96f),
                    _ => new Vector3(120f, 36f, 120f)
                }
                : probeSize;
            probe.resolution = settingsManager != null
                ? settingsManager.CarReflectionDetail switch
                {
                    0 => 64,
                    1 => 128,
                    _ => 256
                }
                : 128;

            int playerVehicleLayer = LayerMask.NameToLayer("PlayerVehicle");
            probe.cullingMask = playerVehicleLayer >= 0 ? ~(1 << playerVehicleLayer) : ~0;

            PlayerReflectionProbeController controller = probeObject.GetComponent<PlayerReflectionProbeController>() ?? probeObject.AddComponent<PlayerReflectionProbeController>();
            controller.Configure(
                transform,
                probeOffset,
                probe.size,
                10f,
                settingsManager != null
                    ? settingsManager.CarReflectionUpdateRate switch
                    {
                        0 => 0.55f,
                        1 => 0.3f,
                        _ => 0.16f
                    }
                    : 0.25f,
                6f);
        }

        private static bool ShouldTuneMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            string materialName = material.name;
            if (IsTransparentMaterial(materialName) || IsLightMaterial(materialName))
            {
                return false;
            }

            return IsPaintMaterial(materialName) ||
                   IsBodyMaterial(materialName) ||
                   material.HasProperty("_Smoothness") ||
                   material.HasProperty("_Glossiness");
        }

        private static void TuneMaterial(Material material, GameSettingsManager settingsManager)
        {
            bool isPaintMaterial = IsPaintMaterial(material.name);
            bool isBodyMaterial = IsBodyMaterial(material.name);

            float metallic = GetFloat(material, 0f, "_Metallic");
            float smoothness = GetFloat(material, 0.55f, "_Smoothness", "_Glossiness");
            int reflectionDetail = settingsManager != null ? settingsManager.CarReflectionDetail : 2;

            if (isPaintMaterial)
            {
                metallic = Mathf.Clamp(metallic, 0f, 0.08f);
                smoothness = reflectionDetail switch
                {
                    0 => Mathf.Clamp(smoothness, 0.72f, 0.86f),
                    1 => Mathf.Clamp(smoothness, 0.78f, 0.9f),
                    _ => Mathf.Clamp(smoothness, 0.8f, 0.94f)
                };
            }
            else if (isBodyMaterial)
            {
                metallic = Mathf.Clamp(metallic, 0f, 0.18f);
                smoothness = reflectionDetail switch
                {
                    0 => Mathf.Clamp(smoothness, 0.6f, 0.76f),
                    1 => Mathf.Clamp(smoothness, 0.64f, 0.82f),
                    _ => Mathf.Clamp(smoothness, 0.68f, 0.86f)
                };
            }
            else
            {
                smoothness = Mathf.Clamp(smoothness, 0.58f, 0.8f);
            }

            SetFloatIfPresent(material, "_Metallic", metallic);
            SetFloatIfPresent(material, "_Smoothness", smoothness);
            SetFloatIfPresent(material, "_Glossiness", smoothness);
            SetFloatIfPresent(material, "_ReceivesSSR", 1f);
            SetFloatIfPresent(material, "_ReceivesSSRTransparent", 0f);
            SetFloatIfPresent(material, "_EnvironmentReflections", 1f);
            SetFloatIfPresent(material, "_GlossyReflections", 1f);
            SetFloatIfPresent(material, "_SpecularHighlights", 1f);
            SetFloatIfPresent(material, "_TransmissionEnable", 0f);
            SetFloatIfPresent(material, "_EnableCoat", isPaintMaterial || isBodyMaterial ? 1f : 0f);
            SetFloatIfPresent(material, "_CoatMask", isPaintMaterial ? 1f : isBodyMaterial ? 0.35f : 0f);
            material.DisableKeyword("_DISABLE_SSR");
            material.DisableKeyword("_DISABLE_SSR_TRANSPARENT");
        }

        private static float GetFloat(Material material, float fallback, params string[] propertyNames)
        {
            for (int index = 0; index < propertyNames.Length; index++)
            {
                string propertyName = propertyNames[index];
                if (material.HasProperty(propertyName))
                {
                    return material.GetFloat(propertyName);
                }
            }

            return fallback;
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
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("paint", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("color", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("police", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("taxi", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsBodyMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("body", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car_color", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car colour", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car color", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsTransparentMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("window", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsLightMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("light", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("emiss", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("brake", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("indicator", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
