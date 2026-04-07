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
            EnsureLightingRig();
            ApplyLighting(false);
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

            Transform existingRig = transform.Find("NightLightingRig");
            lightingRig = existingRig != null ? existingRig : new GameObject("NightLightingRig").transform;
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
            leftHeadlight.spotAngle = headlightSpotAngle;
            leftHeadlight.innerSpotAngle = headlightSpotAngle * 0.6f;
            leftHeadlight.shadows = headlightShadows ? LightShadows.Soft : LightShadows.None;
            rightHeadlight.spotAngle = headlightSpotAngle;
            rightHeadlight.innerSpotAngle = headlightSpotAngle * 0.6f;
            rightHeadlight.shadows = headlightShadows ? LightShadows.Soft : LightShadows.None;

            leftTaillight = CreateLight("TaillightLeft", LightType.Point, new Vector3(leftX, taillightY, rearZ + 0.04f), Quaternion.identity, new Color(1f, 0.18f, 0.14f), taillightIntensity, 5.5f);
            rightTaillight = CreateLight("TaillightRight", LightType.Point, new Vector3(rightX, taillightY, rearZ + 0.04f), Quaternion.identity, new Color(1f, 0.18f, 0.14f), taillightIntensity, 5.5f);
            leftTaillight.shadows = LightShadows.None;
            rightTaillight.shadows = LightShadows.None;

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

        private Light CreateLight(string name, LightType type, Vector3 localPosition, Quaternion localRotation, Color color, float intensity, float range)
        {
            Transform lightTransform = lightingRig.Find(name);
            GameObject lightObject = lightTransform != null ? lightTransform.gameObject : new GameObject(name);
            lightObject.transform.SetParent(lightingRig, false);
            lightObject.transform.localPosition = localPosition;
            lightObject.transform.localRotation = localRotation;

            Light lightComponent = lightObject.GetComponent<Light>() ?? lightObject.AddComponent<Light>();
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
