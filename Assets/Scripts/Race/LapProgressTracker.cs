using UnityEngine;

namespace Underground.Race
{
    public class LapProgressTracker : MonoBehaviour
    {
        [SerializeField] private int totalCheckpoints = 1;

        public int CurrentCheckpointIndex { get; private set; }
        public int CurrentLap { get; private set; } = 1;

        public bool TryPassCheckpoint(int incomingIndex)
        {
            if (incomingIndex != CurrentCheckpointIndex)
            {
                return false;
            }

            CurrentCheckpointIndex++;

            if (CurrentCheckpointIndex >= totalCheckpoints)
            {
                CurrentCheckpointIndex = 0;
                CurrentLap++;
            }

            return true;
        }
    }
}
