using UnityEngine;

namespace Underground.Race
{
    public class RaceStartTrigger : MonoBehaviour
    {
        public static RaceStartTrigger ActivePrompt { get; private set; }

        [SerializeField] private RaceManager raceManager;

        private bool playerInside;

        private void Awake()
        {
            if (raceManager == null)
            {
                raceManager = GetComponent<RaceManager>();
            }
        }

        private void OnEnable()
        {
            RaceManager.RaceStarted += HandleRaceStateChanged;
            RaceManager.RaceEnded += HandleRaceStateChanged;
            RefreshPromptState();
        }

        private void OnDisable()
        {
            RaceManager.RaceStarted -= HandleRaceStateChanged;
            RaceManager.RaceEnded -= HandleRaceStateChanged;

            if (ActivePrompt == this)
            {
                ActivePrompt = null;
            }
        }

        private void Update()
        {
            if (!playerInside || raceManager == null || !raceManager.CanStartRace())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E))
            {
                if (raceManager.TryStartRace())
                {
                    RefreshPromptState();
                }
            }
        }

        public string GetPromptText()
        {
            return raceManager != null ? raceManager.GetStartPrompt() : string.Empty;
        }

        public bool IsPromptVisible()
        {
            return playerInside
                && raceManager != null
                && !raceManager.IsRaceActive
                && (RaceManager.ActiveRace == null || RaceManager.ActiveRace == raceManager);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            playerInside = true;
            RefreshPromptState();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            playerInside = false;
            RefreshPromptState();
        }

        private void HandleRaceStateChanged(RaceManager manager)
        {
            RefreshPromptState();
        }

        private void RefreshPromptState()
        {
            if (IsPromptVisible())
            {
                ActivePrompt = this;
                return;
            }

            if (ActivePrompt == this)
            {
                ActivePrompt = null;
            }
        }
    }
}
