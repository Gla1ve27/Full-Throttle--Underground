using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using UnityEngine;

namespace FullThrottle.SacredCore.Economy
{
    /// <summary>
    /// Holds the rules that make races matter.
    /// The game should feel tense, not cruel.
    /// </summary>
    [DefaultExecutionOrder(-9600)]
    public sealed class FTRiskEconomyDirector : MonoBehaviour
    {
        [SerializeField] private int maxHeat = 5;
        [SerializeField] private int baseRepairCostFloor = 150;

        private FTSaveGateway saveGateway;
        private FTSignalBus bus;

        private void Awake()
        {
            FTServices.Register(this);
            saveGateway = FTServices.Get<FTSaveGateway>();
            bus = FTServices.Get<FTSignalBus>();
        }

        public bool CanEnterEvent(int entryFee) => saveGateway.Profile.bankMoney >= Mathf.Max(0, entryFee);

        public bool TryPayEntryFee(int entryFee)
        {
            entryFee = Mathf.Max(0, entryFee);
            if (!CanEnterEvent(entryFee)) return false;

            saveGateway.Profile.bankMoney -= entryFee;
            bus.Raise(new FTMoneyChangedSignal(saveGateway.Profile.bankMoney));
            saveGateway.Save();
            return true;
        }

        public void AddSessionReward(int money, int reputation)
        {
            FTProfileData profile = saveGateway.Profile;
            profile.session.sessionMoney += Mathf.Max(0, money);
            profile.session.sessionReputation += Mathf.Max(0, reputation);
        }

        public void ApplyCrashPenalty(float damageSeverity01)
        {
            FTProfileData profile = saveGateway.Profile;
            int repair = baseRepairCostFloor + Mathf.RoundToInt(Mathf.Clamp01(damageSeverity01) * 800f);
            profile.session.repairDebt += repair;
        }

        public void AdjustHeat(int delta)
        {
            FTProfileData profile = saveGateway.Profile;
            profile.heat = Mathf.Clamp(profile.heat + delta, 0, maxHeat);
            bus.Raise(new FTHeatChangedSignal(profile.heat));
            saveGateway.Save();
        }

        public void BankSession()
        {
            FTProfileData profile = saveGateway.Profile;
            int netMoney = profile.session.sessionMoney - profile.session.repairDebt - profile.session.wagerExposure;
            profile.bankMoney = Mathf.Max(0, profile.bankMoney + netMoney);
            profile.reputation = Mathf.Max(0, profile.reputation + profile.session.sessionReputation);
            profile.session = new FTSessionState();

            bus.Raise(new FTMoneyChangedSignal(profile.bankMoney));
            bus.Raise(new FTRepChangedSignal(profile.reputation));
            saveGateway.Save();
        }

        public void WipeSessionOnly()
        {
            saveGateway.Profile.session = new FTSessionState();
            saveGateway.Save();
        }
    }
}
