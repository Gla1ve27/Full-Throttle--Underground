using System;
using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [Serializable]
    public sealed class UG2StyleSourceAssetRef
    {
        public string sourcePath;
        public string relativePath;
        public string fileName;
        public string extension;
        public long byteLength;
        public string signature;
        public List<string> identifiers = new List<string>();
        public AudioClip decodedClip;
    }

    [Serializable]
    public sealed class UG2StyleEventRef
    {
        public string eventName;
        public UG2StyleSourceAssetRef source;
    }

    [Serializable]
    public sealed class UG2StyleTierBankRef
    {
        public string tierName;
        public UG2StyleSourceAssetRef bank;
        public List<string> eventNames = new List<string>();
    }

    [Serializable]
    public sealed class UG2StyleRoutingRef
    {
        public string routeName;
        public UG2StyleSourceAssetRef source;
        public List<int> headerValues = new List<int>();
    }
}
