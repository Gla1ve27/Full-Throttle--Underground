using System;
using System.Collections.Generic;
using System.IO;
using UG2Audio.Data;
using UG2Audio.Import;
using UnityEditor;
using UnityEngine;

namespace UG2Audio.Editor
{
    public static class UG2AudioMetadataImporter
    {
        private const string DefaultOutputRoot = "Assets/UG2Audio/Generated";

        [MenuItem("Tools/UG2 Audio/Import Metadata From UG2 Root")]
        public static void ImportFromSelectedRoot()
        {
            string sourceRoot = EditorUtility.OpenFolderPanel("Select local NFS Underground 2 root", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(sourceRoot))
                return;

            Import(sourceRoot, DefaultOutputRoot);
        }

        [MenuItem("Tools/UG2 Audio/Import Profile 18 Vertical Slice")]
        public static void ImportProfile18VerticalSlice()
        {
            string sourceRoot = EditorUtility.OpenFolderPanel("Select local NFS Underground 2 root", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(sourceRoot))
                return;

            const string outputRoot = DefaultOutputRoot + "/VerticalSlice_Profile_18";
            var decoder = new UG2ExternalGinDecoder(string.Empty, outputRoot + "/Decoded");
            Import(sourceRoot, outputRoot, 18, decoder);
        }

        public static List<UG2StyleCarAudioProfile> Import(string sourceRoot, string outputRootAssetPath)
        {
            return Import(sourceRoot, outputRootAssetPath, null, null);
        }

        public static List<UG2StyleCarAudioProfile> Import(
            string sourceRoot,
            string outputRootAssetPath,
            int? profileFilter,
            IUG2AudioDecoder decoder)
        {
            var scanner = new UG2AssetScanner();
            UG2AssetScanResult scan = scanner.Scan(sourceRoot);

            EnsureAssetFolder(outputRootAssetPath);
            EnsureAssetFolder(outputRootAssetPath + "/Profiles");
            EnsureAssetFolder(outputRootAssetPath + "/Engines");
            EnsureAssetFolder(outputRootAssetPath + "/Packages");
            EnsureAssetFolder(outputRootAssetPath + "/Registries");
            EnsureAssetFolder(outputRootAssetPath + "/DebugReports");

            Dictionary<string, UG2ParsedBank> banks = IndexBanks(scan.abkBanks);
            UG2StyleEventRegistry eventRegistry = CreateEventRegistry(scan, outputRootAssetPath + "/Registries");
            UG2StyleShiftPackage shiftPackage = CreateShiftPackage(scan, banks, outputRootAssetPath + "/Packages");
            UG2StyleTurboPackage turboPackage = CreateTurboPackage(scan, banks, outputRootAssetPath + "/Packages");
            UG2StyleSweetenerPackage sweetenerPackage = CreateSweetenerPackage(scan, outputRootAssetPath + "/Packages");
            UG2StyleSkidPackage skidPackage = CreateSkidPackage(scan, banks, outputRootAssetPath + "/Packages");

            var profiles = new List<UG2StyleCarAudioProfile>();
            for (int i = 0; i < scan.engineProfiles.Count; i++)
            {
                UG2EngineProfileMapping mapping = scan.engineProfiles[i];
                if (profileFilter.HasValue && mapping.profileNumber != profileFilter.Value)
                    continue;

                UG2StyleEnginePackage engine = CreateEnginePackage(scan, banks, mapping, outputRootAssetPath + "/Engines");
                DecodeEngineGins(engine, decoder, scan.warnings);
                UG2StyleCarAudioProfile profile = ScriptableObject.CreateInstance<UG2StyleCarAudioProfile>();

                profile.profileNumber = mapping.profileNumber;
                profile.profileName = mapping.profileName;
                profile.sourceRoot = scan.sourceRoot;
                profile.enginePackage = engine;
                profile.shiftPackage = shiftPackage;
                profile.turboPackage = turboPackage;
                profile.sweetenerPackage = sweetenerPackage;
                profile.skidPackage = skidPackage;
                profile.eventRegistry = eventRegistry;
                profile.roadAndWindBanks = CollectRoadAndWind(scan);
                profile.mixMaps = ConvertRouting(scan.mixMaps);
                profile.fxZones = ConvertRouting(scan.fxZones);
                profile.preservedEventNames = new List<string>(eventRegistry.allEventNames);
                profile.warnings = new List<string>(scan.warnings);

                string assetPath = outputRootAssetPath + "/Profiles/Profile_" + mapping.profileNumber.ToString("00") + "_" + Sanitize(mapping.profileName) + ".asset";
                CreateOrReplaceAsset(profile, assetPath);
                CreateProfileDebugReport(profile, outputRootAssetPath + "/DebugReports");
                profiles.Add(profile);
            }

            WriteImportReport(scan, outputRootAssetPath + "/UG2AudioImportReport.txt");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profiles;
        }

        private static UG2StyleEventRegistry CreateEventRegistry(UG2AssetScanResult scan, string outputFolder)
        {
            UG2StyleEventRegistry registry = ScriptableObject.CreateInstance<UG2StyleEventRegistry>();
            registry.sourceRoot = scan.sourceRoot;

            for (int i = 0; i < scan.csiRegistries.Count; i++)
            {
                UG2ParsedRegistry parsed = scan.csiRegistries[i];
                UG2StyleSourceAssetRef source = ToRef(parsed.source);
                registry.registrySources.Add(source);

                for (int j = 0; j < parsed.eventNames.Count; j++)
                {
                    string eventName = parsed.eventNames[j];
                    registry.events.Add(new UG2StyleEventRef { eventName = eventName, source = source });
                    if (!registry.allEventNames.Contains(eventName))
                        registry.allEventNames.Add(eventName);
                }
            }

            registry.allEventNames.Sort(StringComparer.OrdinalIgnoreCase);
            CreateOrReplaceAsset(registry, outputFolder + "/UG2StyleEventRegistry.asset");
            return registry;
        }

        private static UG2StyleEnginePackage CreateEnginePackage(
            UG2AssetScanResult scan,
            Dictionary<string, UG2ParsedBank> banks,
            UG2EngineProfileMapping mapping,
            string outputFolder)
        {
            UG2StyleEnginePackage package = ScriptableObject.CreateInstance<UG2StyleEnginePackage>();
            package.profileNumber = mapping.profileNumber;
            package.profileName = mapping.profileName;
            package.originalMappingSource = scan.executablePath;
            package.accelGin = ToRef(scan.FindByFileName(mapping.accelGinFileName));
            package.decelGin = ToRef(scan.FindByFileName(mapping.decelGinFileName));
            package.engineSpuBank = ToRef(FindBankByFileName(banks, mapping.engineSpuBankFileName), scan.FindByFileName(mapping.engineSpuBankFileName));
            package.engineEeBank = ToRef(FindBankByFileName(banks, mapping.engineEeBankFileName), scan.FindByFileName(mapping.engineEeBankFileName));
            package.sweetenerBank = ToRef(FindBankByFileName(banks, mapping.sweetenerBankFileName), scan.FindByFileName(mapping.sweetenerBankFileName));

            AddEventsFromBank(banks, mapping.engineSpuBankFileName, package.engineEventNames);
            AddEventsFromBank(banks, mapping.engineEeBankFileName, package.engineEventNames);
            AddEventsFromBank(banks, mapping.sweetenerBankFileName, package.sweetenerEventNames);
            AddMissing(package.missingRefs, "accel GIN", mapping.accelGinFileName, package.accelGin);
            AddMissing(package.missingRefs, "decel GIN", mapping.decelGinFileName, package.decelGin);
            AddMissing(package.missingRefs, "SPU engine bank", mapping.engineSpuBankFileName, package.engineSpuBank);
            AddMissing(package.missingRefs, "EE engine bank", mapping.engineEeBankFileName, package.engineEeBank);
            AddMissing(package.missingRefs, "sweetener bank", mapping.sweetenerBankFileName, package.sweetenerBank);
            ValidateAbkRelationship(package, "SPU engine bank", mapping.engineSpuBankFileName, package.engineSpuBank);
            ValidateAbkRelationship(package, "EE engine bank", mapping.engineEeBankFileName, package.engineEeBank);
            ValidateAbkRelationship(package, "sweetener bank", mapping.sweetenerBankFileName, package.sweetenerBank);
            LogAbkPassA("SPU engine bank", FindBankByFileName(banks, mapping.engineSpuBankFileName));
            LogAbkPassA("EE engine bank", FindBankByFileName(banks, mapping.engineEeBankFileName));
            LogAbkPassA("sweetener bank", FindBankByFileName(banks, mapping.sweetenerBankFileName));

            string assetPath = outputFolder + "/Engine_" + mapping.profileNumber.ToString("00") + "_" + Sanitize(mapping.profileName) + ".asset";
            CreateOrReplaceAsset(package, assetPath);
            return package;
        }

        private static UG2StyleShiftPackage CreateShiftPackage(
            UG2AssetScanResult scan,
            Dictionary<string, UG2ParsedBank> banks,
            string outputFolder)
        {
            UG2StyleShiftPackage package = ScriptableObject.CreateInstance<UG2StyleShiftPackage>();
            for (int i = 0; i < scan.abkBanks.Count; i++)
            {
                UG2ParsedBank bank = scan.abkBanks[i];
                string name = bank.source.fileName;
                if (!name.StartsWith("GEAR_", StringComparison.OrdinalIgnoreCase))
                    continue;

                UG2StyleTierBankRef tierRef = ToTierRef(bank, banks);
                if (name.StartsWith("GEAR_SML_", StringComparison.OrdinalIgnoreCase))
                    package.small.Add(tierRef);
                else if (name.StartsWith("GEAR_MED_", StringComparison.OrdinalIgnoreCase))
                    package.medium.Add(tierRef);
                else if (name.StartsWith("GEAR_LRG_", StringComparison.OrdinalIgnoreCase))
                    package.large.Add(tierRef);
                else if (name.StartsWith("GEAR_TK_", StringComparison.OrdinalIgnoreCase))
                    package.truck.Add(tierRef);
            }

            SortTiers(package.small);
            SortTiers(package.medium);
            SortTiers(package.large);
            SortTiers(package.truck);
            CreateOrReplaceAsset(package, outputFolder + "/UG2StyleShiftPackage.asset");
            return package;
        }

        private static UG2StyleTurboPackage CreateTurboPackage(
            UG2AssetScanResult scan,
            Dictionary<string, UG2ParsedBank> banks,
            string outputFolder)
        {
            UG2StyleTurboPackage package = ScriptableObject.CreateInstance<UG2StyleTurboPackage>();
            for (int i = 0; i < scan.abkBanks.Count; i++)
            {
                UG2ParsedBank bank = scan.abkBanks[i];
                string name = bank.source.fileName;
                if (!name.StartsWith("TURBO_", StringComparison.OrdinalIgnoreCase))
                    continue;

                UG2StyleTierBankRef tierRef = ToTierRef(bank, banks);
                if (name.StartsWith("TURBO_SML1_", StringComparison.OrdinalIgnoreCase))
                    package.small1.Add(tierRef);
                else if (name.StartsWith("TURBO_SML2_", StringComparison.OrdinalIgnoreCase))
                    package.small2.Add(tierRef);
                else if (name.StartsWith("TURBO_MED_", StringComparison.OrdinalIgnoreCase))
                    package.medium.Add(tierRef);
                else if (name.StartsWith("TURBO_BIG_", StringComparison.OrdinalIgnoreCase))
                    package.big.Add(tierRef);
                else if (name.StartsWith("TURBO_TRUCK_", StringComparison.OrdinalIgnoreCase))
                    package.truck.Add(tierRef);
            }

            SortTiers(package.small1);
            SortTiers(package.small2);
            SortTiers(package.medium);
            SortTiers(package.big);
            SortTiers(package.truck);
            CreateOrReplaceAsset(package, outputFolder + "/UG2StyleTurboPackage.asset");
            return package;
        }

        private static UG2StyleSweetenerPackage CreateSweetenerPackage(UG2AssetScanResult scan, string outputFolder)
        {
            UG2StyleSweetenerPackage package = ScriptableObject.CreateInstance<UG2StyleSweetenerPackage>();
            for (int i = 0; i < scan.abkBanks.Count; i++)
            {
                UG2ParsedBank bank = scan.abkBanks[i];
                if (bank.source.fileName.StartsWith("SWTN_CAR_", StringComparison.OrdinalIgnoreCase))
                    package.profileSweetenerBanks.Add(ToRef(bank.source));
            }

            package.profileSweetenerBanks.Sort((a, b) => string.Compare(a.fileName, b.fileName, StringComparison.OrdinalIgnoreCase));
            CreateOrReplaceAsset(package, outputFolder + "/UG2StyleSweetenerPackage.asset");
            return package;
        }

        private static UG2StyleSkidPackage CreateSkidPackage(
            UG2AssetScanResult scan,
            Dictionary<string, UG2ParsedBank> banks,
            string outputFolder)
        {
            UG2StyleSkidPackage package = ScriptableObject.CreateInstance<UG2StyleSkidPackage>();
            package.pavement = ToTierRef(FindBank(scan, "SKID_PAV_MB.abk"), banks);
            package.pavementAlt = ToTierRef(FindBank(scan, "SKID_PAV2_MB.abk"), banks);
            package.drift = ToTierRef(FindBank(scan, "SKIDS_DRIFT_MB.abk"), banks);
            package.driftAlt = ToTierRef(FindBank(scan, "SKIDS_DRIFT2_MB.abk"), banks);
            CreateOrReplaceAsset(package, outputFolder + "/UG2StyleSkidPackage.asset");
            return package;
        }

        private static List<UG2StyleSourceAssetRef> CollectRoadAndWind(UG2AssetScanResult scan)
        {
            var refs = new List<UG2StyleSourceAssetRef>();
            string[] names =
            {
                "ROADNOISE_00_MB.abk",
                "WIND_00_MB.abk",
                "WIND_01_MB.abk",
                "TRAFFIC_MB.abk",
                "ENV_COMMON_MB.abk"
            };

            for (int i = 0; i < names.Length; i++)
            {
                UG2SourceAssetRecord source = scan.FindByFileName(names[i]);
                if (source != null)
                    refs.Add(ToRef(source));
            }

            return refs;
        }

        private static List<UG2StyleRoutingRef> ConvertRouting(List<UG2ParsedBinaryMetadata> source)
        {
            var refs = new List<UG2StyleRoutingRef>();
            for (int i = 0; i < source.Count; i++)
            {
                refs.Add(new UG2StyleRoutingRef
                {
                    routeName = Path.GetFileNameWithoutExtension(source[i].source.fileName),
                    source = ToRef(source[i].source),
                    headerValues = new List<int>(source[i].int32HeaderValues)
                });
            }

            return refs;
        }

        private static UG2StyleTierBankRef ToTierRef(UG2ParsedBank bank, Dictionary<string, UG2ParsedBank> banks)
        {
            if (bank == null)
                return null;

            var tierRef = new UG2StyleTierBankRef
            {
                tierName = Path.GetFileNameWithoutExtension(bank.source.fileName),
                bank = ToRef(bank.source),
                eventNames = new List<string>(bank.eventNames)
            };

            return tierRef;
        }

        private static UG2ParsedBank FindBank(UG2AssetScanResult scan, string fileName)
        {
            for (int i = 0; i < scan.abkBanks.Count; i++)
            {
                if (string.Equals(scan.abkBanks[i].source.fileName, fileName, StringComparison.OrdinalIgnoreCase))
                    return scan.abkBanks[i];
            }

            return null;
        }

        private static UG2ParsedBank FindBankByFileName(Dictionary<string, UG2ParsedBank> banks, string fileName)
        {
            if (banks == null || string.IsNullOrEmpty(fileName))
                return null;

            UG2ParsedBank bank;
            return banks.TryGetValue(fileName, out bank) ? bank : null;
        }

        private static Dictionary<string, UG2ParsedBank> IndexBanks(List<UG2ParsedBank> parsedBanks)
        {
            var map = new Dictionary<string, UG2ParsedBank>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < parsedBanks.Count; i++)
            {
                UG2ParsedBank bank = parsedBanks[i];
                if (!map.ContainsKey(bank.source.fileName))
                    map.Add(bank.source.fileName, bank);
            }

            return map;
        }

        private static UG2StyleSourceAssetRef ToRef(UG2SourceAssetRecord source)
        {
            if (source == null)
                return null;

            return new UG2StyleSourceAssetRef
            {
                sourcePath = source.sourcePath,
                relativePath = source.relativePath,
                fileName = source.fileName,
                extension = source.extension,
                byteLength = source.byteLength,
                signature = source.signature,
                identifiers = new List<string>(source.identifiers)
            };
        }

        private static UG2StyleSourceAssetRef ToRef(UG2ParsedBank parsedBank, UG2SourceAssetRecord fallbackSource)
        {
            UG2StyleSourceAssetRef sourceRef = ToRef(parsedBank == null ? fallbackSource : parsedBank.source);
            if (sourceRef == null || parsedBank == null)
                return sourceRef;

            AppendUnique(sourceRef.identifiers, parsedBank.passASummary);
            for (int i = 0; i < parsedBank.cueMetadata.Count; i++)
            {
                UG2AbkCueMetadata cue = parsedBank.cueMetadata[i];
                string cueLine = "ABK Pass A cue: " + cue.cueName +
                                 " index=" + cue.cueIndex +
                                 " tableOffset=0x" + cue.tableOffset.ToString("X") +
                                 " sampleOffset=0x" + (cue.sampleOffset < 0 ? "<none>" : cue.sampleOffset.ToString("X")) +
                                 " confidence=" + cue.confidence;
                if (!sourceRef.identifiers.Contains(cueLine))
                    sourceRef.identifiers.Add(cueLine);
            }

            return sourceRef;
        }

        private static void AddEventsFromBank(Dictionary<string, UG2ParsedBank> banks, string fileName, List<string> target)
        {
            if (string.IsNullOrEmpty(fileName) || target == null)
                return;

            UG2ParsedBank bank;
            if (!banks.TryGetValue(fileName, out bank))
                return;

            for (int i = 0; i < bank.eventNames.Count; i++)
            {
                if (!target.Contains(bank.eventNames[i]))
                    target.Add(bank.eventNames[i]);
            }
        }

        private static void AddMissing(List<string> missing, string role, string expectedFileName, UG2StyleSourceAssetRef source)
        {
            if (!string.IsNullOrEmpty(expectedFileName) && source == null)
                missing.Add(role + ": " + expectedFileName);
        }

        private static void AppendUnique(List<string> target, List<string> values)
        {
            if (target == null || values == null)
                return;

            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];
                if (!string.IsNullOrEmpty(value) && !target.Contains(value))
                    target.Add(value);
            }
        }

        private static void ValidateAbkRelationship(
            UG2StyleEnginePackage package,
            string role,
            string expectedFileName,
            UG2StyleSourceAssetRef source)
        {
            if (package == null)
                return;

            string expected = expectedFileName ?? string.Empty;
            string actual = source == null ? string.Empty : source.fileName;
            bool matched = !string.IsNullOrEmpty(expected) &&
                           !string.IsNullOrEmpty(actual) &&
                           string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
            string message = "Profile " + package.profileNumber.ToString("00") + " " + role +
                             " relationship " + (matched ? "OK" : "MISMATCH") +
                             ": expected=" + (string.IsNullOrEmpty(expected) ? "<empty>" : expected) +
                             " actual=" + (string.IsNullOrEmpty(actual) ? "<missing>" : actual);

            Debug.Log("[UG2AudioMetadataImporter] " + message);
            if (!matched && package.missingRefs != null && !package.missingRefs.Contains(message))
                package.missingRefs.Add(message);
        }

        private static void LogAbkPassA(string role, UG2ParsedBank bank)
        {
            if (bank == null)
            {
                Debug.LogWarning("[UG2AudioMetadataImporter] ABK Pass A " + role + ": no parsed bank was available.");
                return;
            }

            Debug.Log(
                "[UG2AudioMetadataImporter] ABK Pass A " + role + " " + bank.source.fileName + "\n" +
                "Summary:\n" + JoinLines(bank.passASummary) + "\n" +
                "Cue slots:\n" + JoinCueLines(bank.cueMetadata));
        }

        private static void SortTiers(List<UG2StyleTierBankRef> tiers)
        {
            tiers.Sort((a, b) =>
            {
                string left = a == null ? string.Empty : a.tierName;
                string right = b == null ? string.Empty : b.tierName;
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static void DecodeEngineGins(
            UG2StyleEnginePackage engine,
            IUG2AudioDecoder decoder,
            List<string> warnings)
        {
            if (engine == null || decoder == null)
                return;

            DecodeSource(
                engine.accelGin,
                decoder,
                "accel GIN",
                engine.profileNumber,
                "enginePackage.accelGin.decodedClip",
                "profile.enginePackage.accelGin.decodedClip",
                "debugReport.accelDecodedClip",
                warnings);
            DecodeSource(
                engine.decelGin,
                decoder,
                "decel GIN",
                engine.profileNumber,
                "enginePackage.decelGin.decodedClip",
                "profile.enginePackage.decelGin.decodedClip",
                "debugReport.decelDecodedClip",
                warnings);
            EditorUtility.SetDirty(engine);
        }

        private static void DecodeSource(
            UG2StyleSourceAssetRef source,
            IUG2AudioDecoder decoder,
            string role,
            int profileNumber,
            string engineAssignmentPath,
            string profileAssignmentPath,
            string debugReportAssignmentPath,
            List<string> warnings)
        {
            if (source == null)
                return;

            var record = new UG2SourceAssetRecord
            {
                sourcePath = source.sourcePath,
                relativePath = source.relativePath,
                fileName = source.fileName,
                extension = source.extension,
                byteLength = source.byteLength,
                signature = source.signature,
                identifiers = new List<string>(source.identifiers)
            };

            AudioClip clip;
            string diagnostic = string.Empty;
            if (decoder.CanDecode(record) && decoder.TryDecode(record, out clip, out diagnostic))
            {
                source.decodedClip = clip;
                string clipPath = AssetDatabase.GetAssetPath(clip);
                Debug.Log(
                    "[UG2AudioMetadataImporter] Profile " + profileNumber.ToString("00") + " " + role +
                    " decoded clip assignment: " + engineAssignmentPath + " = " + clipPath +
                    "; " + profileAssignmentPath + " resolves through the engine package reference; " +
                    debugReportAssignmentPath + " is populated when CreateProfileDebugReport copies Clip(source).");
                return;
            }

            if (warnings != null)
                warnings.Add("Profile " + profileNumber.ToString("00") + " " + role + " decode skipped/failed for " + source.fileName + ": " + diagnostic);
        }

        private static UG2StyleProfileDebugReport CreateProfileDebugReport(
            UG2StyleCarAudioProfile profile,
            string outputFolder)
        {
            UG2StyleProfileDebugReport report = ScriptableObject.CreateInstance<UG2StyleProfileDebugReport>();
            UG2StyleEnginePackage engine = profile.enginePackage;

            report.profileNumber = profile.profileNumber;
            report.profileName = profile.profileName;
            report.profile = profile;
            report.accelGinRef = RefName(engine == null ? null : engine.accelGin);
            report.decelGinRef = RefName(engine == null ? null : engine.decelGin);
            report.spuBankRef = RefName(engine == null ? null : engine.engineSpuBank);
            report.eeBankRef = RefName(engine == null ? null : engine.engineEeBank);
            report.sweetenerRef = RefName(engine == null ? null : engine.sweetenerBank);
            report.accelDecodedClip = Clip(engine == null ? null : engine.accelGin);
            report.decelDecodedClip = Clip(engine == null ? null : engine.decelGin);
            report.spuDecodedClip = Clip(engine == null ? null : engine.engineSpuBank);
            report.eeDecodedClip = Clip(engine == null ? null : engine.engineEeBank);
            report.sweetenerDecodedClip = Clip(engine == null ? null : engine.sweetenerBank);
            AddAbkRelationshipValidation(profile, report);
            AddAbkPassA(report.spuBankPassAMetadata, report.spuBankCueNames, engine == null ? null : engine.engineSpuBank);
            AddAbkPassA(report.eeBankPassAMetadata, report.eeBankCueNames, engine == null ? null : engine.engineEeBank);
            AddAbkPassA(report.sweetenerBankPassAMetadata, report.sweetenerBankCueNames, engine == null ? null : engine.sweetenerBank);

            AddTierNames(profile.shiftPackage == null ? null : profile.shiftPackage.small, report.shiftCandidates);
            AddTierNames(profile.shiftPackage == null ? null : profile.shiftPackage.medium, report.shiftCandidates);
            AddTierNames(profile.shiftPackage == null ? null : profile.shiftPackage.large, report.shiftCandidates);
            AddTierNames(profile.shiftPackage == null ? null : profile.shiftPackage.truck, report.shiftCandidates);

            AddTierNames(profile.turboPackage == null ? null : profile.turboPackage.small1, report.turboCandidates);
            AddTierNames(profile.turboPackage == null ? null : profile.turboPackage.small2, report.turboCandidates);
            AddTierNames(profile.turboPackage == null ? null : profile.turboPackage.medium, report.turboCandidates);
            AddTierNames(profile.turboPackage == null ? null : profile.turboPackage.big, report.turboCandidates);
            AddTierNames(profile.turboPackage == null ? null : profile.turboPackage.truck, report.turboCandidates);

            AddSkidName(profile.skidPackage == null ? null : profile.skidPackage.pavement, report.skidCandidates);
            AddSkidName(profile.skidPackage == null ? null : profile.skidPackage.pavementAlt, report.skidCandidates);
            AddSkidName(profile.skidPackage == null ? null : profile.skidPackage.drift, report.skidCandidates);
            AddSkidName(profile.skidPackage == null ? null : profile.skidPackage.driftAlt, report.skidCandidates);

            report.eventNames = new List<string>(profile.preservedEventNames);
            report.warnings = new List<string>(profile.warnings);

            string assetPath = outputFolder + "/Debug_Profile_" + profile.profileNumber.ToString("00") + "_" + Sanitize(profile.profileName) + ".asset";
            LogDebugReportDecodedAssignments(profile, report, assetPath);
            CreateOrReplaceAsset(report, assetPath);
            return report;
        }

        private static void LogDebugReportDecodedAssignments(
            UG2StyleCarAudioProfile profile,
            UG2StyleProfileDebugReport report,
            string debugReportAssetPath)
        {
            string profileAssetPath = AssetDatabase.GetAssetPath(profile);
            string engineAssetPath = profile != null && profile.enginePackage != null
                ? AssetDatabase.GetAssetPath(profile.enginePackage)
                : string.Empty;

            Debug.Log(
                "[UG2AudioMetadataImporter] Decoded clip assignment targets for profile " +
                (profile == null ? "<null>" : profile.profileNumber.ToString("00")) + ":\n" +
                "Profile asset: " + profileAssetPath + "\n" +
                "Engine asset: " + engineAssetPath + "\n" +
                "Debug report asset: " + debugReportAssetPath + "\n" +
                "accel: profile.enginePackage.accelGin.decodedClip -> debugReport.accelDecodedClip = " + ClipPath(report == null ? null : report.accelDecodedClip) + "\n" +
                "decel: profile.enginePackage.decelGin.decodedClip -> debugReport.decelDecodedClip = " + ClipPath(report == null ? null : report.decelDecodedClip) + "\n" +
                "spu: profile.enginePackage.engineSpuBank.decodedClip -> debugReport.spuDecodedClip = " + ClipPath(report == null ? null : report.spuDecodedClip) + "\n" +
                "ee: profile.enginePackage.engineEeBank.decodedClip -> debugReport.eeDecodedClip = " + ClipPath(report == null ? null : report.eeDecodedClip) + "\n" +
                "sweetener: profile.enginePackage.sweetenerBank.decodedClip -> debugReport.sweetenerDecodedClip = " + ClipPath(report == null ? null : report.sweetenerDecodedClip) + "\n" +
                "ABK relationship validation entries: " + (report == null || report.abkRelationshipValidation == null ? 0 : report.abkRelationshipValidation.Count) + "\n" +
                "ABK Pass A metadata entries: SPU=" + (report == null || report.spuBankPassAMetadata == null ? 0 : report.spuBankPassAMetadata.Count) +
                " EE=" + (report == null || report.eeBankPassAMetadata == null ? 0 : report.eeBankPassAMetadata.Count) +
                " sweetener=" + (report == null || report.sweetenerBankPassAMetadata == null ? 0 : report.sweetenerBankPassAMetadata.Count));
        }

        private static void AddAbkRelationshipValidation(
            UG2StyleCarAudioProfile profile,
            UG2StyleProfileDebugReport report)
        {
            if (profile == null || report == null)
                return;

            UG2StyleEnginePackage engine = profile.enginePackage;
            int profileNumber = profile.profileNumber;
            AddRelationshipLine(report.abkRelationshipValidation, "SPU engine bank", "CAR_" + profileNumber.ToString("00") + "_ENG_MB_SPU.abk", engine == null ? null : engine.engineSpuBank);
            AddRelationshipLine(report.abkRelationshipValidation, "EE engine bank", "CAR_" + profileNumber.ToString("00") + "_ENG_MB_EE.abk", engine == null ? null : engine.engineEeBank);
            AddRelationshipLine(report.abkRelationshipValidation, "sweetener bank", "SWTN_CAR_" + profileNumber.ToString("00") + "_MB.abk", engine == null ? null : engine.sweetenerBank);
        }

        private static void AddRelationshipLine(
            List<string> target,
            string role,
            string expected,
            UG2StyleSourceAssetRef source)
        {
            if (target == null)
                return;

            string actual = source == null ? string.Empty : source.fileName;
            bool matched = string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
            target.Add(role + ": " + (matched ? "OK" : "MISMATCH") + " expected=" + expected + " actual=" + (string.IsNullOrEmpty(actual) ? "<missing>" : actual));
        }

        private static void AddAbkPassA(
            List<string> metadataTarget,
            List<string> cueTarget,
            UG2StyleSourceAssetRef source)
        {
            if (source == null || source.identifiers == null)
                return;

            for (int i = 0; i < source.identifiers.Count; i++)
            {
                string value = source.identifiers[i];
                if (string.IsNullOrEmpty(value))
                    continue;

                if (value.StartsWith("ABK Pass A cue:", StringComparison.Ordinal))
                {
                    if (cueTarget != null && !cueTarget.Contains(value))
                        cueTarget.Add(value);
                }
                else if (value.StartsWith("ABK Pass A", StringComparison.Ordinal))
                {
                    if (metadataTarget != null && !metadataTarget.Contains(value))
                        metadataTarget.Add(value);
                }
            }
        }

        private static string JoinLines(List<string> lines)
        {
            if (lines == null || lines.Count == 0)
                return "<none>";

            return string.Join("\n", lines.ToArray());
        }

        private static string JoinCueLines(List<UG2AbkCueMetadata> cues)
        {
            if (cues == null || cues.Count == 0)
                return "<none>";

            var lines = new List<string>();
            for (int i = 0; i < cues.Count; i++)
            {
                UG2AbkCueMetadata cue = cues[i];
                lines.Add(cue.cueName + " index=" + cue.cueIndex +
                          " tableOffset=0x" + cue.tableOffset.ToString("X") +
                          " sampleOffset=0x" + (cue.sampleOffset < 0 ? "<none>" : cue.sampleOffset.ToString("X")) +
                          " confidence=" + cue.confidence);
            }

            return string.Join("\n", lines.ToArray());
        }

        private static void AddTierNames(List<UG2StyleTierBankRef> tiers, List<string> target)
        {
            if (tiers == null || target == null)
                return;

            for (int i = 0; i < tiers.Count; i++)
                AddSkidName(tiers[i], target);
        }

        private static void AddSkidName(UG2StyleTierBankRef tier, List<string> target)
        {
            if (tier == null || target == null)
                return;

            string value = tier.tierName + " -> " + RefName(tier.bank);
            if (!target.Contains(value))
                target.Add(value);
        }

        private static string RefName(UG2StyleSourceAssetRef source)
        {
            return source == null ? string.Empty : source.relativePath;
        }

        private static AudioClip Clip(UG2StyleSourceAssetRef source)
        {
            return source == null ? null : source.decodedClip;
        }

        private static string ClipPath(AudioClip clip)
        {
            return clip == null ? "<null>" : AssetDatabase.GetAssetPath(clip);
        }

        private static void WriteImportReport(UG2AssetScanResult scan, string reportAssetPath)
        {
            string projectPath = ToProjectFilePath(reportAssetPath);
            using (var writer = new StreamWriter(projectPath, false))
            {
                writer.WriteLine("UG2 Audio Import Report");
                writer.WriteLine("Source root: " + scan.sourceRoot);
                writer.WriteLine("speed2.exe: " + scan.executablePath);
                writer.WriteLine("ABK banks: " + scan.abkBanks.Count);
                writer.WriteLine("GIN streams: " + scan.ginStreams.Count);
                writer.WriteLine("CSI registries: " + scan.csiRegistries.Count);
                writer.WriteLine("MXB mix maps: " + scan.mixMaps.Count);
                writer.WriteLine("FX zones: " + scan.fxZones.Count);
                writer.WriteLine("Engine profiles: " + scan.engineProfiles.Count);
                writer.WriteLine();
                writer.WriteLine("Engine profile mappings:");
                for (int i = 0; i < scan.engineProfiles.Count; i++)
                {
                    UG2EngineProfileMapping p = scan.engineProfiles[i];
                    writer.WriteLine(p.profileNumber.ToString("00") + " " + p.profileName + " | " +
                                     p.accelGinFileName + " | " + p.decelGinFileName + " | " +
                                     p.engineSpuBankFileName + " | " + p.engineEeBankFileName + " | " +
                                     p.sweetenerBankFileName);
                }

                if (scan.warnings.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("Warnings:");
                    for (int i = 0; i < scan.warnings.Count; i++)
                        writer.WriteLine("- " + scan.warnings[i]);
                }
            }
        }

        private static void CreateOrReplaceAsset(UnityEngine.Object asset, string assetPath)
        {
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(asset, assetPath);
            EditorUtility.SetDirty(asset);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToProjectFilePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, relative);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Unnamed";

            char[] invalid = Path.GetInvalidFileNameChars();
            string result = value;
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');

            result = result.Replace(' ', '_');
            return result;
        }
    }
}
