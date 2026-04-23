using System;
using Underground.TimeSystem;
using Underground.Core.Architecture;
using UnityEngine;

public enum SunTimeMode
{
    GameCycle,
    RealWorldClock,
    InspectorOverride
}

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(TimeOfDay))]
[RequireComponent(typeof(Light))]
public class SunRotation : MonoBehaviour, ITimeOfDayService
{
    private const float MidnightRotation = 270f;
    private const float DayStartHour = 6f;
    private const float SunsetStartHour = 17f;
    private const float NightStartHour = 20f;
    private const float LateNightEndHour = 6f;

    public static SunRotation ActiveInstance { get; private set; }

    [Header("DayNight Asset")]
    public AnimationCurve ambient_intensity_curve;
    public bool update_ambient_intensity = true;
    public Material skyboxMaterial;

    [Header("Time")]
    [SerializeField] private SunTimeMode timeMode = SunTimeMode.GameCycle;
    [SerializeField, Range(0f, 24f)] private float startTimeOfDay = 22f;
    [SerializeField] private float fullDayLengthSeconds = 1200f;
    [SerializeField] private bool useInspectorTimeOverride;
    [SerializeField, Range(0f, 24f)] private float inspectorTimeOfDay = 22f;
    [SerializeField] private bool useLocalRealWorldTime = true;
    [SerializeField, Range(-12f, 14f)] private float utcOffsetHours = 8f;
    [SerializeField] private bool smoothVisualTransitions = true;
    [SerializeField, Range(0.1f, 30f)] private float visualTimeResponse = 8f;

    [Header("Full Throttle Atmosphere")]
    [Tooltip("Keeps the world in a NFSU2/NFS 2015 dusk-to-night range. Daytime values are redirected instead of rendered.")]
    [SerializeField] private bool duskNightOnly = true;
    [SerializeField, Range(0f, 24f)] private float daytimeRedirectHour = PackageTimeOfDayUtility.DefaultDuskNightHour;
    [SerializeField, Range(0f, 1f)] private float duskNightAmbientCeiling = 0.18f;

    [Header("Sun")]
    [SerializeField] private float sunYaw = 166f;
    [SerializeField] private float sunRoll;
    [SerializeField] private float minSunIntensity = 0.03f;
    [SerializeField] private float maxSunIntensity = 1.05f;
    [SerializeField] private Color daySunColor = new Color(1f, 0.95686275f, 0.8392157f);
    [SerializeField] private Color sunsetSunColor = new Color(1f, 0.55f, 0.28f);
    [SerializeField] private Color nightSunColor = new Color(0.18f, 0.22f, 0.32f);

    [Header("Skybox Response")]
    [SerializeField] private bool driveSkybox = false;
    [SerializeField] private bool preferHDRPVolumeSkyWhenAvailable = true;
    [SerializeField] private Color daySkyTint = new Color(0.55f, 0.58f, 0.62f);
    [SerializeField] private Color sunsetSkyTint = new Color(0.55f, 0.38f, 0.36f);
    [SerializeField] private Color nightSkyTint = new Color(0.08f, 0.1f, 0.16f);
    [SerializeField] private Color dayGroundColor = new Color(0.369f, 0.349f, 0.341f);
    [SerializeField] private Color nightGroundColor = new Color(0.045f, 0.05f, 0.065f);
    [SerializeField] private float dayExposure = 1.3f;
    [SerializeField] private float nightExposure = 0.22f;

    private TimeOfDay timeOfDay;
    private Light sunLight;
    private IProgressService progressManager;
    private float lastPublishedTime = -100f;
    private TimeWindow lastPublishedWindow;
    private bool hasPublishedWindow;
    private Material appliedSkyboxMaterial;
    private float visualTimeOfDay = -1f;
    private DayNight volumeSkyOwner;
    private float nextVolumeSkyOwnerLookupTime;
    private bool loggedVolumeSkyHandoff;

    public float TimeOfDay => ConstrainGameplayTime(timeOfDay == null
        ? Mathf.Repeat(startTimeOfDay, 24f)
        : Mathf.Repeat(timeOfDay.seconds_passed / global::TimeOfDay.seconds_in_day * 24f, 24f));

    public TimeWindow CurrentWindow => EvaluateWindow(TimeOfDay);
    public bool IsNight => CurrentWindow == TimeWindow.Night || CurrentWindow == TimeWindow.LateNight;

    private void Awake()
    {
        timeOfDay = GetComponent<TimeOfDay>();
        sunLight = GetComponent<Light>();
        progressManager = ServiceResolver.Resolve<IProgressService>(null);

        if (timeOfDay != null)
        {
            timeOfDay.time_scale = 0f;
        }

        float initialTime = ResolveInitialTime();
        SetTimeWithoutPublish(initialTime);
        visualTimeOfDay = initialTime;
        RegisterAsActive();
        ApplyVisuals(forceSkyRefresh: true);
        PublishTime(force: true);
    }

    private void OnEnable()
    {
        RegisterAsActive();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(ActiveInstance, this))
        {
            ServiceLocator.Unregister<ITimeOfDayService>(this);
            ActiveInstance = null;
        }
    }

    private void Update()
    {
        if (!ReferenceEquals(ActiveInstance, this))
        {
            return;
        }

        if (timeOfDay == null)
        {
            timeOfDay = GetComponent<TimeOfDay>();
            if (timeOfDay == null)
            {
                return;
            }
        }

        if (useInspectorTimeOverride || timeMode == SunTimeMode.InspectorOverride)
        {
            SetTimeWithoutPublish(inspectorTimeOfDay);
        }
        else if (timeMode == SunTimeMode.RealWorldClock)
        {
            SetTimeWithoutPublish(GetRealWorldTimeOfDay());
        }
        else if (fullDayLengthSeconds > 0f)
        {
            float secondsPerRealSecond = global::TimeOfDay.seconds_in_day / fullDayLengthSeconds;
            timeOfDay.seconds_passed = Mathf.Repeat(
                timeOfDay.seconds_passed + Time.deltaTime * secondsPerRealSecond,
                global::TimeOfDay.seconds_in_day);
            inspectorTimeOfDay = TimeOfDay;
        }

        ApplyVisuals(forceSkyRefresh: false);
        PublishTime(force: false);
    }

    public void SetTime(float timeOfDayHours)
    {
        SetTimeWithoutPublish(timeOfDayHours);
        visualTimeOfDay = ConstrainGameplayTime(timeOfDayHours);
        ApplyVisuals(forceSkyRefresh: true);
        PublishTime(force: true);
    }

    public void SetTimeMode(SunTimeMode mode)
    {
        timeMode = mode;
        if (mode == SunTimeMode.RealWorldClock)
        {
            SetTimeWithoutPublish(GetRealWorldTimeOfDay());
        }
        else if (mode == SunTimeMode.InspectorOverride)
        {
            SetTimeWithoutPublish(inspectorTimeOfDay);
        }

        ApplyVisuals(forceSkyRefresh: true);
        PublishTime(force: true);
    }

    private void SetTimeWithoutPublish(float timeOfDayHours)
    {
        float normalizedHours = ConstrainGameplayTime(timeOfDayHours);
        if (timeOfDay == null)
        {
            return;
        }

        timeOfDay.seconds_passed = normalizedHours / 24f * global::TimeOfDay.seconds_in_day;
        inspectorTimeOfDay = normalizedHours;
    }

    private void RegisterAsActive()
    {
        if (ActiveInstance != null && !ReferenceEquals(ActiveInstance, this))
        {
            ServiceLocator.Unregister<ITimeOfDayService>(ActiveInstance);
        }

        ActiveInstance = this;
        ServiceLocator.Register<ITimeOfDayService>(this);
    }

    private void ApplyVisuals(bool forceSkyRefresh)
    {
        float renderTime = ResolveVisualTime();
        float normalizedDay = Mathf.Repeat(renderTime / 24f, 1f);
        float ambient = EvaluateAmbient(normalizedDay);
        float nightBlend = EvaluateNightBlend(renderTime);
        float sunsetBlend = EvaluateSunsetBlend(renderTime);
        if (duskNightOnly)
        {
            ambient = Mathf.Min(ambient, Mathf.Lerp(duskNightAmbientCeiling, 0.045f, nightBlend));
        }

        transform.rotation = Quaternion.Euler(
            MidnightRotation + normalizedDay * 360f,
            sunYaw,
            sunRoll);

        if (sunLight == null)
        {
            sunLight = GetComponent<Light>();
        }

        if (sunLight != null)
        {
            sunLight.intensity = Mathf.Lerp(minSunIntensity, maxSunIntensity, ambient);
            sunLight.color = Color.Lerp(
                Color.Lerp(daySunColor, sunsetSunColor, sunsetBlend),
                nightSunColor,
                nightBlend);
            sunLight.shadows = ambient > 0.08f ? LightShadows.Soft : LightShadows.None;
        }

        if (update_ambient_intensity)
        {
            RenderSettings.ambientIntensity = ambient;
        }

        if (ShouldDriveSkybox())
        {
            ApplySkybox(nightBlend, sunsetBlend, forceSkyRefresh);
        }
    }

    private bool ShouldDriveSkybox()
    {
        if (!driveSkybox)
        {
            return false;
        }

        if (!preferHDRPVolumeSkyWhenAvailable)
        {
            return true;
        }

        if (volumeSkyOwner == null && Time.unscaledTime >= nextVolumeSkyOwnerLookupTime)
        {
            nextVolumeSkyOwnerLookupTime = Time.unscaledTime + 2f;
            volumeSkyOwner = FindFirstObjectByType<DayNight>();
        }

        bool hdrpVolumeOwnsSky = volumeSkyOwner != null
            && volumeSkyOwner.driveVolumeBlend
            && volumeSkyOwner.HasVolumeProfiles;

        if (hdrpVolumeOwnsSky && !loggedVolumeSkyHandoff && Application.isPlaying)
        {
            loggedVolumeSkyHandoff = true;
            Debug.Log("[DayNight] HDRP Volume profiles are driving the sky. SunRotation will only drive the directional light/time.", this);
        }

        return !hdrpVolumeOwnsSky;
    }

    private float EvaluateAmbient(float normalizedDay)
    {
        if (ambient_intensity_curve != null && ambient_intensity_curve.length > 0)
        {
            return Mathf.Clamp01(ambient_intensity_curve.Evaluate(normalizedDay));
        }

        float hour = normalizedDay * 24f;
        float dawn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 8f, hour));
        float dusk = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(17f, 20f, hour));
        return Mathf.Clamp01(Mathf.Min(dawn, dusk));
    }

    private float ResolveInitialTime()
    {
        if (useInspectorTimeOverride || timeMode == SunTimeMode.InspectorOverride)
        {
            return ConstrainGameplayTime(inspectorTimeOfDay);
        }

        if (timeMode == SunTimeMode.RealWorldClock)
        {
            return ConstrainGameplayTime(GetRealWorldTimeOfDay());
        }

        float resolved = progressManager != null ? progressManager.WorldTimeOfDay : startTimeOfDay;
        return ConstrainGameplayTime(resolved);
    }

    private float ResolveVisualTime()
    {
        float target = TimeOfDay;
        if (!Application.isPlaying || !smoothVisualTransitions || visualTimeOfDay < 0f)
        {
            visualTimeOfDay = target;
            return target;
        }

        visualTimeOfDay = SmoothCyclicHours(visualTimeOfDay, target, visualTimeResponse, Time.deltaTime);
        return visualTimeOfDay;
    }

    private float GetRealWorldTimeOfDay()
    {
        DateTime now = useLocalRealWorldTime
            ? DateTime.Now
            : DateTime.UtcNow.AddHours(utcOffsetHours);

        return Mathf.Repeat(
            now.Hour + now.Minute / 60f + now.Second / 3600f + now.Millisecond / 3600000f,
            24f);
    }

    private float ConstrainGameplayTime(float hours)
    {
        hours = Mathf.Repeat(hours, 24f);
        if (!duskNightOnly || PackageTimeOfDayUtility.IsDuskNightHour(hours))
        {
            return hours;
        }

        return PackageTimeOfDayUtility.ConstrainToDuskNightHours(daytimeRedirectHour);
    }

    private static float SmoothCyclicHours(float current, float target, float response, float deltaTime)
    {
        float delta = Mathf.DeltaAngle(current * 15f, target * 15f) / 15f;
        float factor = 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * deltaTime);
        return Mathf.Repeat(current + delta * factor, 24f);
    }

    private void ApplySkybox(float nightBlend, float sunsetBlend, bool forceRefresh)
    {
        Material targetSkybox = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
        if (targetSkybox == null)
        {
            return;
        }

        if (forceRefresh || appliedSkyboxMaterial != targetSkybox)
        {
            RenderSettings.skybox = targetSkybox;
            appliedSkyboxMaterial = targetSkybox;
        }

        Color warmTint = Color.Lerp(daySkyTint, sunsetSkyTint, sunsetBlend);
        SetSkyboxColor(targetSkybox, "_SkyTint", Color.Lerp(warmTint, nightSkyTint, nightBlend));
        SetSkyboxColor(targetSkybox, "_GroundColor", Color.Lerp(dayGroundColor, nightGroundColor, nightBlend));
        SetSkyboxFloat(targetSkybox, "_Exposure", Mathf.Lerp(dayExposure, nightExposure, nightBlend));
    }

    private void PublishTime(bool force)
    {
        TimeWindow currentWindow = CurrentWindow;
        if (force || !hasPublishedWindow)
        {
            hasPublishedWindow = true;
            lastPublishedWindow = currentWindow;
            ServiceLocator.EventBus.Publish(new TimeWindowChangedEvent(currentWindow, currentWindow));
        }
        else if (currentWindow != lastPublishedWindow)
        {
            TimeWindow previous = lastPublishedWindow;
            lastPublishedWindow = currentWindow;
            ServiceLocator.EventBus.Publish(new TimeWindowChangedEvent(previous, currentWindow));
        }

        if (!force && Mathf.Abs(Mathf.DeltaAngle(lastPublishedTime * 15f, TimeOfDay * 15f)) < 0.75f)
        {
            return;
        }

        lastPublishedTime = TimeOfDay;
        progressManager ??= ServiceResolver.Resolve<IProgressService>(null);
        if (progressManager != null)
        {
            progressManager.SetWorldTime(TimeOfDay);
        }
        else
        {
            ServiceLocator.EventBus.Publish(new WorldTimeChangedEvent(TimeOfDay, IsNight));
        }
    }

    private static TimeWindow EvaluateWindow(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (hour < LateNightEndHour)
        {
            return TimeWindow.LateNight;
        }

        if (hour < SunsetStartHour)
        {
            return TimeWindow.Day;
        }

        if (hour < NightStartHour)
        {
            return TimeWindow.Sunset;
        }

        return TimeWindow.Night;
    }

    private static float EvaluateNightBlend(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (hour >= NightStartHour || hour < 4f)
        {
            return 1f;
        }

        if (hour < DayStartHour)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, DayStartHour, hour));
        }

        if (hour >= SunsetStartHour)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SunsetStartHour, NightStartHour, hour));
        }

        return 0f;
    }

    private static float EvaluateSunsetBlend(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (hour < 15.5f || hour > NightStartHour)
        {
            return 0f;
        }

        if (hour <= SunsetStartHour)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15.5f, SunsetStartHour, hour));
        }

        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SunsetStartHour, NightStartHour, hour));
    }

    private static void SetSkyboxColor(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetSkyboxFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }
}
