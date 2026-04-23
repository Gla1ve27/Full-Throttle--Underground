using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    [DefaultExecutionOrder(-9580)]
    public sealed class FTHeatDirector : MonoBehaviour
    {
        [SerializeField] private int maxHeat = 5;

        private FTSaveGateway saveGateway;
        private FTEventBus eventBus;

        private void Awake()
        {
            FTServices.Register(this);
            saveGateway = FTServices.Get<FTSaveGateway>();
            eventBus = FTServices.Get<FTEventBus>();
        }

        public void AddHeat(int amount, string reason)
        {
            SetHeat(saveGateway.Profile.heat + Mathf.Max(0, amount), reason);
        }

        public void ReduceHeat(int amount, string reason)
        {
            SetHeat(saveGateway.Profile.heat - Mathf.Max(0, amount), reason);
        }

        public void SetHeat(int heat, string reason)
        {
            saveGateway.Profile.heat = Mathf.Clamp(heat, 0, maxHeat);
            eventBus.Raise(new FTHeatChangedSignal(saveGateway.Profile.heat));
            saveGateway.Save();
            Debug.Log($"[SacredCore] Heat={saveGateway.Profile.heat}. reason={reason}");
        }
    }
}
