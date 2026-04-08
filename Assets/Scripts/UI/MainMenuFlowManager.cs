using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Underground.UI
{
    public class MainMenuFlowManager : MonoBehaviour
    {
        public MenuState currentState;

        public GameObject splashPanel;
        public GameObject mainMenuPanel;
        public GameObject careerPanel;
        public GameObject quickRacePanel;
        public GameObject customizePanel;
        public GameObject optionsPanel;

        [SerializeField] private MainMenuController mainMenuController;
        [SerializeField] private QuickRaceFlowManager quickRaceFlowManager;
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField] private Selectable mainMenuDefaultSelectable;
        [SerializeField] private Selectable careerDefaultSelectable;
        [SerializeField] private Selectable quickRaceDefaultSelectable;
        [SerializeField] private Selectable customizeDefaultSelectable;
        [SerializeField] private Selectable optionsDefaultSelectable;

        private Coroutine transitionRoutine;
        private MenuState previousState;
        private bool hasPreviousState;

        public MenuState PreviousState => previousState;

        private void Start()
        {
            GoToState(MenuState.Splash, false, true);
        }

        public void Initialize(MainMenuController controller, QuickRaceFlowManager quickRaceManager)
        {
            mainMenuController = controller;
            quickRaceFlowManager = quickRaceManager;
        }

        public void SetDefaultSelectables(
            Selectable mainMenuDefault,
            Selectable careerDefault,
            Selectable quickRaceDefault,
            Selectable customizeDefault,
            Selectable optionsDefault)
        {
            mainMenuDefaultSelectable = mainMenuDefault;
            careerDefaultSelectable = careerDefault;
            quickRaceDefaultSelectable = quickRaceDefault;
            customizeDefaultSelectable = customizeDefault;
            optionsDefaultSelectable = optionsDefault;
        }

        public void GoToState(MenuState state)
        {
            GoToState(state, true, false);
        }

        public void Back()
        {
            if (currentState == MenuState.Splash)
            {
                AdvanceFromSplash();
                return;
            }

            if (currentState == MenuState.MainMenu)
            {
                return;
            }

            if (hasPreviousState && previousState != currentState)
            {
                GoToState(previousState, false, false);
                return;
            }

            GoToState(MenuState.MainMenu, false, false);
        }

        public void AdvanceFromSplash()
        {
            GoToState(MenuState.MainMenu, false, false);
        }

        public void OpenCareerPanel()
        {
            GoToState(MenuState.Career);
        }

        public void OpenQuickRacePanel()
        {
            GoToState(MenuState.QuickRace);
        }

        public void OpenCustomizePanel()
        {
            GoToState(MenuState.Customize);
        }

        public void OpenOptionsPanel()
        {
            GoToState(MenuState.Options);
        }

        public void ContinueCareer()
        {
            mainMenuController?.ContinueGame();
        }

        public void StartCareerNewGame()
        {
            mainMenuController?.StartNewGame();
        }

        public void LoadCareerGame()
        {
            if (mainMenuController != null)
            {
                mainMenuController.LoadGame();
            }
        }

        public void StartQuickRace()
        {
            if (quickRaceFlowManager != null)
            {
                quickRaceFlowManager.EnterQuickRace();
                return;
            }

            mainMenuController?.OpenQuickRace();
        }

        public void OpenAudioOptions()
        {
            Debug.Log("Audio options selected.");
        }

        public void OpenControlsOptions()
        {
            Debug.Log("Controls options selected.");
        }

        public void OpenGraphicsOptions()
        {
            MainMenuNewGraphicsMenuController graphicsMenu = FindFirstObjectByType<MainMenuNewGraphicsMenuController>(FindObjectsInactive.Include);
            if (graphicsMenu == null)
            {
                graphicsMenu = GetComponent<MainMenuNewGraphicsMenuController>();
            }

            if (graphicsMenu == null)
            {
                graphicsMenu = gameObject.AddComponent<MainMenuNewGraphicsMenuController>();
            }

            graphicsMenu.EnsureInitialized();
            graphicsMenu.OpenMenu();
        }

        public void OpenCustomizeVisualHooks()
        {
            Debug.Log("Customize: visual hooks placeholder.");
        }

        public void OpenCustomizePreview()
        {
            Debug.Log("Customize: car preview placeholder.");
        }

        public Selectable GetDefaultSelectableForCurrentState()
        {
            return GetDefaultSelectableForState(currentState);
        }

        public Selectable GetDefaultSelectableForState(MenuState state)
        {
            return state switch
            {
                MenuState.MainMenu => mainMenuDefaultSelectable,
                MenuState.Career => careerDefaultSelectable,
                MenuState.QuickRace => quickRaceDefaultSelectable,
                MenuState.Customize => customizeDefaultSelectable,
                MenuState.Options => optionsDefaultSelectable,
                _ => null
            };
        }

        private void GoToState(MenuState state, bool rememberPrevious, bool immediate)
        {
            if (rememberPrevious && state != currentState)
            {
                previousState = currentState;
                hasPreviousState = true;
            }

            currentState = state;
            UpdateUI(immediate);
        }

        private void UpdateUI()
        {
            UpdateUI(false);
        }

        private void UpdateUI(bool immediate)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(TransitionToState(currentState, immediate));
        }

        private IEnumerator TransitionToState(MenuState state, bool immediate)
        {
            GameObject[] panels =
            {
                splashPanel,
                mainMenuPanel,
                careerPanel,
                quickRacePanel,
                customizePanel,
                optionsPanel
            };

            GameObject targetPanel = GetPanelForState(state);
            float duration = immediate ? 0f : fadeDuration;

            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                if (panels[i] == targetPanel || panels[i].activeSelf)
                {
                    panels[i].SetActive(true);
                }

                CanvasGroup group = GetCanvasGroup(panels[i]);
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0.0001f ? 1f : Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] == null)
                    {
                        continue;
                    }

                    CanvasGroup group = GetCanvasGroup(panels[i]);
                    float targetAlpha = panels[i] == targetPanel ? 1f : 0f;
                    group.alpha = Mathf.Lerp(group.alpha, targetAlpha, t);
                }

                yield return null;
            }

            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                bool isTarget = panels[i] == targetPanel;
                CanvasGroup group = GetCanvasGroup(panels[i]);
                group.alpha = isTarget ? 1f : 0f;
                group.interactable = isTarget;
                group.blocksRaycasts = isTarget;
                panels[i].SetActive(isTarget);
            }

            yield return null;
            SetDefaultSelectionForState(state);
            transitionRoutine = null;
        }

        private void SetDefaultSelectionForState(MenuState state)
        {
            Selectable selectable = GetDefaultSelectableForState(state);
            if (selectable == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private GameObject GetPanelForState(MenuState state)
        {
            return state switch
            {
                MenuState.Splash => splashPanel,
                MenuState.MainMenu => mainMenuPanel,
                MenuState.Career => careerPanel,
                MenuState.QuickRace => quickRacePanel,
                MenuState.Customize => customizePanel,
                MenuState.Options => optionsPanel,
                _ => mainMenuPanel
            };
        }

        private static CanvasGroup GetCanvasGroup(GameObject panel)
        {
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = panel.AddComponent<CanvasGroup>();
            }

            return group;
        }
    }
}
