using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Underground.UI;

namespace Underground.Vehicle
{
    public class InputReader : MonoBehaviour
    {
        [Header("Legacy Axes")]
        [SerializeField] private string steeringAxis = "Horizontal";
        [SerializeField] private string throttleAxis = "Vertical";

        [Header("Keys")]
        [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
        [SerializeField] private KeyCode resetKey = KeyCode.R;
        [SerializeField] private KeyCode reverseKey = KeyCode.S;
        [SerializeField] private KeyCode alternateReverseKey = KeyCode.DownArrow;

        [Header("Response")]
        [SerializeField, Range(1f, 20f)] private float steeringResponsiveness = 10f;
        [SerializeField, Range(1f, 20f)] private float pedalResponsiveness = 12f;
        [SerializeField, Range(0.1f, 0.6f)] private float reverseDoubleTapWindow = 0.3f;

        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steering { get; private set; }
        public bool Handbrake { get; private set; }
        public bool ResetPressed { get; private set; }
        public bool ReverseHeld { get; private set; }

        private bool reverseRequested;
        private float lastReverseTapTime = -1f;
        private GameSettingsManager settingsManager;

        private void Awake()
        {
            settingsManager = FindFirstObjectByType<GameSettingsManager>();
        }

        private void Update()
        {
            if (settingsManager == null)
            {
                settingsManager = FindFirstObjectByType<GameSettingsManager>();
            }

            Vector2 moveInput = ReadMovementInput();
            bool handbrakePressed = ReadHandbrakeInput();
            bool reverseHeld = ReadReverseHeld();
            bool reversePressedThisFrame = ReadReversePressedThisFrame();
            float steeringResponse = steeringResponsiveness * (settingsManager != null ? settingsManager.SteeringSensitivity : 1f);
            float pedalResponse = pedalResponsiveness * (settingsManager != null ? settingsManager.PedalSensitivity : 1f);
            float reverseTapWindow = settingsManager != null ? settingsManager.ReverseDoubleTapWindow : reverseDoubleTapWindow;

            float targetThrottle = Mathf.Clamp01(moveInput.y);
            float targetBrake = Mathf.Clamp01(-moveInput.y);

            Steering = Mathf.MoveTowards(Steering, Mathf.Clamp(moveInput.x, -1f, 1f), steeringResponse * Time.unscaledDeltaTime);
            Throttle = Mathf.MoveTowards(Throttle, targetThrottle, pedalResponse * Time.unscaledDeltaTime);
            Brake = Mathf.MoveTowards(Brake, targetBrake, pedalResponse * Time.unscaledDeltaTime);
            Handbrake = handbrakePressed;
            ReverseHeld = reverseHeld;

            if (reversePressedThisFrame)
            {
                if (lastReverseTapTime >= 0f && Time.unscaledTime - lastReverseTapTime <= reverseTapWindow)
                {
                    reverseRequested = true;
                    lastReverseTapTime = -1f;
                }
                else
                {
                    lastReverseTapTime = Time.unscaledTime;
                }
            }

            if (ReadResetInput())
            {
                ResetPressed = true;
            }
        }

        public void ClearOneShotInputs()
        {
            ResetPressed = false;
        }

        public bool ConsumeReverseRequest()
        {
            bool requested = reverseRequested;
            reverseRequested = false;
            return requested;
        }

        private Vector2 ReadMovementInput()
        {
            float horizontal = Input.GetAxisRaw(steeringAxis);
            float vertical = Input.GetAxisRaw(throttleAxis);

#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null)
            {
                horizontal = Gamepad.current.leftStick.ReadValue().x;
                float rightTrigger = Gamepad.current.rightTrigger.ReadValue();
                float leftTrigger = Gamepad.current.leftTrigger.ReadValue();
                vertical = Mathf.Abs(rightTrigger) > 0.01f || Mathf.Abs(leftTrigger) > 0.01f ? rightTrigger - leftTrigger : vertical;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    horizontal = -1f;
                }
                else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    horizontal = 1f;
                }

                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    vertical = 1f;
                }
                else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                {
                    vertical = -1f;
                }
            }
#endif

            return new Vector2(horizontal, vertical);
        }

        private bool ReadHandbrakeInput()
        {
            bool pressed = Input.GetKey(handbrakeKey);

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pressed |= Keyboard.current.spaceKey.isPressed;
            }

            if (Gamepad.current != null)
            {
                pressed |= Gamepad.current.buttonSouth.isPressed;
            }
#endif

            return pressed;
        }

        private bool ReadReverseHeld()
        {
            bool pressed = Input.GetKey(reverseKey) || Input.GetKey(alternateReverseKey);

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pressed |= Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
            }
#endif

            return pressed;
        }

        private bool ReadReversePressedThisFrame()
        {
            bool pressed = Input.GetKeyDown(reverseKey) || Input.GetKeyDown(alternateReverseKey);

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pressed |= Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame;
            }
#endif

            return pressed;
        }

        private bool ReadResetInput()
        {
            bool pressed = Input.GetKeyDown(resetKey);

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pressed |= Keyboard.current.rKey.wasPressedThisFrame;
            }

            if (Gamepad.current != null)
            {
                pressed |= Gamepad.current.startButton.wasPressedThisFrame;
            }
#endif

            return pressed;
        }
    }
}
