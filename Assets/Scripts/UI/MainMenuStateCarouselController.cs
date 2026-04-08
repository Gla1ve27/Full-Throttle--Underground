using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Underground.UI
{
    public class MainMenuStateCarouselController : MonoBehaviour
    {
        [System.Serializable]
        private class CarouselItem
        {
            public RectTransform root;
            public Image background;
            public Outline outline;
            public TMP_Text icon;
            public TMP_Text label;
            public Button button;
            public string displayName;
            public MenuState targetState;
        }

        [SerializeField] private MainMenuFlowManager flowManager;
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private TMP_Text captionLabel;
        [SerializeField] private float inputRepeatDelay = 0.18f;
        [SerializeField] private int defaultSelectedIndex = 1;
        [SerializeField] private CarouselItem[] items = new CarouselItem[0];

        private int selectedIndex;
        private float nextInputTime;

        public Selectable DefaultSelectable =>
            items != null && items.Length > 0 && defaultSelectedIndex >= 0 && defaultSelectedIndex < items.Length
                ? items[defaultSelectedIndex].button
                : null;

        public void Configure(
            MainMenuFlowManager manager,
            Button leftArrow,
            Button rightArrow,
            TMP_Text caption,
            RectTransform careerRoot,
            Image careerBackground,
            Outline careerOutline,
            TMP_Text careerIcon,
            TMP_Text careerLabel,
            Button careerButton,
            RectTransform quickRaceRoot,
            Image quickRaceBackground,
            Outline quickRaceOutline,
            TMP_Text quickRaceIcon,
            TMP_Text quickRaceLabel,
            Button quickRaceButton,
            RectTransform customizeRoot,
            Image customizeBackground,
            Outline customizeOutline,
            TMP_Text customizeIcon,
            TMP_Text customizeLabel,
            Button customizeButton,
            RectTransform optionsRoot,
            Image optionsBackground,
            Outline optionsOutline,
            TMP_Text optionsIcon,
            TMP_Text optionsLabel,
            Button optionsButton)
        {
            flowManager = manager;
            leftArrowButton = leftArrow;
            rightArrowButton = rightArrow;
            captionLabel = caption;
            items = new[]
            {
                new CarouselItem
                {
                    root = careerRoot,
                    background = careerBackground,
                    outline = careerOutline,
                    icon = careerIcon,
                    label = careerLabel,
                    button = careerButton,
                    displayName = "My Career",
                    targetState = MenuState.Career
                },
                new CarouselItem
                {
                    root = quickRaceRoot,
                    background = quickRaceBackground,
                    outline = quickRaceOutline,
                    icon = quickRaceIcon,
                    label = quickRaceLabel,
                    button = quickRaceButton,
                    displayName = "Quick Race",
                    targetState = MenuState.QuickRace
                },
                new CarouselItem
                {
                    root = customizeRoot,
                    background = customizeBackground,
                    outline = customizeOutline,
                    icon = customizeIcon,
                    label = customizeLabel,
                    button = customizeButton,
                    displayName = "Customize",
                    targetState = MenuState.Customize
                },
                new CarouselItem
                {
                    root = optionsRoot,
                    background = optionsBackground,
                    outline = optionsOutline,
                    icon = optionsIcon,
                    label = optionsLabel,
                    button = optionsButton,
                    displayName = "Settings",
                    targetState = MenuState.Options
                }
            };

            selectedIndex = Mathf.Clamp(defaultSelectedIndex, 0, items.Length - 1);
            HookupButtons();
            ApplySelectionVisuals();
        }

        private void Awake()
        {
            selectedIndex = Mathf.Clamp(defaultSelectedIndex, 0, Mathf.Max(0, items.Length - 1));
            HookupButtons();
            ApplySelectionVisuals();
        }

        private void OnEnable()
        {
            ApplySelectionVisuals();
            SelectCurrent();
        }

        private void Update()
        {
            if (flowManager == null || flowManager.currentState != MenuState.MainMenu)
            {
                return;
            }

            if (Time.unscaledTime < nextInputTime)
            {
                return;
            }

            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                MoveSelection(-1);
                return;
            }

            if (wheel < -0.01f || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                MoveSelection(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                ActivateSelected();
            }
        }

        private void HookupButtons()
        {
            if (leftArrowButton != null)
            {
                leftArrowButton.onClick.RemoveListener(HandlePrevious);
                leftArrowButton.onClick.AddListener(HandlePrevious);
            }

            if (rightArrowButton != null)
            {
                rightArrowButton.onClick.RemoveListener(HandleNext);
                rightArrowButton.onClick.AddListener(HandleNext);
            }

            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                CarouselItem item = items[i];
                if (item?.button == null)
                {
                    continue;
                }

                int index = i;
                item.button.onClick.RemoveAllListeners();
                item.button.onClick.AddListener(() => OnItemClicked(index));

                Navigation navigation = item.button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = items[(i - 1 + items.Length) % items.Length].button;
                navigation.selectOnRight = items[(i + 1) % items.Length].button;
                navigation.selectOnUp = item.button;
                navigation.selectOnDown = item.button;
                item.button.navigation = navigation;
            }
        }

        private void HandlePrevious()
        {
            MoveSelection(-1);
        }

        private void HandleNext()
        {
            MoveSelection(1);
        }

        private void OnItemClicked(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, items.Length - 1);
            ApplySelectionVisuals();
            SelectCurrent();
            ActivateSelected();
        }

        private void MoveSelection(int direction)
        {
            if (items == null || items.Length == 0)
            {
                return;
            }

            selectedIndex += direction;
            if (selectedIndex < 0)
            {
                selectedIndex = items.Length - 1;
            }
            else if (selectedIndex >= items.Length)
            {
                selectedIndex = 0;
            }

            nextInputTime = Time.unscaledTime + inputRepeatDelay;
            ApplySelectionVisuals();
            SelectCurrent();
        }

        private void SelectCurrent()
        {
            if (EventSystem.current == null || items == null || selectedIndex < 0 || selectedIndex >= items.Length)
            {
                return;
            }

            Button button = items[selectedIndex].button;
            if (button != null)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
        }

        private void ApplySelectionVisuals()
        {
            if (items == null || items.Length == 0)
            {
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                CarouselItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                bool selected = i == selectedIndex;
                float scale = selected ? 1.14f : 0.92f;

                if (item.root != null)
                {
                    item.root.localScale = new Vector3(scale, scale, 1f);
                }

                if (item.background != null)
                {
                    item.background.color = selected
                        ? new Color(0.72f, 0.9f, 0.38f, 0.9f)
                        : new Color(1f, 1f, 1f, 0.08f);
                }

                if (item.outline != null)
                {
                    item.outline.effectColor = selected
                        ? new Color(0.88f, 1f, 0.58f, 1f)
                        : new Color(1f, 1f, 1f, 0.18f);
                    item.outline.effectDistance = selected ? new Vector2(4f, 4f) : new Vector2(2f, 2f);
                }

                if (item.icon != null)
                {
                    item.icon.color = selected ? new Color(0.06f, 0.07f, 0.04f, 1f) : new Color(1f, 1f, 1f, 0.75f);
                }

                if (item.label != null)
                {
                    item.label.color = selected ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.62f);
                }
            }

            if (captionLabel != null)
            {
                captionLabel.text = items[selectedIndex].displayName;
            }
        }

        private void ActivateSelected()
        {
            if (flowManager == null || items == null || selectedIndex < 0 || selectedIndex >= items.Length)
            {
                return;
            }

            flowManager.GoToState(items[selectedIndex].targetState);
        }
    }
}
