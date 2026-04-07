using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.EventSystems;
using Underground.Audio;
using Underground.Vehicle;

namespace Underground.Garage
{
    public class GarageShowroomController : MonoBehaviour
    {
        [SerializeField] private Transform displayRoot;
        [SerializeField] private VehicleDynamicsController vehicle;
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] private InputReader vehicleInput;
        [SerializeField] private CarRespawn respawn;
        [SerializeField] private float autoRotateSpeed = 0f;
        [SerializeField] private float manualRotationStep = 24f;
        [SerializeField] private bool allowMouseRotation = true;
        [SerializeField] private float mouseDragSensitivity = 5.5f;
        [SerializeField] private float rotationSmoothing = 10f;
        [SerializeField] private float initialYaw = -34f;
        [SerializeField] private float showroomBodyDrop = -0.16f;

        private Transform modelRoot;
        private Vector3 modelRootBaseLocalPosition;
        private float targetYaw;

        private void Awake()
        {
            if (displayRoot == null)
            {
                displayRoot = transform;
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
            LockVehicle();
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
            displayRoot.localRotation = Quaternion.Slerp(displayRoot.localRotation, desiredRotation, 1f - Mathf.Exp(-rotationSmoothing * Time.unscaledDeltaTime));
        }

        public void RotateLeft()
        {
            targetYaw += manualRotationStep;
        }

        public void RotateRight()
        {
            targetYaw -= manualRotationStep;
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

            modelRoot.localPosition = modelRootBaseLocalPosition + new Vector3(0f, showroomBodyDrop, 0f);
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
