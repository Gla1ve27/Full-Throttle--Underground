using FullThrottle.SacredCore.Career;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using UnityEngine;

namespace FullThrottle.SacredCore.Progression
{
    /// <summary>
    /// Coordinates profile-level unlock checks after money, reputation, race, and rival events.
    /// </summary>
    [DefaultExecutionOrder(-9500)]
    public sealed class FTProgressionDirector : MonoBehaviour
    {
        private FTSaveGateway saveGateway;
        private FTCareerDirector careerDirector;
        private FTEventBus eventBus;

        private void Awake()
        {
            FTServices.Register(this);
            saveGateway = FTServices.Get<FTSaveGateway>();
            FTServices.TryGet(out careerDirector);
            FTServices.TryGet(out eventBus);

            if (eventBus != null)
            {
                eventBus.Subscribe<FTRepChangedSignal>(OnRepChanged);
                eventBus.Subscribe<FTRaceResolvedSignal>(OnRaceResolved);
            }

            Debug.Log("[SacredCore] Progression director online.");
        }

        private void OnDestroy()
        {
            if (eventBus != null)
            {
                eventBus.Unsubscribe<FTRepChangedSignal>(OnRepChanged);
                eventBus.Unsubscribe<FTRaceResolvedSignal>(OnRaceResolved);
            }
        }

        public void RefreshProgression()
        {
            careerDirector?.TryAdvanceAct();
            saveGateway.Save();
            Debug.Log($"[SacredCore] Progression checked. act={saveGateway.Profile.currentActId}, rep={saveGateway.Profile.reputation}, heat={saveGateway.Profile.heat}.");
        }

        private void OnRepChanged(FTRepChangedSignal signal)
        {
            RefreshProgression();
        }

        private void OnRaceResolved(FTRaceResolvedSignal signal)
        {
            RefreshProgression();
        }
    }
}
