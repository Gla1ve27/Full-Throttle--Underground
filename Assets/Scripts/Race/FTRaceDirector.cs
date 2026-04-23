using FullThrottle.SacredCore.Career;
using FullThrottle.SacredCore.Economy;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    /// <summary>
    /// Career structure in executable form.
    /// Begin race, resolve stakes, award progress, bank pressure.
    /// </summary>
    [DefaultExecutionOrder(-9400)]
    public sealed class FTRaceDirector : MonoBehaviour
    {
        private FTRiskEconomyDirector riskEconomy;
        private FTCareerDirector career;
        private FTSaveGateway saveGateway;
        private FTSignalBus bus;

        public FTRaceDefinition ActiveRace { get; private set; }
        public bool RaceLive { get; private set; }

        private void Awake()
        {
            FTServices.Register(this);
            riskEconomy = FTServices.Get<FTRiskEconomyDirector>();
            career = FTServices.Get<FTCareerDirector>();
            saveGateway = FTServices.Get<FTSaveGateway>();
            bus = FTServices.Get<FTSignalBus>();
        }

        public bool TryBeginRace(FTRaceDefinition definition)
        {
            if (definition == null || RaceLive) return false;
            if (!riskEconomy.TryPayEntryFee(definition.entryFee))
            {
                return false;
            }

            ActiveRace = definition;
            RaceLive = true;
            saveGateway.Profile.session.raceInProgress = true;
            saveGateway.Profile.session.activeRaceId = definition.raceId;
            saveGateway.Save();
            Debug.Log($"[SacredCore] Race started: {definition.raceId}, entry={definition.entryFee}, wager={definition.wagerAmount}.");
            return true;
        }

        public void CompleteRace(bool won, float crashSeverity01)
        {
            if (!RaceLive || ActiveRace == null) return;

            riskEconomy.ApplyCrashPenalty(crashSeverity01);

            if (won)
            {
                riskEconomy.AddSessionReward(ActiveRace.payout, ActiveRace.reputationReward);
                riskEconomy.AdjustHeat(1);
                if (!string.IsNullOrWhiteSpace(ActiveRace.rivalId))
                {
                    career.RegisterRivalWin(ActiveRace.rivalId);
                }
                else
                {
                    career.AwardReputation(ActiveRace.reputationReward);
                }
            }
            else
            {
                if (ActiveRace.isWagerRace)
                {
                    saveGateway.Profile.session.wagerExposure += Mathf.Max(0, ActiveRace.wagerAmount);
                }

                riskEconomy.AdjustHeat(1);
            }

            saveGateway.Profile.session.raceInProgress = false;
            saveGateway.Profile.session.activeRaceId = string.Empty;
            bus.Raise(new FTRaceResolvedSignal(ActiveRace, won));
            Debug.Log($"[SacredCore] Race resolved: {ActiveRace.raceId}, won={won}, crash={crashSeverity01:0.00}.");
            saveGateway.Save();
            RaceLive = false;
            ActiveRace = null;
        }
    }
}
