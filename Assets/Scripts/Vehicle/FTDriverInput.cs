using UnityEngine;

namespace FullThrottle.SacredCore.Vehicle
{
    /// <summary>
    /// Minimal input reader for the sacred vehicle core. It avoids package coupling.
    /// </summary>
    public sealed class FTDriverInput : MonoBehaviour
    {
        [SerializeField] private bool readLegacyInput = true;
        [SerializeField] private KeyCode handbrakeKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode alternateHandbrakeKey = KeyCode.Space;

        public float Steer { get; private set; }
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public bool Handbrake { get; private set; }
        public bool ReverseHeld { get; private set; }
        public bool RespawnPressed { get; private set; }

        private void Update()
        {
            if (!readLegacyInput)
            {
                return;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            Steer = Mathf.Clamp(horizontal, -1f, 1f);
            Throttle = Mathf.Clamp01(vertical);
            Brake = Mathf.Clamp01(-vertical);
            Handbrake = Input.GetKey(handbrakeKey) || Input.GetKey(alternateHandbrakeKey);
            ReverseHeld = vertical < -0.1f;
            RespawnPressed = Input.GetKeyDown(KeyCode.R);
        }

        public void SetManual(float steer, float throttle, float brake, bool handbrake, bool reverseHeld)
        {
            readLegacyInput = false;
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Throttle = Mathf.Clamp01(throttle);
            Brake = Mathf.Clamp01(brake);
            Handbrake = handbrake;
            ReverseHeld = reverseHeld;
        }
    }
}
