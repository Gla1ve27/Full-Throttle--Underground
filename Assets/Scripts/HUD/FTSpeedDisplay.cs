using FullThrottle.SacredCore.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace FullThrottle.SacredCore.HUD
{
    public sealed class FTSpeedDisplay : MonoBehaviour
    {
        [SerializeField] private Text speedText;
        [SerializeField] private Text gearText;
        [SerializeField] private FTVehicleTelemetry telemetry;

        private void Awake()
        {
            if (speedText == null) speedText = GetComponent<Text>();
        }

        private void Update()
        {
            if (telemetry == null)
            {
                return;
            }

            if (speedText != null)
            {
                speedText.text = Mathf.RoundToInt(telemetry.SpeedKph).ToString("000");
            }

            if (gearText != null)
            {
                gearText.text = telemetry.Gear < 0 ? "R" : telemetry.Gear.ToString();
            }
        }

        public void SetTelemetry(FTVehicleTelemetry source)
        {
            telemetry = source;
        }
    }
}
