using FullThrottle.SacredCore.Garage;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Camera
{
    public sealed class FTGarageCameraDirector : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera cameraTarget;
        [SerializeField] private Transform showroomAnchor;
        [SerializeField] private float orbitDegreesPerSecond = 7f;
        [SerializeField] private float positionResponse = 7f;

        private FTSelectedCarRuntime selectedCarRuntime;
        private FTCarRegistry carRegistry;
        private FTEventBus eventBus;
        private Vector3 desiredOffset = new Vector3(0f, 1.3f, -5.4f);
        private float orbit;

        private void Awake()
        {
            if (cameraTarget == null) cameraTarget = GetComponent<UnityEngine.Camera>();
            selectedCarRuntime = FTServices.Get<FTSelectedCarRuntime>();
            carRegistry = FTServices.Get<FTCarRegistry>();
            eventBus = FTServices.Get<FTEventBus>();
            eventBus.Subscribe<FTCarSelectionChangedSignal>(OnSelectionChanged);
            RefreshOffset();
        }

        private void OnDestroy()
        {
            eventBus?.Unsubscribe<FTCarSelectionChangedSignal>(OnSelectionChanged);
        }

        private void LateUpdate()
        {
            Transform anchor = showroomAnchor != null ? showroomAnchor : transform;
            orbit += orbitDegreesPerSecond * Time.deltaTime;
            Quaternion orbitRotation = Quaternion.Euler(0f, orbit, 0f);
            Vector3 desiredPosition = anchor.position + orbitRotation * desiredOffset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-positionResponse * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(anchor.position + Vector3.up * 0.9f - transform.position, Vector3.up);
        }

        private void RefreshOffset()
        {
            FTCarDefinition car = carRegistry.Get(selectedCarRuntime.CurrentCarId);
            if (car != null)
            {
                desiredOffset = car.garageCameraOffset;
            }
        }

        private void OnSelectionChanged(FTCarSelectionChangedSignal signal)
        {
            RefreshOffset();
            Debug.Log($"[SacredCore] Garage camera refreshed for {signal.CarId}.");
        }
    }
}
