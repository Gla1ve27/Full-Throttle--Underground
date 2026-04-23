using System.Collections;
using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    public interface IFTVehicleDefinitionReceiver
    {
        void ApplyDefinition(FTCarDefinition definition);
    }

    /// <summary>
    /// Light bridge on the spawned player car.
    /// This script is intentionally small so the sacred core can lean on it safely.
    /// </summary>
    public sealed class FTPlayerVehicleBinder : MonoBehaviour
    {
        [SerializeField] private Rigidbody rigidBody;
        [SerializeField] private WheelCollider[] wheelColliders;
        [SerializeField] private MonoBehaviour[] definitionReceivers;
        [SerializeField] private int startupFreezeFrames = 6;
        [SerializeField] private float startupGroundProbe = 220f;
        [SerializeField] private float groundProbeLift = 80f;
        [SerializeField] private float rootGroundOffset = 0.65f;
        [SerializeField] private bool disableControllerDuringSpawn = true;
        [SerializeField] private LayerMask groundMask = ~0;

        private FTCarDefinition definition;
        private FTVehicleController controller;
        private bool controllerWasEnabled;

        public Rigidbody Body => rigidBody;
        public WheelCollider[] WheelColliders => wheelColliders;
        public FTCarDefinition Definition => definition;

        private void Awake()
        {
            RepairSpawnSettings();
            ResolveReferences();
        }

        private void OnValidate()
        {
            RepairSpawnSettings();
        }

        public void ApplyDefinition(FTCarDefinition carDefinition)
        {
            RepairSpawnSettings();
            ResolveReferences();
            definition = carDefinition;
            MonoBehaviour[] receivers = definitionReceivers != null && definitionReceivers.Length > 0
                ? definitionReceivers
                : GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in receivers)
            {
                if (behaviour != this && behaviour is IFTVehicleDefinitionReceiver receiver)
                {
                    receiver.ApplyDefinition(carDefinition);
                }
            }

            Debug.Log($"[SacredCore] Vehicle binder applied definition {carDefinition?.carId ?? "None"} to {name}.");
        }

        public void PrepareForSpawn(Pose pose)
        {
            RepairSpawnSettings();
            ResolveReferences();
            controllerWasEnabled = controller != null && controller.enabled;
            if (disableControllerDuringSpawn && controller != null)
            {
                controller.enabled = false;
            }

            transform.SetPositionAndRotation(pose.position, pose.rotation);

            if (rigidBody != null)
            {
                rigidBody.isKinematic = true;
                rigidBody.linearVelocity = Vector3.zero;
                rigidBody.angularVelocity = Vector3.zero;
            }

            ResetWheelState();
            Physics.SyncTransforms();
            StartCoroutine(FinishSpawnRoutine());
        }

        private IEnumerator FinishSpawnRoutine()
        {
            SnapNearGround();

            for (int i = 0; i < startupFreezeFrames; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            if (rigidBody != null)
            {
                rigidBody.linearVelocity = Vector3.zero;
                rigidBody.angularVelocity = Vector3.zero;
                rigidBody.isKinematic = false;
                rigidBody.WakeUp();
            }

            ResetWheelState();
            if (disableControllerDuringSpawn && controller != null && controllerWasEnabled)
            {
                controller.enabled = true;
            }

            Debug.Log($"[SacredCore] Vehicle spawn settled at {transform.position}.");
        }

        private void RepairSpawnSettings()
        {
            startupFreezeFrames = Mathf.Max(startupFreezeFrames, 6);
            startupGroundProbe = Mathf.Max(startupGroundProbe, 220f);
            groundProbeLift = Mathf.Max(groundProbeLift, 80f);
            rootGroundOffset = Mathf.Max(rootGroundOffset, 0.65f);
            disableControllerDuringSpawn = true;
        }

        private void SnapNearGround()
        {
            float lift = Mathf.Max(1f, groundProbeLift);
            float distance = lift + Mathf.Max(20f, startupGroundProbe);
            Vector3 origin = transform.position + Vector3.up * lift;
            if (!TryFindGround(origin, distance, out RaycastHit hit))
            {
                Debug.LogWarning($"[SacredCore] Vehicle spawn could not find ground below {transform.position}. Keeping authored spawn pose.");
                return;
            }

            Vector3 position = transform.position;
            position.y = hit.point.y + Mathf.Max(0.05f, rootGroundOffset);
            transform.position = position;
            Debug.Log($"[SacredCore] Vehicle spawn grounded. ground={hit.point}, root={transform.position}, collider={hit.collider.name}");
        }

        private bool TryFindGround(Vector3 origin, float distance, out RaycastHit bestHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore);
            bestHit = default;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hits[i].distance < bestDistance)
                {
                    bestDistance = hits[i].distance;
                    bestHit = hits[i];
                }
            }

            return bestDistance < float.MaxValue;
        }

        private void ResetWheelState()
        {
            if (wheelColliders == null)
            {
                return;
            }

            for (int i = 0; i < wheelColliders.Length; i++)
            {
                WheelCollider wheel = wheelColliders[i];
                if (wheel == null)
                {
                    continue;
                }

                wheel.motorTorque = 0f;
                wheel.brakeTorque = 0f;
                wheel.steerAngle = 0f;
            }
        }

        private void ResolveReferences()
        {
            if (rigidBody == null)
            {
                rigidBody = GetComponent<Rigidbody>();
            }

            if (wheelColliders == null || wheelColliders.Length == 0)
            {
                wheelColliders = GetComponentsInChildren<WheelCollider>(true);
            }

            if (controller == null)
            {
                controller = GetComponent<FTVehicleController>();
            }
        }
    }
}
