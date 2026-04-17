using UnityEngine;

namespace Underground.Race
{
    public enum RaceType
    {
        Sprint,
        Circuit,
        TimeTrial,
        Underground,
        Wager,
        Drift,
        Drag
    }

    [CreateAssetMenu(menuName = "Racing/Race Definition", fileName = "RaceDefinition")]
    public class RaceDefinition : ScriptableObject
    {
        public string raceId;
        public string displayName;
        public RaceType raceType;
        public int rewardMoney;
        public int rewardReputation;
        public int laps = 1;
    }
}
