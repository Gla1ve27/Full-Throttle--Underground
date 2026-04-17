using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Underground.UI
{
    public class MainMenuCarouselController : MonoBehaviour
    {
        [System.Serializable]
        private class MenuTile
        {
            public RectTransform root;
            public Image background;
            public Outline outline;
            public TMP_Text label;
            public Button button;
            public string displayName;
            public MenuAction action;
        }

        private enum MenuAction
        {
            MyCareer,
            QuickRace,
            Settings
        }

        [SerializeField] private MainMenuController menuController;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private TMP_Text modeCaption;
        [SerializeField] private float inputRepeatDelay = 0.18f;
        [SerializeField] private MenuTile[] tiles = new MenuTile[3];

        private int selectedIndex = 1;
        private float nextInputTime;

        public void Initialize(
            MainMenuController controller,
            GameObject settingsOverlay,
            Button previousButton,
            Button nextButton,
            TMP_Text caption,
            RectTransform myCareerRoot,
            Image myCareerBackground,
            Outline myCareerOutline,
            TMP_Text myCareerLabel,
            Button myCareerButton,
            RectTransform quickRaceRoot,
            Image quickRaceBackground,
            Outline quickRaceOutline,
            TMP_Text quickRaceLabel,
            Button quickRaceButton,
            RectTransform settingsRoot,
            Image settingsBackground,
            Outline settingsOutline,
            TMP_Text settingsLabel,
            Button settingsButton)
        {
            menuController = controller;
            settingsPanel = settingsOverlay;
            leftButton = previousButton;
            rightButton = nextButton;
            modeCaption = caption;

            tiles = new[]
            {
                new MenuTile
                {
                    root = myCareerRoot,
                    background = myCareerBackground,
                    outline = myCareerOutline,
                    label = myCareerLabel,
                    button = myCareerButton,
                    displayName = "My Career",
                    action = MenuAction.MyCareer
                },
                new MenuTile
                {
                    root = quickRaceRoot,
                    background = quickRaceBackground,
                    outline = quickRaceOutline,
                    label = quickRaceLabel,
                    button = quickRaceButton,
                    displayName = "Quick Race",
                    action = MenuAction.QuickRace
                },
                new MenuTile
                {
                    root = settingsRoot,
                    background = settingsBackground,
                    outline = settingsOutline,
                    label = settingsLabel,
                    button = settingsButton,
                    displayName = "Settings",
                    action = MenuAction.Settings
                }
            };

            HookupButtons();
            ApplySelectionVisuals();
        }

        private void Awake()
        {
            HookupButtons();
            ApplySelectionVisuals();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextInputTime)
            {
                return;
            }

            float mouseScroll = Input.mouseScrollDelta.y;
            if (mouseScroll > 0.01f || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                MoveSelection(-1);
                return;
            }

            if (mouseScroll < -0.01f || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                MoveSelection(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                ActivateSelected();
            }
        }

        private void HookupButtons()
        {
            if (leftButton != null)
            {
                leftButton.onClick.RemoveListener(HandlePreviousClicked);
                leftButton.onClick.AddListener(HandlePreviousClicked);
            }

            if (rightButton != null)
            {
                rightButton.onClick.RemoveListener(HandleNextClicked);
                rightButton.onClick.AddListener(HandleNextClicked);
            }

            if (tiles == null)
            {
                return;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                MenuTile tile = tiles[i];
                if (tile?.button == null)
                {
                    continue;
                }

                int capturedIndex = i;
                tile.button.onClick.RemoveAllListeners();
                tile.button.onClick.AddListener(() => OnTileClicked(capturedIndex));
            }
        }

        private void HandlePreviousClicked()
        {
            MoveSelection(-1);
        }

        private void HandleNextClicked()
        {
            MoveSelection(1);
        }

        private void OnTileClicked(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, tiles.Length - 1);
            ApplySelectionVisuals();
            ActivateSelected();
        }

        private void MoveSelection(int direction)
        {
            if (tiles == null || tiles.Length == 0)
            {
                return;
            }

            selectedIndex += direction;
            if (selectedIndex < 0)
            {
                selectedIndex = tiles.Length - 1;
            }
            else if (selectedIndex >= tiles.Length)
            {
                selectedIndex = 0;
            }

            nextInputTime = Time.unscaledTime + inputRepeatDelay;
            ApplySelectionVisuals();
        }

        private void ApplySelectionVisuals()
        {
            if (tiles == null || tiles.Length == 0)
            {
                return;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                MenuTile tile = tiles[i];
                if (tile == null)
                {
                    continue;
                }

                bool isSelected = i == selectedIndex;

                if (tile.root != null)
                {
                    tile.root.localScale = isSelected ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
                }

                if (tile.background != null)
                {
                    tile.background.color = isSelected
                        ? new Color(0.73f, 0.95f, 0.38f, 0.88f)
                        : new Color(1f, 1f, 1f, 0.12f);
                }

                if (tile.outline != null)
                {
                    tile.outline.effectColor = isSelected
                        ? new Color(0.85f, 1f, 0.55f, 0.98f)
                        : new Color(1f, 1f, 1f, 0.08f);
                    tile.outline.effectDistance = isSelected ? new Vector2(4f, 4f) : new Vector2(1f, 1f);
                }

                if (tile.label != null)
                {
                    tile.label.color = isSelected
                        ? new Color(1f, 1f, 1f, 0.98f)
                        : new Color(1f, 1f, 1f, 0.52f);
                    tile.label.fontStyle = FontStyles.Bold | FontStyles.Italic;
                }
            }

            if (modeCaption != null)
            {
                modeCaption.text = tiles[selectedIndex].displayName;
            }
        }

        private void ActivateSelected()
        {
            if (tiles == null || selectedIndex < 0 || selectedIndex >= tiles.Length)
            {
                return;
            }

            switch (tiles[selectedIndex].action)
            {
                case MenuAction.MyCareer:
                    menuController?.OpenCareer();
                    break;
                case MenuAction.QuickRace:
                    menuController?.OpenWorld();
                    break;
                case MenuAction.Settings:
                    if (settingsPanel != null)
                    {
                        settingsPanel.SetActive(!settingsPanel.activeSelf);
                    }
                    break;
            }
        }
    }
}
