using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Simple modular downforce and aerodynamic drag.
    /// </summary>
    public sealed class VehicleAeroSystem : MonoBehaviour
    {
        [SerializeField] private float aerodynamicDragCoefficient = 0.36f;

        public void UpdateAero(Rigidbody body, VehicleState state, RuntimeVehicleStats stats)
        {
            if (body == null || state == null || stats == null)
            {
                return;
            }

            ApplyDownforce(body, state, stats);
            ApplyAerodynamicDrag(body, state);
        }

        private void ApplyDownforce(Rigidbody body, VehicleState state, RuntimeVehicleStats stats)
        {
            if (!state.IsGrounded)
            {
                return;
            }

            body.AddForce(-body.transform.up * state.SpeedKph * stats.Downforce, ForceMode.Force);
        }

        private void ApplyAerodynamicDrag(Rigidbody body, VehicleState state)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float speedMs = planarVelocity.magnitude;
            if (speedMs < 0.5f)
            {
                return;
            }

            Vector3 dragForce = -planarVelocity.normalized * speedMs * speedMs * aerodynamicDragCoefficient;
            body.AddForce(dragForce, ForceMode.Force);
        }
    }
}
