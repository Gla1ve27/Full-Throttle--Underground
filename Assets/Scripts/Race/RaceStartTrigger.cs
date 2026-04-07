using System.Collections;
using UnityEngine;

namespace Underground.Race
{
    public class RaceStartTrigger : MonoBehaviour
    {
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private float simulatedRaceDuration = 5f;
        [SerializeField] private bool autoWinForPrototype = true;

        private bool playerInside;
        private bool raceRunning;

        private void Awake()
        {
            if (raceManager == null)
            {
                raceManager = GetComponent<RaceManager>();
            }
        }

        private void Update()
        {
            if (!playerInside || raceRunning || raceManager == null || !raceManager.CanStartRace())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                StartCoroutine(RunPrototypeRace());
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                playerInside = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                playerInside = false;
            }
        }

        private IEnumerator RunPrototypeRace()
        {
            raceRunning = true;
            yield return new WaitForSeconds(simulatedRaceDuration);
            raceManager.CompleteRace(autoWinForPrototype);
            raceRunning = false;
        }
    }
}
