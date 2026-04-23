using UnityEngine;

namespace FullThrottle.SacredCore.Story
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Rival", fileName = "FT_Rival")]
    public sealed class FTRivalDefinition : ScriptableObject
    {
        public string rivalId = "rival_citycore_01";
        public string displayName = "Vandal";
        public string districtId = "city_core";
        public string signatureCarId = "";
        public int reputationReward = 250;
        [TextArea(2, 6)] public string taunt = "You are not part of this city yet.";
        [TextArea(2, 6)] public string defeatBeat = "Gla1ve takes a name from the board tonight.";
    }
}
