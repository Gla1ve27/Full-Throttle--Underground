using System.Collections.Generic;
using UnityEngine;

namespace UG2Audio.Data
{
    [CreateAssetMenu(menuName = "UG2 Audio/Event Registry", fileName = "UG2StyleEventRegistry")]
    public sealed class UG2StyleEventRegistry : ScriptableObject
    {
        public string sourceRoot;
        public List<UG2StyleSourceAssetRef> registrySources = new List<UG2StyleSourceAssetRef>();
        public List<UG2StyleEventRef> events = new List<UG2StyleEventRef>();
        public List<string> allEventNames = new List<string>();
    }
}
