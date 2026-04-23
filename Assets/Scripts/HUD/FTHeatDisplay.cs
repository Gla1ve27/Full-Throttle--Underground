using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using UnityEngine;
using UnityEngine.UI;

namespace FullThrottle.SacredCore.HUD
{
    public sealed class FTHeatDisplay : MonoBehaviour
    {
        [SerializeField] private Text heatText;

        private FTSaveGateway saveGateway;
        private FTEventBus eventBus;

        private void Awake()
        {
            if (heatText == null) heatText = GetComponent<Text>();
            FTServices.TryGet(out saveGateway);
            FTServices.TryGet(out eventBus);
            eventBus?.Subscribe<FTHeatChangedSignal>(OnHeatChanged);
            Refresh();
        }

        private void OnDestroy()
        {
            eventBus?.Unsubscribe<FTHeatChangedSignal>(OnHeatChanged);
        }

        public void Refresh()
        {
            int heat = saveGateway != null ? saveGateway.Profile.heat : 0;
            if (heatText != null)
            {
                heatText.text = $"HEAT {heat}";
            }
        }

        private void OnHeatChanged(FTHeatChangedSignal signal)
        {
            if (heatText != null)
            {
                heatText.text = $"HEAT {signal.Heat}";
            }
        }
    }
}
