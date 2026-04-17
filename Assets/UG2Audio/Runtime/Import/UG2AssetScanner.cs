using System;
using System.IO;

namespace UG2Audio.Import
{
    public sealed class UG2AssetScanner
    {
        private readonly UG2AbkReader abkReader = new UG2AbkReader();
        private readonly UG2GinReader ginReader = new UG2GinReader();
        private readonly UG2CsiReader csiReader = new UG2CsiReader();
        private readonly UG2MixMapReader mixMapReader = new UG2MixMapReader();
        private readonly UG2FxZoneReader fxZoneReader = new UG2FxZoneReader();
        private readonly UG2Speed2AudioTableReader speed2Reader = new UG2Speed2AudioTableReader();

        public UG2AssetScanResult Scan(string sourceRoot)
        {
            if (string.IsNullOrEmpty(sourceRoot))
                throw new ArgumentException("Source root is required.", nameof(sourceRoot));

            sourceRoot = Path.GetFullPath(sourceRoot);
            var result = new UG2AssetScanResult { sourceRoot = UG2BinaryReaderUtil.NormalizePath(sourceRoot) };

            string soundRoot = Path.Combine(sourceRoot, "SOUND");
            if (!Directory.Exists(soundRoot))
                throw new DirectoryNotFoundException("Expected UG2 SOUND directory was not found: " + soundRoot);

            ScanFiles(sourceRoot, soundRoot, "*.abk", path =>
            {
                UG2ParsedBank parsed = abkReader.Read(sourceRoot, path);
                result.abkBanks.Add(parsed);
                result.allAssets.Add(parsed.source);
            });

            ScanFiles(sourceRoot, soundRoot, "*.gin", path =>
            {
                UG2ParsedGin parsed = ginReader.Read(sourceRoot, path);
                result.ginStreams.Add(parsed);
                result.allAssets.Add(parsed.source);
            });

            ScanFiles(sourceRoot, soundRoot, "*.csi", path =>
            {
                UG2ParsedRegistry parsed = csiReader.Read(sourceRoot, path);
                result.csiRegistries.Add(parsed);
                result.allAssets.Add(parsed.source);
            });

            ScanFiles(sourceRoot, soundRoot, "*.mxb", path =>
            {
                UG2ParsedBinaryMetadata parsed = mixMapReader.Read(sourceRoot, path);
                result.mixMaps.Add(parsed);
                result.allAssets.Add(parsed.source);
            });

            ScanFiles(sourceRoot, soundRoot, "*.fx", path =>
            {
                UG2ParsedBinaryMetadata parsed = fxZoneReader.Read(sourceRoot, path);
                result.fxZones.Add(parsed);
                result.allAssets.Add(parsed.source);
            });

            string speed2Path = Path.Combine(sourceRoot, "speed2.exe");
            if (File.Exists(speed2Path))
                speed2Reader.ReadInto(speed2Path, result);
            else
                result.warnings.Add("speed2.exe was not found, so embedded car audio profile mapping is unavailable.");

            ValidateProfileRefs(result);
            return result;
        }

        private static void ScanFiles(string sourceRoot, string root, string pattern, Action<string> read)
        {
            string[] files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
                read(files[i]);
        }

        private static void ValidateProfileRefs(UG2AssetScanResult result)
        {
            for (int i = 0; i < result.engineProfiles.Count; i++)
            {
                UG2EngineProfileMapping profile = result.engineProfiles[i];
                WarnMissing(result, profile.accelGinFileName, "accel GIN", profile.profileNumber);
                WarnMissing(result, profile.engineSpuBankFileName, "SPU engine bank", profile.profileNumber);
                WarnMissing(result, profile.engineEeBankFileName, "EE engine bank", profile.profileNumber);
                WarnMissing(result, profile.sweetenerBankFileName, "sweetener bank", profile.profileNumber);

                if (!string.IsNullOrEmpty(profile.decelGinFileName))
                    WarnMissing(result, profile.decelGinFileName, "decel GIN", profile.profileNumber);
            }
        }

        private static void WarnMissing(UG2AssetScanResult result, string fileName, string role, int profileNumber)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            if (result.FindByFileName(fileName) == null)
                result.warnings.Add("Profile " + profileNumber.ToString("00") + " references missing " + role + ": " + fileName);
        }
    }
}
