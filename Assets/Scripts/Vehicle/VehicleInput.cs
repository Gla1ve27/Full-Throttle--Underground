using UnityEngine;

namespace Underground.Vehicle
{
    public class VehicleInput : MonoBehaviour
    {
        [Header("Axes")]
        public string steeringAxis = "Horizontal";
        public string throttleAxis = "Vertical";

        [Header("Keys")]
        public KeyCode handbrakeKey = KeyCode.Space;
        public KeyCode resetKey = KeyCode.R;

        [Header("Response")]
        [Range(1f, 20f)] public float steeringResponsiveness = 10f;
        [Range(1f, 20f)] public float pedalResponsiveness = 12f;

        public float Steering { get; private set; }
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Handbrake { get; private set; }

        private bool resetRequested;

        private void Update()
        {
            float rawSteering = Input.GetAxisRaw(steeringAxis);
            float rawPedal = Input.GetAxisRaw(throttleAxis);

            float targetThrottle = Mathf.Clamp01(rawPedal);
            float targetBrake = Mathf.Clamp01(-rawPedal);
            float targetHandbrake = Input.GetKey(handbrakeKey) ? 1f : 0f;

            Steering = Mathf.MoveTowards(Steering, rawSteering, steeringResponsiveness * Time.unscaledDeltaTime);
            Throttle = Mathf.MoveTowards(Throttle, targetThrottle, pedalResponsiveness * Time.unscaledDeltaTime);
            Brake = Mathf.MoveTowards(Brake, targetBrake, pedalResponsiveness * Time.unscaledDeltaTime);
            Handbrake = Mathf.MoveTowards(Handbrake, targetHandbrake, pedalResponsiveness * Time.unscaledDeltaTime);

            if (Input.GetKeyDown(resetKey))
            {
                resetRequested = true;
            }
        }

        public bool ConsumeResetRequest()
        {
            if (!resetRequested)
            {
                return false;
            }

            resetRequested = false;
            return true;
        }
    }
}
