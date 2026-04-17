using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [CreateAssetMenu(menuName = "UG2 Audio/Engine Package", fileName = "UG2StyleEnginePackage")]
    public sealed class UG2StyleEnginePackage : ScriptableObject
    {
        public int profileNumber;
        public string profileName;
        public string originalMappingSource;

        public UG2StyleSourceAssetRef accelGin;
        public UG2StyleSourceAssetRef decelGin;
        public UG2StyleSourceAssetRef engineSpuBank;
        public UG2StyleSourceAssetRef engineEeBank;
        public UG2StyleSourceAssetRef sweetenerBank;

        public List<string> engineEventNames = new List<string>();
        public List<string> sweetenerEventNames = new List<string>();
        public List<string> missingRefs = new List<string>();
    }
}
