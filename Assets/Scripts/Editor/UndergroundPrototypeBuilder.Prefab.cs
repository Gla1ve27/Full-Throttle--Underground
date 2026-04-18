using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Underground.Audio;
using Underground.Vehicle;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private const string CustomEngineBasePath = "Assets/Audio/Car Sounds and stuffs/CAR_00_ENG_MB_EE";
        private const string CustomEngineInteriorBasePath = "Assets/Audio/Car Sounds and stuffs/CAR_00_ENG_MB_SPU";
        private const string CustomEngineBankName = "CAR_00_ENG_MB_EE";
        private const string CustomEngineInteriorBankName = "CAR_00_ENG_MB_SPU";
        private const string CustomGearBasePath = "Assets/Audio/Car Sounds and stuffs/GEAR_LRG_Base";
        private const string CustomInductionBasePath = "Assets/Audio/Car Sounds and stuffs/GIN_Acura_ITR";
        private const string CustomSweetenerBasePath = "Assets/Audio/Car Sounds and stuffs/SWTN_CAR_00_MB";
        private const string CustomTurboBasePath = "Assets/Audio/Car Sounds and stuffs/TURBO_SML1_0_MB";
        private const string CustomSkidBasePath = "Assets/Audio/Car Sounds and stuffs/SKID_PAV_MB";
        private const string StarterCarAudioBankPath = "Assets/ScriptableObjects/Vehicles/StarterCarAudioBank.asset";

        private static GameObject CreateOrUpdatePlayerCarPrefab(VehicleStatsData starterStats, bool preserveExistingAsset = false)
        {
            if (preserveExistingAsset && AssetExistsAtPath<GameObject>(PlayerCarPrefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCarPrefabPath);
            }

            const float wheelRadius = 0.34f;
            Vector3 frontLeftWheelPosition = new Vector3(-0.85f, 0.2f, 1.38f);
            Vector3 frontRightWheelPosition = new Vector3(0.85f, 0.2f, 1.38f);
            Vector3 rearLeftWheelPosition = new Vector3(-0.85f, 0.2f, -1.35f);
            Vector3 rearRightWheelPosition = new Vector3(0.85f, 0.2f, -1.35f);

            GameObject carRoot = new GameObject("PlayerCar");
            carRoot.tag = "Player";
            SetLayerRecursively(carRoot, LayerMask.NameToLayer("PlayerVehicle"));
            carRoot.transform.position = new Vector3(0f, 0.65f, 0f);

            BoxCollider collider = carRoot.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(1.9f, 0.9f, 4.2f);
            carRoot.AddComponent<Rigidbody>();

            InputReader input = carRoot.AddComponent<VehicleInput>();
            EngineModel engine = carRoot.AddComponent<EngineModel>();
            GearboxSystem gearbox = carRoot.AddComponent<GearboxSystem>();
            VehicleDynamicsController controller = carRoot.AddComponent<VehicleDynamicsController>();
            carRoot.AddComponent<VehicleNightLightingController>();
            CarRespawn respawn = carRoot.AddComponent<CarRespawn>();

            Transform modelRoot = CreateEmptyChild(carRoot.transform, "ModelRoot", Vector3.zero);
            Transform wheelColliderRoot = CreateEmptyChild(carRoot.transform, "WheelColliders", Vector3.zero);
            Transform centerOfMass = CreateEmptyChild(carRoot.transform, "CenterOfMass", new Vector3(0f, -0.3f, 0.1f));
            CreateEmptyChild(carRoot.transform, "CameraTarget", new Vector3(0f, 1.08f, -0.02f));
            Transform spawnPoint = CreateEmptyChild(carRoot.transform, "SpawnPoint", Vector3.zero);

            ImportedVehicleVisual importedVisual = AttachImportedPlayerVisual(
                modelRoot,
                frontLeftWheelPosition,
                frontRightWheelPosition,
                rearLeftWheelPosition,
                rearRightWheelPosition,
                wheelRadius);

            WheelBuild fl = CreateWheel(modelRoot, wheelColliderRoot, "FL", "Front", true, true, false, false, frontLeftWheelPosition, wheelRadius, importedVisual.frontLeftWheel);
            WheelBuild fr = CreateWheel(modelRoot, wheelColliderRoot, "FR", "Front", false, true, false, false, frontRightWheelPosition, wheelRadius, importedVisual.frontRightWheel);
            WheelBuild rl = CreateWheel(modelRoot, wheelColliderRoot, "RL", "Rear", true, false, true, true, rearLeftWheelPosition, wheelRadius, importedVisual.rearLeftWheel);
            WheelBuild rr = CreateWheel(modelRoot, wheelColliderRoot, "RR", "Rear", false, false, true, true, rearRightWheelPosition, wheelRadius, importedVisual.rearRightWheel);

            GameObject audioRoot = new GameObject("AudioRoot");
            audioRoot.transform.SetParent(carRoot.transform, false);
            VehicleAudioController audioController = audioRoot.AddComponent<VehicleAudioController>();

            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("baseStats").objectReferenceValue = starterStats;
            controllerSo.FindProperty("input").objectReferenceValue = input;
            controllerSo.FindProperty("engineModel").objectReferenceValue = engine;
            controllerSo.FindProperty("gearbox").objectReferenceValue = gearbox;
            controllerSo.FindProperty("centerOfMassReference").objectReferenceValue = centerOfMass;
            controllerSo.FindProperty("lowSpeedAssistEntryKph").floatValue = 12f;
            controllerSo.FindProperty("lowSpeedStopSnapKph").floatValue = 0.85f;
            controllerSo.FindProperty("lowSpeedCoastDeceleration").floatValue = 4.5f;
            controllerSo.FindProperty("lowSpeedLateralDamping").floatValue = 3.5f;
            controllerSo.FindProperty("lowSpeedAngularDamping").floatValue = 2.4f;
            SerializedProperty wheelsProperty = controllerSo.FindProperty("wheels");
            wheelsProperty.arraySize = 4;
            ApplyWheel(wheelsProperty.GetArrayElementAtIndex(0), fl);
            ApplyWheel(wheelsProperty.GetArrayElementAtIndex(1), fr);
            ApplyWheel(wheelsProperty.GetArrayElementAtIndex(2), rl);
            ApplyWheel(wheelsProperty.GetArrayElementAtIndex(3), rr);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject respawnSo = new SerializedObject(respawn);
            respawnSo.FindProperty("vehicle").objectReferenceValue = controller;
            respawnSo.FindProperty("input").objectReferenceValue = input;
            respawnSo.FindProperty("defaultRespawnPoint").objectReferenceValue = spawnPoint;
            respawnSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject audioSo = new SerializedObject(audioController);
            audioSo.FindProperty("gearbox").objectReferenceValue = gearbox;
            audioSo.FindProperty("vehicle").objectReferenceValue = controller;
            audioSo.FindProperty("input").objectReferenceValue = input;
            audioSo.FindProperty("audioBank").objectReferenceValue = CreateOrUpdateStarterAudioBank();
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(carRoot, PlayerCarPrefabPath);
            Object.DestroyImmediate(carRoot);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCarPrefabPath);
        }

        [MenuItem("Full Throttle/Audio/Create Starter Car Audio Bank", priority = 70)]
        public static void CreateStarterCarAudioBankFromTopMenu()
        {
            EnsureProjectFolders();
            NFSU2CarAudioBank bank = CreateOrUpdateStarterAudioBank();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = bank;
            EditorGUIUtility.PingObject(bank);
            EditorUtility.DisplayDialog(
                "Starter Audio Bank Ready",
                $"Created or updated {StarterCarAudioBankPath}. Assign it to VehicleAudioController.Audio Bank on your car.",
                "OK");
        }

        private static AudioClip LoadAudioClipAtPath(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        private static void ConfigureEngineClipImportSettings(params string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return;
            }

            for (int i = 0; i < assetPaths.Length; i++)
            {
                AudioImporter importer = AssetImporter.GetAtPath(assetPaths[i]) as AudioImporter;
                if (importer == null)
                {
                    continue;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool changed = false;

                if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    changed = true;
                }

                if (settings.compressionFormat != AudioCompressionFormat.PCM)
                {
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    changed = true;
                }

                if (!settings.preloadAudioData)
                {
                    settings.preloadAudioData = true;
                    changed = true;
                }

                if (changed)
                {
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
            }
        }

        private static NFSU2CarAudioBank CreateOrUpdateStarterAudioBank()
        {
            ConfigureEngineClipImportSettings(
                $"{CustomEngineBasePath}/{CustomEngineBankName}_01.wav",
                $"{CustomEngineBasePath}/{CustomEngineBankName}_02.wav",
                $"{CustomEngineBasePath}/{CustomEngineBankName}_03.wav",
                $"{CustomEngineBasePath}/{CustomEngineBankName}_04.wav",
                $"{CustomEngineBasePath}/{CustomEngineBankName}_05.wav",
                $"{CustomEngineBasePath}/{CustomEngineBankName}_06.wav",
                $"{CustomEngineBasePath}/{CustomEngineBankName}_07.wav",
                $"{CustomEngineBasePath}/{CustomEngineBankName}_08.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_01.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_02.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_03.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_04.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_05.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_06.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_07.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_08.wav",
                $"{CustomInductionBasePath}/GIN_Acura_ITR.wav",
                $"{CustomInductionBasePath}/GIN_Acura_ITR_DCL.wav",
                $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_01.wav",
                $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_02.wav",
                $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_04.wav",
                $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_06.wav",
                $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_07.wav",
                $"{CustomSkidBasePath}/SKID_PAV_MB_01.wav");

            NFSU2CarAudioBank bank = AssetDatabase.LoadAssetAtPath<NFSU2CarAudioBank>(StarterCarAudioBankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<NFSU2CarAudioBank>();
                AssetDatabase.CreateAsset(bank, StarterCarAudioBankPath);
            }

            bank.defaultTier = VehicleAudioTier.Stock;
            ConfigureStarterTuning(bank.tuning);
            ConfigureStarterTier(bank.GetTier(VehicleAudioTier.Stock), 1f);
            ConfigureStarterTier(bank.GetTier(VehicleAudioTier.Street), 1.05f);
            ConfigureStarterTier(bank.GetTier(VehicleAudioTier.Pro), 1.1f);
            ConfigureStarterTier(bank.GetTier(VehicleAudioTier.Extreme), 1.15f);
            EditorUtility.SetDirty(bank);
            return bank;
        }

        private static void ConfigureStarterTuning(NFSU2CarAudioBank.RuntimeTuning tuning)
        {
            if (tuning == null)
            {
                return;
            }

            tuning.layerFadeSpeed = 6f;
            tuning.rpmRiseResponse = 14f;
            tuning.rpmFallResponse = 5f;
            tuning.decelSweepLowRpmCutoff01 = 0.18f;
            tuning.sweepSeekResponse = 6f;
            tuning.sweepDominantVolume = 0.35f;
            tuning.sweepResyncThresholdSeconds = 0.75f;
            tuning.sweepPitchCorrectionLimit = 0.025f;
            tuning.loopBankDominantVolume = 0.82f;
            tuning.accelCharacterVolume = 0.15f;
            tuning.decelCharacterVolume = 0.10f;
            tuning.engineBandWidth = 0.16f;
            tuning.band01Center = 0.08f;
            tuning.band02Center = 0.16f;
            tuning.band03Center = 0.28f;
            tuning.band04Center = 0.40f;
            tuning.band05Center = 0.54f;
            tuning.band06Center = 0.68f;
            tuning.band07Center = 0.82f;
            tuning.band08Center = 0.94f;
        }

        private static void ConfigureStarterTier(NFSU2CarAudioBank.TierAudioPackage tier, float tierMasterVolume)
        {
            if (tier == null)
            {
                return;
            }

            tier.tierMasterVolume = tierMasterVolume;
            AssignEngineBands(tier);
            AssignLoop(
                tier.idle,
                $"{CustomEngineBasePath}/{CustomEngineBankName}_01.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_01.wav",
                0.95f,
                0.75f,
                new Vector2(0.92f, 1.08f));
            AssignSweep(tier.accelSweep, $"{CustomInductionBasePath}/GIN_Acura_ITR.wav", 0.30f, 0.18f, new Vector2(0.08f, 0.92f));
            AssignSweep(tier.decelSweep, $"{CustomInductionBasePath}/GIN_Acura_ITR_DCL.wav", 0.22f, 0.14f, new Vector2(0.08f, 0.92f));
            ClearLoop(tier.accelLow);
            ClearLoop(tier.accelMid);
            ClearLoop(tier.accelHigh);
            ClearLoop(tier.decelLow);
            ClearLoop(tier.decelMid);
            ClearLoop(tier.decelHigh);
            AssignLoop(
                tier.limiter,
                $"{CustomEngineBasePath}/{CustomEngineBankName}_08.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_08.wav",
                0.72f,
                0.55f,
                new Vector2(0.96f, 1.04f));
            AssignLoop(
                tier.reverse,
                $"{CustomEngineBasePath}/{CustomEngineBankName}_01.wav",
                $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_01.wav",
                0.65f,
                0.5f,
                new Vector2(0.65f, 1f));

            AssignOneShot(tier.accelFromIdle, null, 0.75f);
            AssignOneShot(tier.shift.shiftUp, $"{CustomGearBasePath}/GEAR_LRG_Base_01.wav", 0.9f);
            AssignOneShot(tier.shift.shiftDown, $"{CustomGearBasePath}/GEAR_LRG_Base_02.wav", 0.8f);
            AssignLoop(tier.turbo.spool, $"{CustomTurboBasePath}/TURBO_SML1_0_MB_01.wav", 0.22f, 0.12f, new Vector2(0.9f, 1.12f));
            AssignLoop(tier.turbo.whistle, $"{CustomTurboBasePath}/TURBO_SML1_0_MB_02.wav", 0.14f, 0.07f, new Vector2(0.95f, 1.12f));
            AssignOneShot(tier.turbo.blowOff, $"{CustomTurboBasePath}/TURBO_SML1_0_MB_03.wav", 0.40f);
            tier.turbo.strength = 0.55f;
            AssignLoop(tier.sweetener.intake, $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_01.wav", 0.10f, 0.08f, new Vector2(0.98f, 1.02f));
            AssignLoop(tier.sweetener.drivetrain, $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_04.wav", 0.08f, 0.06f, new Vector2(0.98f, 1.02f));
            AssignLoop(tier.sweetener.sputter, $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_06.wav", 0.14f, 0.10f, new Vector2(0.99f, 1.01f));
            AssignOneShot(tier.sweetener.crackle, $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_02.wav", 0.22f);
            AssignOneShot(tier.sweetener.sparkChatter, $"{CustomSweetenerBasePath}/SWTN_CAR_00_MB_07.wav", 0.16f);
            tier.sweetener.enableLiftOffCrackles = true;
            tier.sweetener.crackleMinRpm01 = 0.40f;
            tier.sweetener.crackleChancePerSecond = 1.8f;
            tier.sweetener.sputterMinRpm01 = 0.30f;
            tier.sweetener.sputterThrottleUpper = 0.14f;
            tier.sweetener.sputterVolume = 0.10f;
            tier.sweetener.sparkChatterChancePerSecond = 0.6f;
            AssignLoop(tier.skid.skid, $"{CustomSkidBasePath}/SKID_PAV_MB_01.wav", 0.45f, 0.16f, new Vector2(0.9f, 1.08f));
        }

        private static void AssignEngineBands(NFSU2CarAudioBank.TierAudioPackage tier)
        {
            if (tier == null)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                float normalizedIndex = i / 7f;
                float lowPitch = Mathf.Lerp(0.98f, 0.94f, normalizedIndex);
                float highPitch = Mathf.Lerp(1.05f, 1.02f, normalizedIndex);
                AssignLoop(
                    tier.GetEngineBand(i),
                    $"{CustomEngineBasePath}/{CustomEngineBankName}_{i + 1:00}.wav",
                    $"{CustomEngineInteriorBasePath}/{CustomEngineInteriorBankName}_{i + 1:00}.wav",
                    1f,
                    0.82f,
                    new Vector2(lowPitch, highPitch));
            }
        }

        private static void AssignLoop(NFSU2CarAudioBank.AudioLoopLayer layer, string clipPath, float exteriorVolume, float interiorVolume, Vector2 pitchRange)
        {
            if (layer == null)
            {
                return;
            }

            AudioClip clip = LoadAudioClipAtPath(clipPath);
            layer.exteriorClip = clip;
            layer.interiorClip = clip;
            layer.exteriorVolume = exteriorVolume;
            layer.interiorVolume = interiorVolume;
            layer.pitchRange = pitchRange;
        }

        private static void AssignLoop(NFSU2CarAudioBank.AudioLoopLayer layer, string exteriorClipPath, string interiorClipPath, float exteriorVolume, float interiorVolume, Vector2 pitchRange)
        {
            if (layer == null)
            {
                return;
            }

            AudioClip exteriorClip = LoadAudioClipAtPath(exteriorClipPath);
            AudioClip interiorClip = LoadAudioClipAtPath(interiorClipPath);
            layer.exteriorClip = exteriorClip;
            layer.interiorClip = interiorClip != null ? interiorClip : exteriorClip;
            layer.exteriorVolume = exteriorVolume;
            layer.interiorVolume = interiorVolume;
            layer.pitchRange = pitchRange;
        }

        private static void AssignSweep(NFSU2CarAudioBank.AudioSweepLayer layer, string clipPath, float exteriorVolume, float interiorVolume, Vector2 playbackWindow01)
        {
            if (layer == null)
            {
                return;
            }

            AudioClip clip = LoadAudioClipAtPath(clipPath);
            layer.exteriorClip = clip;
            layer.interiorClip = clip;
            layer.exteriorVolume = exteriorVolume;
            layer.interiorVolume = interiorVolume;
            layer.playbackWindow01 = playbackWindow01;
            layer.pitchRange = new Vector2(0.98f, 1.02f);
        }

        private static void AssignOneShot(NFSU2CarAudioBank.AudioOneShotLayer layer, string clipPath, float volume)
        {
            if (layer == null)
            {
                return;
            }

            layer.clip = LoadAudioClipAtPath(clipPath);
            layer.volume = volume;
            layer.pitchRange = new Vector2(0.96f, 1.04f);
        }

        private static void ClearLoop(NFSU2CarAudioBank.AudioLoopLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            layer.exteriorClip = null;
            layer.interiorClip = null;
            layer.exteriorVolume = 1f;
            layer.interiorVolume = 0.75f;
            layer.pitchRange = new Vector2(0.98f, 1.02f);
        }

        private static void ClearOneShot(NFSU2CarAudioBank.AudioOneShotLayer layer)
        {
            if (layer == null)
            {
                return;
            }

            layer.clip = null;
            layer.volume = 1f;
            layer.pitchRange = new Vector2(0.96f, 1.04f);
        }

        private static ImportedVehicleVisual AttachImportedPlayerVisual(
            Transform modelRoot,
            Vector3 frontLeftWheelPosition,
            Vector3 frontRightWheelPosition,
            Vector3 rearLeftWheelPosition,
            Vector3 rearRightWheelPosition,
            float targetWheelRadius)
        {
            GameObject visualPrefab = LoadPreferredPlayerVisualPrefab();
            if (visualPrefab == null)
            {
                CreateBodyVisuals(modelRoot);
                return default;
            }

            GameObject visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
            visualInstance.name = "ImportedVisual";
            visualInstance.transform.SetParent(modelRoot, false);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;
            StripRuntimeComponents(visualInstance);
            ConvertRendererMaterialsToUrp(visualInstance);

            Transform sourceFrontLeft = FindDeepChild(visualInstance.transform, "FL");
            Transform sourceFrontRight = FindDeepChild(visualInstance.transform, "FR");
            Transform sourceRearLeft = FindDeepChild(visualInstance.transform, "RL");
            Transform sourceRearRight = FindDeepChild(visualInstance.transform, "RR");

            AlignImportedVehicleBody(
                visualInstance.transform,
                sourceFrontLeft,
                sourceFrontRight,
                sourceRearLeft,
                sourceRearRight,
                frontLeftWheelPosition,
                frontRightWheelPosition,
                rearLeftWheelPosition,
                rearRightWheelPosition,
                targetWheelRadius);

            ImportedVehicleVisual build = new ImportedVehicleVisual
            {
                root = visualInstance,
                frontLeftWheel = CreateDetachedWheelVisual(modelRoot, "FL", frontLeftWheelPosition, targetWheelRadius, sourceFrontLeft),
                frontRightWheel = CreateDetachedWheelVisual(modelRoot, "FR", frontRightWheelPosition, targetWheelRadius, sourceFrontRight),
                rearLeftWheel = CreateDetachedWheelVisual(modelRoot, "RL", rearLeftWheelPosition, targetWheelRadius, sourceRearLeft),
                rearRightWheel = CreateDetachedWheelVisual(modelRoot, "RR", rearRightWheelPosition, targetWheelRadius, sourceRearRight)
            };

            DisableWheelRenderers(sourceFrontLeft);
            DisableWheelRenderers(sourceFrontRight);
            DisableWheelRenderers(sourceRearLeft);
            DisableWheelRenderers(sourceRearRight);

            return build;
        }

        private static void CreateBodyVisuals(Transform parent)
        {
            CreateVisual(parent, "Body", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.55f, 4f), Quaternion.identity);
            CreateVisual(parent, "Cabin", PrimitiveType.Cube, new Vector3(0f, 0.85f, -0.15f), new Vector3(1.35f, 0.5f, 1.8f), Quaternion.identity);
        }

        private static WheelBuild CreateWheel(Transform modelRoot, Transform colliderRoot, string suffix, string axleId, bool leftSide, bool steer, bool drive, bool handbrake, Vector3 localPosition, float wheelRadius, Transform importedWheelVisual)
        {
            GameObject colliderObject = new GameObject($"{suffix}_Collider");
            colliderObject.transform.SetParent(colliderRoot, false);
            colliderObject.transform.localPosition = localPosition;
            WheelCollider wheelCollider = colliderObject.AddComponent<WheelCollider>();
            wheelCollider.radius = wheelRadius;
            wheelCollider.mass = 25f;
            wheelCollider.suspensionDistance = 0.2f;

            Transform visualTransform = importedWheelVisual;
            if (visualTransform == null)
            {
                GameObject wheelVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelVisual.name = $"{suffix}_Mesh";
                wheelVisual.transform.SetParent(modelRoot, false);
                wheelVisual.transform.localPosition = localPosition;
                wheelVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelVisual.transform.localScale = new Vector3(0.68f, 0.12f, 0.68f);
                Object.DestroyImmediate(wheelVisual.GetComponent<Collider>());
                visualTransform = wheelVisual.transform;
            }

            return new WheelBuild
            {
                axleId = axleId,
                leftSide = leftSide,
                collider = wheelCollider,
                mesh = visualTransform,
                steer = steer,
                drive = drive,
                handbrake = handbrake
            };
        }

        private static void AlignImportedVehicleBody(
            Transform visualRoot,
            Transform frontLeftWheel,
            Transform frontRightWheel,
            Transform rearLeftWheel,
            Transform rearRightWheel,
            Vector3 frontLeftTarget,
            Vector3 frontRightTarget,
            Vector3 rearLeftTarget,
            Vector3 rearRightTarget,
            float targetWheelRadius)
        {
            FitImportedVehicleScale(
                visualRoot,
                frontLeftWheel,
                frontRightWheel,
                rearLeftWheel,
                rearRightWheel,
                frontLeftTarget,
                frontRightTarget,
                rearLeftTarget,
                rearRightTarget,
                targetWheelRadius);

            Transform[] wheelAnchors = { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
            Vector3[] targetPositions = { frontLeftTarget, frontRightTarget, rearLeftTarget, rearRightTarget };

            Vector3 importedCenter = Vector3.zero;
            Vector3 targetCenter = Vector3.zero;
            int count = 0;

            for (int i = 0; i < wheelAnchors.Length; i++)
            {
                if (wheelAnchors[i] == null)
                {
                    continue;
                }

                importedCenter += visualRoot.InverseTransformPoint(wheelAnchors[i].position);
                targetCenter += targetPositions[i];
                count++;
            }

            if (count == 0)
            {
                visualRoot.localPosition = new Vector3(0f, 0.08f, 0f);
                return;
            }

            visualRoot.localPosition = (targetCenter / count) - (importedCenter / count);
        }

        private static void FitImportedVehicleScale(
            Transform visualRoot,
            Transform frontLeftWheel,
            Transform frontRightWheel,
            Transform rearLeftWheel,
            Transform rearRightWheel,
            Vector3 frontLeftTarget,
            Vector3 frontRightTarget,
            Vector3 rearLeftTarget,
            Vector3 rearRightTarget,
            float targetWheelRadius)
        {
            if (visualRoot == null)
            {
                return;
            }

            bool hasFrontAxle = TryGetAxleCenterAndTrack(visualRoot, frontLeftWheel, frontRightWheel, out Vector3 importedFrontCenter, out float importedFrontTrack);
            bool hasRearAxle = TryGetAxleCenterAndTrack(visualRoot, rearLeftWheel, rearRightWheel, out Vector3 importedRearCenter, out float importedRearTrack);

            Vector3 targetFrontCenter = (frontLeftTarget + frontRightTarget) * 0.5f;
            Vector3 targetRearCenter = (rearLeftTarget + rearRightTarget) * 0.5f;
            float targetFrontTrack = Mathf.Abs(frontRightTarget.x - frontLeftTarget.x);
            float targetRearTrack = Mathf.Abs(rearRightTarget.x - rearLeftTarget.x);

            float trackScale = 1f;
            float wheelbaseScale = 1f;

            if (hasFrontAxle || hasRearAxle)
            {
                float importedTrack = 0f;
                float targetTrack = 0f;
                int trackCount = 0;

                if (hasFrontAxle && importedFrontTrack > 0.001f)
                {
                    importedTrack += importedFrontTrack;
                    targetTrack += targetFrontTrack;
                    trackCount++;
                }

                if (hasRearAxle && importedRearTrack > 0.001f)
                {
                    importedTrack += importedRearTrack;
                    targetTrack += targetRearTrack;
                    trackCount++;
                }

                if (trackCount > 0)
                {
                    trackScale = targetTrack / Mathf.Max(0.001f, importedTrack);
                }
            }

            if (hasFrontAxle && hasRearAxle)
            {
                float importedWheelbase = Mathf.Abs(importedFrontCenter.z - importedRearCenter.z);
                float targetWheelbase = Mathf.Abs(targetFrontCenter.z - targetRearCenter.z);
                if (importedWheelbase > 0.001f)
                {
                    wheelbaseScale = targetWheelbase / importedWheelbase;
                }
            }

            float wheelRadiusScale = 1f;
            if (TryGetAverageWheelRadius(frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel, out float importedWheelRadius) && importedWheelRadius > 0.001f)
            {
                wheelRadiusScale = Mathf.Clamp(targetWheelRadius / importedWheelRadius, 0.6f, 1.6f);
            }

            float verticalScale = Mathf.Clamp((trackScale + wheelbaseScale + wheelRadiusScale) / 3f, 0.7f, 1.4f);
            visualRoot.localScale = new Vector3(
                Mathf.Clamp(trackScale, 0.75f, 1.35f),
                verticalScale,
                Mathf.Clamp(wheelbaseScale, 0.75f, 1.35f));
        }

        private static bool TryGetAxleCenterAndTrack(Transform root, Transform leftWheel, Transform rightWheel, out Vector3 axleCenter, out float trackWidth)
        {
            axleCenter = Vector3.zero;
            trackWidth = 0f;

            if (root == null || leftWheel == null || rightWheel == null)
            {
                return false;
            }

            Vector3 leftLocal = root.InverseTransformPoint(leftWheel.position);
            Vector3 rightLocal = root.InverseTransformPoint(rightWheel.position);
            axleCenter = (leftLocal + rightLocal) * 0.5f;
            trackWidth = Mathf.Abs(rightLocal.x - leftLocal.x);
            return true;
        }

        private static bool TryGetAverageWheelRadius(Transform frontLeftWheel, Transform frontRightWheel, Transform rearLeftWheel, Transform rearRightWheel, out float averageRadius)
        {
            Transform[] wheels = { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
            float radiusSum = 0f;
            int count = 0;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || !TryGetCombinedRendererBounds(wheels[i], out Bounds bounds))
                {
                    continue;
                }

                radiusSum += Mathf.Max(bounds.extents.y, bounds.extents.z);
                count++;
            }

            averageRadius = count > 0 ? radiusSum / count : 0f;
            return count > 0;
        }

        private static Transform CreateDetachedWheelVisual(Transform modelRoot, string suffix, Vector3 localPosition, float targetWheelRadius, Transform sourceWheel)
        {
            GameObject wheelVisualRoot = new GameObject($"{suffix}_Visual");
            wheelVisualRoot.transform.SetParent(modelRoot, false);
            wheelVisualRoot.transform.localPosition = localPosition;
            wheelVisualRoot.transform.localRotation = Quaternion.identity;

            if (sourceWheel == null)
            {
                GameObject wheelVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelVisual.name = "Mesh";
                wheelVisual.transform.SetParent(wheelVisualRoot.transform, false);
                wheelVisual.transform.localPosition = Vector3.zero;
                wheelVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelVisual.transform.localScale = new Vector3(0.68f, 0.12f, 0.68f);
                Object.DestroyImmediate(wheelVisual.GetComponent<Collider>());
                return wheelVisualRoot.transform;
            }

            GameObject wheelVisualClone = Object.Instantiate(sourceWheel.gameObject, wheelVisualRoot.transform, false);
            wheelVisualClone.name = "Mesh";
            wheelVisualClone.transform.localPosition = Vector3.zero;
            StripRuntimeComponents(wheelVisualClone);
            NormalizeDetachedWheelVisual(wheelVisualRoot.transform, wheelVisualClone.transform, targetWheelRadius);
            return wheelVisualRoot.transform;
        }

        private static void NormalizeDetachedWheelVisual(Transform wheelRoot, Transform wheelClone, float targetWheelRadius)
        {
            if (wheelRoot == null || wheelClone == null)
            {
                return;
            }

            if (!TryGetCombinedRendererBounds(wheelClone, out Bounds bounds))
            {
                return;
            }

            Vector3 localCenter = wheelRoot.InverseTransformPoint(bounds.center);
            wheelClone.localPosition -= localCenter;

            float measuredRadius = Mathf.Max(bounds.extents.y, bounds.extents.z);
            if (measuredRadius > 0.001f)
            {
                float uniformScale = targetWheelRadius / measuredRadius;
                wheelClone.localScale *= uniformScale;
            }

            if (TryGetCombinedRendererBounds(wheelClone, out Bounds adjustedBounds))
            {
                Vector3 adjustedCenter = wheelRoot.InverseTransformPoint(adjustedBounds.center);
                wheelClone.localPosition -= adjustedCenter;
            }
        }

        private static bool TryGetCombinedRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return hasBounds;
        }

        private static void DisableWheelRenderers(Transform sourceWheel)
        {
            if (sourceWheel == null)
            {
                return;
            }

            Renderer[] renderers = sourceWheel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        private static void ConvertRendererMaterialsToUrp(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] sharedMaterials = renderers[i].sharedMaterials;
                bool changed = false;

                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material sourceMaterial = sharedMaterials[materialIndex];
                    Material convertedMaterial = GetOrCreateUrpMaterial(sourceMaterial);
                    if (convertedMaterial != null && convertedMaterial != sourceMaterial)
                    {
                        sharedMaterials[materialIndex] = convertedMaterial;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderers[i].sharedMaterials = sharedMaterials;
                }
            }
        }

        private static Material GetOrCreateUrpMaterial(Material sourceMaterial)
        {
            if (sourceMaterial == null)
            {
                return null;
            }

            Shader preferredLitShader = GetPreferredLitShader();
            if (preferredLitShader == null)
            {
                return sourceMaterial;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            if (string.IsNullOrEmpty(sourcePath))
            {
                return CreateTransientUrpMaterial(sourceMaterial, preferredLitShader);
            }

            string fileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string suffix = preferredLitShader.name.Contains("HDRP") ? "HDRP" : preferredLitShader.name.Contains("Universal") ? "URP" : "SRP";
            string targetPath = $"Assets/Materials/Generated/{fileName}_{suffix}.mat";
            Material convertedMaterial = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (convertedMaterial == null)
            {
                convertedMaterial = new Material(preferredLitShader);
                AssetDatabase.CreateAsset(convertedMaterial, targetPath);
            }

            CopyMaterialProperties(sourceMaterial, convertedMaterial);
            EditorUtility.SetDirty(convertedMaterial);
            return convertedMaterial;
        }

        private static Material CreateTransientUrpMaterial(Material sourceMaterial, Shader urpLit)
        {
            Material convertedMaterial = new Material(urpLit);
            CopyMaterialProperties(sourceMaterial, convertedMaterial);
            return convertedMaterial;
        }

        private static void CopyMaterialProperties(Material sourceMaterial, Material targetMaterial)
        {
            Shader preferredLitShader = GetPreferredLitShader();
            if (preferredLitShader != null)
            {
                targetMaterial.shader = preferredLitShader;
            }

            if (TryGetTexture(sourceMaterial, out Texture baseTexture, "_BaseMap", "_MainTex"))
            {
                targetMaterial.SetTexture("_BaseMap", baseTexture);
            }

            if (TryGetTexture(sourceMaterial, out Texture normalMap, "_BumpMap"))
            {
                targetMaterial.SetTexture("_BumpMap", normalMap);
                targetMaterial.EnableKeyword("_NORMALMAP");
            }

            if (TryGetTexture(sourceMaterial, out Texture emissionMap, "_EmissionMap"))
            {
                targetMaterial.SetTexture("_EmissionMap", emissionMap);
                targetMaterial.EnableKeyword("_EMISSION");
            }

            if (TryGetColor(sourceMaterial, out Color baseColor, "_BaseColor", "_Color"))
            {
                targetMaterial.SetColor("_BaseColor", baseColor);
            }

            if (TryGetColor(sourceMaterial, out Color emissionColor, "_EmissionColor"))
            {
                targetMaterial.SetColor("_EmissionColor", emissionColor);
            }

            if (TryGetFloat(sourceMaterial, out float metallic, "_Metallic"))
            {
                targetMaterial.SetFloat("_Metallic", metallic);
            }

            if (TryGetFloat(sourceMaterial, out float smoothness, "_Smoothness", "_Glossiness"))
            {
                targetMaterial.SetFloat("_Smoothness", smoothness);
            }

            bool isTransparent = sourceMaterial.renderQueue >= (int)RenderQueue.Transparent;
            if (!isTransparent && TryGetFloat(sourceMaterial, out float mode, "_Mode"))
            {
                isTransparent = mode >= 2f;
            }

            if (isTransparent)
            {
                SetupTransparentUrpMaterial(targetMaterial);
            }
            else
            {
                SetupOpaqueUrpMaterial(targetMaterial);
            }
        }

        private static void SetupOpaqueUrpMaterial(Material material)
        {
            if (material.shader != null && material.shader.name == "HDRP/Lit")
            {
                SetFloatIfPresent(material, "_SurfaceType", 0f);
                SetFloatIfPresent(material, "_BlendMode", 0f);
                SetFloatIfPresent(material, "_AlphaCutoffEnable", 0f);
                SetFloatIfPresent(material, "_ZWrite", 1f);
            }

            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        private static void SetupTransparentUrpMaterial(Material material)
        {
            if (material.shader != null && material.shader.name == "HDRP/Lit")
            {
                SetFloatIfPresent(material, "_SurfaceType", 1f);
                SetFloatIfPresent(material, "_BlendMode", 0f);
                SetFloatIfPresent(material, "_AlphaCutoffEnable", 0f);
                SetFloatIfPresent(material, "_ZWrite", 0f);
            }

            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Shader GetPreferredLitShader()
        {
            return Shader.Find("HDRP/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static bool TryGetTexture(Material material, out Texture texture, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (material.HasProperty(propertyNames[i]))
                {
                    texture = material.GetTexture(propertyNames[i]);
                    if (texture != null)
                    {
                        return true;
                    }
                }
            }

            texture = null;
            return false;
        }

        private static bool TryGetColor(Material material, out Color color, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (material.HasProperty(propertyNames[i]))
                {
                    color = material.GetColor(propertyNames[i]);
                    return true;
                }
            }

            color = Color.white;
            return false;
        }

        private static bool TryGetFloat(Material material, out float value, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (material.HasProperty(propertyNames[i]))
                {
                    value = material.GetFloat(propertyNames[i]);
                    return true;
                }
            }

            value = 0f;
            return false;
        }

        private static void ApplyWheel(SerializedProperty property, WheelBuild wheel)
        {
            property.FindPropertyRelative("axleId").stringValue = wheel.axleId;
            property.FindPropertyRelative("leftSide").boolValue = wheel.leftSide;
            property.FindPropertyRelative("collider").objectReferenceValue = wheel.collider;
            property.FindPropertyRelative("mesh").objectReferenceValue = wheel.mesh;
            property.FindPropertyRelative("steer").boolValue = wheel.steer;
            property.FindPropertyRelative("drive").boolValue = wheel.drive;
            property.FindPropertyRelative("handbrake").boolValue = wheel.handbrake;
        }

        private static Transform CreateEmptyChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        private static void CreateVisual(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDeepChild(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void StripRuntimeComponents(GameObject root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform || component is MeshFilter || component is Renderer)
                {
                    continue;
                }

                if (component is Collider || component is Rigidbody || component is Joint || component is AudioSource || component is Camera || component is Light || component is Animator || component is MonoBehaviour)
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        private struct WheelBuild
        {
            public string axleId;
            public bool leftSide;
            public WheelCollider collider;
            public Transform mesh;
            public bool steer;
            public bool drive;
            public bool handbrake;
        }

        private struct ImportedVehicleVisual
        {
            public GameObject root;
            public Transform frontLeftWheel;
            public Transform frontRightWheel;
            public Transform rearLeftWheel;
            public Transform rearRightWheel;
        }
    }
}
