using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [CreateAssetMenu(menuName = "UG2 Audio/Profile Debug Report", fileName = "UG2StyleProfileDebugReport")]
    public sealed class UG2StyleProfileDebugReport : ScriptableObject
    {
        public int profileNumber;
        public string profileName;
        public UG2StyleCarAudioProfile profile;

        public string accelGinRef;
        public string decelGinRef;
        public string spuBankRef;
        public string eeBankRef;
        public string sweetenerRef;

        public AudioClip accelDecodedClip;
        public AudioClip decelDecodedClip;
        public AudioClip spuDecodedClip;
        public AudioClip eeDecodedClip;
        public AudioClip sweetenerDecodedClip;

        public List<string> abkRelationshipValidation = new List<string>();
        public List<string> spuBankPassAMetadata = new List<string>();
        public List<string> eeBankPassAMetadata = new List<string>();
        public List<string> sweetenerBankPassAMetadata = new List<string>();
        public List<string> spuBankCueNames = new List<string>();
        public List<string> eeBankCueNames = new List<string>();
        public List<string> sweetenerBankCueNames = new List<string>();

        public List<string> shiftCandidates = new List<string>();
        public List<string> turboCandidates = new List<string>();
        public List<string> skidCandidates = new List<string>();
        public List<string> eventNames = new List<string>();
        public List<string> warnings = new List<string>();
    }
}
