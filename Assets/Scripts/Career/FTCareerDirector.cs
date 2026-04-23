using System.Collections.Generic;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Story;
using UnityEngine;

namespace FullThrottle.SacredCore.Career
{
    /// <summary>
    /// Protects the structure of Gla1ve's rise.
    /// This is one of the sacred scripts that future AI should not casually rewrite.
    /// </summary>
    [DefaultExecutionOrder(-9590)]
    public sealed class FTCareerDirector : MonoBehaviour
    {
        [SerializeField] private List<FTStoryActDefinition> acts = new();
        [SerializeField] private List<FTRivalDefinition> rivals = new();

        private readonly Dictionary<string, FTStoryActDefinition> actMap = new();
        private readonly Dictionary<string, FTRivalDefinition> rivalMap = new();
        private FTSaveGateway saveGateway;
        private FTSignalBus bus;

        private void Awake()
        {
            FTServices.Register(this);
            saveGateway = FTServices.Get<FTSaveGateway>();
            bus = FTServices.Get<FTSignalBus>();

            actMap.Clear();
            foreach (FTStoryActDefinition act in acts)
            {
                if (act != null && !string.IsNullOrWhiteSpace(act.actId))
                {
                    actMap[act.actId] = act;
                }
            }

            rivalMap.Clear();
            foreach (FTRivalDefinition rival in rivals)
            {
                if (rival != null && !string.IsNullOrWhiteSpace(rival.rivalId))
                {
                    rivalMap[rival.rivalId] = rival;
                }
            }
        }

        public FTStoryActDefinition GetCurrentAct()
        {
            string actId = saveGateway.Profile.currentActId;
            actMap.TryGetValue(actId, out FTStoryActDefinition act);
            return act;
        }

        public void AwardReputation(int amount)
        {
            if (amount <= 0) return;
            saveGateway.Profile.reputation += amount;
            bus.Raise(new FTRepChangedSignal(saveGateway.Profile.reputation));
            TryAdvanceAct();
        }

        public void RegisterRivalWin(string rivalId)
        {
            if (string.IsNullOrWhiteSpace(rivalId)) return;
            if (!saveGateway.Profile.beatenRivalIds.Contains(rivalId))
            {
                saveGateway.Profile.beatenRivalIds.Add(rivalId);
            }

            if (rivalMap.TryGetValue(rivalId, out FTRivalDefinition rival))
            {
                AwardReputation(rival.reputationReward);
            }
            else
            {
                TryAdvanceAct();
            }
        }

        public void TryAdvanceAct()
        {
            FTProfileData profile = saveGateway.Profile;
            foreach (FTStoryActDefinition act in acts)
            {
                if (act == null || profile.unlockedActIds.Contains(act.actId))
                {
                    continue;
                }

                bool repReady = profile.reputation >= act.requiredReputation;
                bool rivalsReady = true;
                foreach (string rivalId in act.requiredRivalWins)
                {
                    if (!profile.beatenRivalIds.Contains(rivalId))
                    {
                        rivalsReady = false;
                        break;
                    }
                }

                if (!repReady || !rivalsReady)
                {
                    continue;
                }

                profile.unlockedActIds.Add(act.actId);
                profile.currentActId = act.actId;

                foreach (string districtId in act.unlockDistrictIds)
                {
                    if (!profile.unlockedDistrictIds.Contains(districtId))
                    {
                        profile.unlockedDistrictIds.Add(districtId);
                    }
                }

                Debug.Log($"[SacredCore] Story advanced to {act.title} ({act.actId}).");
                break;
            }

            saveGateway.Save();
        }
    }
}
