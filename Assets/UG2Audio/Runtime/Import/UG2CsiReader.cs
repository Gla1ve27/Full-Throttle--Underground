using System;
using System.Collections.Generic;

namespace UG2Audio.Import
{
    public sealed class UG2CsiReader
    {
        public UG2ParsedRegistry Read(string sourceRoot, string path)
        {
            byte[] bytes = UG2BinaryReaderUtil.ReadAllBytes(path);
            UG2SourceAssetRecord source = UG2BinaryReaderUtil.CreateRecord(sourceRoot, path, UG2SourceAssetKind.CsiRegistry, bytes);
            var parsed = new UG2ParsedRegistry { source = source };

            if (!source.signature.StartsWith("MOIR", StringComparison.Ordinal))
                parsed.source.identifiers.Add("WARNING: unexpected CSI signature");

            for (int i = 0; i < source.identifiers.Count; i++)
            {
                string token = source.identifiers[i];
                if (LooksLikeEventName(token) && !parsed.eventNames.Contains(token))
                    parsed.eventNames.Add(token);
            }

            return parsed;
        }

        private static bool LooksLikeEventName(string token)
        {
            if (string.IsNullOrEmpty(token) || token == "MOIR")
                return false;

            return token.StartsWith("CAR", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("FX_", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("ENV_", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("AEMS_", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("Play", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("FE", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("SIREN", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("Sputter_Message", StringComparison.OrdinalIgnoreCase);
        }
    }
}
