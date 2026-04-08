using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Underground.UI
{
    public class MenuInputHandler : MonoBehaviour
    {
        [SerializeField] private MainMenuFlowManager flowManager;
        [SerializeField] private MainMenuNewGraphicsMenuController graphicsMenuController;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private float navigationRepeatDelay = 0.18f;

        private float nextNavigationTime;

        private void Awake()
        {
            eventSystem ??= EventSystem.current;

            if (flowManager == null)
            {
                flowManager = GetComponent<MainMenuFlowManager>();
            }

            if (graphicsMenuController == null)
            {
                graphicsMenuController = GetComponent<MainMenuNewGraphicsMenuController>();
            }
        }

        private void Update()
        {
            if (flowManager == null)
            {
                return;
            }

            if (graphicsMenuController == null)
            {
                graphicsMenuController = GetComponent<MainMenuNewGraphicsMenuController>();
            }

            if (flowManager.currentState == MenuState.Splash)
            {
                if (Input.anyKeyDown)
                {
                    flowManager.AdvanceFromSplash();
                }

                return;
            }

            if (graphicsMenuController != null && graphicsMenuController.IsOpen)
            {
                EnsureSelection();

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    graphicsMenuController.StepBackOrClose();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                {
                    SubmitCurrentSelection();
                    return;
                }

                if (Time.unscaledTime < nextNavigationTime)
                {
                    return;
                }

                MoveDirection overlayDirection = ReadMoveDirection();
                if (overlayDirection != MoveDirection.None)
                {
                    ExecuteMove(overlayDirection);
                    nextNavigationTime = Time.unscaledTime + navigationRepeatDelay;
                }

                return;
            }

            EnsureSelection();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                flowManager.Back();
                return;
            }

            if (flowManager.currentState == MenuState.MainMenu)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                SubmitCurrentSelection();
                return;
            }

            if (Time.unscaledTime < nextNavigationTime)
            {
                return;
            }

            MoveDirection direction = ReadMoveDirection();
            if (direction == MoveDirection.None)
            {
                return;
            }

            ExecuteMove(direction);
            nextNavigationTime = Time.unscaledTime + navigationRepeatDelay;
        }

        private void EnsureSelection()
        {
            if (eventSystem == null)
            {
                eventSystem = EventSystem.current;
            }

            if (eventSystem == null || eventSystem.currentSelectedGameObject != null)
            {
                return;
            }

            if (graphicsMenuController != null && graphicsMenuController.IsOpen)
            {
                Selectable graphicsDefault = graphicsMenuController.DefaultSelectable;
                if (graphicsDefault != null)
                {
                    eventSystem.SetSelectedGameObject(graphicsDefault.gameObject);
                }

                return;
            }

            Selectable defaultSelectable = flowManager.GetDefaultSelectableForCurrentState();
            if (defaultSelectable != null)
            {
                eventSystem.SetSelectedGameObject(defaultSelectable.gameObject);
            }
        }

        private void SubmitCurrentSelection()
        {
            if (eventSystem?.currentSelectedGameObject == null)
            {
                return;
            }

            BaseEventData data = new BaseEventData(eventSystem);
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.submitHandler);
        }

        private void ExecuteMove(MoveDirection direction)
        {
            if (eventSystem?.currentSelectedGameObject == null)
            {
                return;
            }

            AxisEventData data = new AxisEventData(eventSystem)
            {
                moveDir = direction
            };

            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.moveHandler);
        }

        private static MoveDirection ReadMoveDirection()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                return MoveDirection.Up;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                return MoveDirection.Down;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                return MoveDirection.Left;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                return MoveDirection.Right;
            }

            return MoveDirection.None;
        }
    }
}
