using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [CreateAssetMenu(menuName = "UG2 Audio/Skid Package", fileName = "UG2StyleSkidPackage")]
    public sealed class UG2StyleSkidPackage : ScriptableObject
    {
        public string eventName = "FX_SKID";
        public UG2StyleTierBankRef pavement;
        public UG2StyleTierBankRef pavementAlt;
        public UG2StyleTierBankRef drift;
        public UG2StyleTierBankRef driftAlt;
    }
}
