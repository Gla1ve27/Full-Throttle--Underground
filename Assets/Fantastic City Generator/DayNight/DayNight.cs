using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Underground.TimeSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DayNight : MonoBehaviour
{

    //  In the 2 fields below, only the materials that will be alternated in the day/night exchange are registered
    //  When adding your buildings(which will have their own materials), you can register the day and night versions of the materials here.
    //  The index of the daytime version of the material must match the index of the nighttime version of the material
    //  Example: When switching to night scene, materialDay[1] will be replaced by materialNight[1]
    //  (Materials that will be used both night and day do not need to be here)
    public Material[] materialDay;    // Add materials that are only used in the day scene, and are substituted in the night scene
    public Material[] materialNight;  // Add night scene materials that will replace day scene materials. (The sequence must be respected)



    public VolumeProfile volumeProfile_Day;  
    public VolumeProfile volumeProfile_Night;
    
    //Don't forget to add the Directional Light here
    public Light directionalLight;

    [Header("HDRP Volume Sky")]
    [Tooltip("Use the Day and Night HDRP Volume Profiles as the source of sky/exposure/fog instead of relying on RenderSettings skybox changes.")]
    public bool driveVolumeBlend = true;
    [Tooltip("At runtime, create separate day/night global volumes and blend their weights. This is the cleanest path for HDRI Sky profile transitions.")]
    public bool createRuntimeBlendVolumes = true;
    [Tooltip("Follow the active SunRotation/TimeOfDay clock so the HDRP sky blend matches gameplay time.")]
    public bool syncWithTimeOfDay = true;
    [Range(0f, 24f)] public float dawnStartHour = 5f;
    [Range(0f, 24f)] public float fullDayHour = 7f;
    [Range(0f, 24f)] public float duskStartHour = 17.5f;
    [Range(0f, 24f)] public float fullNightHour = 20f;
    [Range(0.1f, 20f)] public float volumeBlendResponse = 4f;

    [Header("Full Throttle Atmosphere")]
    [Tooltip("Locks this city to the Full Throttle dusk/night look. Daytime clock values are redirected so the HDRP profile blend never goes bright daytime.")]
    public bool duskNightOnly = true;
    [Range(0f, 1f)] public float minimumDuskNightBlend = 0.72f;

    [Header("Street Lights")]
    public bool driveStreetLights = true;
    [Range(0f, 1f)] public float streetLightOnNightBlend = 0.35f;
    [Range(0.1f, 5f)] public float streetLightRefreshInterval = 1.05f;
    [Range(2f, 30f)] public float streetLightCacheRefreshInterval = 16f;
    public float activeStreetLightDistance = 70f;
    public int maxActiveRealtimeStreetLights = 16;
    public int maxActiveStreetLightBeams = 3;
    public float streetLightIntensity = 1800f;
    public float streetLightRange = 12f;
    public float streetLightSpotAngle = 88f;
    [Range(0f, 3f)] public float streetLightVolumetricDimmer;
    public bool streetLightShadows;
    public GameObject streetLightBeamPrefab;
    public bool autoUseDefaultStreetLightBeamPrefab = true;
    public string defaultStreetLightBeamPrefabPath = "Assets/PolygonStreetRacer/Prefabs/FX/FX_Light_Beam_01.prefab";
    public Vector3 streetLightBeamLocalScale = new Vector3(1.1f, 1.1f, 1.85f);

    public bool HasVolumeProfiles => volumeProfile_Day != null || volumeProfile_Night != null;
    public float CurrentNightBlend => Mathf.Clamp01(currentNightBlend);
    

    [HideInInspector]
    public bool isNight;

    [HideInInspector]
    public bool night;

    [HideInInspector]
    public bool isSpotLights;

    [HideInInspector]
    public bool _spotLights;

    [HideInInspector]
    public float intenseMoonLight = 800f;

    [HideInInspector]
    public float _intenseMoonLight;

    [HideInInspector]
    public float intenseSunLight = 8000f;

    [HideInInspector]
    public float _intenseSunLight;


    [HideInInspector]
    public float temperatureSunLight = 6700f;

    [HideInInspector]
    public float _temperatureSunLight;

    [HideInInspector]
    public float temperatureMoonLight = 9500f;

    [HideInInspector]
    public float _temperatureMoonLight;

    private const string RuntimeDayVolumeName = "Runtime_Day_HDRI_Volume";
    private const string RuntimeNightVolumeName = "Runtime_Night_HDRI_Volume";

    private Volume dayBlendVolume;
    private Volume nightBlendVolume;
    private float currentNightBlend = -1f;
    private readonly List<MeshRenderer> streetGlowRenderers = new List<MeshRenderer>(512);
    private readonly List<StreetLightRuntime> streetLightRuntimes = new List<StreetLightRuntime>(512);
    private float nextStreetLightUpdateTime = -999f;
    private float nextStreetLightCacheTime = -999f;
    private bool lastStreetLightNightState;

    private sealed class StreetLightRuntime
    {
        public Light Light;
        public HDAdditionalLightData HdLight;
        public Transform BeamAnchor;
        public GameObject Beam;
        public float SqrDistance;
    }

    private void Awake()
    {
        ResolveDefaultStreetLightBeamPrefab();
        ApplyLightingLock();
        ApplyVolumeBlend(instant: true);
        RefreshStreetLightCache();
        ApplyStreetLights(instant: true);
    }

    private void OnEnable()
    {
        ResolveDefaultStreetLightBeamPrefab();
        ApplyLightingLock();
        ApplyVolumeBlend(instant: true);
        RefreshStreetLightCache();
        ApplyStreetLights(instant: true);
    }

    private void Update()
    {
        if (driveVolumeBlend && syncWithTimeOfDay && Application.isPlaying)
        {
            ApplyVolumeBlend(instant: false);
        }

        ApplyStreetLights(instant: false);
    }

    private void OnValidate()
    {
        ApplyLightingLock();
    }

    public void ChangeVolume()
    {
        if (driveVolumeBlend)
        {
            float targetBlend = isNight || duskNightOnly ? 1f : 0f;
            SetVolumeBlend(targetBlend, instant: true);
            return;
        }

        Volume volume = GetComponent<Volume>();
        if (volume != null)
        {
            volume.profile = (isNight) ? volumeProfile_Night : volumeProfile_Day;
        }
    }

    public void ChangeMaterial()
    {
        ApplyLightingLock();
        ChangeVolume();

#if UNITY_2020_1_OR_NEWER

        Volume volume = GetComponent<Volume>();
        if (!lockLighting && volume != null && volume.profile != null && volume.profile.TryGet<Exposure>(out var exp))
            exp.compensation.SetValue(new FloatParameter(0));

#endif

        // shift VolumeProfile :  day/night
        if (!lockLighting && !driveVolumeBlend && volume != null)
        {
            volume.profile = (isNight) ? volumeProfile_Night : volumeProfile_Day;
        }


        //Configuring the Directional Light as it is day or night (sun/moon)
        SetDirectionalLight();


        /*
        Substituting Night materials for Day materials (or vice versa) in all Mesh Renders within City-Maker
        Only materials that have been added in "materialDay" and "materialNight" Array
        */

        if (lockLighting)
        {
            SetStreetLights(isNight);
            return;
        }

        GameObject GmObj = GameObject.Find("City-Maker"); ;
        if (GmObj == null) return;
                
        Renderer[] children = GmObj.GetComponentsInChildren<Renderer>();

        Material[] myMaterials;

        for (int i = 0; i < children.Length; i++)
        {
            myMaterials = children[i].GetComponent<Renderer>().sharedMaterials;

            for (int m = 0; m < myMaterials.Length; m++)
            {
                for (int mt = 0; mt < materialDay.Length; mt++)
                if (isNight)
                {
                    if(myMaterials[m] == materialDay[mt])
                        myMaterials[m] = materialNight[mt];

                }
                else
                {
                    if (myMaterials[m] == materialNight[mt])
                        myMaterials[m] = materialDay[mt];
                }


                children[i].GetComponent<MeshRenderer>().sharedMaterials = myMaterials;
            }


        }


        //Toggles street lamp lights on/off
        SetStreetLights(isNight);



    }


    public bool lockLighting = true; // NEW: Set to true to stop FCG from fighting our manual lighting.

    public void SetDirectionalLight()
    {
        if (lockLighting) return; 

        if (directionalLight)
        {
            directionalLight.GetComponent<HDAdditionalLightData>().intensity = (isNight) ? intenseMoonLight : intenseSunLight;
            directionalLight.useColorTemperature = true;
            if (directionalLight.useColorTemperature)
                directionalLight.colorTemperature = (isNight) ? temperatureMoonLight : temperatureSunLight;
        }
    }

    public void SetStreetLights(bool night)
    {
        if (duskNightOnly)
        {
            night = true;
        }

        isNight = night;
        this.night = night;
        SetStreetLightsState(night, force: true);

    }

    private void ApplyLightingLock()
    {
        if (duskNightOnly)
        {
            isNight = true;
            night = true;
        }

        Volume volume = GetComponent<Volume>();
        if (volume != null)
        {
            volume.enabled = !driveVolumeBlend || !createRuntimeBlendVolumes;
        }
    }

    private void ApplyVolumeBlend(bool instant)
    {
        if (!driveVolumeBlend)
        {
            return;
        }

        float targetNightBlend = syncWithTimeOfDay && Application.isPlaying
            ? EvaluateNightBlend(ResolveTimeOfDayHours())
            : isNight ? 1f : 0f;
        if (duskNightOnly)
        {
            targetNightBlend = Mathf.Max(targetNightBlend, minimumDuskNightBlend);
        }

        SetVolumeBlend(targetNightBlend, instant);
    }

    private void SetVolumeBlend(float targetNightBlend, bool instant)
    {
        targetNightBlend = Mathf.Clamp01(targetNightBlend);
        if (currentNightBlend < 0f || instant || !Application.isPlaying)
        {
            currentNightBlend = targetNightBlend;
        }
        else
        {
            currentNightBlend = ExpSmoothing(currentNightBlend, targetNightBlend, volumeBlendResponse, Time.deltaTime);
        }

        isNight = currentNightBlend >= 0.5f;
        night = isNight;

        if (!Application.isPlaying || !createRuntimeBlendVolumes)
        {
            Volume localVolume = GetComponent<Volume>();
            if (localVolume != null)
            {
                localVolume.enabled = true;
                localVolume.profile = currentNightBlend >= 0.5f ? volumeProfile_Night : volumeProfile_Day;
            }

            return;
        }

        EnsureRuntimeVolumes();

        float dayWeight = 1f - currentNightBlend;
        float nightWeight = currentNightBlend;

        if (dayBlendVolume != null)
        {
            dayBlendVolume.profile = volumeProfile_Day;
            dayBlendVolume.isGlobal = true;
            dayBlendVolume.weight = dayWeight;
            dayBlendVolume.enabled = volumeProfile_Day != null && dayWeight > 0.001f;
        }

        if (nightBlendVolume != null)
        {
            nightBlendVolume.profile = volumeProfile_Night;
            nightBlendVolume.isGlobal = true;
            nightBlendVolume.weight = nightWeight;
            nightBlendVolume.enabled = volumeProfile_Night != null && nightWeight > 0.001f;
        }

        Volume local = GetComponent<Volume>();
        if (local != null)
        {
            local.enabled = false;
        }
    }

    private void EnsureRuntimeVolumes()
    {
        if (!driveVolumeBlend || !createRuntimeBlendVolumes || !Application.isPlaying)
        {
            return;
        }

        dayBlendVolume = EnsureChildVolume(RuntimeDayVolumeName, volumeProfile_Day);
        nightBlendVolume = EnsureChildVolume(RuntimeNightVolumeName, volumeProfile_Night);
    }

    private Volume EnsureChildVolume(string objectName, VolumeProfile profile)
    {
        Transform child = transform.Find(objectName);
        if (child == null)
        {
            GameObject volumeObject = new GameObject(objectName);
            volumeObject.transform.SetParent(transform, false);
            child = volumeObject.transform;
        }

        Volume volume = child.GetComponent<Volume>();
        if (volume == null)
        {
            volume = child.gameObject.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 100f;
        volume.profile = profile;
        return volume;
    }

    private float ResolveTimeOfDayHours()
    {
        if (SunRotation.ActiveInstance != null)
        {
            return ConstrainAtmosphereHour(SunRotation.ActiveInstance.TimeOfDay);
        }

        TimeOfDay packageClock = FindFirstObjectByType<TimeOfDay>();
        if (packageClock != null)
        {
            return ConstrainAtmosphereHour(packageClock.seconds_passed / TimeOfDay.seconds_in_day * 24f);
        }

        return duskNightOnly ? PackageTimeOfDayUtility.DefaultDuskNightHour : isNight ? fullNightHour : fullDayHour;
    }

    private float EvaluateNightBlend(float hour)
    {
        hour = ConstrainAtmosphereHour(hour);
        hour = Mathf.Repeat(hour, 24f);

        if (hour >= fullNightHour || hour < dawnStartHour)
        {
            return 1f;
        }

        if (hour < fullDayHour)
        {
            return 1f - Smooth01(Mathf.InverseLerp(dawnStartHour, fullDayHour, hour));
        }

        if (hour >= duskStartHour)
        {
            return Smooth01(Mathf.InverseLerp(duskStartHour, fullNightHour, hour));
        }

        return duskNightOnly ? minimumDuskNightBlend : 0f;
    }

    private float ConstrainAtmosphereHour(float hour)
    {
        return duskNightOnly
            ? PackageTimeOfDayUtility.ConstrainToDuskNightHours(hour)
            : Mathf.Repeat(hour, 24f);
    }

    private static float ExpSmoothing(float current, float target, float response, float dt)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * dt));
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void ApplyStreetLights(bool instant)
    {
        if (!driveStreetLights)
        {
            return;
        }

        if (!instant && Time.unscaledTime < nextStreetLightUpdateTime)
        {
            return;
        }

        nextStreetLightUpdateTime = Time.unscaledTime + Mathf.Max(0.1f, streetLightRefreshInterval);

        if (instant || Time.unscaledTime >= nextStreetLightCacheTime || streetGlowRenderers.Count == 0 && streetLightRuntimes.Count == 0)
        {
            RefreshStreetLightCache();
        }

        bool shouldBeNight = currentNightBlend >= 0f
            ? currentNightBlend >= streetLightOnNightBlend
            : isNight;

        SetStreetLightsState(shouldBeNight, force: instant || shouldBeNight != lastStreetLightNightState);
    }

    private void SetStreetLightsState(bool nightState, bool force)
    {
        lastStreetLightNightState = nightState;

        for (int i = streetGlowRenderers.Count - 1; i >= 0; i--)
        {
            MeshRenderer renderer = streetGlowRenderers[i];
            if (renderer == null)
            {
                streetGlowRenderers.RemoveAt(i);
                continue;
            }

            if (force || renderer.enabled != nightState)
            {
                renderer.enabled = nightState;
            }
        }

        bool lightState = isSpotLights && nightState;
        if (!lightState)
        {
            for (int i = streetLightRuntimes.Count - 1; i >= 0; i--)
            {
                StreetLightRuntime runtime = streetLightRuntimes[i];
                if (runtime == null || runtime.Light == null)
                {
                    streetLightRuntimes.RemoveAt(i);
                    continue;
                }

                runtime.Light.enabled = false;
                if (runtime.Beam != null)
                {
                    runtime.Beam.SetActive(false);
                }
            }

            return;
        }

        Transform focus = ResolveStreetLightFocus();
        float maxDistance = Mathf.Max(12f, activeStreetLightDistance);
        float maxSqrDistance = maxDistance * maxDistance;
        int budget = Mathf.Max(0, maxActiveRealtimeStreetLights);
        int beamBudget = Mathf.Clamp(maxActiveStreetLightBeams, 0, budget);

        for (int i = streetLightRuntimes.Count - 1; i >= 0; i--)
        {
            StreetLightRuntime runtime = streetLightRuntimes[i];
            if (runtime == null || runtime.Light == null)
            {
                streetLightRuntimes.RemoveAt(i);
                continue;
            }

            runtime.SqrDistance = focus != null
                ? (runtime.Light.transform.position - focus.position).sqrMagnitude
                : i;
        }

        streetLightRuntimes.Sort((a, b) => a.SqrDistance.CompareTo(b.SqrDistance));

        int enabledCount = 0;
        int enabledBeamCount = 0;
        for (int i = 0; i < streetLightRuntimes.Count; i++)
        {
            StreetLightRuntime runtime = streetLightRuntimes[i];
            bool active = runtime.SqrDistance <= maxSqrDistance && enabledCount < budget;
            bool beamActive = active && enabledBeamCount < beamBudget;
            ApplyStreetLight(runtime, active, beamActive);
            if (active)
            {
                enabledCount++;
            }

            if (beamActive)
            {
                enabledBeamCount++;
            }
        }
    }

    private void RefreshStreetLightCache()
    {
        nextStreetLightCacheTime = Time.unscaledTime + Mathf.Max(2f, streetLightCacheRefreshInterval);
        streetGlowRenderers.Clear();
        streetLightRuntimes.Clear();

        MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer != null && IsStreetGlowRenderer(renderer.transform))
            {
                streetGlowRenderers.Add(renderer);
            }
        }

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || !IsStreetLampLight(light.transform))
            {
                continue;
            }

            ConfigureStreetLight(light, out HDAdditionalLightData hdLight);
            streetLightRuntimes.Add(new StreetLightRuntime
            {
                Light = light,
                HdLight = hdLight,
                BeamAnchor = FindStreetLightBeamAnchor(light.transform)
            });
        }
    }

    private void ConfigureStreetLight(Light light, out HDAdditionalLightData hdLight)
    {
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.78f, 0.48f);
        light.intensity = streetLightIntensity;
        light.range = streetLightRange;
        light.spotAngle = streetLightSpotAngle;
        light.innerSpotAngle = streetLightSpotAngle * 0.48f;
        light.shadows = streetLightShadows ? LightShadows.Soft : LightShadows.None;

        hdLight = light.GetComponent<HDAdditionalLightData>();
        if (hdLight == null)
        {
            hdLight = light.gameObject.AddComponent<HDAdditionalLightData>();
        }

        hdLight.volumetricDimmer = streetLightVolumetricDimmer;
        hdLight.fadeDistance = Mathf.Max(hdLight.fadeDistance, activeStreetLightDistance * 1.35f);
        hdLight.volumetricFadeDistance = Mathf.Max(hdLight.volumetricFadeDistance, activeStreetLightDistance * 1.1f);
    }

    private void ApplyStreetLight(StreetLightRuntime runtime, bool active, bool beamActive)
    {
        runtime.Light.enabled = active;
        if (runtime.Beam != null)
        {
            runtime.Beam.SetActive(active && beamActive);
        }

        if (!active)
        {
            return;
        }

        ApplyStreetLightPose(runtime);

        if (beamActive)
        {
            EnsureStreetLightBeam(runtime);
            if (runtime.Beam != null)
            {
                runtime.Beam.SetActive(true);
            }
        }

        runtime.Light.intensity = streetLightIntensity;
        runtime.Light.range = streetLightRange;
        runtime.Light.spotAngle = streetLightSpotAngle;
        runtime.Light.shadows = streetLightShadows ? LightShadows.Soft : LightShadows.None;

        if (runtime.HdLight != null)
        {
            runtime.HdLight.volumetricDimmer = streetLightVolumetricDimmer;
        }
    }

    private static void ApplyStreetLightPose(StreetLightRuntime runtime)
    {
        if (runtime == null || runtime.Light == null)
        {
            return;
        }

        Transform anchor = runtime.BeamAnchor != null ? runtime.BeamAnchor : runtime.Light.transform;
        if (anchor != runtime.Light.transform)
        {
            runtime.Light.transform.position = anchor.position;
        }

        runtime.Light.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
    }

    private static bool IsStreetGlowRenderer(Transform transform)
    {
        return transform != null && transform.name == "_LightV";
    }

    private static bool IsStreetLampLight(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        string name = transform.name;
        if (name == "_Spot_Light")
        {
            return true;
        }

        if (!name.Contains("Spot Light") && !name.Contains("Light_Lamp"))
        {
            return false;
        }

        Transform current = transform;
        while (current != null)
        {
            string currentName = current.name;
            if (currentName.Contains("StreetLight") || currentName.Contains("ParkLamp"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static Transform FindStreetLightBeamAnchor(Transform lightTransform)
    {
        Transform lampRoot = FindStreetLampRoot(lightTransform);
        if (lampRoot == null)
        {
            return lightTransform;
        }

        MeshRenderer[] renderers = lampRoot.GetComponentsInChildren<MeshRenderer>(true);
        Transform best = null;
        float bestScore = float.MaxValue;
        Vector3 lightPosition = lightTransform != null ? lightTransform.position : lampRoot.position;
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || !IsStreetGlowRenderer(renderer.transform))
            {
                continue;
            }

            float score = (renderer.transform.position - lightPosition).sqrMagnitude;
            if (renderer.transform.position.y > lightPosition.y + 0.5f)
            {
                score *= 0.18f;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = renderer.transform;
            }
        }

        return best != null ? best : lightTransform;
    }

    private static Transform FindStreetLampRoot(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            string currentName = current.name;
            if (currentName.Contains("StreetLight") || currentName.Contains("ParkLamp"))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static Transform ResolveStreetLightFocus()
    {
        Camera main = Camera.main;
        return main != null ? main.transform : null;
    }

    private void EnsureStreetLightBeam(StreetLightRuntime runtime)
    {
        if (streetLightBeamPrefab == null || runtime == null || runtime.Light == null)
        {
            return;
        }

        Transform anchor = runtime.BeamAnchor != null ? runtime.BeamAnchor : runtime.Light.transform;
        if (runtime.Beam == null)
        {
            Transform existing = anchor.Find("StreetLightBeam_Runtime");
            if (existing == null && anchor != runtime.Light.transform)
            {
                existing = runtime.Light.transform.Find("StreetLightBeam_Runtime");
            }

            if (existing != null)
            {
                runtime.Beam = existing.gameObject;
            }
        }

        if (runtime.Beam == null)
        {
            runtime.Beam = Instantiate(streetLightBeamPrefab, anchor);
            runtime.Beam.name = "StreetLightBeam_Runtime";
        }
        else if (runtime.Beam.transform.parent != anchor)
        {
            runtime.Beam.transform.SetParent(anchor, false);
        }

        runtime.Beam.transform.localPosition = Vector3.zero;
        runtime.Beam.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        runtime.Beam.transform.localScale = streetLightBeamLocalScale;
    }

    private void ResolveDefaultStreetLightBeamPrefab()
    {
#if UNITY_EDITOR
        if (!autoUseDefaultStreetLightBeamPrefab || streetLightBeamPrefab != null || string.IsNullOrEmpty(defaultStreetLightBeamPrefabPath))
        {
            return;
        }

        streetLightBeamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(defaultStreetLightBeamPrefabPath);
#endif
    }


}
