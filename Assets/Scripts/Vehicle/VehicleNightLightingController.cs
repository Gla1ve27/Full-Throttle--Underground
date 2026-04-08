using FCG;
using UnityEngine;
using Underground.TimeSystem;

namespace Underground.Vehicle
{
    public class VehicleNightLightingController : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private DayNightCycleController dayNightCycle;
        [SerializeField] private InputReader input;
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] private bool trafficLighting;
        [SerializeField] private bool headlightShadows = true;
        [SerializeField] private float headlightIntensity = 18f;
        [SerializeField] private float headlightRange = 36f;
        [SerializeField] private float headlightSpotAngle = 76f;
        [SerializeField] private float taillightIntensity = 2f;
        [SerializeField] private float brakeLightIntensity = 4.6f;

        private TrafficCar trafficCar;
        private Transform lightingRig;
        private Light leftHeadlight;
        private Light rightHeadlight;
        private Light leftTaillight;
        private Light rightTaillight;
        private bool rigCreated;
        private float nextLookupTime;

        public void ConfigureForTraffic(bool enableHeadlightShadows)
        {
            trafficLighting = true;
            headlightShadows = enableHeadlightShadows;
            headlightIntensity = 10f;
            headlightRange = 24f;
            headlightSpotAngle = 80f;
            taillightIntensity = 1.25f;
            brakeLightIntensity = 2.8f;
        }

        private void Awake()
        {
            ResolveReferences();
            SubscribeToAppearanceChanges();
            EnsureLightingRig();
            ApplyLighting(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromAppearanceChanges();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextLookupTime)
            {
                nextLookupTime = Time.unscaledTime + 1f;
                ResolveReferences();
            }

            EnsureLightingRig();
            bool nightActive = dayNightCycle != null && dayNightCycle.IsNight;
            ApplyLighting(nightActive);
        }

        /// <summary>
        /// Forces the lighting rig to be rebuilt from scratch.
        /// Called automatically when PlayerCarAppearanceController swaps the model.
        /// </summary>
        public void RebuildLightingRig()
        {
            DestroyLightingRig();
            EnsureLightingRig();
        }

        private void SubscribeToAppearanceChanges()
        {
            PlayerCarAppearanceController appearance = GetComponent<PlayerCarAppearanceController>();
            if (appearance != null)
            {
                appearance.AppearanceChanged += RebuildLightingRig;
            }
        }

        private void UnsubscribeFromAppearanceChanges()
        {
            PlayerCarAppearanceController appearance = GetComponent<PlayerCarAppearanceController>();
            if (appearance != null)
            {
                appearance.AppearanceChanged -= RebuildLightingRig;
            }
        }

        private void DestroyLightingRig()
        {
            if (lightingRig != null)
            {
                Object.Destroy(lightingRig.gameObject);
            }

            lightingRig = null;
            leftHeadlight = null;
            rightHeadlight = null;
            leftTaillight = null;
            rightTaillight = null;
            rigCreated = false;
        }

        private void ResolveReferences()
        {
            if (modelRoot == null)
            {
                Transform candidate = transform.Find("ModelRoot");
                modelRoot = candidate != null ? candidate : transform;
            }

            if (dayNightCycle == null)
            {
                dayNightCycle = FindFirstObjectByType<DayNightCycleController>();
            }

            if (input == null)
            {
                input = GetComponent<InputReader>();
            }

            if (vehicleBody == null)
            {
                vehicleBody = GetComponent<Rigidbody>();
            }

            if (trafficCar == null)
            {
                trafficCar = GetComponent<TrafficCar>();
            }
        }

        private void EnsureLightingRig()
        {
            if (rigCreated)
            {
                return;
            }

            // Always create a fresh rig container — never reuse orphaned children
            // from imported prefabs that may have "HeadlightLeft" without a Light component.
            if (lightingRig != null)
            {
                Object.Destroy(lightingRig.gameObject);
            }

            GameObject rigObject = new GameObject("NightLightingRig");
            lightingRig = rigObject.transform;
            lightingRig.SetParent(transform, false);

            if (!TryGetModelBounds(out Vector3 localMin, out Vector3 localMax))
            {
                localMin = new Vector3(-0.8f, 0.1f, -1.8f);
                localMax = new Vector3(0.8f, 1.1f, 1.8f);
            }

            float width = localMax.x - localMin.x;
            float height = localMax.y - localMin.y;
            float frontZ = localMax.z;
            float rearZ = localMin.z;
            float headlightY = localMin.y + (height * 0.42f);
            float taillightY = localMin.y + (height * 0.36f);
            float leftX = localMin.x + Mathf.Max(0.18f, width * 0.22f);
            float rightX = localMax.x - Mathf.Max(0.18f, width * 0.22f);

            leftHeadlight = CreateLight("HeadlightLeft", LightType.Spot, new Vector3(leftX, headlightY, frontZ - 0.02f), Quaternion.Euler(2f, 0f, 0f), new Color(1f, 0.96f, 0.9f), headlightIntensity, headlightRange);
            rightHeadlight = CreateLight("HeadlightRight", LightType.Spot, new Vector3(rightX, headlightY, frontZ - 0.02f), Quaternion.Euler(2f, 0f, 0f), new Color(1f, 0.96f, 0.9f), headlightIntensity, headlightRange);

            if (leftHeadlight != null)
            {
                leftHeadlight.spotAngle = headlightSpotAngle;
                leftHeadlight.innerSpotAngle = headlightSpotAngle * 0.6f;
                leftHeadlight.shadows = headlightShadows ? LightShadows.Soft : LightShadows.None;
            }

            if (rightHeadlight != null)
            {
                rightHeadlight.spotAngle = headlightSpotAngle;
                rightHeadlight.innerSpotAngle = headlightSpotAngle * 0.6f;
                rightHeadlight.shadows = headlightShadows ? LightShadows.Soft : LightShadows.None;
            }

            leftTaillight = CreateLight("TaillightLeft", LightType.Point, new Vector3(leftX, taillightY, rearZ + 0.04f), Quaternion.identity, new Color(1f, 0.18f, 0.14f), taillightIntensity, 5.5f);
            rightTaillight = CreateLight("TaillightRight", LightType.Point, new Vector3(rightX, taillightY, rearZ + 0.04f), Quaternion.identity, new Color(1f, 0.18f, 0.14f), taillightIntensity, 5.5f);

            if (leftTaillight != null)
            {
                leftTaillight.shadows = LightShadows.None;
            }

            if (rightTaillight != null)
            {
                rightTaillight.shadows = LightShadows.None;
            }

            rigCreated = true;
        }

        private void ApplyLighting(bool nightActive)
        {
            if (!rigCreated)
            {
                return;
            }

            float brakeFactor = GetBrakeFactor();
            SetLightState(leftHeadlight, nightActive, headlightIntensity);
            SetLightState(rightHeadlight, nightActive, headlightIntensity);
            SetLightState(leftTaillight, nightActive, Mathf.Lerp(taillightIntensity, brakeLightIntensity, brakeFactor));
            SetLightState(rightTaillight, nightActive, Mathf.Lerp(taillightIntensity, brakeLightIntensity, brakeFactor));
        }

        private float GetBrakeFactor()
        {
            if (input != null)
            {
                float playerBrake = Mathf.Max(input.Brake, input.Handbrake ? 1f : 0f);
                if (playerBrake > 0.01f)
                {
                    return playerBrake;
                }
            }

            if (trafficCar != null)
            {
                return trafficCar.status == TrafficCar.StatusCar.stoppedAtTrafficLights ||
                       trafficCar.status == TrafficCar.StatusCar.waitingForAnotherVehicleToPass ||
                       trafficCar.status == TrafficCar.StatusCar.bloked ||
                       trafficCar.status == TrafficCar.StatusCar.crashed
                    ? 1f
                    : 0f;
            }

            if (trafficLighting && vehicleBody != null)
            {
                return vehicleBody.linearVelocity.sqrMagnitude < 1f ? 0.4f : 0f;
            }

            return 0f;
        }

        /// <summary>
        /// Creates a light under the lighting rig. Always creates a fresh GameObject
        /// to avoid inheriting stale children from imported prefabs that share the same name
        /// but may lack a Light component (which caused MissingComponentException).
        /// </summary>
        private Light CreateLight(string name, LightType type, Vector3 localPosition, Quaternion localRotation, Color color, float intensity, float range)
        {
            // Always create a new, clean game object for our light.
            // Do NOT reuse existing children — imported prefabs can leave
            // "HeadlightLeft" objects without a Light component.
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(lightingRig, false);
            lightObject.transform.localPosition = localPosition;
            lightObject.transform.localRotation = localRotation;

            Light lightComponent = lightObject.AddComponent<Light>();
            if (lightComponent == null)
            {
                Debug.LogWarning($"[VehicleNightLightingController] Failed to add Light component to {name}. Skipping.");
                return null;
            }

            lightComponent.type = type;
            lightComponent.color = color;
            lightComponent.intensity = intensity;
            lightComponent.range = range;
            lightComponent.enabled = false;
            return lightComponent;
        }

        private void SetLightState(Light lightComponent, bool enabled, float intensity)
        {
            if (lightComponent == null)
            {
                return;
            }

            lightComponent.enabled = enabled;
            lightComponent.intensity = intensity;
        }

        private bool TryGetModelBounds(out Vector3 localMin, out Vector3 localMax)
        {
            Renderer[] renderers = (modelRoot != null ? modelRoot : transform).GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            localMin = Vector3.zero;
            localMax = Vector3.zero;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3[] corners =
                {
                    new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                    new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                    new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                    new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                    new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                    new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                    new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                    new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
                };

                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 localCorner = transform.InverseTransformPoint(corners[cornerIndex]);
                    if (!hasBounds)
                    {
                        localMin = localCorner;
                        localMax = localCorner;
                        hasBounds = true;
                    }
                    else
                    {
                        localMin = Vector3.Min(localMin, localCorner);
                        localMax = Vector3.Max(localMax, localCorner);
                    }
                }
            }

            return hasBounds;
        }
    }
}
