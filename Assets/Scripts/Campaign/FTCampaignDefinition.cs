using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Campaign
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Campaign", fileName = "FT_Campaign")]
    public sealed class FTCampaignDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string campaignId = "full_throttle_gla1ve";
        public string title = "Full Throttle";
        public string protagonistRealName = "Marc Badua";
        public string protagonistStreetName = "Gla1ve";
        [TextArea(3, 8)] public string thesis =
            "A nobody becomes a name that changes what the city respects.";

        [Header("Campaign Spine")]
        public List<FTCampaignChapterDefinition> chapters = new();

        public FTCampaignChapterDefinition FindChapter(string chapterId)
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                FTCampaignChapterDefinition chapter = chapters[i];
                if (chapter != null && chapter.chapterId == chapterId)
                {
                    return chapter;
                }
            }

            return null;
        }
    }
}
