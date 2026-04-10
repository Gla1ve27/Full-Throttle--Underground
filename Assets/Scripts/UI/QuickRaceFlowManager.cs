using UnityEngine;

namespace Underground.UI
{
    public class QuickRaceFlowManager : MonoBehaviour
    {
        [SerializeField] private MainMenuController mainMenuController;
        [SerializeField] private QuickRaceSelectionPanelManager selectionPanelManager;

        public void Initialize(MainMenuController controller)
        {
            mainMenuController = controller;
            ResolveSelectionPanelManager();
        }

        public void SetSelectionPanelManager(QuickRaceSelectionPanelManager manager)
        {
            selectionPanelManager = manager;
        }

        public void EnterQuickRace()
        {
            ResolveSelectionPanelManager();
            if (selectionPanelManager != null)
            {
                selectionPanelManager.OpenSelection();
                return;
            }

            if (mainMenuController == null)
            {
                Debug.LogWarning("QuickRaceFlowManager has no MainMenuController assigned.");
                return;
            }

            mainMenuController.OpenQuickRace();
        }

        private void ResolveSelectionPanelManager()
        {
            selectionPanelManager ??= GetComponent<QuickRaceSelectionPanelManager>();
            selectionPanelManager ??= FindFirstObjectByType<QuickRaceSelectionPanelManager>(FindObjectsInactive.Include);
        }
    }
}
