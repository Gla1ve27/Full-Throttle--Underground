using System;
using System.Collections.Generic;

namespace UG2Audio.Import
{
    public sealed class UG2GinReader
    {
        public UG2ParsedGin Read(string sourceRoot, string path)
        {
            byte[] bytes = UG2BinaryReaderUtil.ReadAllBytes(path);
            UG2SourceAssetRecord source = UG2BinaryReaderUtil.CreateRecord(sourceRoot, path, UG2SourceAssetKind.GinStream, bytes);
            string fileName = source.fileName ?? string.Empty;

            var parsed = new UG2ParsedGin
            {
                source = source,
                isDecel = fileName.IndexOf("DCL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          fileName.IndexOf("Decel", StringComparison.OrdinalIgnoreCase) >= 0,
                rawIdentifiers = new List<string>(source.identifiers)
            };

            if (!source.signature.StartsWith("Gnsu20", StringComparison.Ordinal))
                parsed.source.identifiers.Add("WARNING: unexpected GIN signature");

            // GIN files have an offset-heavy header. Preserve a bounded snapshot so a later
            // decoder can use the original stream structure without rescanning every file.
            parsed.headerOffsets = UG2BinaryReaderUtil.ReadInt32HeaderValues(bytes, 48);
            return parsed;
        }
    }
}
