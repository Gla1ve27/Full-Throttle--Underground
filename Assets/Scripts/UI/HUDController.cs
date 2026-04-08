using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Underground.Core.Architecture;
using Underground.Save;
using Underground.Session;
using Underground.TimeSystem;
using Underground.Race;
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
        [SerializeField] private global::Speedometer speedometer;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text gearText;
        [SerializeField] private TMP_Text bankMoneyText;
        [SerializeField] private TMP_Text sessionMoneyText;
        [SerializeField] private TMP_Text reputationText;
        [SerializeField] private TMP_Text riskText;
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text dayNightText;
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text racePromptText;
        [SerializeField] private TMP_Text raceObjectiveText;
        [SerializeField] private TMP_Text nextLevelText;
        [SerializeField] private TMP_Text challengeText;
        [SerializeField] private Image speedGaugeFill;
        [SerializeField] private Image speedGaugeGlowFill;
        [SerializeField] private Image riskGaugeFill;
        [SerializeField] private RectTransform riskClusterRoot;
        [SerializeField] private RectTransform racePromptRoot;

        private Canvas attachedCanvas;

        private void Awake()
        {
            ResolveVehicleReferences();
            if (session == null) session = ServiceResolver.Resolve<ISessionService>(null) as SessionManager ?? FindFirstObjectByType<SessionManager>();
            if (progress == null) progress = ServiceResolver.Resolve<IProgressService>(null) as PersistentProgressManager ?? FindFirstObjectByType<PersistentProgressManager>();
            if (riskSystem == null) riskSystem = ServiceResolver.Resolve<IRiskService>(null) as RiskSystem ?? FindFirstObjectByType<RiskSystem>();
            if (timeSystem == null) timeSystem = ServiceResolver.Resolve<ITimeOfDayService>(null) as DayNightCycleController ?? FindFirstObjectByType<DayNightCycleController>();
            if (settingsManager == null) settingsManager = FindFirstObjectByType<GameSettingsManager>();

            attachedCanvas = GetComponent<Canvas>();
            ResolveViewReferences();
        }

        public void BindView(
            global::Speedometer speedometerValue,
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
            speedometer = speedometerValue;
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

        public void RefreshViewBindings()
        {
            ResolveViewReferences();
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

            if (speedometer == null || clockText == null || riskClusterRoot == null || racePromptRoot == null)
            {
                ResolveViewReferences();
            }

            if (vehicle == null || vehicle.Rigidbody == null || !vehicle.CompareTag("Player"))
            {
                ResolveVehicleReferences();
            }

            int speedKph = vehicle != null ? Mathf.RoundToInt(Mathf.Abs(vehicle.ForwardSpeedKph)) : 0;
            float normalizedSpeed = 0f;
            if (vehicle != null && vehicle.RuntimeStats != null)
            {
                normalizedSpeed = Mathf.Clamp01(vehicle.SpeedKph / Mathf.Max(1f, vehicle.RuntimeStats.MaxSpeedKph));
            }

            bool isNight = timeSystem != null && timeSystem.IsNight;

            if (speedometer != null)
            {
                if (vehicle != null && speedometer.target != vehicle.Rigidbody)
                {
                    speedometer.target = vehicle.Rigidbody;
                }

                if (vehicle != null && vehicle.RuntimeStats != null)
                {
                    speedometer.maxSpeed = Mathf.Max(1f, vehicle.RuntimeStats.MaxSpeedKph);
                }
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
                    challengeText.text = isNight ? "POLICE HEAT" : "RACER CHALLENGES";
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

            if (riskClusterRoot != null)
            {
                riskClusterRoot.gameObject.SetActive(isNight);
            }

            if (clockText != null)
            {
                float worldTime = timeSystem != null
                    ? timeSystem.TimeOfDay
                    : (progress != null ? progress.WorldTimeOfDay : 12f);
                clockText.text = FormatGameClock(worldTime);
            }

            UpdateRacePrompt();

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

        private void ResolveVehicleReferences()
        {
            VehicleDynamicsController playerVehicle = FindPlayerVehicle();
            if (playerVehicle != null)
            {
                vehicle = playerVehicle;
            }
            else if (vehicle == null)
            {
                vehicle = FindFirstObjectByType<VehicleDynamicsController>();
            }

            if (vehicle != null)
            {
                gearbox = vehicle.GetComponent<GearboxSystem>() ?? gearbox;
                damageSystem = vehicle.GetComponent<VehicleDamageSystem>() ?? damageSystem;
            }
            else
            {
                gearbox ??= FindFirstObjectByType<GearboxSystem>();
                damageSystem ??= FindFirstObjectByType<VehicleDamageSystem>();
            }
        }

        private void ResolveViewReferences()
        {
            speedometer ??= GetComponentInChildren<global::Speedometer>(true);
            gearText ??= FindText("GearValue");
            bankMoneyText ??= FindText("LevelDetail");
            sessionMoneyText ??= FindText("SessionMoney");
            reputationText ??= FindText("LevelValue");
            riskText ??= FindText("RiskValue");
            damageText ??= FindText("DamageValue");
            dayNightText ??= FindText("ChallengeState");
            clockText ??= FindText("ClockValue");
            racePromptText ??= FindText("RacePromptValue");
            raceObjectiveText ??= FindText("RaceObjectiveValue");
            nextLevelText ??= FindText("NextLevel");
            challengeText ??= FindText("ChallengeTitle");
            riskGaugeFill ??= FindImage("RiskFill");

            if (riskClusterRoot == null)
            {
                Transform riskCluster = FindDescendant(transform, "RiskCluster");
                riskClusterRoot = riskCluster as RectTransform;
            }

            if (racePromptRoot == null)
            {
                Transform promptRoot = FindDescendant(transform, "RacePromptRoot");
                racePromptRoot = promptRoot as RectTransform;
            }
        }

        private TMP_Text FindText(string objectName)
        {
            Transform target = FindDescendant(transform, objectName);
            return target != null ? target.GetComponent<TMP_Text>() : null;
        }

        private Image FindImage(string objectName)
        {
            Transform target = FindDescendant(transform, objectName);
            return target != null ? target.GetComponent<Image>() : null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static VehicleDynamicsController FindPlayerVehicle()
        {
            VehicleDynamicsController[] vehicles = FindObjectsByType<VehicleDynamicsController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] != null && vehicles[i].CompareTag("Player"))
                {
                    return vehicles[i];
                }
            }

            return null;
        }

        private static string FormatGameClock(float timeOfDay)
        {
            float normalized = Mathf.Repeat(timeOfDay, 24f);
            int totalMinutes = Mathf.RoundToInt(normalized * 60f) % (24 * 60);
            int hours24 = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            int hours12 = hours24 % 12;
            if (hours12 == 0)
            {
                hours12 = 12;
            }

            string suffix = hours24 >= 12 ? "PM" : "AM";
            return $"{hours12:00}:{minutes:00} {suffix}";
        }

        private void UpdateRacePrompt()
        {
            if (racePromptRoot == null)
            {
                return;
            }

            RaceManager activeRace = RaceManager.ActiveRace;
            RaceStartTrigger activePrompt = RaceStartTrigger.ActivePrompt;

            bool showObjective = activeRace != null && activeRace.IsRaceActive;
            bool showPrompt = !showObjective && activePrompt != null && activePrompt.IsPromptVisible();
            racePromptRoot.gameObject.SetActive(showObjective || showPrompt);

            if (racePromptText != null)
            {
                racePromptText.text = showPrompt
                    ? activePrompt.GetPromptText()
                    : (showObjective ? activeRace.DisplayName : string.Empty);
            }

            if (raceObjectiveText != null)
            {
                raceObjectiveText.text = showObjective
                    ? activeRace.ActiveObjectiveText
                    : (showPrompt ? "Street race marker detected" : string.Empty);
            }
        }
    }
}
