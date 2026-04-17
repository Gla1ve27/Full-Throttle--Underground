using System;
using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Import
{
    public enum UG2SourceAssetKind
    {
        Unknown,
        AbkBank,
        GinStream,
        CsiRegistry,
        MixMap,
        FxZone,
        Executable
    }

    [Serializable]
    public sealed class UG2SourceAssetRecord
    {
        public UG2SourceAssetKind kind;
        public string sourcePath;
        public string relativePath;
        public string fileName;
        public string extension;
        public long byteLength;
        public string signature;
        public List<string> identifiers = new List<string>();

        public bool HasIdentifier(string value)
        {
            return identifiers != null && identifiers.Contains(value);
        }
    }

    [Serializable]
    public sealed class UG2EngineProfileMapping
    {
        public int profileNumber = -1;
        public string profileName;
        public string accelGinFileName;
        public string decelGinFileName;
        public string engineSpuBankFileName;
        public string engineEeBankFileName;
        public string sweetenerBankFileName;
    }

    [Serializable]
    public sealed class UG2ParsedBank
    {
        public UG2SourceAssetRecord source;
        public List<string> eventNames = new List<string>();
        public List<string> rawIdentifiers = new List<string>();
        public List<int> headerValues = new List<int>();
        public List<UG2AbkCueMetadata> cueMetadata = new List<UG2AbkCueMetadata>();
        public List<string> passASummary = new List<string>();
        public List<string> passAWarnings = new List<string>();
        public bool hasAbkcSignature;
        public bool hasBnkChunk;
        public int bnkChunkOffset = -1;
        public int dataOffset = -1;
        public int firstCueCountCandidate = -1;
        public int cueTableOffset = -1;
    }

    [Serializable]
    public sealed class UG2AbkCueMetadata
    {
        public int cueIndex;
        public string cueName;
        public int tableOffset;
        public int rawId;
        public int sampleOffset;
        public int sampleLength;
        public string confidence;
    }

    [Serializable]
    public sealed class UG2ParsedGin
    {
        public UG2SourceAssetRecord source;
        public bool isDecel;
        public List<int> headerOffsets = new List<int>();
        public List<string> rawIdentifiers = new List<string>();
    }

    [Serializable]
    public sealed class UG2ParsedRegistry
    {
        public UG2SourceAssetRecord source;
        public List<string> eventNames = new List<string>();
    }

    [Serializable]
    public sealed class UG2ParsedBinaryMetadata
    {
        public UG2SourceAssetRecord source;
        public List<int> int32HeaderValues = new List<int>();
        public List<string> rawIdentifiers = new List<string>();
    }

    [Serializable]
    public sealed class UG2AssetScanResult
    {
        public string sourceRoot;
        public string executablePath;
        public List<UG2SourceAssetRecord> allAssets = new List<UG2SourceAssetRecord>();
        public List<UG2ParsedBank> abkBanks = new List<UG2ParsedBank>();
        public List<UG2ParsedGin> ginStreams = new List<UG2ParsedGin>();
        public List<UG2ParsedRegistry> csiRegistries = new List<UG2ParsedRegistry>();
        public List<UG2ParsedBinaryMetadata> mixMaps = new List<UG2ParsedBinaryMetadata>();
        public List<UG2ParsedBinaryMetadata> fxZones = new List<UG2ParsedBinaryMetadata>();
        public List<UG2EngineProfileMapping> engineProfiles = new List<UG2EngineProfileMapping>();
        public List<string> decelGinFileNames = new List<string>();
        public List<string> warnings = new List<string>();

        public UG2SourceAssetRecord FindByFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            for (int i = 0; i < allAssets.Count; i++)
            {
                if (string.Equals(allAssets[i].fileName, fileName, StringComparison.OrdinalIgnoreCase))
                    return allAssets[i];
            }

            return null;
        }
    }

    public interface IUG2AudioDecoder
    {
        bool CanDecode(UG2SourceAssetRecord source);
        bool TryDecode(UG2SourceAssetRecord source, out AudioClip clip, out string diagnostic);
    }

    public sealed class UG2NullAudioDecoder : IUG2AudioDecoder
    {
        public bool CanDecode(UG2SourceAssetRecord source)
        {
            return false;
        }

        public bool TryDecode(UG2SourceAssetRecord source, out AudioClip clip, out string diagnostic)
        {
            clip = null;
            diagnostic = "No decoder is registered for this proprietary UG2 payload yet.";
            return false;
        }
    }
}
