using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Arcade wall-glance behavior. No dead-stop wall punishment —
    /// redirects velocity along the wall surface and preserves speed.
    /// </summary>
    public sealed class VehicleWallResponseSystem : MonoBehaviour
    {
        [SerializeField] private float wallGlanceSpeedRetention = 0.90f;
        [SerializeField] private float wallBounceImpulse = 2.25f;
        [SerializeField] private float minimumGlanceSpeedKph = 18f;

        private Rigidbody body;
        private VehicleState state;

        public void Initialize(Rigidbody rb, VehicleState vehicleState)
        {
            body = rb;
            state = vehicleState;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (body == null || state == null || collision == null || state.SpeedKph < minimumGlanceSpeedKph)
            {
                return;
            }

            Vector3 wallNormal = Vector3.zero;
            int normalCount = 0;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.normal.y > 0.45f)
                {
                    continue;
                }

                Vector3 planarNormal = Vector3.ProjectOnPlane(contact.normal, Vector3.up);
                if (planarNormal.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                wallNormal += planarNormal.normalized;
                normalCount++;
            }

            if (normalCount == 0)
            {
                return;
            }

            wallNormal.Normalize();
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float intoWallSpeed = -Vector3.Dot(planarVelocity, wallNormal);

            if (intoWallSpeed <= 0.2f)
            {
                return;
            }

            Vector3 tangentVelocity = planarVelocity - Vector3.Project(planarVelocity, wallNormal);
            float retainedSpeed = planarVelocity.magnitude * Mathf.Clamp01(wallGlanceSpeedRetention);

            if (tangentVelocity.sqrMagnitude > 0.01f)
            {
                tangentVelocity = tangentVelocity.normalized * Mathf.Max(tangentVelocity.magnitude, retainedSpeed * 0.65f);
            }
            else
            {
                tangentVelocity = Vector3.Reflect(planarVelocity, wallNormal) * 0.35f;
            }

            body.linearVelocity = tangentVelocity + Vector3.Project(body.linearVelocity, Vector3.up);
            body.AddForce(wallNormal * wallBounceImpulse, ForceMode.VelocityChange);
        }
    }
}
