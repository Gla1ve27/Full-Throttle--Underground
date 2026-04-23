using FullThrottle.SacredCore.Race;
using FullThrottle.SacredCore.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace FullThrottle.SacredCore.HUD
{
    public sealed class FTRaceStatusDisplay : MonoBehaviour
    {
        [SerializeField] private Text statusText;

        private FTRaceDirector raceDirector;
        private FTEventBus eventBus;

        private void Awake()
        {
            if (statusText == null) statusText = GetComponent<Text>();
            FTServices.TryGet(out raceDirector);
            FTServices.TryGet(out eventBus);
            eventBus?.Subscribe<FTRaceResolvedSignal>(OnRaceResolved);
            Refresh();
        }

        private void OnDestroy()
        {
            eventBus?.Unsubscribe<FTRaceResolvedSignal>(OnRaceResolved);
        }

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (statusText == null)
            {
                return;
            }

            if (raceDirector != null && raceDirector.RaceLive && raceDirector.ActiveRace != null)
            {
                statusText.text = raceDirector.ActiveRace.displayName;
            }
            else
            {
                statusText.text = "";
            }
        }

        private void OnRaceResolved(FTRaceResolvedSignal signal)
        {
            if (statusText != null)
            {
                statusText.text = signal.Won ? "WON" : "LOST";
            }
        }
    }
}
