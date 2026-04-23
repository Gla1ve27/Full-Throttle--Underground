using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.HUD
{
    public sealed class FTHUDDirector : MonoBehaviour
    {
        [SerializeField] private FTVehicleTelemetry telemetry;
        [SerializeField] private FTSpeedDisplay speedDisplay;
        [SerializeField] private FTMinimapDirector minimap;

        private void Awake()
        {
            if (speedDisplay == null) speedDisplay = GetComponentInChildren<FTSpeedDisplay>(true);
            if (minimap == null) minimap = GetComponentInChildren<FTMinimapDirector>(true);
        }

        private void Update()
        {
            ResolveTelemetry();
            if (speedDisplay != null) speedDisplay.SetTelemetry(telemetry);
            if (minimap != null && telemetry != null) minimap.SetTarget(telemetry.transform);
        }

        private void ResolveTelemetry()
        {
            if (telemetry != null)
            {
                return;
            }

            FTVehicleController controller = FindFirstObjectByType<FTVehicleController>();
            if (controller != null)
            {
                telemetry = controller.Telemetry;
                Debug.Log($"[SacredCore] HUD bound to telemetry on {controller.name}.");
            }
        }
    }
}
