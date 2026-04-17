using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [CreateAssetMenu(menuName = "UG2 Audio/Sweetener Package", fileName = "UG2StyleSweetenerPackage")]
    public sealed class UG2StyleSweetenerPackage : ScriptableObject
    {
        public List<UG2StyleSourceAssetRef> profileSweetenerBanks = new List<UG2StyleSourceAssetRef>();
        public List<string> eventNames = new List<string> { "CAR_SWTN", "CAR_Sputter", "CAR_SputOutput" };
    }
}
