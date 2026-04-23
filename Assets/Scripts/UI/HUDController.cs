using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Underground.Core.Architecture;
using Underground.Save;
using Underground.Session;
using Underground.TimeSystem;
using Underground.Race;
using Underground.Vehicle;
using Underground.Vehicle.V2;

namespace Underground.UI
{
    public enum HudInstrumentMode
    {
        DigitalTach,
        AnalogueSpeedometer
    }

    public class HUDController : MonoBehaviour
    {
        [SerializeField] private VehicleControllerV2 vehicleV2;
        [SerializeField] private SessionManager session;
        [SerializeField] private PersistentProgressManager progress;
        [SerializeField] private TimeOfDay packageTimeOfDay;
        [SerializeField] private GameSettingsManager settingsManager;

        [Header("Instrument Mode")]
        [SerializeField] private HudInstrumentMode instrumentMode = HudInstrumentMode.DigitalTach;
        [SerializeField] private RectTransform digitalInstrumentRoot;
        [SerializeField] private RectTransform analogueInstrumentRoot;

        [Header("Text")]
        [SerializeField] private TachometerHudDisplay tachometer;
        [SerializeField] private global::Speedometer speedometer;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text gearText;
        [SerializeField] private TMP_Text bankMoneyText;
        [SerializeField] private TMP_Text sessionMoneyText;
        [SerializeField] private TMP_Text reputationText;
        [SerializeField] private TMP_Text dayNightText;
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text racePromptText;
        [SerializeField] private TMP_Text raceObjectiveText;
        [SerializeField] private TMP_Text nextLevelText;
        [SerializeField] private TMP_Text challengeText;
        [SerializeField] private Image speedGaugeFill;
        [SerializeField] private Image speedGaugeGlowFill;
        [SerializeField] private RectTransform racePromptRoot;

        private Canvas attachedCanvas;

        private void Awake()
        {
            ResolveVehicleReferences();
            if (session == null) session = ServiceResolver.Resolve<ISessionService>(null) as SessionManager ?? FindFirstObjectByType<SessionManager>();
            if (progress == null) progress = ServiceResolver.Resolve<IProgressService>(null) as PersistentProgressManager ?? FindFirstObjectByType<PersistentProgressManager>();
            ResolveTimeSystemReference();
            if (settingsManager == null) settingsManager = FindFirstObjectByType<GameSettingsManager>();

            attachedCanvas = GetComponent<Canvas>();
            ResolveViewReferences();
            ApplyInstrumentMode();
        }

        public void BindView(
            TachometerHudDisplay tachometerValue,
            global::Speedometer speedometerValue,
            RectTransform digitalRootValue,
            RectTransform analogueRootValue,
            TMP_Text speedValue,
            TMP_Text gearValue,
            TMP_Text bankValue,
            TMP_Text sessionValue,
            TMP_Text reputationValue,
            TMP_Text dayNightValue,
            TMP_Text nextLevelValue,
            TMP_Text challengeValue,
            Image speedGaugeValue,
            Image speedGlowValue)
        {
            tachometer = tachometerValue;
            speedometer = speedometerValue;
            digitalInstrumentRoot = digitalRootValue;
            analogueInstrumentRoot = analogueRootValue;
            speedText = speedValue;
            gearText = gearValue;
            bankMoneyText = bankValue;
            sessionMoneyText = sessionValue;
            reputationText = reputationValue;
            dayNightText = dayNightValue;
            nextLevelText = nextLevelValue;
            challengeText = challengeValue;
            speedGaugeFill = speedGaugeValue;
            speedGaugeGlowFill = speedGlowValue;
            ApplyInstrumentMode();
        }

        public void RefreshViewBindings()
        {
            ResolveViewReferences();
            ApplyInstrumentMode();
        }

        public HudInstrumentMode InstrumentMode => instrumentMode;

        public void SetInstrumentMode(HudInstrumentMode mode)
        {
            if (instrumentMode == mode)
            {
                return;
            }

            instrumentMode = mode;
            ApplyInstrumentMode();
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
            ResolveTimeSystemReference();

            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
                if (settingsManager != null)
                {
                    settingsManager.SettingsChanged += ApplyVisibility;
                    ApplyVisibility();
                }
            }

            if (tachometer == null || clockText == null || racePromptRoot == null)
            {
                ResolveViewReferences();
                ApplyInstrumentMode();
            }

            
            bool hasV2 = vehicleV2 != null && vehicleV2.IsInitialized && vehicleV2.enabled; if (!hasV2) { ResolveVehicleReferences(); hasV2 = vehicleV2 != null && vehicleV2.IsInitialized && vehicleV2.enabled; }

            int speedKph;
            float maxSpeedKph;
            if (hasV2)
            {
                speedKph = Mathf.RoundToInt(Mathf.Abs(vehicleV2.State.ForwardSpeedKph));
                maxSpeedKph = vehicleV2.RuntimeStats != null ? Mathf.Max(1f, vehicleV2.RuntimeStats.MaxSpeedKph) : 260f;
            }
            
            else
            {
                speedKph = 0;
                maxSpeedKph = 260f;
            }
            float normalizedSpeed = (float)speedKph / maxSpeedKph;
            bool analogueMode = instrumentMode == HudInstrumentMode.AnalogueSpeedometer;

            if (analogueMode && speedometer != null)
            {
                Rigidbody activeRb = hasV2 ? vehicleV2.Rigidbody : null;
                if (activeRb != null && speedometer.target != activeRb)
                {
                    speedometer.target = activeRb;
                }

                speedometer.SetSpeed(speedKph);
            }

            if (speedText != null)
            {
                speedText.text = analogueMode ? speedKph.ToString() : string.Empty;
            }

            if (gearText != null && analogueMode)
            {
                if (hasV2)
                {
                    gearText.text = vehicleV2.State.IsReversing ? "R" : vehicleV2.State.Gear.ToString();
                }
                
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

            if (dayNightText != null)
            {
                dayNightText.text = PackageTimeOfDayUtility.IsNight(packageTimeOfDay) ? "NIGHT" : "DAY";
            }

            if (clockText != null)
            {
                float worldTime = packageTimeOfDay != null
                    ? PackageTimeOfDayUtility.GetHours(packageTimeOfDay)
                    : (progress != null ? progress.WorldTimeOfDay : PackageTimeOfDayUtility.DefaultDuskNightHour);
                clockText.text = FormatGameClock(worldTime);
            }

            UpdateRacePrompt();

            if (speedGaugeFill != null)
            {
                speedGaugeFill.fillAmount = analogueMode ? Mathf.Lerp(0.1f, 0.86f, normalizedSpeed) : 0f;
                speedGaugeFill.color = Color.Lerp(new Color(0.24f, 0.62f, 1f, 0.88f), new Color(1f, 0.38f, 0.46f, 0.98f), normalizedSpeed);
            }

            if (speedGaugeGlowFill != null)
            {
                speedGaugeGlowFill.fillAmount = analogueMode ? Mathf.Lerp(0.14f, 0.92f, normalizedSpeed) : 0f;
            }
        }

        private void ResolveTimeSystemReference()
        {
            if (packageTimeOfDay == null)
            {
                packageTimeOfDay = PackageTimeOfDayUtility.FindPackageTimeOfDay();
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

        private void ApplyInstrumentMode()
        {
            bool digitalMode = instrumentMode == HudInstrumentMode.DigitalTach;

            if (digitalInstrumentRoot != null)
            {
                digitalInstrumentRoot.gameObject.SetActive(digitalMode);
            }
            else if (tachometer != null)
            {
                tachometer.gameObject.SetActive(digitalMode);
            }

            if (analogueInstrumentRoot != null)
            {
                analogueInstrumentRoot.gameObject.SetActive(!digitalMode);
            }
            else if (speedometer != null)
            {
                speedometer.gameObject.SetActive(!digitalMode);
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
            // Try V2 first
            if (vehicleV2 == null || !vehicleV2.IsInitialized)
            {
                VehicleControllerV2[] v2s = FindObjectsByType<VehicleControllerV2>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < v2s.Length; i++)
                {
                    if (v2s[i] != null && v2s[i].CompareTag("Player") && v2s[i].IsInitialized)
                    {
                        vehicleV2 = v2s[i];
                        break;
                    }
                }
            }
        }

        private void ResolveViewReferences()
        {
            if (digitalInstrumentRoot == null)
            {
                digitalInstrumentRoot = FindDescendant(transform, "DigitalTachRoot") as RectTransform;
            }

            if (analogueInstrumentRoot == null)
            {
                analogueInstrumentRoot = FindDescendant(transform, "AnalogueSpeedometerRoot") as RectTransform;
            }

            tachometer ??= GetComponentInChildren<TachometerHudDisplay>(true);
            speedometer ??= GetComponentInChildren<global::Speedometer>(true);

            gearText ??= FindText("AnalogueGearValue");
            gearText ??= FindText("GearValue");
            bankMoneyText ??= FindText("LevelDetail");
            sessionMoneyText ??= FindText("SessionMoney");
            reputationText ??= FindText("LevelValue");
            dayNightText ??= FindText("ChallengeState");
            clockText ??= FindText("ClockValue");
            racePromptText ??= FindText("RacePromptValue");
            raceObjectiveText ??= FindText("RaceObjectiveValue");
            nextLevelText ??= FindText("NextLevel");
            challengeText ??= FindText("ChallengeTitle");

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

        private static VehicleControllerV2 FindPlayerVehicle()
        {
            VehicleControllerV2[] vehicles = FindObjectsByType<VehicleControllerV2>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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



