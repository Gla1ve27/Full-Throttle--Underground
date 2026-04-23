using System.Collections.Generic;
using FullThrottle.SacredCore.Race;
using UnityEngine;

namespace FullThrottle.SacredCore.Campaign
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Campaign Chapter", fileName = "FT_CampaignChapter")]
    public sealed class FTCampaignChapterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string chapterId = "chapter_01_filler_on_the_grid";
        public string actId = "act_01_unknown_name";
        public int chapterNumber = 1;
        public string title = "Filler On The Grid";
        public string districtId = "city_core";
        [TextArea(2, 5)] public string narrativePurpose;

        [Header("Progression Gates")]
        public int requiredReputation;
        public List<string> requiredChapterIds = new();
        public List<string> requiredRaceWins = new();
        public List<string> requiredRivalWins = new();

        [Header("Gameplay Content")]
        public List<FTRaceDefinition> requiredRaces = new();
        public List<FTRaceDefinition> optionalRaces = new();
        public List<FTOutrunRivalDefinition> roamingOutrunRivals = new();

        [Header("Narrative Beats")]
        public FTNarrativeBeatDefinition introBeat;
        public FTNarrativeBeatDefinition preRaceBeat;
        public FTNarrativeBeatDefinition postRaceBeat;
        public FTNarrativeBeatDefinition garageBeat;
        public FTNarrativeBeatDefinition radioFollowUpBeat;

        [Header("Rewards")]
        public int moneyReward;
        public int reputationReward;
        public List<string> unlockDistrictIds = new();
        public List<string> unlockRewardIds = new();
        public List<string> unlockChapterIds = new();
    }
}
