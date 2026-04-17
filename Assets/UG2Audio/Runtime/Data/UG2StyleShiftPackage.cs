using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [CreateAssetMenu(menuName = "UG2 Audio/Shift Package", fileName = "UG2StyleShiftPackage")]
    public sealed class UG2StyleShiftPackage : ScriptableObject
    {
        public string eventName = "FX_SHIFTING_01";
        public List<UG2StyleTierBankRef> small = new List<UG2StyleTierBankRef>();
        public List<UG2StyleTierBankRef> medium = new List<UG2StyleTierBankRef>();
        public List<UG2StyleTierBankRef> large = new List<UG2StyleTierBankRef>();
        public List<UG2StyleTierBankRef> truck = new List<UG2StyleTierBankRef>();
    }
}
