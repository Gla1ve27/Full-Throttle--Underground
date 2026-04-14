// ============================================================
// VehicleRoster.cs
// Part 1 — Architecture & Guardrails
// Place at: Assets/FullThrottle/Vehicles/VehicleRoster.cs
// ============================================================

using System.Collections.Generic;

namespace Underground.Vehicle
{
    /// <summary>
    /// Single source of truth for lore-friendly vehicle IDs and
    /// legacy-to-canonical ID migration.
    /// </summary>
    public static class VehicleRoster
    {
        // ── Canonical Roster Entry ───────────────────────────────────────────
        public readonly struct RosterEntry
        {
            public string CanonicalId { get; }
            public string DisplayName { get; }
            public string ManufacturerLore { get; }
            public VehicleArchetype Archetype { get; }
            public DrivetrainType Drivetrain { get; }

            public RosterEntry(
                string canonicalId,
                string displayName,
                string manufacturerLore,
                VehicleArchetype archetype,
                DrivetrainType drivetrain)
            {
                CanonicalId = canonicalId;
                DisplayName = displayName;
                ManufacturerLore = manufacturerLore;
                Archetype = archetype;
                Drivetrain = drivetrain;
            }
        }

        // ── Canonical lore IDs ───────────────────────────────────────────────
        public const string HeroCarId            = "solstice_type_s";
        public const string ZodicSClassicId      = "zodic_s_classic";
        public const string MaverickVengeanceId  = "maverick_vengeance_srt";
        public const string ProtosoCId           = "protoso_c16";
        public const string WeaverPupId          = "weaver_pup_s";
        public const string StratosElementId     = "stratos_element_9";
        public const string ReizanGtRbId         = "reizan_gt_rb";
        public const string ReizanIconId         = "reizan_icon_iv";
        public const string UrukGrinderId        = "uruk_grinder_4x4";
        public const string ReizanVanguardId     = "reizan_vanguard_34";
        public const string CyroMonolithId       = "cyro_monolith";
        public const string HanseExecutiveId     = "hanse_executive";

        // ── Hero visual kit IDs (NOT separate cars) ──────────────────────────
        public const string SolsticeKitB = "solstice_type_s_kit_b";
        public const string SolsticeKitC = "solstice_type_s_kit_c";
        public const string SolsticeKitD = "solstice_type_s_kit_d";

        // ── Full Roster Metadata ────────────────────────────────────────────
        public static readonly RosterEntry[] CoreRoster =
        {
            new RosterEntry(ZodicSClassicId,      "Zodic S-Classic",       "Zodic",    VehicleArchetype.StreetCompact, DrivetrainType.FWD),
            new RosterEntry(MaverickVengeanceId, "Maverick Vengeance SRT","Maverick", VehicleArchetype.Muscle,        DrivetrainType.RWD),
            new RosterEntry(ProtosoCId,          "Protoso C-16",          "Protoso",  VehicleArchetype.StreetCompact, DrivetrainType.FWD),
            new RosterEntry(WeaverPupId,         "Weaver Pup S",          "Weaver",   VehicleArchetype.StreetCompact, DrivetrainType.FWD),
            new RosterEntry(StratosElementId,    "Stratos Element 9",     "Stratos",  VehicleArchetype.Supercar,      DrivetrainType.AWD),
            new RosterEntry(ReizanGtRbId,         "Reizan GT-RB",          "Reizan",   VehicleArchetype.Sports,        DrivetrainType.AWD),
            new RosterEntry(ReizanIconId,         "Reizan Icon IV",        "Reizan",   VehicleArchetype.Sports,        DrivetrainType.RWD),
            new RosterEntry(UrukGrinderId,        "Uruk Grinder 4x4",      "Uruk",     VehicleArchetype.Offroad,       DrivetrainType.AWD),
            new RosterEntry(ReizanVanguardId,    "Reizan Vanguard 34",    "Reizan",   VehicleArchetype.Sports,        DrivetrainType.RWD),
            new RosterEntry(CyroMonolithId,       "Cyro Monolith",         "Cyro",     VehicleArchetype.Executive,     DrivetrainType.RWD),
            new RosterEntry(HanseExecutiveId,     "Hanse Executive",       "Hanse",    VehicleArchetype.Executive,     DrivetrainType.AWD),
            new RosterEntry(HeroCarId,            "Solstice Type-S",       "Solstice", VehicleArchetype.Hero,          DrivetrainType.RWD),
        };

        // ── All canonical IDs in roster order ───────────────────────────────
        public static readonly string[] AllCanonicalIds =
        {
            HeroCarId,
            ZodicSClassicId,
            MaverickVengeanceId,
            ProtosoCId,
            WeaverPupId,
            StratosElementId,
            ReizanGtRbId,
            ReizanIconId,
            UrukGrinderId,
            ReizanVanguardId,
            CyroMonolithId,
            HanseExecutiveId,
        };

        // ── Legacy → canonical migration ────────────────────────────────────
        // Maps any old ID (save file, Gemini-modified, or arcade temp name)
        // to the correct canonical lore ID.
        private static readonly Dictionary<string, string> LegacyToCanonical =
            new Dictionary<string, string>
        {
            // Hero car and its old rmcar26 variants → all resolve to hero base
            { "rmcar26",           HeroCarId },
            { "rmcar26_b",         HeroCarId },   // now Kit B upgrade
            { "rmcar26_c",         HeroCarId },   // now Kit C upgrade
            { "rmcar26_d",         HeroCarId },   // now Kit D upgrade
            { "starter_car",       HeroCarId },
            { "starter",           HeroCarId },
            { "default_car",       HeroCarId },
            { "default",           HeroCarId },

            // Old display-name strings
            { "simple_retro_car",          ZodicSClassicId },
            { "Simple Retro Car",          ZodicSClassicId },
            { "simple retro car",          ZodicSClassicId },

            { "arcade_car_7",              MaverickVengeanceId },
            { "Arcade Car 7",              MaverickVengeanceId },
            { "arcade car 7",              MaverickVengeanceId },
            { "Car 7",                     MaverickVengeanceId },

            { "arcade_car_2",              ProtosoCId },
            { "Arcade Car 2",              ProtosoCId },
            { "arcade car 2",              ProtosoCId },
            { "Car 2",                     ProtosoCId },

            { "arcade_car_3",              WeaverPupId },
            { "Arcade Car 3",              WeaverPupId },
            { "arcade car 3",              WeaverPupId },
            { "Car 3",                     WeaverPupId },

            { "arcade_car_1",              StratosElementId },
            { "Arcade Car 1",              StratosElementId },
            { "arcade car 1",              StratosElementId },
            { "Car 1",                     StratosElementId },

            { "arcade_car_4",              ReizanGtRbId },
            { "Arcade Car 4",              ReizanGtRbId },
            { "arcade car 4",              ReizanGtRbId },
            { "Car 4",                     ReizanGtRbId },

            { "arcade_car_5",              ReizanIconId },
            { "Arcade Car 5",              ReizanIconId },
            { "arcade car 5",              ReizanIconId },
            { "Car 5",                     ReizanIconId },

            { "arcade_car_8",              UrukGrinderId },
            { "Arcade Car 8",              UrukGrinderId },
            { "arcade car 8",              UrukGrinderId },
            { "Car 8",                     UrukGrinderId },

            { "arcade_car_6",              ReizanVanguardId },
            { "Arcade Car 6",              ReizanVanguardId },
            { "arcade car 6",              ReizanVanguardId },
            { "Car 6",                     ReizanVanguardId },

            { "american_sedan",            CyroMonolithId },
            { "American Sedan",            CyroMonolithId },

            { "american_sedan_stylized",   HanseExecutiveId },
            { "American Sedan Stylized",   HanseExecutiveId },

            // arcade 9 and 10 — no lore assignment yet, keep as-is
            { "arcade_car_9",  "arcade_car_9" },
            { "arcade_car_10", "arcade_car_10" },
        };

        /// <summary>
        /// Resolves any old or interim ID to its canonical lore ID.
        /// Returns the input unchanged if it is already canonical.
        /// </summary>
        public static string MigrateLegacyId(string rawId)
        {
            if (string.IsNullOrEmpty(rawId)) return HeroCarId;
            if (LegacyToCanonical.TryGetValue(rawId, out string canonical)) return canonical;

            string slug = rawId.Trim().ToLowerInvariant()
                               .Replace(" ", "_").Replace("-", "_");
            if (LegacyToCanonical.TryGetValue(slug, out canonical)) return canonical;

            return rawId;
        }

        /// <summary>Returns true if the given ID is a canonical lore ID.</summary>
        public static bool IsCanonical(string id)
        {
            for (int i = 0; i < AllCanonicalIds.Length; i++)
                if (AllCanonicalIds[i] == id) return true;
            return false;
        }

        /// <summary>Returns true if the given ID is a known legacy/old ID.</summary>
        public static bool IsLegacyId(string id)
        {
            return !string.IsNullOrEmpty(id) && LegacyToCanonical.ContainsKey(id);
        }

        /// <summary>Tries to find a roster entry by canonical ID.</summary>
        public static bool TryGetEntry(string canonicalId, out RosterEntry entry)
        {
            for (int i = 0; i < CoreRoster.Length; i++)
            {
                if (CoreRoster[i].CanonicalId == canonicalId)
                {
                    entry = CoreRoster[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
