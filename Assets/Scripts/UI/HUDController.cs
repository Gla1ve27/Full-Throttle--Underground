using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Underground.Save;
using Underground.Session;
using Underground.TimeSystem;
using Underground.Vehicle;

namespace Underground.UI
{
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private GearboxSystem gearbox;
        [SerializeField] private SessionManager session;
        [SerializeField] private PersistentProgressManager progress;
        [SerializeField] private VehicleDamageSystem damageSystem;
        [SerializeField] private RiskSystem riskSystem;
        [SerializeField] private DayNightCycleController timeSystem;
        [SerializeField] private GameSettingsManager settingsManager;

        [Header("Text")]
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text gearText;
        [SerializeField] private TMP_Text bankMoneyText;
        [SerializeField] private TMP_Text sessionMoneyText;
        [SerializeField] private TMP_Text reputationText;
        [SerializeField] private TMP_Text riskText;
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text dayNightText;
        [SerializeField] private TMP_Text nextLevelText;
        [SerializeField] private TMP_Text challengeText;
        [SerializeField] private Image speedGaugeFill;
        [SerializeField] private Image speedGaugeGlowFill;
        [SerializeField] private Image riskGaugeFill;

        private Canvas attachedCanvas;

        private void Awake()
        {
            if (vehicle == null) vehicle = FindFirstObjectByType<VehicleDynamicsController>();
            if (gearbox == null) gearbox = FindFirstObjectByType<GearboxSystem>();
            if (session == null) session = FindFirstObjectByType<SessionManager>();
            if (progress == null) progress = FindFirstObjectByType<PersistentProgressManager>();
            if (damageSystem == null) damageSystem = FindFirstObjectByType<VehicleDamageSystem>();
            if (riskSystem == null) riskSystem = FindFirstObjectByType<RiskSystem>();
            if (timeSystem == null) timeSystem = FindFirstObjectByType<DayNightCycleController>();
            if (settingsManager == null) settingsManager = FindFirstObjectByType<GameSettingsManager>();

            attachedCanvas = GetComponent<Canvas>();
        }

        public void BindView(
            TMP_Text speedValue,
            TMP_Text gearValue,
            TMP_Text bankValue,
            TMP_Text sessionValue,
            TMP_Text reputationValue,
            TMP_Text riskValue,
            TMP_Text damageValue,
            TMP_Text dayNightValue,
            TMP_Text nextLevelValue,
            TMP_Text challengeValue,
            Image speedGaugeValue,
            Image speedGlowValue,
            Image riskGaugeValue)
        {
            speedText = speedValue;
            gearText = gearValue;
            bankMoneyText = bankValue;
            sessionMoneyText = sessionValue;
            reputationText = reputationValue;
            riskText = riskValue;
            damageText = damageValue;
            dayNightText = dayNightValue;
            nextLevelText = nextLevelValue;
            challengeText = challengeValue;
            speedGaugeFill = speedGaugeValue;
            speedGaugeGlowFill = speedGlowValue;
            riskGaugeFill = riskGaugeValue;
        }

        private void OnEnable()
        {
            if (settingsManager != null)
            {
                settingsManager.SettingsChanged += ApplyVisibility;
            }

            ApplyVisibility();
        }

        private void OnDisable()
        {
            if (settingsManager != null)
            {
                settingsManager.SettingsChanged -= ApplyVisibility;
            }
        }

        private void Update()
        {
            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
                if (settingsManager != null)
                {
                    settingsManager.SettingsChanged += ApplyVisibility;
                    ApplyVisibility();
                }
            }

            int speedKph = vehicle != null ? Mathf.RoundToInt(Mathf.Abs(vehicle.ForwardSpeedKph)) : 0;
            float normalizedSpeed = 0f;
            if (vehicle != null && vehicle.RuntimeStats != null)
            {
                normalizedSpeed = Mathf.Clamp01(vehicle.SpeedKph / Mathf.Max(1f, vehicle.RuntimeStats.MaxSpeedKph));
            }

            if (speedText != null)
            {
                speedText.text = speedKph.ToString();
            }

            if (gearText != null && gearbox != null)
            {
                gearText.text = vehicle != null && vehicle.IsReversing ? "R" : gearbox.CurrentGear.ToString();
            }

            if (progress != null)
            {
                int currentLevel = GetReputationLevel(progress.SavedReputation);
                int nextLevel = currentLevel + 1;
                int nextLevelThreshold = GetReputationThreshold(nextLevel);
                int repToNext = Mathf.Max(0, nextLevelThreshold - progress.SavedReputation);

                if (reputationText != null)
                {
                    reputationText.text = currentLevel.ToString();
                }

                if (bankMoneyText != null)
                {
                    bankMoneyText.text = $"BANK\n{progress.SavedMoney:N0}";
                }

                if (nextLevelText != null)
                {
                    nextLevelText.text = $"NEXT {nextLevel}\n{repToNext:N0} REP";
                }
            }

            if (sessionMoneyText != null && session != null)
            {
                sessionMoneyText.text = $"SESSION ${session.SessionMoney:N0}";
            }

            if (riskSystem != null)
            {
                int riskTier = Mathf.Clamp(Mathf.CeilToInt(riskSystem.CurrentRisk), 0, 5);
                if (riskText != null)
                {
                    riskText.text = riskTier.ToString();
                }

                if (challengeText != null)
                {
                    challengeText.text = riskTier > 0 ? "POLICE HEAT" : "RACER CHALLENGES";
                }

                if (riskGaugeFill != null)
                {
                    riskGaugeFill.fillAmount = Mathf.Lerp(0.08f, 0.92f, Mathf.InverseLerp(0f, 5f, riskSystem.CurrentRisk));
                }
            }

            if (damageSystem != null && damageText != null)
            {
                damageText.text = $"DMG {Mathf.RoundToInt(damageSystem.DamageNormalized * 100f)}%";
            }

            if (timeSystem != null && dayNightText != null)
            {
                dayNightText.text = timeSystem.IsNight ? "NIGHT" : "DAY";
            }

            if (speedGaugeFill != null)
            {
                speedGaugeFill.fillAmount = Mathf.Lerp(0.1f, 0.86f, normalizedSpeed);
                speedGaugeFill.color = Color.Lerp(new Color(0.24f, 0.62f, 1f, 0.88f), new Color(1f, 0.38f, 0.46f, 0.98f), normalizedSpeed);
            }

            if (speedGaugeGlowFill != null)
            {
                speedGaugeGlowFill.fillAmount = Mathf.Lerp(0.14f, 0.92f, normalizedSpeed);
            }
        }

        private void ApplyVisibility()
        {
            if (attachedCanvas == null)
            {
                attachedCanvas = GetComponent<Canvas>();
            }

            if (attachedCanvas != null && settingsManager != null)
            {
                attachedCanvas.enabled = settingsManager.ShowHud;
            }
        }

        private static int GetReputationLevel(int totalReputation)
        {
            return Mathf.Max(1, (totalReputation / 500) + 1);
        }

        private static int GetReputationThreshold(int level)
        {
            return Mathf.Max(1, level) * 500;
        }
    }
}
