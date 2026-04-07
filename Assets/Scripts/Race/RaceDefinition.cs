using UnityEngine;

namespace Underground.Race
{
    public enum RaceType
    {
        Sprint,
        Circuit,
        TimeTrial,
        Underground,
        Wager
    }

    [CreateAssetMenu(menuName = "Racing/Race Definition", fileName = "RaceDefinition")]
    public class RaceDefinition : ScriptableObject
    {
        public string raceId;
        public string displayName;
        public RaceType raceType;
        public bool nightOnly;
        public int entryFee;
        public int rewardMoney;
        public int rewardReputation;
        public int minReputation;
        public int laps = 1;
        public bool allowsCarWager;
    }
}
