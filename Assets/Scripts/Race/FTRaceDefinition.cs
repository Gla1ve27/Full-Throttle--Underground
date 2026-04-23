using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Race Definition", fileName = "FT_Race")]
    public sealed class FTRaceDefinition : ScriptableObject
    {
        public string raceId = "race_citycore_01";
        public string displayName = "Night Entry";
        public string districtId = "city_core";
        public string requiredActId = "act_01_unknown_name";
        public string rivalId = "";
        public int entryFee = 250;
        public int payout = 900;
        public int reputationReward = 120;
        public bool isWagerRace = false;
        public int wagerAmount = 0;
        public List<string> allowedCarIds = new();
    }
}
