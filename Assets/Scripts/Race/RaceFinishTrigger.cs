using UnityEngine;

namespace Underground.Race
{
    public class RaceFinishTrigger : MonoBehaviour
    {
        [SerializeField] private RaceManager raceManager;

        public void Configure(RaceManager manager)
        {
            raceManager = manager;
        }

        private void OnTriggerEnter(Collider other)
        {
            raceManager?.HandlePlayerReachedFinish(other);
        }
    }
}
