using UnityEngine;

namespace Underground.Vehicle.V2
{
    /// <summary>
    /// Bridges the existing <see cref="InputReader"/> to the V2 <see cref="DriverCommand"/> struct.
    /// Snaps a per-frame command that physics systems consume immutably.
    /// </summary>
    public sealed class VehicleInputAdapter : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;

        public DriverCommand CurrentCommand { get; private set; }

        private void Awake()
        {
            if (inputReader == null)
            {
                inputReader = GetComponent<InputReader>();
            }
        }

        /// <summary>
        /// Must be called before FixedUpdate physics systems run.
        /// Snapshots the current input state into an immutable command.
        /// </summary>
        public void CaptureCommand()
        {
            if (inputReader == null)
            {
                CurrentCommand = DriverCommand.None;
                return;
            }

            CurrentCommand = new DriverCommand(
                throttle: inputReader.Throttle,
                brake: inputReader.Brake,
                steering: inputReader.Steering,
                handbrake: inputReader.Handbrake,
                reverseHeld: inputReader.ReverseHeld,
                upshiftRequested: inputReader.ConsumeUpshiftRequest(),
                downshiftRequested: inputReader.ConsumeDownshiftRequest()
            );
        }

        public void SetInputReader(InputReader reader)
        {
            inputReader = reader;
        }
    }
}
