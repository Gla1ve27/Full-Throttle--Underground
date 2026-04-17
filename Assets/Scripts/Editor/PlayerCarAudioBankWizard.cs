// PlayerCarAudioBankWizard.cs
// Creates a tuned NFSU2CarAudioBank specifically for PlayerCar testing.
// Menu: Underground > Audio > Create PlayerCar UG2 Test Bank

using UnityEditor;
using UnityEngine;
using Underground.Audio;

namespace Underground.EditorTools
{
    public static class PlayerCarAudioBankWizard
    {
        // ── Clip source paths (same source clips as StarterCarAudioBank) ──
        private const string EngineBase     = "Assets/Audio/Car Sounds and stuffs/CAR_00_ENG_MB_EE";
        private const string EngineName     = "CAR_00_ENG_MB_EE";
        private const string InteriorBase   = "Assets/Audio/Car Sounds and stuffs/CAR_00_ENG_MB_SPU";
        private const string InteriorName   = "CAR_00_ENG_MB_SPU";
        private const string GinAccelPath   = "Assets/Audio/Car Sounds and stuffs/GIN_Acura_ITR/GIN_Acura_ITR.wav";
        private const string GinDecelPath   = "Assets/Audio/Car Sounds and stuffs/GIN_Acura_ITR_DCL/GIN_Acura_ITR_DCL.wav";
        private const string GearBase       = "Assets/Audio/Car Sounds and stuffs/GEAR_LRG_Base";
        private const string SweetenerBase  = "Assets/Audio/Car Sounds and stuffs/SWTN_CAR_00_MB";
        private const string TurboBase      = "Assets/Audio/Car Sounds and stuffs/TURBO_SML1_0_MB";
        private const string SkidBase       = "Assets/Audio/Car Sounds and stuffs/SKID_PAV_MB";

        private const string TestBankPath = "Assets/ScriptableObjects/Vehicles/PlayerCar_UG2_Test_AudioBank.asset";

        [MenuItem("Underground/Audio/Create PlayerCar UG2 Test Bank", priority = 71)]
        public static void CreatePlayerCarTestBank()
        {
            EnsureFolder("Assets/ScriptableObjects");
            EnsureFolder("Assets/ScriptableObjects/Vehicles");

            NFSU2CarAudioBank bank = AssetDatabase.LoadAssetAtPath<NFSU2CarAudioBank>(TestBankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<NFSU2CarAudioBank>();
                AssetDatabase.CreateAsset(bank, TestBankPath);
            }

            bank.defaultTier = VehicleAudioTier.Stock;
            ApplyTunedTuning(bank.tuning);
            ApplyTunedTier(bank.GetTier(VehicleAudioTier.Stock), 1f);
            ApplyTunedTier(bank.GetTier(VehicleAudioTier.Street), 1.05f);
            ApplyTunedTier(bank.GetTier(VehicleAudioTier.Pro), 1.1f);
            ApplyTunedTier(bank.GetTier(VehicleAudioTier.Extreme), 1.15f);

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = bank;
            EditorGUIUtility.PingObject(bank);

            EditorUtility.DisplayDialog(
                "PlayerCar UG2 Test Bank Ready",
                $"Created / updated:\n{TestBankPath}\n\n" +
                "Assign this to VehicleAudioController.audioBank on the PlayerCar " +
                "(AudioRoot child) to hear the tuned mix.",
                "OK");
        }

        // ────────────────────────────────────────────────────────────────────
        //  TUNING — tweaked for immediate playability
        // ────────────────────────────────────────────────────────────────────

        private static void ApplyTunedTuning(NFSU2CarAudioBank.RuntimeTuning t)
        {
            if (t == null) return;

            // ── Output ──
            t.masterVolume           = 1f;
            t.spatialBlend           = 1f;
            t.minDistance             = 4f;
            t.maxDistance             = 85f;
            t.layerFadeSpeed         = 6f;   // was 9 → slower fades prevent "restart" artefact on lift-off

            // ── Telemetry smoothing ──
            t.rpmRiseResponse        = 14f;  // was 12 → snappier on-throttle response
            t.rpmFallResponse        = 5f;   // was 8  → slower coast-down for smoother decel
            t.throttleResponse       = 16f;
            t.brakeResponse          = 16f;
            t.speedResponse          = 8f;

            // ── State thresholds ──
            t.idleUpper01            = 0.18f;
            t.decelSweepLowRpmCutoff01 = 0.18f; // was 0.12 → avoid decel sweep sound near idle
            t.lowUpper01             = 0.42f;
            t.midUpper01             = 0.72f;
            t.limiterEnter01         = 0.94f;
            t.throttleOnThreshold    = 0.12f;
            t.throttleOffThreshold   = 0.06f;
            t.idleSpeedThresholdKph  = 6f;

            // ── Launch ──
            t.launchThrottleThreshold = 0.62f;
            t.launchRpmUpper01       = 0.36f;
            t.launchSpeedUpperKph    = 9f;
            t.launchHoldTime         = 0.22f;

            // ── Hybrid loop bank — ENGINE BANDS ARE THE MAIN BODY ──
            t.band01Center           = 0.08f;
            t.band02Center           = 0.16f;
            t.band03Center           = 0.28f;
            t.band04Center           = 0.40f;
            t.band05Center           = 0.54f;
            t.band06Center           = 0.68f;
            t.band07Center           = 0.82f;
            t.band08Center           = 0.94f;
            t.engineBandWidth        = 0.16f;
            t.loopBankDominantVolume  = 0.82f;  // was 0.55 → bands are now the dominant sound
            t.accelCharacterVolume   = 0.15f;   // was 0    → subtle sweep character on accel
            t.decelCharacterVolume   = 0.10f;   // was 0    → subtle sweep character on decel

            // ── Sweep scrubbing ──
            t.sweepSeekResponse      = 6f;
            t.sweepDominantVolume    = 0.35f;   // was 0.75 → sweeps are character, not body
            t.sweepResyncThresholdSeconds = 0.75f;
            t.sweepPitchCorrectionLimit   = 0.025f;

            // ── Interior detection ──
            t.interiorDetectionExtents = new Vector3(1.2f, 1.1f, 1.7f);
            t.interiorTransitionSpeed  = 6f;
        }

        private static void ApplyTunedTier(NFSU2CarAudioBank.TierAudioPackage tier, float masterVolume)
        {
            if (tier == null) return;

            tier.tierMasterVolume = masterVolume;

            // ── 8 engine bands (main body) ──
            for (int i = 0; i < 8; i++)
            {
                float n = i / 7f;
                float lowPitch  = Mathf.Lerp(0.98f, 0.94f, n);
                float highPitch = Mathf.Lerp(1.05f, 1.02f, n);
                SetLoop(tier.GetEngineBand(i),
                    Clip($"{EngineBase}/{EngineName}_{i + 1:00}.wav"),
                    Clip($"{InteriorBase}/{InteriorName}_{i + 1:00}.wav"),
                    1f, 0.82f, new Vector2(lowPitch, highPitch));
            }

            // ── Idle — clearly audible, band 1 reuse ──
            SetLoop(tier.idle,
                Clip($"{EngineBase}/{EngineName}_01.wav"),
                Clip($"{InteriorBase}/{InteriorName}_01.wav"),
                0.95f, 0.75f, new Vector2(0.92f, 1.08f));

            // ── Sweeps — kept LOW so they add character, not dominate ──
            SetSweep(tier.accelSweep, Clip(GinAccelPath),
                0.30f, 0.18f, new Vector2(0.08f, 0.92f));  // was 0.8/0.55
            SetSweep(tier.decelSweep, Clip(GinDecelPath),
                0.22f, 0.14f, new Vector2(0.08f, 0.92f));  // was 0.7/0.48

            // ── Accel/decel character loops: cleared (using sweeps instead) ──
            ClearLoop(tier.accelLow);
            ClearLoop(tier.accelMid);
            ClearLoop(tier.accelHigh);
            ClearLoop(tier.decelLow);
            ClearLoop(tier.decelMid);
            ClearLoop(tier.decelHigh);

            // ── Limiter — band 8 at reduced volume ──
            SetLoop(tier.limiter,
                Clip($"{EngineBase}/{EngineName}_08.wav"),
                Clip($"{InteriorBase}/{InteriorName}_08.wav"),
                0.72f, 0.55f, new Vector2(0.96f, 1.04f));

            // ── Reverse — band 1 at low pitch ──
            SetLoop(tier.reverse,
                Clip($"{EngineBase}/{EngineName}_01.wav"),
                Clip($"{InteriorBase}/{InteriorName}_01.wav"),
                0.65f, 0.5f, new Vector2(0.65f, 1f));

            // ── One-shots ──
            SetOneShot(tier.accelFromIdle, null, 0.75f);

            // ── Shift ──
            tier.shift.duckAmount = 0.28f;
            tier.shift.duckDuration = 0.12f;
            tier.shift.cooldown = 0.08f;
            tier.shift.pitchDrop = 0.12f;
            SetOneShot(tier.shift.shiftUp,   Clip($"{GearBase}/GEAR_LRG_Base_01.wav"), 0.9f);
            SetOneShot(tier.shift.shiftDown,  Clip($"{GearBase}/GEAR_LRG_Base_02.wav"), 0.8f);

            // ── Turbo (subtle) ──
            tier.turbo.strength = 0.55f;  // was 0.65
            { AudioClip c = Clip($"{TurboBase}/TURBO_SML1_0_MB_01.wav"); SetLoop(tier.turbo.spool, c, c, 0.22f, 0.12f, new Vector2(0.9f, 1.12f)); }
            { AudioClip c = Clip($"{TurboBase}/TURBO_SML1_0_MB_02.wav"); SetLoop(tier.turbo.whistle, c, c, 0.14f, 0.07f, new Vector2(0.95f, 1.12f)); }
            SetOneShot(tier.turbo.blowOff, Clip($"{TurboBase}/TURBO_SML1_0_MB_03.wav"), 0.40f);

            // ── Sweetener (subtle character) ──
            { AudioClip c = Clip($"{SweetenerBase}/SWTN_CAR_00_MB_01.wav"); SetLoop(tier.sweetener.intake, c, c, 0.10f, 0.08f, new Vector2(0.98f, 1.02f)); }
            { AudioClip c = Clip($"{SweetenerBase}/SWTN_CAR_00_MB_04.wav"); SetLoop(tier.sweetener.drivetrain, c, c, 0.08f, 0.06f, new Vector2(0.98f, 1.02f)); }
            { AudioClip c = Clip($"{SweetenerBase}/SWTN_CAR_00_MB_06.wav"); SetLoop(tier.sweetener.sputter, c, c, 0.14f, 0.10f, new Vector2(0.99f, 1.01f)); }
            SetOneShot(tier.sweetener.crackle,      Clip($"{SweetenerBase}/SWTN_CAR_00_MB_02.wav"), 0.22f);
            SetOneShot(tier.sweetener.sparkChatter,  Clip($"{SweetenerBase}/SWTN_CAR_00_MB_07.wav"), 0.16f);

            // ── Lift-off crackles: enabled subtly to give life to decel ──
            tier.sweetener.enableLiftOffCrackles = true;
            tier.sweetener.crackleMinRpm01       = 0.40f;
            tier.sweetener.crackleChancePerSecond = 1.8f;   // subtle, not spammy
            tier.sweetener.sputterMinRpm01       = 0.30f;
            tier.sweetener.sputterThrottleUpper  = 0.14f;
            tier.sweetener.sputterVolume         = 0.10f;   // was 0 → now audible but restrained
            tier.sweetener.sparkChatterChancePerSecond = 0.6f; // rare spark pops

            // ── Skid ──
            { AudioClip c = Clip($"{SkidBase}/SKID_PAV_MB_01.wav"); SetLoop(tier.skid.skid, c, c, 0.45f, 0.16f, new Vector2(0.9f, 1.08f)); }
            tier.skid.slipThreshold = 0.18f;
            tier.skid.fullSlip      = 0.75f;
        }

        // ── Helpers ──

        private static AudioClip Clip(string path)
        {
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void SetLoop(NFSU2CarAudioBank.AudioLoopLayer layer, AudioClip ext, AudioClip interior,
            float extVol, float intVol, Vector2 pitchRange)
        {
            if (layer == null) return;
            layer.exteriorClip   = ext;
            layer.interiorClip   = interior != null ? interior : ext;
            layer.exteriorVolume = extVol;
            layer.interiorVolume = intVol;
            layer.pitchRange     = pitchRange;
        }

        private static void SetSweep(NFSU2CarAudioBank.AudioSweepLayer layer, AudioClip clip,
            float extVol, float intVol, Vector2 playbackWindow)
        {
            if (layer == null) return;
            layer.exteriorClip    = clip;
            layer.interiorClip    = clip;
            layer.exteriorVolume  = extVol;
            layer.interiorVolume  = intVol;
            layer.playbackWindow01 = playbackWindow;
            layer.pitchRange      = new Vector2(0.98f, 1.02f);
        }

        private static void ClearLoop(NFSU2CarAudioBank.AudioLoopLayer layer)
        {
            if (layer == null) return;
            layer.exteriorClip   = null;
            layer.interiorClip   = null;
            layer.exteriorVolume = 1f;
            layer.interiorVolume = 0.75f;
            layer.pitchRange     = new Vector2(0.98f, 1.02f);
        }

        private static void SetOneShot(NFSU2CarAudioBank.AudioOneShotLayer layer, AudioClip clip, float vol)
        {
            if (layer == null) return;
            layer.clip       = clip;
            layer.volume     = vol;
            layer.pitchRange = new Vector2(0.96f, 1.04f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int lastSlash = path.LastIndexOf('/');
            if (lastSlash <= 0) return;
            string parent = path.Substring(0, lastSlash);
            string folder = path.Substring(lastSlash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
