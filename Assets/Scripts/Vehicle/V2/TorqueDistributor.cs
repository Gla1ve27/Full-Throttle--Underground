using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Distributes drive torque to wheels based on drivetrain type (FWD/RWD/AWD).
    /// Includes pseudo-LSD behavior for stable launches and predictable drift exits.
    /// </summary>
    public sealed class TorqueDistributor : MonoBehaviour
    {
        [Header("LSD")]
        [SerializeField, Range(0f, 1f)] private float lsdLockFactor = 0.45f;

        [Header("AWD Split")]
        [SerializeField, Range(0f, 1f)] private float awdFrontBias = 0.35f;

        /// <summary>
        /// Applies motor torque to all driven wheels, distributing based on drivetrain layout.
        /// </summary>
        public void DistributeTorque(
            WheelSet[] wheels,
            float totalTorque,
            DrivetrainType drivetrain,
            VehicleState state)
        {
            if (wheels == null || wheels.Length == 0)
            {
                return;
            }

            switch (drivetrain)
            {
                case DrivetrainType.FWD:
                    ApplyToAxle(wheels, totalTorque, isFront: true);
                    break;

                case DrivetrainType.RWD:
                    ApplyToAxle(wheels, totalTorque, isFront: false);
                    break;

                case DrivetrainType.AWD:
                    float frontTorque = totalTorque * awdFrontBias;
                    float rearTorque = totalTorque * (1f - awdFrontBias);
                    ApplyToAxle(wheels, frontTorque, isFront: true);
                    ApplyToAxle(wheels, rearTorque, isFront: false);
                    break;
            }
        }

        private void ApplyToAxle(WheelSet[] wheels, float axleTorque, bool isFront)
        {
            WheelCollider left = null;
            WheelCollider right = null;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelSet w = wheels[i];
                if (w == null || w.collider == null || !w.drive)
                {
                    continue;
                }

                bool wheelIsFront = w.steer || w.axleId == "Front";
                if (wheelIsFront != isFront)
                {
                    continue;
                }

                if (w.leftSide)
                {
                    left = w.collider;
                }
                else
                {
                    right = w.collider;
                }
            }

            if (left == null && right == null)
            {
                // No driven wheels on this axle — distribute to all driven wheels as fallback
                int drivenCount = 0;
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null && wheels[i].collider != null && wheels[i].drive)
                    {
                        drivenCount++;
                    }
                }

                if (drivenCount == 0)
                {
                    return;
                }

                float perWheel = axleTorque / drivenCount;
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null && wheels[i].collider != null && wheels[i].drive)
                    {
                        wheels[i].collider.motorTorque = perWheel;
                    }
                }

                return;
            }

            // Pseudo-LSD: transfer torque from the faster-spinning wheel to the slower one
            if (left != null && right != null)
            {
                float leftRpm = Mathf.Abs(left.rpm);
                float rightRpm = Mathf.Abs(right.rpm);
                float maxRpm = Mathf.Max(leftRpm, rightRpm, 0.01f);
                float rpmDifference = Mathf.Abs(leftRpm - rightRpm) / maxRpm;
                float lockAmount = rpmDifference * lsdLockFactor;

                float halfTorque = axleTorque * 0.5f;
                float leftBias = leftRpm < rightRpm ? (1f + lockAmount) : (1f - lockAmount);
                float rightBias = rightRpm < leftRpm ? (1f + lockAmount) : (1f - lockAmount);

                left.motorTorque = halfTorque * leftBias;
                right.motorTorque = halfTorque * rightBias;
            }
            else if (left != null)
            {
                left.motorTorque = axleTorque;
            }
            else
            {
                right.motorTorque = axleTorque;
            }
        }
    }
}
