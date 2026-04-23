using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Audio
{
    [DefaultExecutionOrder(-7800)]
    public sealed class FTGarageAudioPreviewDirector : MonoBehaviour
    {
        [SerializeField] private Transform sourceAnchor;
        [SerializeField] private bool enablePreviewAudio;
        [SerializeField] private bool previewOnSelection = true;

        private FTSelectedCarRuntime selectedCarRuntime;
        private FTCarRegistry carRegistry;
        private FTAudioProfileRegistry audioRegistry;
        private FTEventBus eventBus;
        private AudioSource idleSource;
        private AudioSource revSource;
        private FTVehicleAudioProfile profile;
        private float rev01;

        private void Awake()
        {
            selectedCarRuntime = FTServices.Get<FTSelectedCarRuntime>();
            carRegistry = FTServices.Get<FTCarRegistry>();
            audioRegistry = FTServices.Get<FTAudioProfileRegistry>();
            eventBus = FTServices.Get<FTEventBus>();
            eventBus.Subscribe<FTCarSelectionChangedSignal>(OnSelectionChanged);
            CreateSources();
            ResolveCurrentProfile();
        }

        private void OnDestroy()
        {
            eventBus?.Unsubscribe<FTCarSelectionChangedSignal>(OnSelectionChanged);
        }

        private void Update()
        {
            if (!enablePreviewAudio)
            {
                StopSources();
                return;
            }

            if (profile == null || idleSource == null || revSource == null)
            {
                return;
            }

            float targetRev = previewOnSelection ? profile.garagePreview.previewThrottle : 0f;
            float response = targetRev > rev01
                ? 1f / Mathf.Max(0.05f, profile.garagePreview.revRiseSeconds)
                : 1f / Mathf.Max(0.05f, profile.garagePreview.revFallSeconds);
            rev01 = Mathf.Lerp(rev01, targetRev, 1f - Mathf.Exp(-response * Time.deltaTime));

            idleSource.volume = Mathf.Lerp(0.45f, 0.2f, rev01) * profile.idle.volume;
            revSource.volume = rev01 * profile.lowAccel.volume;
            revSource.pitch = Mathf.Lerp(0.88f, 1.14f, rev01);
        }

        public void TriggerPreviewRev(float intensity01)
        {
            previewOnSelection = true;
            if (profile != null)
            {
                profile.garagePreview.previewThrottle = Mathf.Clamp01(intensity01);
            }
        }

        private void ResolveCurrentProfile()
        {
            if (!enablePreviewAudio)
            {
                StopSources();
                return;
            }

            FTCarDefinition car = carRegistry.Get(selectedCarRuntime.CurrentCarId);
            if (car == null)
            {
                return;
            }

            audioRegistry.ResolveProfile(car, out profile, out string report);
            if (profile == null)
            {
                Debug.LogWarning($"[SacredCore] Garage audio preview missing profile. {report}");
                return;
            }

            idleSource.clip = profile.idle.clip;
            revSource.clip = profile.lowAccel.clip != null ? profile.lowAccel.clip : profile.midAccel.clip;
            if (idleSource.clip != null && !idleSource.isPlaying) idleSource.Play();
            if (revSource.clip != null && !revSource.isPlaying) revSource.Play();
            Debug.Log($"[SacredCore] Garage audio preview car={car.carId}, profile={profile.audioProfileId}. {report}");
        }

        private void OnSelectionChanged(FTCarSelectionChangedSignal signal)
        {
            ResolveCurrentProfile();
            rev01 = 0f;
        }

        private void CreateSources()
        {
            Transform parent = sourceAnchor != null ? sourceAnchor : transform;
            idleSource = CreateLoop("FT_GarageIdle", parent);
            revSource = CreateLoop("FT_GarageRev", parent);
        }

        private void StopSources()
        {
            if (idleSource != null && idleSource.isPlaying)
            {
                idleSource.Stop();
            }

            if (revSource != null && revSource.isPlaying)
            {
                revSource.Stop();
            }
        }

        private static AudioSource CreateLoop(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.35f;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
