using System;
using System.Collections.Generic;
using UnityEngine;

namespace Underground.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleDynamicsController : MonoBehaviour
    {
        [Serializable]
        public class WheelAxle
        {
            public string label = "Axle";
            public WheelCollider leftWheel;
            public WheelCollider rightWheel;
            public Transform leftVisual;
            public Transform rightVisual;
            public bool steering;
            public bool powered;
            public bool handbrake;
        }

        [Header("References")]
        public VehicleInput inputSource;
        public Transform centerOfMassOverride;

        [Header("Axles")]
        public List<WheelAxle> axles = new List<WheelAxle>();

        [Header("Drive")]
        public float maxMotorTorque = 2400f;
        public float maxBrakeTorque = 3600f;
        public float maxHandbrakeTorque = 6000f;
        public float topSpeedKph = 180f;

        [Header("Steering")]
        public float maxSteerAngle = 32f;
        public float steeringSpeedReferenceKph = 160f;
        public AnimationCurve steeringBySpeed = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 0.7f),
            new Keyframe(1f, 0.35f));

        [Header("Stability")]
        public float downforce = 90f;
        public float lateralGripAssist = 2.5f;
        public float antiRollForce = 4500f;
        public float resetLift = 1.2f;

        public Rigidbody Rigidbody { get; private set; }
        public float SpeedKph { get; private set; }
        public float ForwardSpeedKph { get; private set; }
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();

            if (inputSource == null)
            {
                inputSource = GetComponent<VehicleInput>();
            }

            ApplyCenterOfMass();
            EnsureSteeringCurve();
        }

        private void OnValidate()
        {
            EnsureSteeringCurve();
        }

        private void FixedUpdate()
        {
            if (Rigidbody == null || inputSource == null || axles.Count == 0)
            {
                return;
            }

            ApplyCenterOfMass();
            UpdateTelemetry();
            ApplySteering();
            ApplyDrive();
            ApplyBraking();
            ApplyAntiRoll();
            ApplyDownforce();
            ApplyLateralGripAssist();

            if (inputSource.ConsumeResetRequest())
            {
                ResetVehiclePose();
            }
        }

        private void LateUpdate()
        {
            SyncWheelVisuals();
        }

        private void ApplyCenterOfMass()
        {
            if (centerOfMassOverride != null)
            {
                Rigidbody.centerOfMass = transform.InverseTransformPoint(centerOfMassOverride.position);
            }
        }

        private void UpdateTelemetry()
        {
            SpeedKph = Rigidbody.velocity.magnitude * 3.6f;
            ForwardSpeedKph = Vector3.Dot(Rigidbody.velocity, transform.forward) * 3.6f;
            IsGrounded = false;

            for (int i = 0; i < axles.Count; i++)
            {
                WheelAxle axle = axles[i];

                if ((axle.leftWheel != null && axle.leftWheel.isGrounded) ||
                    (axle.rightWheel != null && axle.rightWheel.isGrounded))
                {
                    IsGrounded = true;
                    return;
                }
            }
        }

        private void ApplySteering()
        {
            float speedFactor = Mathf.Clamp01(Mathf.Abs(ForwardSpeedKph) / Mathf.Max(1f, steeringSpeedReferenceKph));
            float steeringMultiplier = steeringBySpeed.Evaluate(speedFactor);
            float steerAngle = inputSource.Steering * maxSteerAngle * steeringMultiplier;

            for (int i = 0; i < axles.Count; i++)
            {
                WheelAxle axle = axles[i];
                if (!axle.steering)
                {
                    continue;
                }

                if (axle.leftWheel != null)
                {
                    axle.leftWheel.steerAngle = steerAngle;
                }

                if (axle.rightWheel != null)
                {
                    axle.rightWheel.steerAngle = steerAngle;
                }
            }
        }

        private void ApplyDrive()
        {
            int poweredWheelCount = 0;

            for (int i = 0; i < axles.Count; i++)
            {
                if (!axles[i].powered)
                {
                    continue;
                }

                if (axles[i].leftWheel != null)
                {
                    poweredWheelCount++;
                }

                if (axles[i].rightWheel != null)
                {
                    poweredWheelCount++;
                }
            }

            if (poweredWheelCount == 0)
            {
                return;
            }

            float topSpeedLimiter = 1f - Mathf.Clamp01(Mathf.InverseLerp(topSpeedKph * 0.85f, topSpeedKph, Mathf.Abs(ForwardSpeedKph)));
            float handbrakeCut = Mathf.Lerp(1f, 0.2f, inputSource.Handbrake);
            float torquePerWheel = (inputSource.Throttle * maxMotorTorque * topSpeedLimiter * handbrakeCut) / poweredWheelCount;

            for (int i = 0; i < axles.Count; i++)
            {
                WheelAxle axle = axles[i];
                if (!axle.powered)
                {
                    if (axle.leftWheel != null)
                    {
                        axle.leftWheel.motorTorque = 0f;
                    }

                    if (axle.rightWheel != null)
                    {
                        axle.rightWheel.motorTorque = 0f;
                    }

                    continue;
                }

                if (axle.leftWheel != null)
                {
                    axle.leftWheel.motorTorque = torquePerWheel;
                }

                if (axle.rightWheel != null)
                {
                    axle.rightWheel.motorTorque = torquePerWheel;
                }
            }
        }

        private void ApplyBraking()
        {
            float footBrakeTorque = inputSource.Brake * maxBrakeTorque;
            float handbrakeTorque = inputSource.Handbrake * maxHandbrakeTorque;

            for (int i = 0; i < axles.Count; i++)
            {
                WheelAxle axle = axles[i];
                float totalBrakeTorque = footBrakeTorque;

                if (axle.handbrake)
                {
                    totalBrakeTorque += handbrakeTorque;
                }

                if (axle.leftWheel != null)
                {
                    axle.leftWheel.brakeTorque = totalBrakeTorque;
                }

                if (axle.rightWheel != null)
                {
                    axle.rightWheel.brakeTorque = totalBrakeTorque;
                }
            }
        }

        private void ApplyAntiRoll()
        {
            for (int i = 0; i < axles.Count; i++)
            {
                WheelAxle axle = axles[i];

                if (axle.leftWheel == null || axle.rightWheel == null)
                {
                    continue;
                }

                float leftTravel = 1f;
                float rightTravel = 1f;

                bool leftGrounded = axle.leftWheel.GetGroundHit(out WheelHit leftHit);
                bool rightGrounded = axle.rightWheel.GetGroundHit(out WheelHit rightHit);

                if (leftGrounded)
                {
                    leftTravel = CalculateSuspensionTravel(axle.leftWheel, leftHit);
                }

                if (rightGrounded)
                {
                    rightTravel = CalculateSuspensionTravel(axle.rightWheel, rightHit);
                }

                float antiRoll = (leftTravel - rightTravel) * antiRollForce;

                if (leftGrounded)
                {
                    Rigidbody.AddForceAtPosition(axle.leftWheel.transform.up * -antiRoll, axle.leftWheel.transform.position);
                }

                if (rightGrounded)
                {
                    Rigidbody.AddForceAtPosition(axle.rightWheel.transform.up * antiRoll, axle.rightWheel.transform.position);
                }
            }
        }

        private void ApplyDownforce()
        {
            if (!IsGrounded)
            {
                return;
            }

            Rigidbody.AddForce(-transform.up * SpeedKph * downforce, ForceMode.Force);
        }

        private void ApplyLateralGripAssist()
        {
            if (!IsGrounded)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(Rigidbody.velocity);
            float handbrakeGripReduction = Mathf.Lerp(1f, 0.35f, inputSource.Handbrake);
            Vector3 correctiveForce = -transform.right * localVelocity.x * lateralGripAssist * handbrakeGripReduction;
            Rigidbody.AddForce(correctiveForce, ForceMode.Acceleration);
        }

        private void ResetVehiclePose()
        {
            Rigidbody.velocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
            Rigidbody.position += Vector3.up * resetLift;
            Rigidbody.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        private void SyncWheelVisuals()
        {
            for (int i = 0; i < axles.Count; i++)
            {
                UpdateWheelPose(axles[i].leftWheel, axles[i].leftVisual);
                UpdateWheelPose(axles[i].rightWheel, axles[i].rightVisual);
            }
        }

        private static float CalculateSuspensionTravel(WheelCollider wheel, WheelHit hit)
        {
            float suspensionDistance = Mathf.Max(0.001f, wheel.suspensionDistance);
            float travel = (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / suspensionDistance;
            return Mathf.Clamp01(travel);
        }

        private static void UpdateWheelPose(WheelCollider wheel, Transform visual)
        {
            if (wheel == null || visual == null)
            {
                return;
            }

            wheel.GetWorldPose(out Vector3 worldPosition, out Quaternion worldRotation);
            visual.position = worldPosition;
            visual.rotation = worldRotation;
        }

        private void EnsureSteeringCurve()
        {
            if (steeringBySpeed == null || steeringBySpeed.length == 0)
            {
                steeringBySpeed = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.5f, 0.7f),
                    new Keyframe(1f, 0.35f));
            }
        }
    }
}
