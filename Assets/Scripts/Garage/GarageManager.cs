using UnityEngine;
using UnityEngine.SceneManagement;
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

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<SessionManager>();
            }

            if (persistentProgress == null)
            {
                persistentProgress = FindFirstObjectByType<PersistentProgressManager>();
            }
        }

        public void SaveAndBankProgress()
        {
            float currentTime = dayNightCycle != null
                ? dayNightCycle.TimeOfDay
                : (persistentProgress != null ? persistentProgress.WorldTimeOfDay : 12f);
            sessionManager?.BankSession(currentTime);
            persistentProgress?.SaveNow(currentTime);
        }

        public void ExitGarageToWorld()
        {
            sessionManager?.BeginSession();
            SceneManager.LoadScene(worldSceneName);
        }
    }
}
