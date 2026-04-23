using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    [DefaultExecutionOrder(-9570)]
    public sealed class FTWagerDirector : MonoBehaviour
    {
        private FTSaveGateway saveGateway;
        private FTEventBus eventBus;

        private void Awake()
        {
            FTServices.Register(this);
            saveGateway = FTServices.Get<FTSaveGateway>();
            eventBus = FTServices.Get<FTEventBus>();
        }

        public bool TryReserveWager(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (saveGateway.Profile.bankMoney < amount)
            {
                Debug.LogWarning($"[SacredCore] Wager denied. bank={saveGateway.Profile.bankMoney}, requested={amount}");
                return false;
            }

            saveGateway.Profile.session.wagerExposure += amount;
            eventBus.Raise(new FTWagerChangedSignal(saveGateway.Profile.session.wagerExposure));
            saveGateway.Save();
            Debug.Log($"[SacredCore] Wager reserved: {amount}. exposure={saveGateway.Profile.session.wagerExposure}");
            return true;
        }

        public void ClearWagerExposure(string reason)
        {
            saveGateway.Profile.session.wagerExposure = 0;
            eventBus.Raise(new FTWagerChangedSignal(0));
            saveGateway.Save();
            Debug.Log($"[SacredCore] Wager exposure cleared. reason={reason}");
        }
    }
}
