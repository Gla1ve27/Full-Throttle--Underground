using TMPro;
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
        [SerializeField] private RepairSystem repairSystem;
        [SerializeField] private UpgradePurchaseAction engineUpgradeAction;
        [SerializeField] private GarageShowroomController showroomController;
        [SerializeField] private VehicleDynamicsController displayedVehicle;
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

            if (repairSystem == null)
            {
                repairSystem = FindFirstObjectByType<RepairSystem>();
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
                displayedVehicle = FindFirstObjectByType<VehicleDynamicsController>();
            }

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
            VehicleDynamicsController activeVehicle = showroomController != null && showroomController.CurrentVehicle != null
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
            garageManager?.ExitGarageToWorld();
        }

        public void RepairCar()
        {
            bool repaired = repairSystem != null && repairSystem.Repair();
            SetStatus(repaired ? "Car repaired." : "Repair unavailable.");
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
            bool changed = showroomController != null && showroomController.SelectPreviousCar();
            if (changed)
            {
                SetStatus($"Selected {showroomController.CurrentCarDisplayName}.");
            }

            Refresh();
        }

        public void SelectNextCar()
        {
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
            BindButton(continueButton, ExitGarage);
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

        private void HandleVehicleChanged(VehicleDynamicsController newVehicle)
        {
            displayedVehicle = newVehicle;
            Refresh();
        }

        private string ResolveCurrentCarId()
        {
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
    }
}
