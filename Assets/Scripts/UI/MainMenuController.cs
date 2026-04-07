using UnityEngine;
using UnityEngine.SceneManagement;
using Underground.Save;

namespace Underground.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private PersistentProgressManager progressManager;
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private string garageSceneName = "Garage";

        private void Awake()
        {
            if (progressManager == null)
            {
                progressManager = FindFirstObjectByType<PersistentProgressManager>();
            }

            if (saveSystem == null)
            {
                saveSystem = FindFirstObjectByType<SaveSystem>();
            }
        }

        public void ContinueGame()
        {
            SceneManager.LoadScene(garageSceneName);
        }

        public void StartNewGame()
        {
            saveSystem?.DeleteSave();
            progressManager?.ResetToDefaults();
            progressManager?.SaveNow();
            SceneManager.LoadScene(garageSceneName);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
