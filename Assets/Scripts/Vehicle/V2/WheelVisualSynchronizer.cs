using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Synchronizes wheel mesh transforms with WheelCollider poses.
    /// Also handles anti-roll bar stabilization.
    /// </summary>
    public sealed class WheelVisualSynchronizer : MonoBehaviour
    {
        public void SyncVisuals(WheelSet[] wheels)
        {
            if (wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null || wheels[i].collider == null || wheels[i].mesh == null)
                {
                    continue;
                }

                wheels[i].collider.GetWorldPose(out Vector3 worldPosition, out Quaternion worldRotation);
                wheels[i].mesh.position = worldPosition;
                wheels[i].mesh.rotation = worldRotation;
            }
        }

        public void ApplyAntiRoll(Rigidbody body, WheelSet[] wheels, float antiRollForce)
        {
            if (body == null || wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet leftWheel = wheels[i];
                if (leftWheel == null || leftWheel.collider == null || !leftWheel.leftSide)
                {
                    continue;
                }

                WheelSet rightWheel = FindAxlePartner(wheels, leftWheel.axleId, false);
                if (rightWheel == null || rightWheel.collider == null)
                {
                    continue;
                }

                float leftTravel = 1f;
                float rightTravel = 1f;

                bool leftGrounded = leftWheel.collider.GetGroundHit(out WheelHit leftHit);
                bool rightGrounded = rightWheel.collider.GetGroundHit(out WheelHit rightHit);

                if (leftGrounded)
                {
                    leftTravel = CalculateSuspensionTravel(leftWheel.collider, leftHit);
                }

                if (rightGrounded)
                {
                    rightTravel = CalculateSuspensionTravel(rightWheel.collider, rightHit);
                }

                float antiRoll = (leftTravel - rightTravel) * antiRollForce;

                if (leftGrounded)
                {
                    body.AddForceAtPosition(leftWheel.collider.transform.up * -antiRoll, leftWheel.collider.transform.position);
                }

                if (rightGrounded)
                {
                    body.AddForceAtPosition(rightWheel.collider.transform.up * antiRoll, rightWheel.collider.transform.position);
                }
            }
        }

        private static WheelSet FindAxlePartner(WheelSet[] wheels, string axleId, bool leftSide)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null && wheels[i].axleId == axleId && wheels[i].leftSide == leftSide)
                {
                    return wheels[i];
                }
            }

            return null;
        }

        private static float CalculateSuspensionTravel(WheelCollider wheel, WheelHit hit)
        {
            float suspensionDistance = Mathf.Max(0.001f, wheel.suspensionDistance);
            float travel = (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / suspensionDistance;
            return Mathf.Clamp01(travel);
        }
    }
}
