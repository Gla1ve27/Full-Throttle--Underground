using System.Collections.Generic;
using UnityEngine;

namespace FullThrottle.SacredCore.Story
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Story Act", fileName = "FT_StoryAct")]
    public sealed class FTStoryActDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string actId = "act_01_unknown_name";
        public string title = "Unknown Name";
        [TextArea(4, 10)] public string summary =
            "Gla1ve enters the city with almost nothing, trying to turn hunger into status.";

        [Header("Progression Gates")]
        public int requiredReputation = 0;
        public List<string> requiredRivalWins = new();
        public List<string> unlockDistrictIds = new();

        [Header("Presentation")]
        [TextArea(3, 8)] public string introMonologue =
            "The city does not know Gla1ve yet. That is exactly why the climb matters.";
    }
}
