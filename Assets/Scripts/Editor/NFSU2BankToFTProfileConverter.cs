#if UNITY_EDITOR
using FullThrottle.SacredCore.Audio;
using Underground.Audio;
using UnityEditor;
using UnityEngine;

namespace FullThrottle.SacredCore.EditorTools
{
    public static class NFSU2BankToFTProfileConverter
    {
        private const string DefaultOutputFolder = "Assets/ScriptableObjects/FullThrottle/AudioProfiles";

        [MenuItem("Full Throttle/Audio/Convert Selected NFSU2 Bank To FT Profile")]
        public static void ConvertSelectedBankToProfile()
        {
            NFSU2CarAudioBank bank = Selection.activeObject as NFSU2CarAudioBank;
            if (bank == null)
            {
                EditorUtility.DisplayDialog(
                    "NFSU2 Bank Converter",
                    "Select one NFSU2CarAudioBank asset first. This tool converts authoring data into one SacredCore FTVehicleAudioProfile.",
                    "OK");
                return;
            }

            VehicleAudioTier tier = bank.defaultTier;
            string profileId = ToProfileId(bank.name, tier);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultOutputFolder}/{profileId}.asset");
            FTVehicleAudioProfile profile = ConvertBank(bank, tier, profileId, assetPath);
            Selection.activeObject = profile;
        }

        [MenuItem("Full Throttle/Audio/Build Car 27 Hero Prototype Profile")]
        public static void BuildCar27HeroPrototypeProfile()
        {
            const string bankPath = "Assets/ScriptableObjects/Vehicles/Car_27_AudioBank.asset";
            const string outputPath = "Assets/ScriptableObjects/FullThrottle/AudioProfiles/car_27_hero_stock.asset";
            NFSU2CarAudioBank bank = AssetDatabase.LoadAssetAtPath<NFSU2CarAudioBank>(bankPath);
            if (bank == null)
            {
                EditorUtility.DisplayDialog(
                    "Car 27 Hero Prototype",
                    $"Could not find {bankPath}. Create/import the NFSU2CarAudioBank first, then rerun this.",
                    "OK");
                return;
            }

            FTVehicleAudioProfile profile = ConvertBank(bank, VehicleAudioTier.Stock, "car_27_hero_stock", outputPath);
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        public static FTVehicleAudioProfile ConvertBank(NFSU2CarAudioBank bank, VehicleAudioTier tier, string profileId, string outputAssetPath)
        {
            if (bank == null)
            {
                Debug.LogError("[SacredCore] NFSU2 converter received null bank.");
                return null;
            }

            EnsureOutputFolder(outputAssetPath);

            FTVehicleAudioProfile profile = AssetDatabase.LoadAssetAtPath<FTVehicleAudioProfile>(outputAssetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FTVehicleAudioProfile>();
                AssetDatabase.CreateAsset(profile, outputAssetPath);
            }

            ApplyBankToProfile(bank, tier, profileId, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            profile.ValidateRequiredLayers(out string validation);
            Debug.Log($"[SacredCore] Converted NFSU2CarAudioBank '{bank.name}' tier={tier} -> FTVehicleAudioProfile '{profile.audioProfileId}'. {validation}");
            return profile;
        }

        public static void ApplyBankToProfile(NFSU2CarAudioBank bank, VehicleAudioTier tier, string profileId, FTVehicleAudioProfile profile)
        {
            if (bank == null || profile == null)
            {
                return;
            }

            NFSU2CarAudioBank.TierAudioPackage package = bank.GetTier(tier);
            float tierVolume = Mathf.Max(0.01f, package.tierMasterVolume);

            profile.audioProfileId = string.IsNullOrWhiteSpace(profileId) ? ToProfileId(bank.name, tier) : profileId;
            profile.audioFamilyTag = InferFamilyTag(bank.name);
            profile.dedicatedHeroProfile = true;
            profile.devEmergencyFallback = false;
            profile.inheritsFamily = false;

            CopyLoop(package.idle, profile.idle, 900f, 650f, 1450f, 0.74f * tierVolume, null);
            CopyLoop(Choose(package.accelLow, package.GetEngineBand(1), package.GetEngineBand(2)), profile.lowAccel, 1850f, 950f, 3000f, 0.72f * tierVolume, profile.idle.clip);
            CopyLoop(Choose(package.accelMid, package.GetEngineBand(3), package.GetEngineBand(4)), profile.midAccel, 3900f, 2650f, 5550f, 0.78f * tierVolume, profile.lowAccel.clip);
            CopyLoop(Choose(package.accelHigh, package.GetEngineBand(5), package.GetEngineBand(6)), profile.highAccel, 5950f, 5000f, 7250f, 0.78f * tierVolume, profile.midAccel.clip);
            CopyLoop(Choose(package.limiter, package.GetEngineBand(7), package.accelHigh), profile.topRedline, 7425f, 7050f, 8150f, 0.58f * tierVolume, profile.highAccel.clip);

            CopyLoop(Choose(package.decelLow, package.GetEngineBand(1), package.accelLow), profile.lowDecel, 1650f, 850f, 2500f, 0.34f * tierVolume, profile.lowAccel.clip);
            CopyLoop(Choose(package.decelMid, package.GetEngineBand(4), package.accelMid), profile.midDecel, 3550f, 2550f, 5200f, 0.34f * tierVolume, profile.midAccel.clip);
            CopyLoop(Choose(package.decelHigh, package.GetEngineBand(6), package.accelHigh), profile.highDecel, 6750f, 6200f, 7300f, 0.18f * tierVolume, profile.highAccel.clip);

            CopyTransient(package.shift.shiftUp, profile.shiftUp, 0.72f * tierVolume);
            CopyTransient(package.shift.shiftDown, profile.shiftDown, 0.64f * tierVolume);
            CopyTransient(package.sweetener.crackle, profile.throttleLift, 0.20f * tierVolume);

            CopyLoop(package.sweetener.intake, profile.intake, 4200f, 1800f, 7800f, 0.09f * tierVolume, null);
            CopyLoop(package.sweetener.drivetrain, profile.drivetrainWhine, 4200f, 1100f, 8100f, 0.055f * tierVolume, null);
            CopyLoop(package.sweetener.sputter, profile.sweetenerSputterLoop, 4200f, 2300f, 7600f, Mathf.Max(0.02f, package.sweetener.sputterVolume) * tierVolume, null);
            CopyTransient(package.sweetener.crackle, profile.sweetenerCrackle, 0.20f * tierVolume);
            CopyTransient(package.sweetener.sparkChatter, profile.sparkChatter, 0.08f * tierVolume);

            profile.crackleMinRPM01 = Mathf.Clamp01(package.sweetener.crackleMinRpm01);
            profile.crackleChancePerSecond = Mathf.Clamp(package.sweetener.crackleChancePerSecond, 0f, 8f);
            profile.sputterMinRPM01 = Mathf.Clamp01(package.sweetener.sputterMinRpm01);
            profile.sputterThrottleUpper = Mathf.Clamp01(package.sweetener.sputterThrottleUpper);
            profile.sparkChatterChancePerSecond = Mathf.Clamp(package.sweetener.sparkChatterChancePerSecond, 0f, 2f);

            CopyLoop(package.turbo.spool, profile.turboSpool, 4500f, 1800f, 7800f, package.turbo.strength * 0.20f * tierVolume, null);
            CopyLoop(package.turbo.whistle, profile.turboWhistle, 5600f, 3200f, 8100f, package.turbo.strength * 0.08f * tierVolume, null);
            CopyTransient(package.turbo.blowOff, profile.turboBlowoff, 0.38f * package.turbo.strength * tierVolume);

            CopyLoop(package.skid.skid, profile.skidTire, 3000f, 1000f, 7000f, 0.34f * tierVolume, null);

            CopySweep(package.accelSweep, profile.accelSweep, 0.12f * tierVolume);
            CopySweep(package.decelSweep, profile.decelSweep, 0.10f * tierVolume);

            profile.limiter.enterRPM01 = Mathf.Clamp01(bank.tuning.limiterEnter01);
            profile.limiter.volumePulse = Mathf.Clamp(package.limiter.exteriorVolume * 0.045f, 0.02f, 0.12f);
            profile.limiter.pulseHz = 12f;

            profile.response.rpmRiseResponse = Mathf.Clamp(bank.tuning.rpmRiseResponse * 0.72f, 5f, 11f);
            profile.response.rpmFallResponse = Mathf.Clamp(bank.tuning.rpmFallResponse * 0.82f, 5f, 10f);
            profile.response.throttleResponse = Mathf.Clamp(bank.tuning.throttleResponse * 0.82f, 8f, 14f);
            profile.response.volumeResponse = Mathf.Clamp(bank.tuning.layerFadeSpeed * 1.25f, 9f, 15f);
            profile.response.pitchResponse = 6.5f;
            profile.response.pitchClamp = new Vector2(0.76f, 1.23f);
            profile.response.minimumBedVolume = 0.32f;
            profile.response.bedProtectionBoost = 0.18f;
            profile.response.shiftDuck = Mathf.Clamp(package.shift.duckAmount * 0.42f, 0.08f, 0.16f);
            profile.response.shiftDuckDuration = Mathf.Clamp(package.shift.duckDuration, 0.06f, 0.12f);
            profile.response.shiftPitchDip = Mathf.Clamp(package.shift.pitchDrop * 0.08f, 0.004f, 0.014f);

            profile.garagePreview.revStyle = "true-profile-preview";
            profile.garagePreview.previewThrottle = 0.32f;
            profile.garagePreview.revRiseSeconds = 0.85f;
            profile.garagePreview.revFallSeconds = 1.1f;
        }

        private static void CopyLoop(NFSU2CarAudioBank.AudioLoopLayer source, FTAudioLoopLayer target, float referenceRPM, float minRPM, float maxRPM, float volume, AudioClip fallback)
        {
            target.clip = ResolveClip(source, fallback);
            target.referenceRPM = referenceRPM;
            target.minRPM = minRPM;
            target.maxRPM = maxRPM;
            target.volume = Mathf.Clamp(volume * ResolveExteriorVolume(source), 0f, 2f);
            target.basePitch = ResolveBasePitch(source);
        }

        private static void CopyTransient(NFSU2CarAudioBank.AudioOneShotLayer source, FTAudioTransientLayer target, float volume)
        {
            target.clip = source != null ? source.clip : null;
            target.volume = Mathf.Clamp(volume * (source != null ? source.volume : 1f), 0f, 2f);
            target.pitchRange = source != null ? source.pitchRange : new Vector2(0.96f, 1.04f);
            target.cooldown = Mathf.Max(0.06f, target.cooldown);
        }

        private static void CopySweep(NFSU2CarAudioBank.AudioSweepLayer source, FTAudioSweepLayer target, float volume)
        {
            AudioClip clip = source != null ? source.exteriorClip != null ? source.exteriorClip : source.interiorClip : null;
            target.enabled = clip != null;
            target.clip = clip;
            target.volume = Mathf.Clamp(volume * (source != null ? source.exteriorVolume : 1f), 0f, 1f);
            target.playbackWindow01 = source != null ? source.playbackWindow01 : new Vector2(0.03f, 0.97f);
            target.pitchCorrectionRange = source != null ? source.pitchRange : new Vector2(0.98f, 1.02f);
            target.seekResponse = 6f;
            target.resyncThresholdSeconds = 0.65f;
        }

        private static NFSU2CarAudioBank.AudioLoopLayer Choose(params NFSU2CarAudioBank.AudioLoopLayer[] layers)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                NFSU2CarAudioBank.AudioLoopLayer layer = layers[i];
                if (ResolveClip(layer, null) != null)
                {
                    return layer;
                }
            }

            return layers.Length > 0 ? layers[0] : null;
        }

        private static AudioClip ResolveClip(NFSU2CarAudioBank.AudioLoopLayer source, AudioClip fallback)
        {
            if (source == null)
            {
                return fallback;
            }

            return source.exteriorClip != null ? source.exteriorClip : source.interiorClip != null ? source.interiorClip : fallback;
        }

        private static float ResolveExteriorVolume(NFSU2CarAudioBank.AudioLoopLayer source)
        {
            return source != null ? Mathf.Max(0f, source.exteriorVolume) : 1f;
        }

        private static float ResolveBasePitch(NFSU2CarAudioBank.AudioLoopLayer source)
        {
            if (source == null)
            {
                return 1f;
            }

            return Mathf.Clamp((source.pitchRange.x + source.pitchRange.y) * 0.5f, 0.75f, 1.35f);
        }

        private static string ToProfileId(string sourceName, VehicleAudioTier tier)
        {
            string safe = sourceName.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            return $"{safe}_{tier.ToString().ToLowerInvariant()}";
        }

        private static string InferFamilyTag(string sourceName)
        {
            string lower = sourceName.ToLowerInvariant();
            if (lower.Contains("truck") || lower.Contains("tk")) return "heavy_truck";
            if (lower.Contains("turbo")) return "turbo_i4";
            if (lower.Contains("v6")) return "turbo_v6";
            if (lower.Contains("27")) return "turbo_i4_hero";
            return "ug2_imported";
        }

        private static void EnsureOutputFolder(string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
