// ─────────────────────────────────────────────────────────────────────────────
//  Part 2 — Roster Asset Generator
//  Creates VehicleDefinition + VehicleStatsData ScriptableObject assets
//  for every car in VehicleRoster.CoreRoster.
//
//  Menu: Underground → Vehicles → Generate Roster Assets
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Underground.Vehicle;

namespace Underground.Editor
{
    public static class RosterAssetGenerator
    {
        private const string MenuPath = VehicleConstants.VehicleMenuRoot + "Generate Roster Assets";

        [MenuItem(MenuPath, priority = 100)]
        public static void GenerateAll()
        {
            int created = 0;
            int skipped = 0;

            foreach (VehicleRoster.RosterEntry entry in VehicleRoster.CoreRoster)
            {
                bool wasCreated = GenerateVehicleAssets(entry);
                if (wasCreated) created++;
                else skipped++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RosterAssetGenerator] Done. Created {created} vehicle(s), skipped {skipped} (already exist).");
            EditorUtility.DisplayDialog(
                "Roster Asset Generator",
                $"Created assets for {created} vehicle(s).\nSkipped {skipped} (already exist).",
                "OK");
        }

        /// <summary>
        /// Generates the folder structure, VehicleStatsData, and VehicleDefinition
        /// for a single roster entry. Returns true if assets were created.
        /// </summary>
        private static bool GenerateVehicleAssets(VehicleRoster.RosterEntry entry)
        {
            string vehicleFolder = VehicleConstants.VehicleFolder(entry.CanonicalId);
            string dataFolder = VehicleConstants.VehicleDataFolder(entry.CanonicalId);

            VehicleDefinition def = null;
            string defPath = $"{dataFolder}/{VehicleConstants.DefinitionAssetName(entry.CanonicalId)}.asset";
            if (File.Exists(defPath))
            {
                def = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(defPath);
            }

            // Create folders
            EnsureFolder(vehicleFolder);
            EnsureFolder(dataFolder);
            EnsureFolder($"{vehicleFolder}/{VehicleConstants.PrefabsFolder}");
            EnsureFolder($"{vehicleFolder}/{VehicleConstants.MaterialsFolder}");
            EnsureFolder($"{vehicleFolder}/{VehicleConstants.MeshesFolder}");
            EnsureFolder($"{vehicleFolder}/{VehicleConstants.TexturesFolder}");

            // ── Create VehicleStatsData ──
            string statsPath = $"{dataFolder}/{VehicleConstants.StatsAssetName(entry.CanonicalId)}.asset";
            VehicleStatsData stats = null;
            if (File.Exists(statsPath))
            {
                stats = AssetDatabase.LoadAssetAtPath<VehicleStatsData>(statsPath);
            }
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<VehicleStatsData>();
                ApplyArchetypeDefaults(stats, entry);
                AssetDatabase.CreateAsset(stats, statsPath);
            }
            stats.vehicleId = entry.CanonicalId;
            stats.displayName = entry.DisplayName;
            stats.archetype = entry.Archetype;
            stats.drivetrain = entry.Drivetrain;
            EditorUtility.SetDirty(stats);

            // ── Create VehicleDefinition ──
            bool defExisted = def != null;
            if (!defExisted)
            {
                def = ScriptableObject.CreateInstance<VehicleDefinition>();
            }

            def.vehicleId = entry.CanonicalId;
            def.displayName = entry.DisplayName;
            def.manufacturerLoreName = entry.ManufacturerLore;
            def.description = $"{entry.DisplayName} by {entry.ManufacturerLore}. {GetArchetypeFlavorText(entry.Archetype)}";
            def.archetype = entry.Archetype;
            def.drivetrain = entry.Drivetrain;
            def.stats = stats;
            def.statsAssetPath = statsPath;
            def.visualPrefabPath = ResolveVisualPrefabPath(entry.CanonicalId);
            def.price = GetArchetypeBasePrice(entry.Archetype);

            // Copy legacy wheel mapping configs to prevent sinking wheels
            ApplyLegacyWheelMapping(def, entry.CanonicalId);

            if (!defExisted)
            {
                AssetDatabase.CreateAsset(def, defPath);
                Debug.Log($"[RosterAssetGenerator] Created {entry.CanonicalId} → {defPath}");
            }
            else
            {
                EditorUtility.SetDirty(def);
                Debug.Log($"[RosterAssetGenerator] Updated {entry.CanonicalId} → {defPath}");
            }
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Archetype-based placeholder tuning
        //  Each archetype gets distinct baseline values so cars feel different
        //  from the start. These are starting points for manual refinement.
        // ─────────────────────────────────────────────────────────────────────
        private static void ApplyArchetypeDefaults(VehicleStatsData stats, VehicleRoster.RosterEntry entry)
        {
            switch (entry.Archetype)
            {
                case VehicleArchetype.StreetCompact:
                    ApplyStreetCompact(stats, entry.Drivetrain);
                    break;
                case VehicleArchetype.Muscle:
                    ApplyMuscle(stats, entry.Drivetrain);
                    break;
                case VehicleArchetype.Executive:
                    ApplyExecutive(stats, entry.Drivetrain);
                    break;
                case VehicleArchetype.Sports:
                    ApplySports(stats, entry.Drivetrain);
                    break;
                case VehicleArchetype.Supercar:
                    ApplySupercar(stats, entry.Drivetrain);
                    break;
                case VehicleArchetype.Offroad:
                    ApplyOffroad(stats, entry.Drivetrain);
                    break;
                case VehicleArchetype.Hero:
                    ApplyHero(stats, entry.Drivetrain);
                    break;
            }
        }

        // ── StreetCompact: Agile, low mass, lower speed ceiling ──
        private static void ApplyStreetCompact(VehicleStatsData s, DrivetrainType dt)
        {
            s.horsepower = 160f;
            s.maxMotorTorque = 320f;
            s.maxBrakeTorque = 3200f;
            s.maxSpeedKph = 185f;
            s.defaultMass = 1050f;
            s.weightDistributionFront = 0.60f;
            s.centerOfMassHeight = -0.38f;
            s.centerOfMassOffset = new Vector3(0f, -0.38f, 0.05f);

            s.spring = 22000f;
            s.damper = 2800f;
            s.suspensionDistance = 0.16f;
            s.antiRollForce = 2400f;

            s.maxSteerAngle = 28f;
            s.highSpeedSteerReduction = 0.22f;
            s.steeringResponse = 88f;

            s.forwardStiffness = 1.20f;
            s.sidewaysStiffness = 1.35f;
            s.frontGrip = 1.05f;
            s.rearGrip = 0.92f;
            s.traction = 0.95f;
            s.brakeGrip = 0.90f;
            s.slipAngle = 13f;
            s.recoveryRate = 4.2f;
            s.highSpeedStability = 0.80f;

            s.downforce = 18f;
            s.lateralGripAssist = 1.20f;
            s.handbrakeGripMultiplier = 0.45f;
            s.resetLift = 1.0f;

            s.driftAssist = 0.55f;
            s.counterSteerAssist = 0.50f;
            s.yawStability = 0.45f;
            s.nitrousGripAssist = 0.12f;

            s.idleRPM = 850f;
            s.maxRPM = 7200f;
            s.shiftUpRPM = 6800f;
            s.shiftDownRPM = 2200f;
            s.finalDriveRatio = 4.10f;
            s.gearRatios = new float[] { 0f, 3.20f, 2.10f, 1.55f, 1.18f, 0.95f };

            s.torqueCurve = CreateCurve(0.30f, 0.55f, 0.78f, 0.82f, 0.72f, 0.50f);

            s.baseValue = 12000;
            s.repairCostPerDamagePoint = 12;
        }

        // ── Muscle: Heavy torque, raw power, less graceful cornering ──
        private static void ApplyMuscle(VehicleStatsData s, DrivetrainType dt)
        {
            s.horsepower = 460f;
            s.maxMotorTorque = 720f;
            s.maxBrakeTorque = 5200f;
            s.maxSpeedKph = 245f;
            s.defaultMass = 1780f;
            s.weightDistributionFront = 0.54f;
            s.centerOfMassHeight = -0.42f;
            s.centerOfMassOffset = new Vector3(0f, -0.42f, -0.08f);

            s.spring = 32000f;
            s.damper = 4200f;
            s.suspensionDistance = 0.17f;
            s.antiRollForce = 3600f;

            s.maxSteerAngle = 22f;
            s.highSpeedSteerReduction = 0.15f;
            s.steeringResponse = 58f;

            s.forwardStiffness = 1.35f;
            s.sidewaysStiffness = 1.20f;
            s.frontGrip = 0.90f;
            s.rearGrip = 0.85f;
            s.traction = 0.80f;
            s.brakeGrip = 0.95f;
            s.slipAngle = 12f;
            s.recoveryRate = 2.8f;
            s.highSpeedStability = 0.88f;

            s.downforce = 28f;
            s.lateralGripAssist = 1.05f;
            s.handbrakeGripMultiplier = 0.55f;
            s.resetLift = 1.3f;

            s.driftAssist = 0.65f;
            s.counterSteerAssist = 0.55f;
            s.yawStability = 0.35f;
            s.nitrousGripAssist = 0.18f;

            s.idleRPM = 700f;
            s.maxRPM = 5400f;
            s.shiftUpRPM = 4800f;
            s.shiftDownRPM = 1600f;
            s.finalDriveRatio = 3.50f;
            s.gearRatios = new float[] { 0f, 2.66f, 1.78f, 1.30f, 1.00f, 0.80f, 0.62f };

            s.torqueCurve = CreateCurve(0.35f, 0.58f, 0.80f, 0.88f, 0.78f, 0.55f);

            s.baseValue = 28000;
            s.repairCostPerDamagePoint = 22;
        }

        // ── Executive: Stable, heavier, composed at speed ──
        private static void ApplyExecutive(VehicleStatsData s, DrivetrainType dt)
        {
            s.horsepower = 340f;
            s.maxMotorTorque = 580f;
            s.maxBrakeTorque = 5000f;
            s.maxSpeedKph = 230f;
            s.defaultMass = 1650f;
            s.weightDistributionFront = 0.52f;
            s.centerOfMassHeight = -0.44f;
            s.centerOfMassOffset = new Vector3(0f, -0.44f, 0.02f);

            s.spring = 28000f;
            s.damper = 3800f;
            s.suspensionDistance = 0.19f;
            s.antiRollForce = 3400f;

            s.maxSteerAngle = 24f;
            s.highSpeedSteerReduction = 0.18f;
            s.steeringResponse = 64f;

            s.forwardStiffness = 1.30f;
            s.sidewaysStiffness = 1.40f;
            s.frontGrip = 1.00f;
            s.rearGrip = 0.98f;
            s.traction = 1.00f;
            s.brakeGrip = 1.00f;
            s.slipAngle = 16f;
            s.recoveryRate = 3.8f;
            s.highSpeedStability = 1.10f;

            s.downforce = 38f;
            s.lateralGripAssist = 1.18f;
            s.handbrakeGripMultiplier = 0.48f;
            s.resetLift = 1.2f;

            s.driftAssist = 0.40f;
            s.counterSteerAssist = 0.65f;
            s.yawStability = 0.55f;
            s.nitrousGripAssist = 0.14f;

            s.idleRPM = 700f;
            s.maxRPM = 5600f;
            s.shiftUpRPM = 5000f;
            s.shiftDownRPM = 1800f;
            s.finalDriveRatio = 3.70f;
            s.gearRatios = new float[] { 0f, 2.40f, 1.72f, 1.28f, 1.00f, 0.82f, 0.68f };

            s.torqueCurve = CreateCurve(0.28f, 0.48f, 0.68f, 0.80f, 0.74f, 0.58f);

            s.baseValue = 35000;
            s.repairCostPerDamagePoint = 28;
        }

        // ── Sports: Balanced, expressive, all-rounder ──
        private static void ApplySports(VehicleStatsData s, DrivetrainType dt)
        {
            s.horsepower = 380f;
            s.maxMotorTorque = 600f;
            s.maxBrakeTorque = 5400f;
            s.maxSpeedKph = 260f;
            s.defaultMass = 1380f;
            s.weightDistributionFront = 0.50f;
            s.centerOfMassHeight = -0.46f;
            s.centerOfMassOffset = new Vector3(0f, -0.46f, 0.0f);

            s.spring = 34000f;
            s.damper = 4200f;
            s.suspensionDistance = 0.15f;
            s.antiRollForce = 3800f;

            s.maxSteerAngle = 24f;
            s.highSpeedSteerReduction = 0.16f;
            s.steeringResponse = 78f;

            s.forwardStiffness = 1.35f;
            s.sidewaysStiffness = 1.50f;
            s.frontGrip = 1.05f;
            s.rearGrip = 1.02f;
            s.traction = 1.05f;
            s.brakeGrip = 1.05f;
            s.slipAngle = 15f;
            s.recoveryRate = 3.6f;
            s.highSpeedStability = 1.05f;

            s.downforce = 42f;
            s.lateralGripAssist = 1.15f;
            s.handbrakeGripMultiplier = 0.50f;
            s.resetLift = 1.1f;

            s.driftAssist = 0.50f;
            s.counterSteerAssist = 0.60f;
            s.yawStability = 0.42f;
            s.nitrousGripAssist = 0.15f;

            s.idleRPM = 900f;
            s.maxRPM = 7800f;
            s.shiftUpRPM = 7200f;
            s.shiftDownRPM = 2800f;
            s.finalDriveRatio = 3.90f;
            s.gearRatios = new float[] { 0f, 3.10f, 2.05f, 1.50f, 1.15f, 0.90f, 0.72f };

            s.torqueCurve = CreateCurve(0.26f, 0.50f, 0.72f, 0.85f, 0.80f, 0.60f);

            s.baseValue = 32000;
            s.repairCostPerDamagePoint = 25;
        }

        // ── Supercar: Exotic top-tier with extreme performance ──
        private static void ApplySupercar(VehicleStatsData s, DrivetrainType dt)
        {
            s.horsepower = 620f;
            s.maxMotorTorque = 850f;
            s.maxBrakeTorque = 6800f;
            s.maxSpeedKph = 320f;
            s.defaultMass = 1420f;
            s.weightDistributionFront = 0.42f;
            s.centerOfMassHeight = -0.50f;
            s.centerOfMassOffset = new Vector3(0f, -0.50f, -0.12f);

            s.spring = 42000f;
            s.damper = 5200f;
            s.suspensionDistance = 0.12f;
            s.antiRollForce = 4800f;

            s.maxSteerAngle = 22f;
            s.highSpeedSteerReduction = 0.12f;
            s.steeringResponse = 90f;

            s.forwardStiffness = 1.50f;
            s.sidewaysStiffness = 1.60f;
            s.frontGrip = 1.15f;
            s.rearGrip = 1.10f;
            s.traction = 1.10f;
            s.brakeGrip = 1.15f;
            s.slipAngle = 14f;
            s.recoveryRate = 3.2f;
            s.highSpeedStability = 1.20f;

            s.downforce = 65f;
            s.lateralGripAssist = 1.10f;
            s.handbrakeGripMultiplier = 0.55f;
            s.resetLift = 1.0f;

            s.driftAssist = 0.35f;
            s.counterSteerAssist = 0.55f;
            s.yawStability = 0.50f;
            s.nitrousGripAssist = 0.10f;

            s.idleRPM = 1000f;
            s.maxRPM = 8500f;
            s.shiftUpRPM = 8000f;
            s.shiftDownRPM = 3200f;
            s.finalDriveRatio = 3.40f;
            s.gearRatios = new float[] { 0f, 3.40f, 2.20f, 1.62f, 1.25f, 0.98f, 0.78f, 0.64f };

            s.torqueCurve = CreateCurve(0.22f, 0.45f, 0.70f, 0.90f, 0.88f, 0.65f);

            s.baseValue = 85000;
            s.repairCostPerDamagePoint = 45;
        }

        // ── Offroad: High suspension, loose traction, rugged ──
        private static void ApplyOffroad(VehicleStatsData s, DrivetrainType dt)
        {
            s.horsepower = 280f;
            s.maxMotorTorque = 520f;
            s.maxBrakeTorque = 4000f;
            s.maxSpeedKph = 175f;
            s.defaultMass = 1920f;
            s.weightDistributionFront = 0.50f;
            s.centerOfMassHeight = -0.32f;
            s.centerOfMassOffset = new Vector3(0f, -0.32f, 0.0f);

            s.spring = 18000f;
            s.damper = 3000f;
            s.suspensionDistance = 0.28f;
            s.antiRollForce = 2200f;

            s.maxSteerAngle = 30f;
            s.highSpeedSteerReduction = 0.24f;
            s.steeringResponse = 55f;

            s.forwardStiffness = 1.10f;
            s.sidewaysStiffness = 1.15f;
            s.frontGrip = 0.88f;
            s.rearGrip = 0.85f;
            s.traction = 1.15f;
            s.brakeGrip = 0.85f;
            s.slipAngle = 18f;
            s.recoveryRate = 3.0f;
            s.highSpeedStability = 0.70f;

            s.downforce = 12f;
            s.lateralGripAssist = 1.25f;
            s.handbrakeGripMultiplier = 0.60f;
            s.resetLift = 1.5f;

            s.driftAssist = 0.70f;
            s.counterSteerAssist = 0.70f;
            s.yawStability = 0.30f;
            s.nitrousGripAssist = 0.20f;

            s.idleRPM = 750f;
            s.maxRPM = 5000f;
            s.shiftUpRPM = 4400f;
            s.shiftDownRPM = 1400f;
            s.finalDriveRatio = 4.30f;
            s.gearRatios = new float[] { 0f, 3.80f, 2.40f, 1.68f, 1.20f, 0.90f };

            s.torqueCurve = CreateCurve(0.40f, 0.62f, 0.78f, 0.82f, 0.70f, 0.48f);

            s.baseValue = 22000;
            s.repairCostPerDamagePoint = 18;
        }

        // ── Hero: Best blend of style and control — Solstice Type-S ──
        private static void ApplyHero(VehicleStatsData s, DrivetrainType dt)
        {
            s.horsepower = 420f;
            s.maxMotorTorque = 650f;
            s.maxBrakeTorque = 5600f;
            s.maxSpeedKph = 275f;
            s.defaultMass = 1340f;
            s.weightDistributionFront = 0.48f;
            s.centerOfMassHeight = -0.48f;
            s.centerOfMassOffset = new Vector3(0f, -0.48f, 0.03f);

            s.spring = 35000f;
            s.damper = 4500f;
            s.suspensionDistance = 0.15f;
            s.antiRollForce = 4000f;

            s.maxSteerAngle = 24f;
            s.highSpeedSteerReduction = 0.14f;
            s.steeringResponse = 82f;

            s.forwardStiffness = 1.40f;
            s.sidewaysStiffness = 1.55f;
            s.frontGrip = 1.10f;
            s.rearGrip = 1.08f;
            s.traction = 1.08f;
            s.brakeGrip = 1.10f;
            s.slipAngle = 14f;
            s.recoveryRate = 3.8f;
            s.highSpeedStability = 1.12f;

            s.downforce = 48f;
            s.lateralGripAssist = 1.15f;
            s.handbrakeGripMultiplier = 0.50f;
            s.resetLift = 1.1f;

            s.driftAssist = 0.55f;
            s.counterSteerAssist = 0.62f;
            s.yawStability = 0.45f;
            s.nitrousGripAssist = 0.16f;

            s.idleRPM = 900f;
            s.maxRPM = 7600f;
            s.shiftUpRPM = 7000f;
            s.shiftDownRPM = 2600f;
            s.finalDriveRatio = 3.85f;
            s.gearRatios = new float[] { 0f, 3.00f, 2.00f, 1.48f, 1.12f, 0.88f, 0.72f };

            s.torqueCurve = CreateCurve(0.24f, 0.48f, 0.70f, 0.86f, 0.82f, 0.62f);

            s.baseValue = 40000;
            s.repairCostPerDamagePoint = 30;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static AnimationCurve CreateCurve(float v0, float v1, float v2, float v3, float v4, float v5)
        {
            return new AnimationCurve(
                new Keyframe(0.00f, v0),
                new Keyframe(0.20f, v1),
                new Keyframe(0.45f, v2),
                new Keyframe(0.68f, v3),
                new Keyframe(0.86f, v4),
                new Keyframe(1.00f, v5));
        }

        private static void ApplyLegacyWheelMapping(VehicleDefinition def, string canonicalId)
        {
            def.showroomBodyDrop = -0.16f;
            def.useDetachedWheelVisuals = true;

            switch (canonicalId)
            {
                case "solstice_type_s":
                case "rmcar26":
                case "rmcar26_b":
                case "rmcar26_c":
                case "rmcar26_d":
                    def.wheelMapping = new CarWheelMapping(
                        "RMCar26_WheelFrontLeft/RMCar26WheelFrontLeft",
                        "RMCar26_WheelFrontRight/RMCar26WheelFrontRight",
                        "RMCar26_WheelRearLeft/RMCar26WheelRearLeft",
                        "RMCar26_WheelRearRight/RMCar26WheelRearRight");
                    break;
                case "zodic_s_classic":
                case "simple_retro_car":
                    def.wheelMapping = new CarWheelMapping("FL", "FR", "RL", "RR");
                    break;
                case "cyro_monolith":
                case "american_sedan":
                    def.wheelMapping = new CarWheelMapping(
                        "Car_Wheels/C_WheelFL",
                        "Car_Wheels/C_WheelFR",
                        "Car_Wheels/C_Wheels_B",
                        "Car_Wheels/C_Wheels_B");
                    break;
                case "hanse_executive":
                case "american_sedan_stylized":
                    def.wheelMapping = new CarWheelMapping(
                        "CS_WheelFL",
                        "CS_WheelFR",
                        "CS_Wheels_B",
                        "CS_Wheels_B");
                    break;
                case "maverick_vengeance_srt":
                case "protoso_c16":
                case "weaver_pup_s":
                case "reizan_gt_rb":
                case "reizan_icon_iv":
                case "reizan_vanguard_34":
                case "uruk_grinder_4x4":
                case "stratos_element_9":
                case "arcade_car_1":
                case "arcade_car_2":
                case "arcade_car_3":
                case "arcade_car_4":
                case "arcade_car_5":
                case "arcade_car_6":
                case "arcade_car_7":
                case "arcade_car_8":
                case "arcade_car_9":
                case "arcade_car_10":
                    def.wheelMapping = new CarWheelMapping(
                        "Front Left Wheel",
                        "Front Right Wheel",
                        "Rear Left Wheel",
                        "Rear Right Wheel");
                    break;
                case "reizan_350z":
                    // The 350z fbx uses numeric indices for wheel components.
                    // We'll use a specific mapping that PlayerCarAppearanceController will resolve.
                    def.wheelMapping = new CarWheelMapping(
                        "wheel1",
                        "wheel2",
                        "wheel3",
                        "wheel4");
                    break;
            }
        }

        /// <summary>
        /// Best-effort mapping from canonical ID to existing visual prefab.
        /// Falls back to the RMCar26 prefab as a universal placeholder.
        /// </summary>
        private static string ResolveVisualPrefabPath(string canonicalId)
        {
            // Map lore IDs back to the existing prefabs that are in the project.
            // These are placeholder mappings — swap to final assets per car later.
            switch (canonicalId)
            {
                case "solstice_type_s":       return "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26.prefab";
                case "zodic_s_classic":       return "Assets/Polyeler/Simple Retro Car/Prefabs/Simple Retro Car.prefab";
                case "maverick_vengeance_srt": return "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 7.prefab";
                case "protoso_c16":           return "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 2.prefab";
                case "weaver_pup_s":          return "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 3.prefab";
                case "reizan_gt_rb":          return "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 4.prefab";
                case "reizan_icon_iv":        return "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 5.prefab";
                case "reizan_vanguard_34":    return "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 6.prefab";
                case "uruk_grinder_4x4":      return "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 8.prefab";
                case "cyro_monolith":         return "Assets/High Matters/Free American Sedans/Prefabs/Car.prefab";
                case "hanse_executive":       return "Assets/High Matters/Free American Sedans/Prefabs/Car_stylized.prefab";
                case "stratos_element_9":     return "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs/Car 1.prefab";
                case "reizan_350z":           return "Assets/Blender3DByBads/350z.fbx";
                default:                      return "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs/RMCar26.prefab";
            }
        }

        private static string GetArchetypeFlavorText(VehicleArchetype archetype)
        {
            return archetype switch
            {
                VehicleArchetype.StreetCompact => "A nimble city runner — quick reflexes, tight corners.",
                VehicleArchetype.Muscle        => "Raw American muscle — big torque, big slides.",
                VehicleArchetype.Executive     => "Refined luxury cruiser — composed at any speed.",
                VehicleArchetype.Sports        => "A balanced sports machine — expressive and engaging.",
                VehicleArchetype.Supercar      => "Exotic performance — extreme grip, extreme speed.",
                VehicleArchetype.Offroad       => "Built for rough terrain — high travel, loose grip.",
                VehicleArchetype.Hero          => "The protagonist's signature ride — the best of everything.",
                _                              => "A vehicle of the underground."
            };
        }

        private static int GetArchetypeBasePrice(VehicleArchetype archetype)
        {
            return archetype switch
            {
                VehicleArchetype.StreetCompact => 12000,
                VehicleArchetype.Muscle        => 28000,
                VehicleArchetype.Executive     => 35000,
                VehicleArchetype.Sports        => 32000,
                VehicleArchetype.Supercar      => 85000,
                VehicleArchetype.Offroad       => 22000,
                VehicleArchetype.Hero          => 0, // starter car — no purchase needed
                _                              => 20000
            };
        }

        /// <summary>
        /// Creates nested Unity asset folders if they don't exist.
        /// </summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
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
