using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Core.Architecture;
using Underground.Save;
using Underground.Session;
using Underground.TimeSystem;

namespace Underground.Garage
{
    public class GarageManager : MonoBehaviour
    {
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private PersistentProgressManager persistentProgress;
        [SerializeField] private DayNightCycleController dayNightCycle;
        [SerializeField] private string worldSceneName = "World";

        private bool autoSavedOnGarageEntry;

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = ServiceResolver.Resolve<ISessionService>(null) as SessionManager
                    ?? FindFirstObjectByType<SessionManager>();
            }

            if (persistentProgress == null)
            {
                persistentProgress = ServiceResolver.Resolve<IProgressService>(null) as PersistentProgressManager
                    ?? FindFirstObjectByType<PersistentProgressManager>();
            }

            if (dayNightCycle == null)
            {
                dayNightCycle = ServiceResolver.Resolve<ITimeOfDayService>(null) as DayNightCycleController
                    ?? FindFirstObjectByType<DayNightCycleController>();
            }
        }

        private void Start()
        {
            AutoSaveOnGarageEntry();
        }

        public void SaveAndBankProgress()
        {
            if (QuickRaceSessionData.IsActive)
            {
                return;
            }

            float currentTime = dayNightCycle != null
                ? dayNightCycle.TimeOfDay
                : (persistentProgress != null ? persistentProgress.WorldTimeOfDay : 12f);
            sessionManager?.BankSession(currentTime);
            persistentProgress?.SaveNow(currentTime);
        }

        public void ExitGarageToWorld()
        {
            float currentTime = dayNightCycle != null
                ? dayNightCycle.TimeOfDay
                : (persistentProgress != null ? persistentProgress.WorldTimeOfDay : 12f);

            persistentProgress?.SaveNow(currentTime, worldSceneName);
            sessionManager?.BeginSession();
            SceneManager.LoadScene(worldSceneName);
        }

        private void AutoSaveOnGarageEntry()
        {
            if (autoSavedOnGarageEntry || QuickRaceSessionData.IsActive)
            {
                return;
            }

            autoSavedOnGarageEntry = true;
            SaveAndBankProgress();
        }
    }
}
