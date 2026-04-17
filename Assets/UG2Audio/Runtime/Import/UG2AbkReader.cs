using System;
using System.Collections.Generic;
using System.IO;

namespace UG2Audio.Import
{
    public sealed class UG2AbkReader
    {
        private const int CueTableOffset = 0x80;
        private const int MaxGeneratedCueNames = 64;

        private static readonly string[] EventPrefixes =
        {
            "CAR",
            "FX_",
            "ENV_",
            "AEMS_",
            "SIREN"
        };

        public UG2ParsedBank Read(string sourceRoot, string path)
        {
            byte[] bytes = UG2BinaryReaderUtil.ReadAllBytes(path);
            UG2SourceAssetRecord source = UG2BinaryReaderUtil.CreateRecord(sourceRoot, path, UG2SourceAssetKind.AbkBank, bytes);
            var parsed = new UG2ParsedBank
            {
                source = source,
                rawIdentifiers = new List<string>(source.identifiers)
            };

            parsed.hasAbkcSignature = source.signature.StartsWith("ABKC", StringComparison.Ordinal);
            parsed.headerValues = UG2BinaryReaderUtil.ReadInt32HeaderValues(bytes, 32);
            parsed.bnkChunkOffset = IndexOfAscii(bytes, "BNKl");
            parsed.hasBnkChunk = parsed.bnkChunkOffset >= 0;
            parsed.dataOffset = ReadInt32(bytes, 0x18, -1);
            parsed.cueTableOffset = bytes.Length > CueTableOffset ? CueTableOffset : -1;
            parsed.firstCueCountCandidate = ReadFirstCueCountCandidate(bytes);

            BuildPassAMetadata(parsed, bytes);

            if (!parsed.hasAbkcSignature)
                parsed.source.identifiers.Add("WARNING: unexpected ABK signature");

            for (int i = 0; i < source.identifiers.Count; i++)
            {
                string token = source.identifiers[i];
                if (LooksLikeEventName(token) && !parsed.eventNames.Contains(token))
                    parsed.eventNames.Add(token);
            }

            return parsed;
        }

        private static void BuildPassAMetadata(UG2ParsedBank parsed, byte[] bytes)
        {
            string fileName = parsed.source == null ? string.Empty : parsed.source.fileName;
            string bankName = Path.GetFileNameWithoutExtension(fileName);
            int byteLength = bytes == null ? 0 : bytes.Length;

            parsed.passASummary.Add("ABK Pass A: signature=" + (parsed.hasAbkcSignature ? "ABKC" : "unexpected"));
            parsed.passASummary.Add("ABK Pass A: byteLength=" + byteLength);
            parsed.passASummary.Add("ABK Pass A: dataOffset=0x" + ToHex(parsed.dataOffset));
            parsed.passASummary.Add("ABK Pass A: BNKl offset=0x" + ToHex(parsed.bnkChunkOffset) + " found=" + parsed.hasBnkChunk);
            parsed.passASummary.Add("ABK Pass A: first cue count candidate=" + parsed.firstCueCountCandidate + " at 0x" + ToHex(parsed.cueTableOffset));

            if (!parsed.hasAbkcSignature)
                parsed.passAWarnings.Add("ABK Pass A warning: expected ABKC signature.");

            if (!parsed.hasBnkChunk)
                parsed.passAWarnings.Add("ABK Pass A warning: BNKl chunk marker was not found.");

            if (parsed.dataOffset >= 0 && parsed.bnkChunkOffset >= 0 && parsed.dataOffset != parsed.bnkChunkOffset)
                parsed.passAWarnings.Add("ABK Pass A warning: header data offset does not match BNKl marker offset.");

            int cueCount = DetermineStableCueCount(parsed, fileName);
            if (cueCount > 0)
            {
                AddGeneratedCueSlots(parsed, bytes, bankName, cueCount);
                parsed.passASummary.Add("ABK Pass A: stable generated cue slots=" + cueCount);
            }
            else
            {
                parsed.passASummary.Add("ABK Pass A: stable generated cue slots=0");
                parsed.passAWarnings.Add("ABK Pass A warning: cue slot count is not stable enough for generated clip names yet.");
            }

            for (int i = 0; i < parsed.passAWarnings.Count; i++)
                parsed.passASummary.Add(parsed.passAWarnings[i]);
        }

        private static int DetermineStableCueCount(UG2ParsedBank parsed, string fileName)
        {
            int count = parsed.firstCueCountCandidate;
            if (count <= 0 || count > MaxGeneratedCueNames)
                return 0;

            if (fileName.IndexOf("_ENG_MB_SPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("_ENG_MB_EE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return count;
            }

            return 0;
        }

        private static void AddGeneratedCueSlots(UG2ParsedBank parsed, byte[] bytes, string bankName, int cueCount)
        {
            for (int i = 0; i < cueCount; i++)
            {
                int cueNumber = i + 1;
                int tableOffset = CueTableOffset + i * 4;
                parsed.cueMetadata.Add(new UG2AbkCueMetadata
                {
                    cueIndex = cueNumber,
                    cueName = bankName + "_" + cueNumber.ToString("00"),
                    tableOffset = tableOffset,
                    rawId = ReadByte(bytes, tableOffset, -1),
                    sampleOffset = ReadInt32(bytes, 0x98 + i * 4, -1),
                    sampleLength = -1,
                    confidence = "stable-engine-bank-count"
                });
            }
        }

        private static int ReadFirstCueCountCandidate(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= CueTableOffset)
                return -1;

            return bytes[CueTableOffset];
        }

        private static int ReadByte(byte[] bytes, int offset, int fallback)
        {
            if (bytes == null || offset < 0 || offset >= bytes.Length)
                return fallback;

            return bytes[offset];
        }

        private static int ReadInt32(byte[] bytes, int offset, int fallback)
        {
            if (bytes == null || offset < 0 || offset + 4 > bytes.Length)
                return fallback;

            return BitConverter.ToInt32(bytes, offset);
        }

        private static int IndexOfAscii(byte[] bytes, string marker)
        {
            if (bytes == null || string.IsNullOrEmpty(marker))
                return -1;

            byte[] markerBytes = System.Text.Encoding.ASCII.GetBytes(marker);
            for (int i = 0; i <= bytes.Length - markerBytes.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < markerBytes.Length; j++)
                {
                    if (bytes[i + j] != markerBytes[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                    return i;
            }

            return -1;
        }

        private static string ToHex(int value)
        {
            return value < 0 ? "<none>" : value.ToString("X");
        }

        private static bool LooksLikeEventName(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            for (int i = 0; i < EventPrefixes.Length; i++)
            {
                if (token.StartsWith(EventPrefixes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
