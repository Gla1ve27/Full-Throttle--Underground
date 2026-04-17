using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [CreateAssetMenu(menuName = "UG2 Audio/Car Audio Profile", fileName = "UG2StyleCarAudioProfile")]
    public sealed class UG2StyleCarAudioProfile : ScriptableObject
    {
        public int profileNumber;
        public string profileName;
        public string sourceRoot;

        public UG2StyleEnginePackage enginePackage;
        public UG2StyleShiftPackage shiftPackage;
        public UG2StyleTurboPackage turboPackage;
        public UG2StyleSweetenerPackage sweetenerPackage;
        public UG2StyleSkidPackage skidPackage;
        public UG2StyleEventRegistry eventRegistry;

        public List<UG2StyleSourceAssetRef> roadAndWindBanks = new List<UG2StyleSourceAssetRef>();
        public List<UG2StyleRoutingRef> mixMaps = new List<UG2StyleRoutingRef>();
        public List<UG2StyleRoutingRef> fxZones = new List<UG2StyleRoutingRef>();
        public List<string> preservedEventNames = new List<string>();
        public List<string> warnings = new List<string>();
    }
}
