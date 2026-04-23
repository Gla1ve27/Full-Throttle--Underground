using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    /// <summary>
    /// Defensive physics sanity pass to stop launch-at-spawn behavior.
    /// Attach this to the spawned player car root or prefab.
    /// </summary>
    public sealed class FTVehiclePhysicsGuard : MonoBehaviour
    {
        [SerializeField] private WheelCollider[] wheelColliders;
        [SerializeField] private float minWheelRadius = 0.24f;
        [SerializeField] private float maxWheelRadius = 0.60f;
        [SerializeField] private bool zeroVelocityOnEnable = true;
        [SerializeField] private float minimumBodyMass = 950f;
        [SerializeField] private float linearDamping = 0.04f;
        [SerializeField] private float angularDamping = 0.14f;
        [SerializeField] private float maxAngularVelocity = 8f;

        private void OnEnable()
        {
            SanitizeWheelRadii();
            SanitizeBody();

            if (zeroVelocityOnEnable && TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        public void SanitizeWheelRadii()
        {
            if (wheelColliders == null || wheelColliders.Length == 0)
            {
                wheelColliders = GetComponentsInChildren<WheelCollider>(true);
            }

            foreach (WheelCollider wheel in wheelColliders)
            {
                if (wheel == null) continue;
                float clamped = Mathf.Clamp(wheel.radius, minWheelRadius, maxWheelRadius);
                if (!Mathf.Approximately(clamped, wheel.radius))
                {
                    Debug.LogWarning($"[SacredCore] Clamped wheel radius on {wheel.name} from {wheel.radius:F3} to {clamped:F3}");
                    wheel.radius = clamped;
                }
            }
        }

        public void SanitizeBody()
        {
            if (!TryGetComponent(out Rigidbody body))
            {
                return;
            }

            if (body.mass < minimumBodyMass)
            {
                Debug.LogWarning($"[SacredCore] Raised vehicle mass on {name} from {body.mass:F1} to {minimumBodyMass:F1} to prevent spawn launch.");
                body.mass = minimumBodyMass;
            }

            body.linearDamping = Mathf.Max(body.linearDamping, linearDamping);
            body.angularDamping = Mathf.Clamp(angularDamping, 0.04f, 0.16f);
            body.maxAngularVelocity = Mathf.Min(body.maxAngularVelocity <= 0f ? maxAngularVelocity : body.maxAngularVelocity, maxAngularVelocity);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }
}
