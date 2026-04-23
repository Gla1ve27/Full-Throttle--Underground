using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Core.Architecture;
using Underground.Save;
using Underground.Session;
using Underground.TimeSystem;
using Underground.Vehicle;

namespace Underground.Garage
{
    public class GarageManager : MonoBehaviour
    {
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private PersistentProgressManager persistentProgress;
        [SerializeField] private GarageShowroomController showroomController;
        [SerializeField] private TimeOfDay packageTimeOfDay;
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

            if (packageTimeOfDay == null)
            {
                packageTimeOfDay = PackageTimeOfDayUtility.FindPackageTimeOfDay();
            }

            if (showroomController == null)
            {
                showroomController = FindFirstObjectByType<GarageShowroomController>();
            }
        }

        private void Start()
        {
            AutoSaveOnGarageEntry();
        }

        public void SaveAndBankProgress()
        {
            string syncedCarId = SyncSelectedCarFromShowroom();
            if (!string.IsNullOrEmpty(syncedCarId))
            {
                VehicleSceneSelectionBridge.SetPendingCarId(syncedCarId);
            }

            float currentTime = packageTimeOfDay != null
                ? PackageTimeOfDayUtility.GetHours(packageTimeOfDay)
                : (persistentProgress != null ? persistentProgress.WorldTimeOfDay : PackageTimeOfDayUtility.DefaultDuskNightHour);
            sessionManager?.BankSession(currentTime);
            persistentProgress?.SaveNow(currentTime);
        }

        public void ExitGarageToWorld()
        {
            string syncedCarId = SyncSelectedCarFromShowroom();
            if (!string.IsNullOrEmpty(syncedCarId))
            {
                VehicleSceneSelectionBridge.SetPendingCarId(syncedCarId);
            }

            float currentTime = packageTimeOfDay != null
                ? PackageTimeOfDayUtility.GetHours(packageTimeOfDay)
                : (persistentProgress != null ? persistentProgress.WorldTimeOfDay : PackageTimeOfDayUtility.DefaultDuskNightHour);

            persistentProgress?.SaveNow(currentTime, worldSceneName);
            sessionManager?.BeginSession();
            SceneManager.LoadScene(worldSceneName);
        }


        private string SyncSelectedCarFromShowroom()
        {
            if (showroomController == null)
            {
                showroomController = FindFirstObjectByType<GarageShowroomController>();
            }

            if (showroomController == null || persistentProgress == null)
            {
                return string.Empty;
            }

            string displayedCarId = PlayerCarCatalog.MigrateCarId(showroomController.CurrentCarId);
            if (string.IsNullOrEmpty(displayedCarId) || !persistentProgress.OwnsCar(displayedCarId))
            {
                return string.Empty;
            }

            if (!string.Equals(displayedCarId, persistentProgress.CurrentOwnedCarId, System.StringComparison.OrdinalIgnoreCase))
            {
                persistentProgress.SetCurrentCar(displayedCarId);
                Debug.Log($"[GarageManager] Synced showroom selection to save before scene transition: {displayedCarId}");
            }
            else
            {
                Debug.Log($"[GarageManager] Showroom selection already matches progress: {displayedCarId}");
            }

            return displayedCarId;
        }

        private void AutoSaveOnGarageEntry()
        {
            if (autoSavedOnGarageEntry)
            {
                return;
            }

            autoSavedOnGarageEntry = true;
            SaveAndBankProgress();
        }
    }
}
