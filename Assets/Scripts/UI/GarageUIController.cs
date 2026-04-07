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
        [SerializeField] private Button bankButton;
        [SerializeField] private Button repairButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button rotateLeftButton;
        [SerializeField] private Button rotateRightButton;
        [SerializeField] private bool buttonsBound;

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

            if (displayedVehicle == null)
            {
                displayedVehicle = FindFirstObjectByType<VehicleDynamicsController>();
            }

            ResolveButtons();
            BindButtons();
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (progressManager == null)
            {
                return;
            }

            if (moneyText != null) moneyText.text = $"Money: {progressManager.SavedMoney}";
            if (reputationText != null) reputationText.text = $"Reputation: {progressManager.SavedReputation}";
            if (currentCarText != null) currentCarText.text = $"Current Car: {progressManager.CurrentOwnedCarId}";

            if (displayedVehicle != null && displayedVehicle.BaseStats != null)
            {
                if (displayNameText != null) displayNameText.text = displayedVehicle.BaseStats.displayName.ToUpperInvariant();
                if (brandText != null) brandText.text = "UNDERGROUND GARAGE";

                RuntimeVehicleStats stats = displayedVehicle.RuntimeStats;
                float accelerationSource = stats != null && stats.MaxMotorTorque > 0f
                    ? stats.MaxMotorTorque
                    : displayedVehicle.BaseStats.maxMotorTorque;
                float topSpeedSource = stats != null && stats.MaxSpeedKph > 0f
                    ? stats.MaxSpeedKph
                    : displayedVehicle.BaseStats.maxSpeedKph;
                float forwardGrip = stats != null && stats.ForwardStiffness > 0f
                    ? stats.ForwardStiffness
                    : displayedVehicle.BaseStats.forwardStiffness;
                float sidewaysGrip = stats != null && stats.SidewaysStiffness > 0f
                    ? stats.SidewaysStiffness
                    : displayedVehicle.BaseStats.sidewaysStiffness;

                float acceleration = Mathf.Clamp01(accelerationSource / 2600f);
                float topSpeed = Mathf.Clamp01(topSpeedSource / 320f);
                float handling = Mathf.Clamp01((forwardGrip + sidewaysGrip) / 5.2f);
                float overallRating = ((acceleration + topSpeed + handling) / 3f) * 10f;

                if (accelerationFill != null) accelerationFill.fillAmount = acceleration;
                if (topSpeedFill != null) topSpeedFill.fillAmount = topSpeed;
                if (handlingFill != null) handlingFill.fillAmount = handling;
                if (ratingText != null) ratingText.text = overallRating.ToString("0.00");
            }
        }

        public void BankProgress()
        {
            garageManager?.SaveAndBankProgress();
            SetStatus("Progress banked.");
            Refresh();
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

        public void RotateLeft()
        {
            showroomController?.RotateLeft();
        }

        public void RotateRight()
        {
            showroomController?.RotateRight();
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
            if (bankButton == null) bankButton = FindButton("Bank Progress");
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

            BindButton(bankButton, BankProgress);
            BindButton(repairButton, RepairCar);
            BindButton(upgradeButton, BuyEngineUpgrade);
            BindButton(continueButton, ExitGarage);
            BindButton(rotateLeftButton, RotateLeft);
            BindButton(rotateRightButton, RotateRight);
            buttonsBound = true;
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
    }
}
