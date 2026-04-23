using FullThrottle.SacredCore.Vehicle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Underground.Vehicle.V2;

namespace Underground.UI
{
    public sealed class TachometerHudDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FTVehicleTelemetry ftTelemetry;
        [SerializeField] private VehicleControllerV2 vehicleV2;
        [SerializeField] private TMP_Text rpmText;
        [SerializeField] private TMP_Text unitText;
        [SerializeField] private TMP_Text gearText;
        [SerializeField] private TMP_Text absText;
        [SerializeField] private TMP_Text tcrText;
        [SerializeField] private TMP_Text stmText;
        [SerializeField] private Image[] rpmSegments;

        [Header("Tuning")]
        [SerializeField] private float idleRPM = 900f;
        [SerializeField] private float redlineRPM = 7200f;
        [SerializeField] private float displaySmoothing = 18f;
        [SerializeField] private int redlineSegmentStart = 38;

        [Header("Colors")]
        [SerializeField] private Color inactiveColor = new Color(0.16f, 0.17f, 0.18f, 0.85f);
        [SerializeField] private Color activeColor = new Color(0.86f, 0.88f, 0.9f, 0.96f);
        [SerializeField] private Color redlineColor = new Color(1f, 0.12f, 0.28f, 0.98f);
        [SerializeField] private Color gearColor = new Color(0.18f, 1f, 0.45f, 0.98f);
        [SerializeField] private Color dimTextColor = new Color(0.45f, 0.48f, 0.53f, 0.82f);
        [SerializeField] private Color assistActiveColor = new Color(0.18f, 1f, 0.45f, 1f);

        private float displayedRPM;
        private float displayedSpeedKph;

        public void Bind(
            TMP_Text rpmValue,
            TMP_Text unitValue,
            TMP_Text gearValue,
            TMP_Text absValue,
            TMP_Text tcrValue,
            TMP_Text stmValue,
            Image[] segments)
        {
            rpmText = rpmValue;
            unitText = unitValue;
            gearText = gearValue;
            absText = absValue;
            tcrText = tcrValue;
            stmText = stmValue;
            rpmSegments = segments;
            ApplyStaticStyle();
        }

        private void Awake()
        {
            ResolveVehicle();
            ApplyStaticStyle();
        }

        private void Update()
        {
            ResolveVehicle();

            float rpm = GetRPM(out float rpm01);
            float speedKph = GetSpeedKph();
            displayedRPM = Mathf.Lerp(displayedRPM <= 1f ? rpm : displayedRPM, rpm, 1f - Mathf.Exp(-displaySmoothing * Time.deltaTime));
            displayedSpeedKph = Mathf.Lerp(displayedSpeedKph, speedKph, 1f - Mathf.Exp(-displaySmoothing * Time.deltaTime));
            rpm01 = Mathf.Clamp01(Mathf.InverseLerp(idleRPM, redlineRPM, displayedRPM));

            if (rpmText != null)
            {
                rpmText.text = Mathf.RoundToInt(displayedSpeedKph).ToString("000");
            }

            if (gearText != null)
            {
                gearText.text = GetGearText();
            }

            UpdateAssistIndicators();
            UpdateSegments(rpm01);
        }

        private void ResolveVehicle()
        {
            if (ftTelemetry == null)
            {
                FTVehicleController controller = FindFirstObjectByType<FTVehicleController>();
                ftTelemetry = controller != null ? controller.Telemetry : null;
            }

            if (vehicleV2 == null || !vehicleV2.IsInitialized)
            {
                VehicleControllerV2[] controllers = FindObjectsByType<VehicleControllerV2>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < controllers.Length; i++)
                {
                    if (controllers[i] != null && controllers[i].CompareTag("Player") && controllers[i].IsInitialized)
                    {
                        vehicleV2 = controllers[i];
                        break;
                    }
                }
            }
        }

        private float GetRPM(out float rpm01)
        {
            if (ftTelemetry != null)
            {
                rpm01 = ftTelemetry.NormalizedRPM;
                return Mathf.Max(idleRPM, ftTelemetry.EngineRPM);
            }

            if (vehicleV2 != null && vehicleV2.IsInitialized && vehicleV2.State != null)
            {
                if (vehicleV2.RuntimeStats != null)
                {
                    idleRPM = Mathf.Max(1f, vehicleV2.RuntimeStats.IdleRPM);
                    redlineRPM = Mathf.Max(idleRPM + 1f, vehicleV2.RuntimeStats.MaxRPM);
                }

                rpm01 = vehicleV2.State.NormalizedRPM;
                return Mathf.Max(idleRPM, vehicleV2.State.EngineRPM);
            }

            rpm01 = 0f;
            return idleRPM;
        }

        private float GetSpeedKph()
        {
            if (ftTelemetry != null)
            {
                return Mathf.Abs(ftTelemetry.ForwardSpeedKph);
            }

            if (vehicleV2 != null && vehicleV2.IsInitialized && vehicleV2.State != null)
            {
                return Mathf.Abs(vehicleV2.State.ForwardSpeedKph);
            }

            return 0f;
        }

        private string GetGearText()
        {
            if (ftTelemetry != null)
            {
                return ftTelemetry.Gear < 0 ? "R" : Mathf.Max(1, ftTelemetry.Gear).ToString();
            }

            if (vehicleV2 != null && vehicleV2.IsInitialized && vehicleV2.State != null)
            {
                return vehicleV2.State.IsReversing ? "R" : Mathf.Max(1, vehicleV2.State.Gear).ToString();
            }

            return "1";
        }

        private void UpdateSegments(float rpm01)
        {
            if (rpmSegments == null || rpmSegments.Length == 0)
            {
                return;
            }

            int activeCount = Mathf.Clamp(Mathf.RoundToInt(rpm01 * rpmSegments.Length), 0, rpmSegments.Length);
            int redStart = Mathf.Clamp(redlineSegmentStart, 0, rpmSegments.Length - 1);
            for (int i = 0; i < rpmSegments.Length; i++)
            {
                Image segment = rpmSegments[i];
                if (segment == null)
                {
                    continue;
                }

                bool active = i < activeCount;
                bool red = i >= redStart;
                segment.color = active ? (red ? redlineColor : activeColor) : inactiveColor;
            }
        }

        private void ApplyStaticStyle()
        {
            if (unitText != null)
            {
                unitText.text = "km/h";
                unitText.color = dimTextColor;
            }

            if (gearText != null)
            {
                gearText.color = gearColor;
            }

            if (absText != null)
            {
                absText.text = "ABS";
                absText.color = dimTextColor;
            }

            if (tcrText != null)
            {
                tcrText.text = "TCR";
                tcrText.color = dimTextColor;
            }

            if (stmText != null)
            {
                stmText.text = "STM";
                stmText.color = dimTextColor;
            }
        }

        private void UpdateAssistIndicators()
        {
            if (ftTelemetry == null)
            {
                return;
            }

            if (absText != null)
            {
                absText.color = ftTelemetry.AbsActive ? assistActiveColor : dimTextColor;
            }

            if (tcrText != null)
            {
                tcrText.color = ftTelemetry.TractionControlActive ? assistActiveColor : dimTextColor;
            }

            if (stmText != null)
            {
                stmText.color = ftTelemetry.StabilityControlActive ? assistActiveColor : dimTextColor;
            }
        }
    }
}
