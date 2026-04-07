using UnityEngine;
using Underground.Save;

namespace Underground.TimeSystem
{
    public class DayNightCycleController : MonoBehaviour
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private Transform sunPivot;
        [SerializeField] private float fullDayLengthSeconds = 1200f;
        [SerializeField, Range(0f, 24f)] private float startTimeOfDay = 12f;
        [SerializeField] private Gradient ambientColorByTime;
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private Material daySkyboxMaterial;
        [SerializeField] private Material nightSkyboxMaterial;

        public float TimeOfDay { get; private set; }
        public bool IsNight => TimeOfDay >= 20f || TimeOfDay < 6f;

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            TimeOfDay = progressManager != null ? progressManager.WorldTimeOfDay : startTimeOfDay;
            ApplyLighting();
        }

        private void Update()
        {
            if (fullDayLengthSeconds <= 0f)
            {
                return;
            }

            TimeOfDay += (24f / fullDayLengthSeconds) * Time.deltaTime;
            if (TimeOfDay >= 24f)
            {
                TimeOfDay -= 24f;
            }

            ApplyLighting();
            progressManager?.SetWorldTime(TimeOfDay);
        }

        public void SetTime(float timeOfDay)
        {
            TimeOfDay = Mathf.Repeat(timeOfDay, 24f);
            ApplyLighting();
        }

        private void ApplyLighting()
        {
            float normalizedTime = TimeOfDay / 24f;

            if (sunPivot != null)
            {
                sunPivot.rotation = Quaternion.Euler((normalizedTime * 360f) - 90f, 170f, 0f);
            }
            else if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler((normalizedTime * 360f) - 90f, 170f, 0f);
            }

            if (sunLight != null)
            {
                float dayFactor = Mathf.Clamp01(Vector3.Dot(-sunLight.transform.forward, Vector3.up));
                sunLight.intensity = Mathf.Lerp(0.1f, 1f, dayFactor);
            }

            if (ambientColorByTime != null && ambientColorByTime.colorKeys.Length > 0)
            {
                RenderSettings.ambientLight = ambientColorByTime.Evaluate(normalizedTime);
            }

            Material targetSkybox = IsNight ? nightSkyboxMaterial : daySkyboxMaterial;
            if (targetSkybox != null && RenderSettings.skybox != targetSkybox)
            {
                RenderSettings.skybox = targetSkybox;
                DynamicGI.UpdateEnvironment();
            }
        }
    }
}
