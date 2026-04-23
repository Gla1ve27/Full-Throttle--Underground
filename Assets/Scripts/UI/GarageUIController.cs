using TMPro;
using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;
using UnityEngine.UI;
using Underground.Garage;
using Underground.Save;
using Underground.Vehicle;

namespace Underground.UI
{
    public class GarageUIController : MonoBehaviour
    {
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private GarageManager garageManager;
        [SerializeField] private FTGarageDirector ftGarageDirector;
        [SerializeField] private FTGarageShowroomDirector ftShowroomDirector;
        [SerializeField] private FTSelectedCarRuntime ftSelectedCarRuntime;
        [SerializeField] private FTCarRegistry ftCarRegistry;
        [SerializeField] private FTSaveGateway ftSaveGateway;
        [SerializeField] private UpgradePurchaseAction engineUpgradeAction;
        [SerializeField] private GarageShowroomController showroomController;
        [SerializeField] private Underground.Vehicle.V2.VehicleControllerV2 displayedVehicle;
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text reputationText;
        [SerializeField] private TMP_Text currentCarText;
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private TMP_Text brandText;
        [SerializeField] private TMP_Text ratingText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image accelerationFill;
        [SerializeField] private Image topSpeedFill;
        [SerializeField] private Image handlingFill;
        [SerializeField] private Button repairButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button rotateLeftButton;
        [SerializeField] private Button rotateRightButton;
        [SerializeField] private bool buttonsBound;

        private string lastResolvedCarId;

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (garageManager == null)
            {
                garageManager = FindFirstObjectByType<GarageManager>();
            }

            if (engineUpgradeAction == null)
            {
                engineUpgradeAction = FindFirstObjectByType<UpgradePurchaseAction>();
            }

            if (showroomController == null)
            {
                showroomController = FindFirstObjectByType<GarageShowroomController>();
            }

            if (displayedVehicle == null && showroomController != null)
            {
                displayedVehicle = showroomController.CurrentVehicle;
            }

            if (displayedVehicle == null)
            {
                displayedVehicle = FindFirstObjectByType<Underground.Vehicle.V2.VehicleControllerV2>();
            }

            ResolveFTReferences();
            ResolveButtons();
            RemoveLegacyBankButton();
            BindButtons();
        }

        private void OnEnable()
        {
            if (showroomController == null)
            {
                showroomController = FindFirstObjectByType<GarageShowroomController>();
            }

            if (showroomController != null)
            {
                showroomController.VehicleChanged += HandleVehicleChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (showroomController != null)
            {
                showroomController.VehicleChanged -= HandleVehicleChanged;
            }
        }

        private void LateUpdate()
        {
            string resolvedCarId = ResolveCurrentCarId();
            if (!string.IsNullOrEmpty(resolvedCarId) && resolvedCarId != lastResolvedCarId)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            if (TryRefreshFT())
            {
                return;
            }

            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (showroomController == null)
            {
                showroomController = FindFirstObjectByType<GarageShowroomController>();
            }

            if (progressManager == null)
            {
                return;
            }

            string resolvedCarId = ResolveCurrentCarId();
            Underground.Vehicle.V2.VehicleControllerV2 activeVehicle = showroomController != null && showroomController.CurrentVehicle != null
                ? showroomController.CurrentVehicle
                : displayedVehicle;
            displayedVehicle = activeVehicle;

            string selectedCarName = !string.IsNullOrEmpty(showroomController != null ? showroomController.CurrentCarDisplayName : null)
                ? showroomController.CurrentCarDisplayName
                : ResolveDisplayName(progressManager.CurrentOwnedCarId);

            if (moneyText != null) moneyText.text = $"Money: {progressManager.SavedMoney}";
            if (reputationText != null) reputationText.text = $"Reputation: {progressManager.SavedReputation}";
            if (currentCarText != null) currentCarText.text = $"Current Car: {selectedCarName}";

            if (displayNameText != null)
            {
                displayNameText.text = selectedCarName.ToUpperInvariant();
            }

            if (brandText != null)
            {
                brandText.text = "UNDERGROUND GARAGE";
            }

            if (activeVehicle != null && activeVehicle.BaseStats != null)
            {
                PlayerCarAppearanceController appearanceController = activeVehicle.GetComponent<PlayerCarAppearanceController>();
                string displayName = appearanceController != null && !string.IsNullOrEmpty(appearanceController.CurrentCarDisplayName)
                    ? appearanceController.CurrentCarDisplayName
                    : selectedCarName;

                if (displayNameText != null) displayNameText.text = displayName.ToUpperInvariant();

                RuntimeVehicleStats stats = activeVehicle.RuntimeStats;
                float accelerationSource = stats != null && stats.MaxMotorTorque > 0f
                    ? stats.MaxMotorTorque
                    : activeVehicle.BaseStats.maxMotorTorque;
                float topSpeedSource = stats != null && stats.MaxSpeedKph > 0f
                    ? stats.MaxSpeedKph
                    : activeVehicle.BaseStats.maxSpeedKph;
                float forwardGrip = stats != null && stats.ForwardStiffness > 0f
                    ? stats.ForwardStiffness
                    : activeVehicle.BaseStats.forwardStiffness;
                float sidewaysGrip = stats != null && stats.SidewaysStiffness > 0f
                    ? stats.SidewaysStiffness
                    : activeVehicle.BaseStats.sidewaysStiffness;

                float acceleration = Mathf.Clamp01(accelerationSource / 2600f);
                float topSpeed = Mathf.Clamp01(topSpeedSource / 320f);
                float handling = Mathf.Clamp01((forwardGrip + sidewaysGrip) / 5.2f);
                float overallRating = ((acceleration + topSpeed + handling) / 3f) * 10f;

                if (accelerationFill != null) accelerationFill.fillAmount = acceleration;
                if (topSpeedFill != null) topSpeedFill.fillAmount = topSpeed;
                if (handlingFill != null) handlingFill.fillAmount = handling;
                if (ratingText != null) ratingText.text = overallRating.ToString("0.00");
            }

            lastResolvedCarId = resolvedCarId;
        }

        public void ExitGarage()
        {
            SetStatus("Leaving garage...");
            ResolveFTReferences();
            if (ftGarageDirector != null)
            {
                ftGarageDirector.ContinueToWorld();
                return;
            }

            garageManager?.ExitGarageToWorld();
        }

        public void RepairCar()
        {
            SetStatus("Repair unavailable.");
            Refresh();
        }

        public void BuyEngineUpgrade()
        {
            bool purchased = engineUpgradeAction != null && engineUpgradeAction.TryPurchaseAssignedUpgrade();
            SetStatus(purchased ? "Engine upgrade installed." : "Upgrade locked or already owned.");
            Refresh();
        }

        public void SelectPreviousCar()
        {
            ResolveFTReferences();
            if (ftGarageDirector != null)
            {
                string carId = ftGarageDirector.BrowsePreviousOwnedCar();
                SetStatus($"Selected {ResolveFTDisplayName(carId)}.");
                Refresh();
                return;
            }

            bool changed = showroomController != null && showroomController.SelectPreviousCar();
            if (changed)
            {
                SetStatus($"Selected {showroomController.CurrentCarDisplayName}.");
            }

            Refresh();
        }

        public void SelectNextCar()
        {
            ResolveFTReferences();
            if (ftGarageDirector != null)
            {
                string carId = ftGarageDirector.BrowseNextOwnedCar();
                SetStatus($"Selected {ResolveFTDisplayName(carId)}.");
                Refresh();
                return;
            }

            bool changed = showroomController != null && showroomController.SelectNextCar();
            if (changed)
            {
                SetStatus($"Selected {showroomController.CurrentCarDisplayName}.");
            }

            Refresh();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void ResolveButtons()
        {
            if (repairButton == null) repairButton = FindButton("Repair Car");
            if (upgradeButton == null) upgradeButton = FindButton("Buy Engine");
            if (continueButton == null) continueButton = FindButton("Continue");
            if (rotateLeftButton == null) rotateLeftButton = FindButton("<");
            if (rotateRightButton == null) rotateRightButton = FindButton(">");
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            BindButton(repairButton, RepairCar);
            BindButton(upgradeButton, BuyEngineUpgrade);
            BindExclusiveButton(continueButton, ExitGarage);
            BindButton(rotateLeftButton, SelectPreviousCar);
            BindButton(rotateRightButton, SelectNextCar);
            buttonsBound = true;
        }

        private void RemoveLegacyBankButton()
        {
            Button legacyBankButton = FindButton("Bank Progress");
            if (legacyBankButton != null)
            {
                Destroy(legacyBankButton.gameObject);
            }
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void BindExclusiveButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private Button FindButton(string objectName)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == objectName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private void HandleVehicleChanged(Underground.Vehicle.V2.VehicleControllerV2 newVehicle)
        {
            displayedVehicle = newVehicle;
            Refresh();
        }

        private string ResolveCurrentCarId()
        {
            ResolveFTReferences();
            if (ftSelectedCarRuntime != null && !string.IsNullOrEmpty(ftSelectedCarRuntime.CurrentCarId))
            {
                return ftSelectedCarRuntime.CurrentCarId;
            }

            if (showroomController != null && !string.IsNullOrEmpty(showroomController.CurrentCarId))
            {
                return showroomController.CurrentCarId;
            }

            return progressManager != null
                ? PlayerCarCatalog.MigrateCarId(progressManager.CurrentOwnedCarId)
                : string.Empty;
        }

        private static string ResolveDisplayName(string carId)
        {
            string resolvedCarId = PlayerCarCatalog.MigrateCarId(carId);
            if (PlayerCarCatalog.TryGetDefinition(resolvedCarId, out PlayerCarDefinition selectedCar))
            {
                return selectedCar.DisplayName;
            }

            return resolvedCarId;
        }

        private bool TryRefreshFT()
        {
            ResolveFTReferences();
            if (ftSelectedCarRuntime == null || ftCarRegistry == null || ftSaveGateway == null)
            {
                return false;
            }

            string carId = ftSelectedCarRuntime.CurrentCarId;
            if (string.IsNullOrEmpty(carId))
            {
                return false;
            }

            FTCarDefinition selectedCar = ftCarRegistry.Get(carId);
            if (selectedCar == null)
            {
                return false;
            }

            if (moneyText != null) moneyText.text = $"Money: {ftSaveGateway.Profile.bankMoney}";
            if (reputationText != null) reputationText.text = $"Reputation: {ftSaveGateway.Profile.reputation}";
            if (currentCarText != null) currentCarText.text = $"Current Car: {selectedCar.displayName}";
            if (displayNameText != null) displayNameText.text = selectedCar.displayName.ToUpperInvariant();
            if (brandText != null) brandText.text = "UNDERGROUND GARAGE";

            if (accelerationFill != null) accelerationFill.fillAmount = Mathf.Clamp01(selectedCar.feel.acceleration / 10f);
            if (topSpeedFill != null) topSpeedFill.fillAmount = Mathf.Clamp01(selectedCar.feel.topSpeed / 10f);
            if (handlingFill != null) handlingFill.fillAmount = Mathf.Clamp01(selectedCar.feel.handling / 10f);
            if (ratingText != null)
            {
                float rating = (selectedCar.feel.acceleration + selectedCar.feel.topSpeed + selectedCar.feel.handling) / 3f;
                ratingText.text = rating.ToString("0.00");
            }

            lastResolvedCarId = carId;
            return true;
        }

        private string ResolveFTDisplayName(string carId)
        {
            ResolveFTReferences();
            FTCarDefinition car = ftCarRegistry != null ? ftCarRegistry.Get(carId) : null;
            return car != null ? car.displayName : carId;
        }

        private void ResolveFTReferences()
        {
            if (ftGarageDirector == null)
            {
                ftGarageDirector = FindFirstObjectByType<FTGarageDirector>();
            }

            if (ftShowroomDirector == null)
            {
                ftShowroomDirector = FindFirstObjectByType<FTGarageShowroomDirector>();
            }

            if (ftSelectedCarRuntime == null && !FTServices.TryGet(out ftSelectedCarRuntime))
            {
                ftSelectedCarRuntime = FindFirstObjectByType<FTSelectedCarRuntime>();
            }

            if (ftCarRegistry == null && !FTServices.TryGet(out ftCarRegistry))
            {
                ftCarRegistry = FindFirstObjectByType<FTCarRegistry>();
            }

            if (ftSaveGateway == null && !FTServices.TryGet(out ftSaveGateway))
            {
                ftSaveGateway = FindFirstObjectByType<FTSaveGateway>();
            }
        }
    }
}
