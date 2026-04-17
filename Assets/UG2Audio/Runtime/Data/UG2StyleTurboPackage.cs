using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [CreateAssetMenu(menuName = "UG2 Audio/Turbo Package", fileName = "UG2StyleTurboPackage")]
    public sealed class UG2StyleTurboPackage : ScriptableObject
    {
        public string eventName = "FX_TURBO_01";
        public List<UG2StyleTierBankRef> small1 = new List<UG2StyleTierBankRef>();
        public List<UG2StyleTierBankRef> small2 = new List<UG2StyleTierBankRef>();
        public List<UG2StyleTierBankRef> medium = new List<UG2StyleTierBankRef>();
        public List<UG2StyleTierBankRef> big = new List<UG2StyleTierBankRef>();
        public List<UG2StyleTierBankRef> truck = new List<UG2StyleTierBankRef>();
    }
}
