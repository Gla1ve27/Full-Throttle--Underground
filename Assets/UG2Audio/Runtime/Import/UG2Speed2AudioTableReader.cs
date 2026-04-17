using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UG2Audio.Import
{
    public sealed class UG2Speed2AudioTableReader
    {
        private static readonly Regex EngineTripletRegex = new Regex(
            @"(GIN_[A-Za-z0-9_]+\.gin)[\x00-\x20]+(CAR_(\d{2})_ENG_MB_SPU\.abk)[\x00-\x20]+(CAR_\3_ENG_MB_EE\.abk)",
            RegexOptions.Compiled);

        private static readonly Regex GinFileRegex = new Regex(
            @"GIN_[A-Za-z0-9_]+\.gin",
            RegexOptions.Compiled);

        private static readonly Regex SweetenerRegex = new Regex(
            @"SWTN_CAR_(\d{2})_MB\.abk",
            RegexOptions.Compiled);

        public void ReadInto(string speed2ExePath, UG2AssetScanResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            byte[] bytes = UG2BinaryReaderUtil.ReadAllBytes(speed2ExePath);
            string text = UG2BinaryReaderUtil.GetLatin1String(bytes);
            result.executablePath = UG2BinaryReaderUtil.NormalizePath(speed2ExePath);

            var byProfile = new Dictionary<int, UG2EngineProfileMapping>();
            MatchCollection triplets = EngineTripletRegex.Matches(text);
            for (int i = 0; i < triplets.Count; i++)
            {
                Match match = triplets[i];
                int profileNumber;
                if (!int.TryParse(match.Groups[3].Value, out profileNumber))
                    continue;

                var mapping = new UG2EngineProfileMapping
                {
                    profileNumber = profileNumber,
                    accelGinFileName = match.Groups[1].Value,
                    profileName = StripGinName(match.Groups[1].Value),
                    engineSpuBankFileName = match.Groups[2].Value,
                    engineEeBankFileName = match.Groups[4].Value,
                    sweetenerBankFileName = "SWTN_CAR_" + profileNumber.ToString("00") + "_MB.abk"
                };

                if (!byProfile.ContainsKey(profileNumber))
                    byProfile.Add(profileNumber, mapping);
            }

            MatchCollection sweeteners = SweetenerRegex.Matches(text);
            for (int i = 0; i < sweeteners.Count; i++)
            {
                int profileNumber;
                if (!int.TryParse(sweeteners[i].Groups[1].Value, out profileNumber))
                    continue;

                UG2EngineProfileMapping mapping;
                if (byProfile.TryGetValue(profileNumber, out mapping))
                    mapping.sweetenerBankFileName = sweeteners[i].Value;
            }

            MatchCollection gins = GinFileRegex.Matches(text);
            for (int i = 0; i < gins.Count; i++)
            {
                string fileName = gins[i].Value;
                bool isDecel = fileName.IndexOf("DCL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               fileName.IndexOf("Decel", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isDecel && !result.decelGinFileNames.Contains(fileName))
                    result.decelGinFileNames.Add(fileName);
            }

            foreach (UG2EngineProfileMapping mapping in byProfile.Values)
                mapping.decelGinFileName = GuessDecelFor(mapping.accelGinFileName, result.decelGinFileNames);

            result.engineProfiles.Clear();
            var orderedKeys = new List<int>(byProfile.Keys);
            orderedKeys.Sort();
            for (int i = 0; i < orderedKeys.Count; i++)
                result.engineProfiles.Add(byProfile[orderedKeys[i]]);

            if (result.engineProfiles.Count == 0)
                result.warnings.Add("No engine profile triplets were found in speed2.exe.");
        }

        private static string StripGinName(string ginFileName)
        {
            if (string.IsNullOrEmpty(ginFileName))
                return string.Empty;

            string name = ginFileName;
            if (name.StartsWith("GIN_", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(4);
            if (name.EndsWith(".gin", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            return name;
        }

        private static string GuessDecelFor(string accelGinFileName, List<string> decelFileNames)
        {
            if (string.IsNullOrEmpty(accelGinFileName) || decelFileNames == null || decelFileNames.Count == 0)
                return string.Empty;

            string accelKey = NormalizeForMatch(accelGinFileName);
            string best = string.Empty;
            int bestScore = 0;

            for (int i = 0; i < decelFileNames.Count; i++)
            {
                string candidate = decelFileNames[i];
                string candidateKey = NormalizeForMatch(candidate);
                int score = CommonTokenScore(accelKey, candidateKey);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore >= 4 ? best : string.Empty;
        }

        private static string NormalizeForMatch(string value)
        {
            string key = StripGinName(value).ToLowerInvariant();
            key = key.Replace("_dcl", string.Empty);
            key = key.Replace("_decel", string.Empty);
            key = key.Replace("dcl", string.Empty);
            key = key.Replace("decel", string.Empty);
            key = key.Replace("gin_", string.Empty);
            return key;
        }

        private static int CommonTokenScore(string left, string right)
        {
            string[] leftTokens = left.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            string[] rightTokens = right.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            int score = 0;

            for (int i = 0; i < leftTokens.Length; i++)
            {
                for (int j = 0; j < rightTokens.Length; j++)
                {
                    if (leftTokens[i].Length >= 3 && leftTokens[i] == rightTokens[j])
                        score += leftTokens[i].Length;
                }
            }

            return score;
        }
    }
}
