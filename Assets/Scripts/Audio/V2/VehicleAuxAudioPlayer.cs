using UnityEngine;
using UnityEngine.Audio;

namespace Underground.Audio.V2
{
    /// <summary>
    /// Auxiliary continuous loops: turbo spool/whistle, intake resonance,
    /// drivetrain whine, and sputter. All RPM/throttle-gated.
    /// </summary>
    public sealed class VehicleAuxAudioPlayer : MonoBehaviour
    {
        [System.Serializable]
        private sealed class AuxRuntime
        {
            public AudioSource Source;
            public float TargetVolume;
            public float TargetPitch = 1f;
        }

        [Header("Spatial")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.78f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 55f;
        [SerializeField] private AudioMixerGroup outputGroup;

        [Header("Response")]
        [SerializeField] private float volumeResponse = 12f;
        [SerializeField] private float pitchResponse = 10f;

        private Transform sourceRoot;
        private AuxRuntime turboSpoolRuntime;
        private AuxRuntime turboWhistleRuntime;
        private AuxRuntime intakeRuntime;
        private AuxRuntime drivetrainRuntime;
        private AuxRuntime sputterRuntime;

        // Bank settings cached
        private NFSU2CarAudioBank.TurboAudioPackage turboSettings;
        private NFSU2CarAudioBank.SweetenerAudioPackage sweetenerSettings;
        private bool isExterior = true;
        private bool initialized;

        public void Initialize(Transform root)
        {
            sourceRoot = root;
            initialized = true;
        }

        public void ApplyFromBank(NFSU2CarAudioBank.TierAudioPackage tier, bool exterior)
        {
            if (tier == null) return;
            isExterior = exterior;
            turboSettings = tier.turbo;
            sweetenerSettings = tier.sweetener;

            // Rebuild sources
            DestroyAuxSources();
            turboSpoolRuntime = CreateAux("AUX_TurboSpool", GetClip(turboSettings?.spool));
            turboWhistleRuntime = CreateAux("AUX_TurboWhistle", GetClip(turboSettings?.whistle));
            intakeRuntime = CreateAux("AUX_Intake", GetClip(sweetenerSettings?.intake));
            drivetrainRuntime = CreateAux("AUX_Drivetrain", GetClip(sweetenerSettings?.drivetrain));
            sputterRuntime = CreateAux("AUX_Sputter", GetClip(sweetenerSettings?.sputter));
        }

        public void UpdateAux(EngineAudioStateFeed feed, float dt)
        {
            if (!initialized || feed == null) return;

            // Turbo spool
            if (turboSpoolRuntime != null && turboSettings != null)
            {
                float spoolVol = feed.TurboSpool * GetVolume(turboSettings.spool) * turboSettings.strength;
                float spoolPitch = Mathf.Lerp(
                    turboSettings.spool?.pitchRange.x ?? 0.85f,
                    turboSettings.spool?.pitchRange.y ?? 1.35f,
                    feed.TurboSpool);
                turboSpoolRuntime.TargetVolume = spoolVol;
                turboSpoolRuntime.TargetPitch = spoolPitch;
            }

            // Turbo whistle
            if (turboWhistleRuntime != null && turboSettings != null)
            {
                bool whistleActive = feed.NormalizedRPM > (turboSettings.whistleStartRpm01)
                    && feed.SmoothedThrottle > (turboSettings.whistleStartThrottle);
                float whistleT = whistleActive ? feed.TurboSpool : 0f;
                turboWhistleRuntime.TargetVolume = whistleT * GetVolume(turboSettings.whistle) * turboSettings.strength;
                turboWhistleRuntime.TargetPitch = Mathf.Lerp(
                    turboSettings.whistle?.pitchRange.x ?? 0.9f,
                    turboSettings.whistle?.pitchRange.y ?? 1.3f,
                    feed.NormalizedRPM);
            }

            // Intake
            if (intakeRuntime != null)
            {
                float intakeT = feed.SmoothedThrottle * Mathf.InverseLerp(0.15f, 0.6f, feed.NormalizedRPM);
                intakeRuntime.TargetVolume = intakeT * GetVolume(sweetenerSettings?.intake);
                intakeRuntime.TargetPitch = Mathf.Lerp(
                    sweetenerSettings?.intake?.pitchRange.x ?? 0.9f,
                    sweetenerSettings?.intake?.pitchRange.y ?? 1.3f,
                    feed.NormalizedRPM);
            }

            // Drivetrain
            if (drivetrainRuntime != null)
            {
                float driveT = Mathf.InverseLerp(5f, 80f, feed.SmoothedSpeedKph);
                drivetrainRuntime.TargetVolume = driveT * GetVolume(sweetenerSettings?.drivetrain);
                drivetrainRuntime.TargetPitch = Mathf.Lerp(
                    sweetenerSettings?.drivetrain?.pitchRange.x ?? 0.8f,
                    sweetenerSettings?.drivetrain?.pitchRange.y ?? 1.45f,
                    feed.NormalizedRPM);
            }

            // Sputter (off-throttle, mid-high RPM)
            if (sputterRuntime != null && sweetenerSettings != null)
            {
                bool sputterActive = feed.SmoothedThrottle < (sweetenerSettings.sputterThrottleUpper)
                    && feed.NormalizedRPM > (sweetenerSettings.sputterMinRpm01);
                float sputterT = sputterActive ? Mathf.InverseLerp(sweetenerSettings.sputterMinRpm01, 0.85f, feed.NormalizedRPM) : 0f;
                sputterRuntime.TargetVolume = sputterT * (sweetenerSettings.sputterVolume) * GetVolume(sweetenerSettings.sputter);
                sputterRuntime.TargetPitch = Mathf.Lerp(0.9f, 1.1f, feed.NormalizedRPM);
            }

            // Smooth all
            SmoothApply(turboSpoolRuntime, dt);
            SmoothApply(turboWhistleRuntime, dt);
            SmoothApply(intakeRuntime, dt);
            SmoothApply(drivetrainRuntime, dt);
            SmoothApply(sputterRuntime, dt);
        }

        private void SmoothApply(AuxRuntime r, float dt)
        {
            if (r?.Source == null) return;
            r.Source.volume = Mathf.Lerp(r.Source.volume, r.TargetVolume, 1f - Mathf.Exp(-volumeResponse * dt));
            r.Source.pitch = Mathf.Lerp(r.Source.pitch, r.TargetPitch, 1f - Mathf.Exp(-pitchResponse * dt));
            if (!r.Source.isPlaying && r.Source.clip != null) r.Source.Play();
        }

        private float GetVolume(NFSU2CarAudioBank.AudioLoopLayer layer)
        {
            if (layer == null) return 0f;
            return isExterior ? layer.exteriorVolume : layer.interiorVolume;
        }

        private AudioClip GetClip(NFSU2CarAudioBank.AudioLoopLayer layer)
        {
            if (layer == null) return null;
            return isExterior ? layer.exteriorClip : layer.interiorClip;
        }

        private AuxRuntime CreateAux(string name, AudioClip clip)
        {
            if (sourceRoot == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(sourceRoot, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.pitch = 1f;
            src.priority = 100;
            src.dopplerLevel = 0f;
            src.spatialBlend = spatialBlend;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.outputAudioMixerGroup = outputGroup;
            if (clip != null) { src.clip = clip; src.Play(); }
            return new AuxRuntime { Source = src };
        }

        private void DestroyAuxSources()
        {
            if (sourceRoot == null) return;
            for (int i = sourceRoot.childCount - 1; i >= 0; i--)
            {
                var child = sourceRoot.GetChild(i);
                if (child.name.StartsWith("AUX_")) Destroy(child.gameObject);
            }
        }
    }
}
