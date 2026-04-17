using System;

namespace UG2Audio.Import
{
    public sealed class UG2MixMapReader
    {
        public UG2ParsedBinaryMetadata Read(string sourceRoot, string path)
        {
            byte[] bytes = UG2BinaryReaderUtil.ReadAllBytes(path);
            UG2SourceAssetRecord source = UG2BinaryReaderUtil.CreateRecord(sourceRoot, path, UG2SourceAssetKind.MixMap, bytes);
            return new UG2ParsedBinaryMetadata
            {
                source = source,
                int32HeaderValues = UG2BinaryReaderUtil.ReadInt32HeaderValues(bytes, 64),
                rawIdentifiers = UG2BinaryReaderUtil.ExtractPrintableTokens(bytes)
            };
        }
    }
}
