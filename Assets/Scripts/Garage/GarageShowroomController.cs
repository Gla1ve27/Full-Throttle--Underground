using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.EventSystems;
using Underground.Audio;
using Underground.Save;
using Underground.Vehicle;

namespace Underground.Garage
{
    public class GarageShowroomController : MonoBehaviour
    {
        [SerializeField] private Transform displayRoot;
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] private InputReader vehicleInput;
        [SerializeField] private CarRespawn respawn;
        [SerializeField] private float autoRotateSpeed = 0f;
        [SerializeField] private float manualRotationStep = 24f;
        [SerializeField] private bool allowMouseRotation = true;
        [SerializeField] private float mouseDragSensitivity = 5.5f;
        [SerializeField] private float rotationSmoothing = 10f;
        [SerializeField] private float initialYaw = 270f;
        [SerializeField] private float showroomBodyDrop = -0.16f;
        [SerializeField] private float showroomGroundClearance = 0.02f;

        private float activeShowroomBodyDrop;

        public event Action<VehicleDynamicsController> VehicleChanged;

        public VehicleDynamicsController CurrentVehicle => vehicle;
        public string CurrentCarId => appearanceController != null && !string.IsNullOrEmpty(appearanceController.CurrentCarId)
            ? appearanceController.CurrentCarId
            : (progressManager != null ? PlayerCarCatalog.MigrateCarId(progressManager.CurrentOwnedCarId) : string.Empty);
        public string CurrentCarDisplayName => appearanceController != null ? appearanceController.CurrentCarDisplayName : string.Empty;

        private Transform modelRoot;
        private Vector3 modelRootBaseLocalPosition;
        private float targetYaw;
        private PlayerCarAppearanceController appearanceController;

        private void Awake()
        {
            if (displayRoot == null)
            {
                displayRoot = transform;
            }

            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (vehicle == null)
            {
                vehicle = GetComponentInChildren<VehicleDynamicsController>(true);
            }

            if (vehicle != null && vehicleBody == null)
            {
                vehicleBody = vehicle.GetComponent<Rigidbody>();
            }

            if (vehicleInput == null)
            {
                vehicleInput = GetComponentInChildren<InputReader>(true);
            }

            if (respawn == null)
            {
                respawn = GetComponentInChildren<CarRespawn>(true);
            }

            if (vehicle != null)
            {
                modelRoot = vehicle.transform.Find("ModelRoot");
                if (modelRoot != null)
                {
                    modelRootBaseLocalPosition = modelRoot.localPosition;
                }
            }

            targetYaw = initialYaw;
        }

        private void Start()
        {
            InitializeDisplayedVehicle();
            LockVehicle();
            activeShowroomBodyDrop = ResolveCurrentBodyDrop();
            ApplyShowroomRideHeight();
            if (displayRoot != null)
            {
                displayRoot.localRotation = Quaternion.Euler(0f, targetYaw, 0f);
            }
        }

        private void Update()
        {
            if (displayRoot == null)
            {
                return;
            }

            HandleMouseRotation();
            if (!allowMouseRotation && Mathf.Abs(autoRotateSpeed) > 0.0001f)
            {
                targetYaw += autoRotateSpeed * Time.unscaledDeltaTime;
            }

            Quaternion desiredRotation = Quaternion.Euler(0f, targetYaw, 0f);
            displayRoot.localRotation = Quaternion.Slerp(
                displayRoot.localRotation,
                desiredRotation,
                1f - Mathf.Exp(-rotationSmoothing * Time.unscaledDeltaTime));
        }

        public void RotateLeft()
        {
            targetYaw += manualRotationStep;
        }

        public void RotateRight()
        {
            targetYaw -= manualRotationStep;
        }

        public bool SelectPreviousCar()
        {
            return SelectCar(-1);
        }

        public bool SelectNextCar()
        {
            return SelectCar(1);
        }

        private void InitializeDisplayedVehicle()
        {
            if (vehicle == null)
            {
                vehicle = GetComponentInChildren<VehicleDynamicsController>(true);
            }

            if (vehicle == null)
            {
                return;
            }

            appearanceController = vehicle.GetComponent<PlayerCarAppearanceController>();
            if (appearanceController == null)
            {
                appearanceController = vehicle.gameObject.AddComponent<PlayerCarAppearanceController>();
            }

            appearanceController.SetShowroomPresentationMode(true);
            appearanceController.ApplyCurrentSelection();
            RefreshVehicleReferences();
            VehicleChanged?.Invoke(vehicle);
        }

        private bool SelectCar(int direction)
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (progressManager == null)
            {
                Debug.LogWarning("[Showroom] No PersistentProgressManager found.");
                return false;
            }

            if (appearanceController == null && vehicle != null)
            {
                appearanceController = vehicle.GetComponent<PlayerCarAppearanceController>();
            }

            if (appearanceController == null)
            {
                Debug.LogWarning("[Showroom] No PlayerCarAppearanceController found.");
                return false;
            }

            appearanceController.SetShowroomPresentationMode(true);

            var ownedCars = PlayerCarCatalog.GetOwnedCars(progressManager);
            Debug.Log($"[Showroom] CurrentOwnedCarId = {progressManager.CurrentOwnedCarId}");

            foreach (var c in ownedCars)
            {
                Debug.Log($"[Showroom] Available Owned Car: {c.CarId}");
            }

            if (ownedCars.Count == 0)
            {
                Debug.LogWarning("[Showroom] No owned cars available.");
                return false;
            }

            int currentIndex = -1;
            string currentResolvedId = PlayerCarCatalog.MigrateCarId(progressManager.CurrentOwnedCarId);

            for (int i = 0; i < ownedCars.Count; i++)
            {
                if (ownedCars[i].CarId == currentResolvedId)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            // Try each owned car until one applies successfully.
            for (int attempt = 1; attempt <= ownedCars.Count; attempt++)
            {
                int nextIndex = (currentIndex + (direction * attempt) + ownedCars.Count) % ownedCars.Count;
                PlayerCarDefinition selectedCar = ownedCars[nextIndex];

                Debug.Log($"[Showroom] Trying car: {selectedCar.CarId}");

                bool applied = appearanceController.ApplyAppearance(selectedCar.CarId);
                if (!applied)
                {
                    Debug.LogWarning($"[Showroom] Failed to apply car: {selectedCar.CarId}. Skipping.");
                    continue;
                }

                // Save only after successful appearance application.
                progressManager.SetCurrentCar(selectedCar.CarId);
                progressManager.SaveNow(progressManager.WorldTimeOfDay);

                RefreshVehicleReferences();
                activeShowroomBodyDrop = selectedCar.ShowroomBodyDrop;
                ApplyShowroomRideHeight();
                VehicleChanged?.Invoke(vehicle);

                Debug.Log($"[Showroom] Successfully applied car: {selectedCar.CarId}");
                return true;
            }

            Debug.LogWarning("[Showroom] No valid owned car could be displayed.");
            return false;
        }

        private void RefreshVehicleReferences()
        {
            if (vehicle == null)
            {
                return;
            }

            vehicleBody = vehicle.GetComponent<Rigidbody>();
            vehicleInput = vehicle.GetComponent<InputReader>();
            respawn = vehicle.GetComponent<CarRespawn>();
            modelRoot = vehicle.transform.Find("ModelRoot");
            // Intentionally do not recapture modelRootBaseLocalPosition here.
        }

        private void HandleMouseRotation()
        {
            if (!allowMouseRotation || !IsRightMouseHeld() || IsPointerOverUi())
            {
                return;
            }

            float mouseDeltaX = ReadMouseDeltaX();
            if (Mathf.Abs(mouseDeltaX) < 0.0001f)
            {
                return;
            }

            targetYaw += mouseDeltaX * mouseDragSensitivity;
        }

        private void ApplyShowroomRideHeight()
        {
            if (modelRoot == null)
            {
                return;
            }

            Vector3 localPosition = modelRootBaseLocalPosition + new Vector3(0f, activeShowroomBodyDrop, 0f);

            if (ShouldUseBoundsGrounding() && TryGetModelLocalBounds(out Bounds bounds))
            {
                float groundedOffset = showroomGroundClearance - bounds.min.y;
                localPosition.y += groundedOffset;
            }

            modelRoot.localPosition = localPosition;
        }

        private float ResolveCurrentBodyDrop()
        {
            if (progressManager == null)
            {
                return showroomBodyDrop;
            }

            string resolvedId = PlayerCarCatalog.MigrateCarId(progressManager.CurrentOwnedCarId);
            if (PlayerCarCatalog.TryGetDefinition(resolvedId, out PlayerCarDefinition def))
            {
                return def.ShowroomBodyDrop;
            }

            return showroomBodyDrop;
        }

        private bool TryGetModelLocalBounds(out Bounds localBounds)
        {
            localBounds = default;
            if (modelRoot == null)
            {
                return false;
            }

            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;
                Vector3 localMin = modelRoot.InverseTransformPoint(worldBounds.min);
                Vector3 localMax = modelRoot.InverseTransformPoint(worldBounds.max);

                Bounds rendererLocalBounds = new Bounds((localMin + localMax) * 0.5f, localMax - localMin);
                if (!hasBounds)
                {
                    localBounds = rendererLocalBounds;
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(rendererLocalBounds.min);
                    localBounds.Encapsulate(rendererLocalBounds.max);
                }
            }

            return hasBounds;
        }

        private bool ShouldUseBoundsGrounding()
        {
            string resolvedId = CurrentCarId;
            if (!PlayerCarCatalog.TryGetDefinition(resolvedId, out PlayerCarDefinition definition))
            {
                return false;
            }

            return !definition.UseDetachedWheelVisuals;
        }

        private void LockVehicle()
        {
            if (vehicleInput != null)
            {
                vehicleInput.enabled = false;
            }

            if (respawn != null)
            {
                respawn.enabled = false;
            }

            if (vehicleBody != null)
            {
                vehicleBody.linearVelocity = Vector3.zero;
                vehicleBody.angularVelocity = Vector3.zero;
                vehicleBody.isKinematic = true;
                vehicleBody.constraints = RigidbodyConstraints.FreezeAll;
            }

            VehicleAudioController audioController = GetComponentInChildren<VehicleAudioController>(true);
            if (audioController != null)
            {
                audioController.enabled = false;
            }

            AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                audioSources[i].Stop();
                audioSources[i].enabled = false;
            }
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static bool IsRightMouseHeld()
        {
            bool held = Input.GetMouseButton(1);

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                held |= Mouse.current.rightButton.isPressed;
            }
#endif

            return held;
        }

        private static float ReadMouseDeltaX()
        {
            float deltaX = Input.GetAxisRaw("Mouse X");

#if ENABLE_INPUT_SYSTEM
            if (Mathf.Abs(deltaX) < 0.0001f && Mouse.current != null)
            {
                deltaX = Mouse.current.delta.ReadValue().x * 0.02f;
            }
#endif

            return deltaX;
        }
    }
}