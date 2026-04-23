using FullThrottle.SacredCore.Story;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    [CreateAssetMenu(menuName = "Full Throttle/Sacred Core/Outrun Rival", fileName = "FT_OutrunRival")]
    public sealed class FTOutrunRivalDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string outrunId = "outrun_citycore_01";
        public string displayName = "Unnamed Runner";
        public FTRivalDefinition storyRival;
        public FTCarDefinition carDefinition;

        [Header("Challenge")]
        public string districtId = "city_core";
        public string rewardId = "visual_stage_1";
        [Tooltip("Player wins by opening this straight-line distance from the rival while leading.")]
        [Min(50f)] public float leadDistanceToWin = 1000f;
        [Tooltip("Rival wins by opening this straight-line distance from the player while leading.")]
        [Min(50f)] public float rivalLeadDistanceToLose = 1000f;
        [Min(5f)] public float challengePromptRange = 16f;
        [Tooltip("Legacy field kept for old assets. Runtime Outrun no longer fails from max separation unless a leader reaches the win gap.")]
        [Min(15f)] public float maxChallengeSeparation = 1400f;

        [Header("Roaming Behavior")]
        [Min(20f)] public float roamTargetSpeedKph = 112f;
        [Range(0f, 1f)] public float aggression = 0.72f;
        [Range(0f, 1f)] public float mistakeChance = 0.08f;
    }
}
