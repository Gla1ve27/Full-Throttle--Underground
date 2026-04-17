using Underground.Core.Architecture;
using UnityEngine;

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
    [SerializeField, Range(0f, 24f)] private float startTimeOfDay = 22f;
    [SerializeField] private float fullDayLengthSeconds = 1200f;
    [SerializeField] private bool useInspectorTimeOverride;
    [SerializeField, Range(0f, 24f)] private float inspectorTimeOfDay = 22f;

    [Header("Sun")]
    [SerializeField] private float sunYaw = 166f;
    [SerializeField] private float sunRoll;
    [SerializeField] private float minSunIntensity = 0.03f;
    [SerializeField] private float maxSunIntensity = 1.05f;
    [SerializeField] private Color daySunColor = new Color(1f, 0.95686275f, 0.8392157f);
    [SerializeField] private Color sunsetSunColor = new Color(1f, 0.55f, 0.28f);
    [SerializeField] private Color nightSunColor = new Color(0.18f, 0.22f, 0.32f);

    [Header("Skybox Response")]
    [SerializeField] private bool driveSkybox = true;
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

    public float TimeOfDay => timeOfDay == null
        ? Mathf.Repeat(startTimeOfDay, 24f)
        : Mathf.Repeat(timeOfDay.seconds_passed / global::TimeOfDay.seconds_in_day * 24f, 24f);

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

        float initialTime = progressManager != null ? progressManager.WorldTimeOfDay : startTimeOfDay;
        SetTimeWithoutPublish(initialTime);
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

        if (useInspectorTimeOverride)
        {
            SetTimeWithoutPublish(inspectorTimeOfDay);
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
        ApplyVisuals(forceSkyRefresh: true);
        PublishTime(force: true);
    }

    private void SetTimeWithoutPublish(float timeOfDayHours)
    {
        float normalizedHours = Mathf.Repeat(timeOfDayHours, 24f);
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
        float normalizedDay = Mathf.Repeat(TimeOfDay / 24f, 1f);
        float ambient = EvaluateAmbient(normalizedDay);
        float nightBlend = EvaluateNightBlend(TimeOfDay);
        float sunsetBlend = EvaluateSunsetBlend(TimeOfDay);

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

        if (driveSkybox)
        {
            ApplySkybox(nightBlend, sunsetBlend, forceSkyRefresh);
        }
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
