using UnityEngine;

namespace Underground.UI
{
    public class QuickRaceFlowManager : MonoBehaviour
    {
        [SerializeField] private MainMenuController mainMenuController;

        public void Initialize(MainMenuController controller)
        {
            mainMenuController = controller;
        }

        public void EnterQuickRace()
        {
            if (mainMenuController == null)
            {
                Debug.LogWarning("QuickRaceFlowManager has no MainMenuController assigned.");
                return;
            }

            mainMenuController.OpenQuickRace();
        }
    }
}
